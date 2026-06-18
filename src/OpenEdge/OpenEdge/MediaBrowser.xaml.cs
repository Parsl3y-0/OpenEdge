using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEdge;

public enum MediaBrowserTagFilterMode
{
    None,
    MatchingFirst,
    MatchSelected,
    ExactMatch,
    MissingAny
}

public partial class MediaBrowser : UserControl, INotifyPropertyChanged
{
    private sealed class SourceFilterOption
    {
        public string Label { get; init; } = "";
        public string SourceId { get; init; } = "";
        public string FolderPath { get; init; } = "";

        public string Key => SourceId + "|" + FolderPath;
    }

    #region Fields
    private MediaCatalogService _mediaCatalog;
    private List<MediaItem> _allMedia = new List<MediaItem>();
    private List<MediaItem> _filteredMedia = new List<MediaItem>();
    private string _searchText = "";
    private MediaBrowserFilterKind _filterKind = MediaBrowserFilterKind.All;
    private string _filterSourceId = "";
    private string _filterFolderPath = "";
    private string _sortField = "Name";
    private bool _sortAscending = true;
    private bool _showThumbnails = false;
    private int _thumbnailSize = 150;
    private MediaBrowserTagFilterMode _tagFilterMode = MediaBrowserTagFilterMode.None;
    private List<string> _tagFilterTags = new List<string>();
    private Dictionary<string, BitmapSource> _thumbnailCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
    private Queue<string> _thumbnailCacheOrder = new Queue<string>();
    private object _thumbnailLock = new object();
    private CancellationTokenSource _thumbnailLoadCancellation = new CancellationTokenSource();
    private int _thumbnailLoadGeneration;
    private const int TileLayoutPageSize = 300;
    private const int MaxThumbnailCacheEntries = 450;

    private bool _isLoading;
    private bool _useTileLayout;
    private int _tileLayoutItemLimit = TileLayoutPageSize;
    private string _selectedSourceFilterKey = "|";
    private bool _suppressSelectionNavigation;
    #endregion

    #region Events
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action<MediaItem> MediaSelected;
    public event Action<IReadOnlyList<MediaItem>> MediaSelectionChanged;
    #endregion

    #region Constructor
    public MediaBrowser(MediaCatalogService mediaCatalog)
    {
        _mediaCatalog = mediaCatalog ?? throw new ArgumentNullException(nameof(mediaCatalog));
        InitializeComponent();
        DataContext = this;
        SetExplorerLayout(enabled: false);
        PopulateSourceFilter();
        BeginLoadMedia();
    }
    #endregion

    #region Media Loading and Filtering
    private void BeginLoadMedia()
    {
        if (_isLoading)
        {
            return;
        }

        SessionTraceLogger.Info("media-browser", "begin load showThumbs=" + ShowThumbnails + " tile=" + _useTileLayout);
        SessionTraceLogger.Memory("media-browser", "before load");
        _isLoading = true;
        CancelThumbnailLoading(clearCache: true);
        _allMedia = new List<MediaItem>();
        _filteredMedia = new List<MediaItem>();
        OnPropertyChanged(nameof(MediaItems));
        _ = LoadMediaAsync();
    }

    private async Task LoadMediaAsync()
    {
        List<MediaItem> loadedItems = await Task.Run(delegate
        {
            List<MediaItem> items = new List<MediaItem>();
            MediaCatalogSnapshot snapshot = _mediaCatalog.Snapshot;
            foreach (MediaItemRecord mediaItemRecord in snapshot.ActiveItems)
            {
                items.Add(new MediaItem(
                    mediaItemRecord.FullPath,
                    mediaItemRecord.RelativePath,
                    mediaItemRecord.FileName,
                    mediaItemRecord.Kind,
                    mediaItemRecord.SourceId,
                    mediaItemRecord.SizeBytes,
                    DateTime.FromFileTimeUtc(mediaItemRecord.LastWriteUtcTicks)
                ));
            }
            return items;
        });

		_allMedia = loadedItems;
		await Dispatcher.InvokeAsync(delegate
		{
			PopulateSourceFilter(loadedItems);
		});
		ApplyFilter();
		_isLoading = false;
        SessionTraceLogger.Info("media-browser", "load complete items=" + loadedItems.Count + " filtered=" + _filteredMedia.Count);
        SessionTraceLogger.Memory("media-browser", "after load");
    }

    private void StartThumbnailLoad(IEnumerable<MediaItem> items)
    {
        if (!ShowThumbnails)
        {
            return;
        }
        CancellationToken token;
        int generation;
        lock (_thumbnailLock)
        {
            _thumbnailLoadCancellation.Cancel();
            _thumbnailLoadCancellation.Dispose();
            _thumbnailLoadCancellation = new CancellationTokenSource();
            token = _thumbnailLoadCancellation.Token;
            generation = ++_thumbnailLoadGeneration;
        }
        List<MediaItem> pendingItems = items.Where(delegate(MediaItem item)
        {
            return item != null && item.Thumbnail == null;
        }).ToList();
        SessionTraceLogger.Info("thumbnail-batch", "start generation=" + generation + " pending=" + pendingItems.Count + " size=" + ThumbnailSize);
        SessionTraceLogger.Memory("thumbnail-batch", "before generation " + generation);
        _ = LoadThumbnailsAsync(pendingItems, generation, token);
    }

    private async Task LoadThumbnailsAsync(IEnumerable<MediaItem> items, int generation, CancellationToken cancellationToken)
    {
        foreach (MediaItem item in items)
        {
            if (cancellationToken.IsCancellationRequested || generation != _thumbnailLoadGeneration || !ShowThumbnails)
            {
                return;
            }

            BitmapSource thumbnail;
            try
            {
                thumbnail = await Task.Run(delegate
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                    return GetThumbnail(item);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                SessionTraceLogger.Info("thumbnail-batch", "cancel generation=" + generation);
                return;
            }

            if (cancellationToken.IsCancellationRequested || generation != _thumbnailLoadGeneration || !ShowThumbnails)
            {
                return;
            }

            await Dispatcher.InvokeAsync(delegate
            {
                if (!cancellationToken.IsCancellationRequested && generation == _thumbnailLoadGeneration && ShowThumbnails)
                {
                    item.Thumbnail = thumbnail;
                }
            });
        }
        SessionTraceLogger.Info("thumbnail-batch", "complete generation=" + generation);
        SessionTraceLogger.Memory("thumbnail-batch", "after generation " + generation);
    }

    private void CancelThumbnailLoading(bool clearCache)
    {
        lock (_thumbnailLock)
        {
            SessionTraceLogger.Info("thumbnail-batch", "cancel requested clearCache=" + clearCache + " generation=" + _thumbnailLoadGeneration);
            _thumbnailLoadCancellation.Cancel();
            _thumbnailLoadGeneration++;
            if (clearCache)
            {
                _thumbnailCache.Clear();
                _thumbnailCacheOrder.Clear();
            }
        }
    }

    private void ApplyFilter()
    {
        var filtered = _allMedia.Where(item =>
            (string.IsNullOrEmpty(_searchText) || 
             item.FileName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0) &&
            (_filterKind == MediaBrowserFilterKind.All || 
             (_filterKind == MediaBrowserFilterKind.Image && item.Kind == MediaKind.Image) ||
             (_filterKind == MediaBrowserFilterKind.Video && item.Kind == MediaKind.Video) ||
             (_filterKind == MediaBrowserFilterKind.Gif && item.Kind == MediaKind.Gif)) &&
            (string.IsNullOrEmpty(_filterSourceId) || item.SourceId == _filterSourceId) &&
            (string.IsNullOrEmpty(_filterFolderPath) || IsDirectlyInDirectory(item.FullPath, _filterFolderPath))
        ).ToList();

        if (_tagFilterMode == MediaBrowserTagFilterMode.MatchSelected)
        {
            filtered = filtered.Where(item => GetTagMatchCount(item) == _tagFilterTags.Count).ToList();
        }
        else if (_tagFilterMode == MediaBrowserTagFilterMode.ExactMatch)
        {
            filtered = filtered.Where(IsExactTagMatch).ToList();
        }
        else if (_tagFilterMode == MediaBrowserTagFilterMode.MissingAny)
        {
            filtered = filtered.Where(item => GetTagMatchCount(item) < _tagFilterTags.Count).ToList();
        }

        switch (_sortField)
        {
            case "Date":
                filtered = (_sortAscending ? filtered.OrderBy(item => item.LastWriteUtc) : filtered.OrderByDescending(item => item.LastWriteUtc)).ToList();
                break;
            case "Size":
                filtered = (_sortAscending ? filtered.OrderBy(item => item.SizeBytes) : filtered.OrderByDescending(item => item.SizeBytes)).ToList();
                break;
            default:
                filtered = (_sortAscending ? filtered.OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase) : filtered.OrderByDescending(item => item.FileName, StringComparer.OrdinalIgnoreCase)).ToList();
                break;
        }

        if (_tagFilterMode == MediaBrowserTagFilterMode.MatchingFirst)
        {
            filtered = filtered.OrderByDescending(GetTagMatchCount).ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        _filteredMedia = filtered;
        if (_useTileLayout)
        {
            _tileLayoutItemLimit = TileLayoutPageSize;
        }
        OnPropertyChanged(nameof(MediaItems));
        UpdateTilePagingStatus();

        if (ShowThumbnails)
        {
            StartThumbnailLoad(MediaItems);
        }
    }
    #endregion

    #region Properties
    public IReadOnlyList<MediaItem> MediaItems => _useTileLayout ? _filteredMedia.Take(_tileLayoutItemLimit).ToList() : _filteredMedia;

    public IReadOnlyList<MediaItem> SelectedMediaItems => MediaItemsControl.SelectedItems.Cast<MediaItem>().ToList();

    public double TilePreviewHeight => Math.Max(82, ThumbnailSize * 0.8);

    public IReadOnlyList<string> SelectedRelativePaths => MediaItemsControl.SelectedItems.Cast<MediaItem>().Select(delegate(MediaItem item)
    {
        return item.RelativePath;
    }).Where(delegate(string path)
    {
        return !string.IsNullOrWhiteSpace(path);
    }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public int FilteredMediaCount => _filteredMedia.Count;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                ApplyFilter();
                OnPropertyChanged();
            }
        }
    }

    public MediaBrowserFilterKind FilterKind
    {
        get => _filterKind;
        set
        {
            if (_filterKind != value)
            {
                _filterKind = value;
                ApplyFilter();
                OnPropertyChanged();
            }
        }
    }

    public string FilterSource
    {
        get => _filterSourceId;
        set
        {
            string filterSource = value ?? "";
            if (_filterSourceId != filterSource || !string.IsNullOrEmpty(_filterFolderPath))
            {
                _filterSourceId = filterSource;
                _filterFolderPath = "";
                _selectedSourceFilterKey = _filterSourceId + "|";
                ApplyFilter();
                OnPropertyChanged();
            }
        }
    }

    public bool ShowThumbnails
    {
        get => _showThumbnails;
        set
        {
            if (_showThumbnails != value)
            {
                _showThumbnails = value;
                if (!_showThumbnails)
                {
                    CancelThumbnailLoading(clearCache: false);
                }
                OnPropertyChanged();
            }
        }
    }

    public int ThumbnailSize
    {
        get => _thumbnailSize;
        set
        {
            if (_thumbnailSize != value)
            {
                _thumbnailSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TilePreviewHeight));
            }
        }
    }

    public bool UseTileLayout
    {
        get => _useTileLayout;
        set
        {
            if (_useTileLayout != value)
            {
                _useTileLayout = value;
                _tileLayoutItemLimit = TileLayoutPageSize;
                SetExplorerLayout(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(MediaItems));
                UpdateTilePagingStatus();
            }
        }
    }

    public BitmapSource GetThumbnail(MediaItem item)
    {
        if (item == null)
        {
            return null;
        }
        string cacheKey = item.Kind + ":" + item.FullPath + ":" + ThumbnailSize;
        lock (_thumbnailLock)
        {
            if (_thumbnailCache.TryGetValue(cacheKey, out var cached))
                return cached;
        }

        try
        {
            BitmapSource bitmap;
            if (item.Kind == MediaKind.Video)
            {
                bitmap = CreateMediaPlaceholderThumbnail(ThumbnailSize, item.Kind);
            }
            else
            {
                if (!ImageFileSafety.IsSafeForWpfDecode(item.FullPath, out string unsafeReason))
                {
                    SessionTraceLogger.Info("thumbnail-skip", "Skipped unsafe thumbnail image: " + item.FullPath + " reason=" + unsafeReason);
                    return CreateMediaPlaceholderThumbnail(ThumbnailSize, item.Kind);
                }
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.DecodePixelWidth = ThumbnailSize;
                image.UriSource = new Uri(item.FullPath);
                image.EndInit();
                image.Freeze();
                bitmap = image;
            }

            lock (_thumbnailLock)
            {
                if (!_thumbnailCache.ContainsKey(cacheKey))
                {
                    _thumbnailCache[cacheKey] = bitmap;
                    _thumbnailCacheOrder.Enqueue(cacheKey);
                    TrimThumbnailCache();
                }
            }
            return bitmap;
        }
        catch (Exception ex)
        {
            SessionTraceLogger.Error("thumbnail-decode", "Failed to load thumbnail: " + item.FullPath, ex);
            return CreateMediaPlaceholderThumbnail(Math.Max(64, ThumbnailSize), item.Kind);
        }
    }

    private static BitmapSource CreateMediaPlaceholderThumbnail(int requestedSize, MediaKind kind)
    {
        int size = Math.Max(64, requestedSize);
        int stride = size * 4;
        byte[] pixels = new byte[size * stride];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * stride + x * 4;
                bool border = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
                bool filmStrip = x < size / 7 || x > size - size / 7;
                bool stripHole = filmStrip && y % Math.Max(8, size / 8) < Math.Max(3, size / 28);
                bool triangle = kind == MediaKind.Video && x > size * 0.38 && x < size * 0.72 && Math.Abs(y - size / 2) < (x - size * 0.35) * 0.62;
                bool mountain = kind != MediaKind.Video && y > size * 0.58 && y > size - x * 0.55;
                bool sun = kind != MediaKind.Video && (x - size * 0.70) * (x - size * 0.70) + (y - size * 0.28) * (y - size * 0.28) < size * size * 0.018;
                byte r = 34;
                byte g = 28;
                byte b = 42;
                if (border)
                {
                    r = 138; g = 106; b = 176;
                }
                else if (stripHole)
                {
                    r = 95; g = 70; b = 122;
                }
                else if (filmStrip)
                {
                    r = 22; g = 20; b = 28;
                }
                else if (triangle || mountain || sun)
                {
                    r = 230; g = 210; b = 255;
                }
                else
                {
                    r = (byte)(32 + y * 24 / size);
                    g = (byte)(26 + x * 12 / size);
                    b = (byte)(45 + x * 36 / size);
                }
                pixels[index] = b;
                pixels[index + 1] = g;
                pixels[index + 2] = r;
                pixels[index + 3] = 255;
            }
        }
        WriteableBitmap bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private void TrimThumbnailCache()
    {
        while (_thumbnailCache.Count > MaxThumbnailCacheEntries && _thumbnailCacheOrder.Count > 0)
        {
            string oldestKey = _thumbnailCacheOrder.Dequeue();
            _thumbnailCache.Remove(oldestKey);
        }
    }
    #endregion

    #region INotifyPropertyChanged Implementation
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion

    #region Event Handlers
    // Methods to be called from XAML controls
    public void SelectMedia(MediaItem item)
    {
        MediaSelected?.Invoke(item);
    }
    
    private void PopulateSourceFilter()
    {
        PopulateSourceFilter(_allMedia);
    }

    private void PopulateSourceFilter(IEnumerable<MediaItem> items)
    {
        if (_mediaCatalog == null)
        {
            return;
        }

        List<SourceFilterOption> sourceFilterOptions = BuildSourceFilterOptions(items ?? Enumerable.Empty<MediaItem>());
        SourceCombo.Items.Clear();

        int selectedIndex = 0;
        for (int i = 0; i < sourceFilterOptions.Count; i++)
        {
            SourceFilterOption sourceFilterOption = sourceFilterOptions[i];
            SourceCombo.Items.Add(CreateComboBoxItem(sourceFilterOption.Label, sourceFilterOption));
            if (string.Equals(sourceFilterOption.Key, _selectedSourceFilterKey, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i;
            }
        }

        if (SourceCombo.Items.Count > 0)
        {
            SourceCombo.SelectedIndex = selectedIndex;
        }
        if (MediaTypeCombo.SelectedIndex < 0)
        {
            MediaTypeCombo.SelectedIndex = 0;
        }
        if (TileSizeCombo != null && TileSizeCombo.SelectedIndex < 0)
        {
            TileSizeCombo.SelectedIndex = 1;
        }
        if (SortFieldCombo.SelectedIndex < 0)
        {
            SortFieldCombo.SelectedIndex = 0;
        }
        if (SortDirectionCombo.SelectedIndex < 0)
        {
            SortDirectionCombo.SelectedIndex = 0;
        }
    }

    private List<SourceFilterOption> BuildSourceFilterOptions(IEnumerable<MediaItem> items)
    {
        Dictionary<string, MediaSourceDefinition> sourceLookup = _mediaCatalog.GetSources().ToDictionary((MediaSourceDefinition source) => source.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<MediaItem>> itemsBySource = items.GroupBy((MediaItem item) => item.SourceId, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, MediaItem> group) => group.Key, (IGrouping<string, MediaItem> group) => group.ToList(), StringComparer.OrdinalIgnoreCase);
        List<SourceFilterOption> list = new List<SourceFilterOption>
        {
            new SourceFilterOption
            {
                Label = "All Sources"
            }
        };

        foreach (MediaSourceDefinition item in sourceLookup.Values.OrderBy((MediaSourceDefinition source) => source.SortOrder).ThenBy((MediaSourceDefinition source) => source.Name, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(new SourceFilterOption
            {
                Label = item.Name,
                SourceId = item.Id,
                FolderPath = item.RootPath
            });

            if (!itemsBySource.TryGetValue(item.Id, out List<MediaItem> value) || value.Count == 0 || string.IsNullOrWhiteSpace(item.RootPath))
            {
                continue;
            }

            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MediaItem item2 in value)
            {
                string directoryName = Path.GetDirectoryName(item2.FullPath);
                while (!string.IsNullOrWhiteSpace(directoryName) && IsSameDirectoryOrChild(directoryName, item.RootPath) && !string.Equals(directoryName, item.RootPath, StringComparison.OrdinalIgnoreCase))
                {
                    hashSet.Add(directoryName);
                    directoryName = Directory.GetParent(directoryName)?.FullName;
                }
            }

            foreach (string item3 in hashSet.OrderBy(delegate(string folderPath)
            {
                return Path.GetRelativePath(item.RootPath, folderPath).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            }, StringComparer.OrdinalIgnoreCase))
            {
                string text = Path.GetRelativePath(item.RootPath, item3).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
                list.Add(new SourceFilterOption
                {
                    Label = item.Name + " / " + text,
                    SourceId = item.Id,
                    FolderPath = item3
                });
            }
        }

        return list;
    }

    private static ComboBoxItem CreateComboBoxItem(string content, object tag)
    {
        return new ComboBoxItem
        {
            Content = content,
            Tag = tag,
            Background = new SolidColorBrush(Color.FromRgb(34, 34, 34)),
            Foreground = Brushes.White
        };
    }

    public void ApplyTagFilter(MediaBrowserTagFilterMode mode, IEnumerable<string> tags)
    {
        _tagFilterTags = (tags ?? Enumerable.Empty<string>()).Where(delegate(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag);
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _tagFilterMode = _tagFilterTags.Count == 0 ? MediaBrowserTagFilterMode.None : mode;
        SessionTraceLogger.Info("media-browser", "tag filter mode=" + _tagFilterMode + " tags=" + string.Join(",", _tagFilterTags));
        ApplyFilter();
        OnPropertyChanged(nameof(FilteredMediaCount));
    }

    public void ClearTagFilter()
    {
        ApplyTagFilter(MediaBrowserTagFilterMode.None, Array.Empty<string>());
    }

    private int GetTagMatchCount(MediaItem item)
    {
        if (item == null || _tagFilterTags.Count == 0)
        {
            return 0;
        }
        HashSet<string> itemTags = ParseTags(_mediaCatalog.GetTags(item.RelativePath));
        int count = 0;
        foreach (string tag in _tagFilterTags)
        {
            if (itemTags.Contains(tag))
            {
                count++;
            }
        }
        return count;
    }

    private bool IsExactTagMatch(MediaItem item)
    {
        if (item == null)
        {
            return false;
        }
        HashSet<string> itemTags = ParseTags(_mediaCatalog.GetTags(item.RelativePath));
        return itemTags.Count == _tagFilterTags.Count && GetTagMatchCount(item) == _tagFilterTags.Count;
    }

    private static HashSet<string> ParseTags(string tags)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(tags))
        {
            return result;
        }
        foreach (string tag in tags.Split(new char[3] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = tag.Trim();
            if (trimmed.Length != 0)
            {
                result.Add(trimmed);
            }
        }
        return result;
    }

    private void MediaTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MediaTypeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            switch (tag)
            {
                case "Image":
                    FilterKind = MediaBrowserFilterKind.Image;
                    break;
                case "Video":
                    FilterKind = MediaBrowserFilterKind.Video;
                    break;
                case "Gif":
                    FilterKind = MediaBrowserFilterKind.Gif;
                    break;
                default:
                    FilterKind = MediaBrowserFilterKind.All;
                    break;
            }
        }
    }

    private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            if (selectedItem.Tag is SourceFilterOption sourceFilterOption)
            {
                _filterSourceId = sourceFilterOption.SourceId ?? "";
                _filterFolderPath = sourceFilterOption.FolderPath ?? "";
                _selectedSourceFilterKey = sourceFilterOption.Key;
            }
            else
            {
                _filterSourceId = "";
                _filterFolderPath = "";
                _selectedSourceFilterKey = "|";
            }
            ApplyFilter();
        }
    }

    private void ThumbnailToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ThumbnailToggle.IsChecked == true)
        {
            ThumbnailToggle.Content = "Thumbs On";
            ShowThumbnails = true;
        }
        else
        {
            ThumbnailToggle.Content = "Thumbs Off";
            ShowThumbnails = false;
            foreach (MediaItem item in _allMedia)
            {
                item.Thumbnail = null;
            }
        }
        
        // Trigger UI refresh
        OnPropertyChanged(nameof(MediaItems));

        if (ShowThumbnails)
        {
            StartThumbnailLoad(MediaItems);
        }
    }

    private void SortFieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortFieldCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            _sortField = selectedItem.Tag?.ToString() ?? "Name";
            ApplyFilter();
        }
    }

    private void SortDirectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortDirectionCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            _sortAscending = !string.Equals(selectedItem.Tag?.ToString(), "Descending", StringComparison.OrdinalIgnoreCase);
            ApplyFilter();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        BeginLoadMedia();
    }

    public void ReloadMedia()
    {
        SessionTraceLogger.Info("media-browser", "reload requested");
        BeginLoadMedia();
    }

    public void EnableThumbnails(int thumbnailSize)
    {
        ThumbnailSize = thumbnailSize;
        ShowThumbnails = true;
        if (ThumbnailToggle != null)
        {
            ThumbnailToggle.IsChecked = true;
            ThumbnailToggle.Content = "Thumbs On";
        }
        if (_filteredMedia.Count > 0)
        {
            StartThumbnailLoad(MediaItems);
        }
    }

    public void EnableExplorerLayout(int thumbnailSize)
    {
        SessionTraceLogger.Info("explorer-view", "enable thumbnailSize=" + thumbnailSize + " filtered=" + _filteredMedia.Count);
        SessionTraceLogger.Memory("explorer-view", "enable");
        ThumbnailSize = thumbnailSize;
        ShowThumbnails = true;
        if (ThumbnailToggle != null)
        {
            ThumbnailToggle.IsChecked = true;
            ThumbnailToggle.Content = "Thumbs On";
        }
        UseTileLayout = true;
        if (_filteredMedia.Count > 0)
        {
            StartThumbnailLoad(MediaItems);
        }
        UpdateTilePagingStatus();
    }

    public void DisableExplorerLayout()
    {
        SessionTraceLogger.Info("explorer-view", "disable filtered=" + _filteredMedia.Count);
        SessionTraceLogger.Memory("explorer-view", "disable");
        UseTileLayout = false;
    }

    private void SetExplorerLayout(bool enabled)
    {
        if (MediaItemsControl == null)
        {
            return;
        }

        FrameworkElementFactory panelFactory = new FrameworkElementFactory(enabled ? typeof(WrapPanel) : typeof(VirtualizingStackPanel));
        if (enabled)
        {
            Binding widthBinding = new Binding("ActualWidth")
            {
                Source = MediaItemsControl
            };
            panelFactory.SetBinding(FrameworkElement.WidthProperty, widthBinding);
        }
        MediaItemsControl.ItemsPanel = new ItemsPanelTemplate(panelFactory);
        MediaItemsControl.ItemTemplate = (DataTemplate)MediaItemsControl.Resources[enabled ? "TileMediaItemTemplate" : "ListMediaItemTemplate"];
        MediaItemsControl.SetValue(ScrollViewer.CanContentScrollProperty, !enabled);
        MediaItemsControl.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, enabled ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
        VirtualizingPanel.SetIsVirtualizing(MediaItemsControl, !enabled);

        Style itemStyle = CreateDarkMediaItemStyle(enabled);
        MediaItemsControl.ItemContainerStyle = itemStyle;
        UpdateTilePagingStatus();
    }

    private Style CreateDarkMediaItemStyle(bool tileLayout)
    {
        Style itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        if (tileLayout)
        {
            itemStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, (double)ThumbnailSize + 16.0));
            itemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 10, 10)));
        }
        else
        {
            itemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 0, 6)));
        }

        FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "SelectionBorder";
        borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        FrameworkElementFactory presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        presenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        presenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
        presenterFactory.SetValue(ContentPresenter.SnapsToDevicePixelsProperty, true);
        borderFactory.AppendChild(presenterFactory);

        ControlTemplate template = new ControlTemplate(typeof(ListBoxItem));
        template.VisualTree = borderFactory;
        Trigger hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(70, 74, 49, 94)), "SelectionBorder"));
        hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(130, 227, 155, 232)), "SelectionBorder"));
        Trigger selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(125, 126, 40, 130)), "SelectionBorder"));
        selectedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(230, 227, 155, 232)), "SelectionBorder"));
        Trigger disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Gray));
        template.Triggers.Add(hoverTrigger);
        template.Triggers.Add(selectedTrigger);
        template.Triggers.Add(disabledTrigger);
        itemStyle.Setters.Add(new Setter(Control.TemplateProperty, template));
        return itemStyle;
    }

    private void TileSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TileSizeCombo.SelectedItem is ComboBoxItem selectedItem && int.TryParse(selectedItem.Tag?.ToString(), out int thumbnailSize))
        {
            ChangeTileSize(thumbnailSize);
        }
    }

    private void ChangeTileSize(int thumbnailSize)
    {
        if (ThumbnailSize == thumbnailSize)
        {
            return;
        }
        SessionTraceLogger.Info("explorer-view", "tile size change from=" + ThumbnailSize + " to=" + thumbnailSize);
        SessionTraceLogger.Memory("explorer-view", "before tile size change");
        ThumbnailSize = thumbnailSize;
        CancelThumbnailLoading(clearCache: true);
        foreach (MediaItem item in _allMedia)
        {
            item.Thumbnail = null;
        }
        if (_useTileLayout)
        {
            SetExplorerLayout(enabled: true);
        }
        OnPropertyChanged(nameof(MediaItems));
        if (ShowThumbnails)
        {
            StartThumbnailLoad(MediaItems);
        }
        SessionTraceLogger.Memory("explorer-view", "after tile size change");
    }

    private void SelectVisibleButton_Click(object sender, RoutedEventArgs e)
    {
        SessionTraceLogger.Info("explorer-view", "select visible count=" + MediaItems.Count);
        _suppressSelectionNavigation = true;
        try
        {
            MediaItemsControl.SelectedItems.Clear();
            foreach (MediaItem item in MediaItems)
            {
                MediaItemsControl.SelectedItems.Add(item);
            }
        }
        finally
        {
            _suppressSelectionNavigation = false;
        }
        MediaSelectionChanged?.Invoke(SelectedMediaItems);
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        SessionTraceLogger.Info("explorer-view", "clear selection count=" + SelectedMediaItems.Count);
        _suppressSelectionNavigation = true;
        try
        {
            MediaItemsControl.SelectedItems.Clear();
        }
        finally
        {
            _suppressSelectionNavigation = false;
        }
        MediaSelectionChanged?.Invoke(SelectedMediaItems);
    }

    private void ShowMoreTilesButton_Click(object sender, RoutedEventArgs e)
    {
        SessionTraceLogger.Info("explorer-view", "show more beforeLimit=" + _tileLayoutItemLimit + " filtered=" + _filteredMedia.Count);
        SessionTraceLogger.Memory("explorer-view", "before show more");
        _tileLayoutItemLimit += TileLayoutPageSize;
        OnPropertyChanged(nameof(MediaItems));
        UpdateTilePagingStatus();
        if (ShowThumbnails)
        {
            StartThumbnailLoad(MediaItems);
        }
        SessionTraceLogger.Memory("explorer-view", "after show more");
    }

    private void UpdateTilePagingStatus()
    {
        if (TilePagingPanel == null)
        {
            return;
        }
        if (!_useTileLayout)
        {
            TilePagingPanel.Visibility = Visibility.Collapsed;
            return;
        }
        int shown = Math.Min(_tileLayoutItemLimit, _filteredMedia.Count);
        TilePagingPanel.Visibility = Visibility.Visible;
        TilePagingStatusText.Text = "Showing " + shown + " of " + _filteredMedia.Count + " filtered media item(s).";
        ShowMoreTilesButton.IsEnabled = shown < _filteredMedia.Count;
        ShowMoreTilesButton.Visibility = shown < _filteredMedia.Count ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsSameDirectoryOrChild(string path, string parentPath)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(parentPath))
        {
            return false;
        }
        if (string.Equals(path, parentPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        string value = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectlyInDirectory(string filePath, string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }
        string parent = Path.GetDirectoryName(filePath) ?? "";
        return string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private void MediaItemsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionNavigation)
        {
            return;
        }
        IReadOnlyList<MediaItem> selectedItems = SelectedMediaItems;
        MediaSelectionChanged?.Invoke(selectedItems);
        if (!_suppressSelectionNavigation && MediaItemsControl.SelectedItem is MediaItem selectedItem)
        {
            MediaSelected?.Invoke(selectedItem);
        }
    }
    #endregion
}
