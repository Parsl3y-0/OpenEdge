using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using OpenEdge; // For MediaItem and MediaBrowserFilterKind

namespace OpenEdge;

public partial class ImageTagger : Page, IComponentConnector
{
	private MediaBrowser mediaBrowser;

	private bool mediaBrowserInitialized;

	private bool explorerViewEnabled;

	private ImageTaggerGroup activeTaggerGroup;

	private ImageTaggerBulkTagger activeBulkTagger;

	private readonly List<string> selectedBrowserMediaPaths = new List<string>();

	private bool browserMultiSelectionMode;

	private readonly List<ImageButton[]> tagCategories = new List<ImageButton[]>();

	private readonly List<string> tagCategoryTitles = new List<string>();

	private readonly Dictionary<ImageButton, string> modTagGroupsByButton = new Dictionary<ImageButton, string>();

	public List<string> images = new List<string>();

	private int currentImagePointer;

	public List<string> videos = new List<string>();

	private int currentVideoPointer;



	private bool typeButton = true;

	private bool forced;

	private int forcedTagAmount;

	public Page1 parent;

	private HomeworkScreen hwScreen;

	public SoundPlayer spa;

	public string[] tagGroups = new string[5] { "", "", "", "", "" };

	private List<ImageButton> buttons;

	private MediaCatalogService mediaCatalog;

	public MediaCatalogService MediaCatalog => mediaCatalog;

	private bool shouldBeMuted;
	private string currentVideoRelativePath = "";
	private List<string> tagNames = new List<string>();
	private List<string> tags = new List<string>();
	private string[] rawTags = new string[0];
	


	public void setMuted(bool mute)
	{
		currentVideo.IsMuted = mute;
		shouldBeMuted = mute;
	}

	public void setBackground(string backgroundPath)
	{
		if (string.IsNullOrWhiteSpace(backgroundPath))
		{
			return;
		}
		string text = RuntimePaths.ResolveRuntimePath(backgroundPath);
		if (File.Exists(text))
		{
			BitmapImage img = new BitmapImage();
			img.BeginInit();
			img.UriSource = new Uri(text);
			img.EndInit();
			img.Freeze();
			base.Dispatcher.Invoke(delegate
			{
				heartbg.Source = img;
			});
		}
	}

	public ImageTagger(MediaCatalogService mediaCatalog)
	{
		this.mediaCatalog = mediaCatalog;
		InitializeComponent();
		InitializeTagButtons();
		Loaded += ImageTagger_Loaded;

		if (File.Exists(RuntimePaths.TagGroupsFile))
		{
			string[] array = File.ReadAllLines(RuntimePaths.TagGroupsFile);
			if (array.Length == 5)
			{
				tagGroups = array;
			}
		}
		reloadImagesVideosTags();
	}

	private void InitializeTagButtons()
	{
		createImageButtons();
		ImageButton[] array = buttons.ToArray();
		foreach (ImageButton button in array)
		{
			button.brothers = array;
		}
		BuildTagCategories();
		RenderTagCategories();
	}

	private void BuildTagCategories()
	{
		tagCategories.Clear();
		tagCategoryTitles.Clear();
		AddTagCategory("Expressions", buttons.Take(8).ToArray());
		AddTagCategory("Nudity", buttons.Skip(8).Take(4).ToArray());
		AddTagCategory("Group Size", buttons.Skip(12).Take(4).ToArray());
		AddTagCategory("Gender", buttons.Skip(16).Take(4).ToArray());
		AddTagCategory("Special", buttons.Skip(20).Take(4).ToArray());
		AddTagCategory("Audio", buttons.Skip(24).Take(2).ToArray());
		AddTagCategory("Mixing", buttons.Skip(26).Take(2).ToArray());
		AddTagCategory("Genre", buttons.Skip(28).Take(26).ToArray());
		AddTagCategory("Focus", buttons.Skip(54).Take(12).ToArray());
		AddTagCategory("Tools", buttons.Skip(66).Take(6).ToArray());
		ImageButton[] array = buttons.Skip(72).Where(delegate(ImageButton button)
		{
			return button.grid.Visibility != Visibility.Hidden && !modTagGroupsByButton.ContainsKey(button);
		}).ToArray();
		if (array.Length != 0)
		{
			AddTagCategory("Custom", array);
		}
		foreach (IGrouping<string, KeyValuePair<ImageButton, string>> group in modTagGroupsByButton.Where(delegate(KeyValuePair<ImageButton, string> item)
		{
			return item.Key.grid.Visibility != Visibility.Hidden;
		}).GroupBy((KeyValuePair<ImageButton, string> item) => item.Value, StringComparer.OrdinalIgnoreCase))
		{
			AddTagCategory(group.Key, group.Select((KeyValuePair<ImageButton, string> item) => item.Key).ToArray());
		}
	}

	private void AddTagCategory(string title, ImageButton[] categoryButtons)
	{
		ImageButton[] sortedButtons = categoryButtons.OrderBy(delegate(ImageButton button)
		{
			return button.grid.Visibility == Visibility.Hidden;
		}).ThenBy(delegate(ImageButton button)
		{
			return button.name ?? "";
		}, StringComparer.OrdinalIgnoreCase).ToArray();
		if (sortedButtons.Length == 0)
		{
			return;
		}
		tagCategories.Add(sortedButtons);
		tagCategoryTitles.Add(title);
	}

	private void RenderTagCategories()
	{
		TagCategoryPanel.Children.Clear();
		for (int i = 0; i < tagCategories.Count; i++)
		{
			string text = ((i < tagCategoryTitles.Count) ? tagCategoryTitles[i] : "Custom");
			Border border = new Border
			{
				Margin = new Thickness(0.0, 0.0, 8.0, 8.0),
				Padding = new Thickness(8.0),
				Background = new SolidColorBrush(Color.FromArgb(70, 18, 18, 24)),
				BorderBrush = new SolidColorBrush(Color.FromArgb(70, 138, 106, 176)),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(8.0)
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(new TextBlock
			{
				Text = text,
				Foreground = new SolidColorBrush(Color.FromRgb(237, 237, 237)),
				FontSize = 15.0,
				FontFamily = new FontFamily("Segoe UI"),
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			});
			int num = tagCategories[i].Count(delegate(ImageButton button)
			{
				return button.grid.Visibility != Visibility.Hidden;
			});
			UniformGrid uniformGrid = new UniformGrid
			{
				Columns = Math.Min(3, Math.Max(1, num))
			};
			foreach (ImageButton item in tagCategories[i])
			{
				if (item.grid.Visibility != Visibility.Hidden)
				{
					uniformGrid.Children.Add(item.grid);
				}
			}
			stackPanel.Children.Add(uniformGrid);
			border.Child = stackPanel;
			TagCategoryPanel.Children.Add(border);
		}
	}

	private void MediaBrowser_MediaSelected(MediaItem item)
	{
		if (mediaBrowser != null && mediaBrowser.SelectedMediaItems.Count > 1)
		{
			return;
		}
		SaveCurrentMediaTags();
		// When media is selected in the browser, set it as the current media
		if (item.Kind == MediaKind.Image)
		{
			if (images.Contains(item.RelativePath))
			{
				typeButton = true;
				typeBtn.Content = "Image";
				currentVideo.Stop();
				currentVideo.Source = null;
				currentVideo.Visibility = Visibility.Collapsed;
				currentImage.Visibility = Visibility.Visible;
				currentImagePointer = images.IndexOf(item.RelativePath);
				setImage();
			}
		}
		else if (item.Kind == MediaKind.Video)
		{
			if (videos.Contains(item.RelativePath))
			{
				typeButton = false;
				typeBtn.Content = "Video";
				currentImage.Source = null;
				currentImage.Visibility = Visibility.Collapsed;
				currentVideo.Visibility = Visibility.Visible;
				currentVideoPointer = videos.IndexOf(item.RelativePath);
				setVideo();
			}
		}
	}

	private void MediaBrowser_MediaSelectionChanged(IReadOnlyList<MediaItem> selectedItems)
	{
		UpdateBrowserSelectionFromBrowser();
		browserMultiSelectionMode = selectedBrowserMediaPaths.Count > 1;
		if (browserMultiSelectionMode)
		{
			ShowCommonTagsForBrowserSelection();
		}
	}

	private void UpdateBrowserSelectionFromBrowser()
	{
		selectedBrowserMediaPaths.Clear();
		if (mediaBrowser != null)
		{
			selectedBrowserMediaPaths.AddRange(mediaBrowser.SelectedRelativePaths);
		}
		UpdateBrowserSelectionStatus();
	}

	public void setParent(Page1 parent)
	{
		this.parent = parent;
	}

	public void setForced(bool forced)
	{
		this.forced = forced;
		if (forced)
		{
			backBtn.Visibility = Visibility.Collapsed;
		}
		else
		{
			backBtn.Visibility = Visibility.Visible;
		}
	}

	public void setForcedTagAmount(int amount)
	{
		forcedTagAmount = amount;
		if (amount > 0)
		{
			forcedAmountLabel.Content = "Add " + amount + " more tags";
			setForced(forced: true);
		}
		else
		{
			setForced(forced: false);
			forcedAmountLabel.Content = "";
		}
	}

	public int getForcedAmount()
	{
		return forcedTagAmount;
	}

	public bool getForced()
	{
		return forced;
	}

	public void reloadImagesVideosTags()
	{
		Task.Run(delegate
		{
			mediaCatalog.Reload();
			getVideos();
			getImages();
			getTags();
		}).ContinueWith(delegate
		{
			base.Dispatcher.Invoke(delegate
			{
				if (typeButton)
				{
					setImage();
				}
				else
				{
					setVideo();
				}
			});
		});
	}

	private void ImageTagger_Loaded(object sender, RoutedEventArgs e)
	{
		SessionTraceLogger.Info("tagger", "loaded initialized=" + mediaBrowserInitialized);
		SessionTraceLogger.Memory("tagger", "loaded");
		if (mediaBrowserInitialized)
		{
			return;
		}
		mediaBrowserInitialized = true;
		Dispatcher.BeginInvoke(new Action(delegate
		{
			mediaBrowser = new MediaBrowser(mediaCatalog);
			mediaBrowser.MediaSelected += MediaBrowser_MediaSelected;
			mediaBrowser.MediaSelectionChanged += MediaBrowser_MediaSelectionChanged;
			MediaBrowserHost.Content = mediaBrowser;
			ApplyExplorerViewState();
		}));
	}

	private void createImageButtons()
	{
		modTagGroupsByButton.Clear();
		buttons = new List<ImageButton>
		{
			new ImageButton(this, "Glaring", 2),
			new ImageButton(this, "Smiling", 2),
			new ImageButton(this, "Smirking", 2),
			new ImageButton(this, "Deadpan", 2),
			new ImageButton(this, "Ahegao", 2),
			new ImageButton(this, "Annoyed", 2),
			new ImageButton(this, "Surprise", 2),
			new ImageButton(this, "Horny", 2),
			new ImageButton(this, "Nude", 1),
			new ImageButton(this, "Suggestive", 1),
			new ImageButton(this, "Fully-Clothed", 1),
			new ImageButton(this, "Censored", 1),
			new ImageButton(this, "Solo", 3),
			new ImageButton(this, "Duo", 3),
			new ImageButton(this, "Threesome", 3),
			new ImageButton(this, "Group", 3),
			new ImageButton(this, "Girl(s)"),
			new ImageButton(this, "Futa(s)", -1, "chicks with dicks"),
			new ImageButton(this, "Non-Binary(s)", -1, "for anything in between and outside the scope of male and female"),
			new ImageButton(this, "Guy(s)"),
			new ImageButton(this, "Caption", 0, "medias tagged as a caption won't be shown normally, they have a chance to be shown for a longer time when you edge"),
			new ImageButton(this, "Loop", 0, "video will loop when reaching the end instead of picking a new video"),
			new ImageButton(this, "From Beginning", 0, "video will always play from the start"),
			new ImageButton(this, "Shows-Instructor", 0, "shows the instructor/domme/owner"),
			new ImageButton(this, "Always Mute", 5, "overrides the mute setting on the homescreen so this will never play audio"),
			new ImageButton(this, "Always Audio", 5, "overrides the mute setting on the homescreen so this will always play audio"),
			new ImageButton(this, "Reduce Volume", 6, "when mixing the audio, play this more quietly than other files"),
			new ImageButton(this, "Increase Volume", 6, "when mixing the audio, play this louder than other files"),
			new ImageButton(this, "Femdom", 0),
			new ImageButton(this, "Maledom", 0),
			new ImageButton(this, "Masturbation", 0),
			new ImageButton(this, "Vanilla-Sex", 0),
			new ImageButton(this, "Anal", 0),
			new ImageButton(this, "Double-Penetration", 0),
			new ImageButton(this, "Cunnilingus", 0),
			new ImageButton(this, "Blowjob", 0),
			new ImageButton(this, "Thighjob", 0),
			new ImageButton(this, "Footjob", 0),
			new ImageButton(this, "Handjob", 0),
			new ImageButton(this, "Boobjob", 0),
			new ImageButton(this, "Nipple-Play", 0),
			new ImageButton(this, "CBT", 0, "cock and ball torture"),
			new ImageButton(this, "Bondage", 0),
			new ImageButton(this, "SPH", 0, "small penis humiliation"),
			new ImageButton(this, "Chastity", 0),
			new ImageButton(this, "T&D", 0, "tease and denial"),
			new ImageButton(this, "POV", 0, "point of view"),
			new ImageButton(this, "Femboy", 0),
			new ImageButton(this, "MILF", 0),
			new ImageButton(this, "Cuckold", 0),
			new ImageButton(this, "Hypnosis", 0),
			new ImageButton(this, "Humiliation", 0),
			new ImageButton(this, "Lesbian", 0, "female same sex attraction"),
			new ImageButton(this, "Gay", 0, "male same sex attraction"),
			new ImageButton(this, "Boobs"),
			new ImageButton(this, "Ass"),
			new ImageButton(this, "Mouth"),
			new ImageButton(this, "Cock"),
			new ImageButton(this, "Feet"),
			new ImageButton(this, "Hands"),
			new ImageButton(this, "Pussy"),
			new ImageButton(this, "Armpits"),
			new ImageButton(this, "Thighs"),
			new ImageButton(this, "Midriff"),
			new ImageButton(this, "Cum"),
			new ImageButton(this, "Cropped"),
			new ImageButton(this, "Dildo", 0),
			new ImageButton(this, "Plug", 0),
			new ImageButton(this, "Strap-on", 0),
			new ImageButton(this, "Collar", 0),
			new ImageButton(this, "Leash", 0),
			new ImageButton(this, "Gag", 0)
		};
		buttons.AddRange(getCustomTags());
		removeDuplicateButtons();
	}

	public void removeDuplicateButtons()
	{
		List<ImageButton> list = new List<ImageButton>();
		List<string> list2 = new List<string>();
		foreach (ImageButton button in buttons)
		{
			if (!list2.Contains(button.name) || button.name == "")
			{
				list2.Add(button.name);
				list.Add(button);
			}
		}
		buttons = list;
		foreach (ImageButton button in modTagGroupsByButton.Keys.ToList())
		{
			if (!buttons.Contains(button))
			{
				modTagGroupsByButton.Remove(button);
			}
		}
	}

	public ImageButton[] getCustomTags()
	{
		List<string> list = new List<string>();
		if (!Directory.Exists(RuntimePaths.LinesDir))
		{
			Directory.CreateDirectory(RuntimePaths.LinesDir);
		}
		string[] directories = Directory.GetDirectories(RuntimePaths.LinesDir);
		for (int i = 0; i < directories.Length; i++)
		{
			string text = directories[i] + "\\";
			if (Directory.Exists(text + "Scripts") && Directory.Exists(text + "Vocab"))
			{
				list.Add(text);
			}
		}
		List<ImageButton> list2 = new List<ImageButton>();
		string[] collection = new string[0];
		List<string> list3 = new List<string>();
		foreach (string item in list)
		{
			if (File.Exists(item + "\\Vocab\\Base\\tags.txt"))
			{
				collection = File.ReadAllLines(item + "\\Vocab\\Base\\tags.txt");
			}
			if (File.Exists(item + "\\Vocab\\Extend\\tags.txt"))
			{
				list3.AddRange(File.ReadAllLines(item + "\\Vocab\\Extend\\tags.txt"));
			}
		}
		list3.AddRange(collection);
		foreach (string item2 in list3)
		{
			if (item2.Trim() != "")
			{
				list2.Add(new ImageButton(this, item2, 0));
			}
			else
			{
				list2.Add(new ImageButton(0));
			}
		}
		foreach (ModTagDefinition tagDefinition in ModService.GetEnabledTagDefinitions())
		{
			string text2 = string.IsNullOrWhiteSpace(tagDefinition.Label) ? tagDefinition.Key : tagDefinition.Label;
			if (!string.IsNullOrWhiteSpace(text2))
			{
				ImageButton imageButton = new ImageButton(this, text2.Trim(), 0);
				modTagGroupsByButton[imageButton] = string.IsNullOrWhiteSpace(tagDefinition.Group) ? "Custom" : tagDefinition.Group.Trim();
				list2.Add(imageButton);
			}
		}
		return list2.ToArray();
	}

	private void getTags()
	{
		Dictionary<string, string> tagMap = mediaCatalog.GetTagMapSnapshot();
		rawTags = tagMap.Select(delegate(KeyValuePair<string, string> item)
		{
			return item.Key + "kljnrbkrbasxalkxmbt" + item.Value;
		}).ToArray();
		tagNames = new List<string>();
		tags = new List<string>();
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> item2 in tagMap)
		{
			if ((images.Contains(item2.Key) || videos.Contains(item2.Key)) && !list.Contains(item2.Key))
			{
				tagNames.Add(item2.Key);
				tags.Add(item2.Value);
				list.Add(item2.Key);
			}
		}
	}

	private string BuildCurrentTagString()
	{
		return string.Concat(buttons.Select(delegate(ImageButton button)
		{
			return button.isActive();
		}));
	}

	private void UpdateLocalTagCache(string relativePath, string tagString)
	{
		int num = tagNames.IndexOf(relativePath);
		if (string.IsNullOrWhiteSpace(tagString))
		{
			if (num >= 0)
			{
				tagNames.RemoveAt(num);
				tags.RemoveAt(num);
			}
			return;
		}
		if (num >= 0)
		{
			tags[num] = tagString;
		}
		else
		{
			tagNames.Add(relativePath);
			tags.Add(tagString);
		}
	}

	private string GetCachedTagsForPath(string relativePath)
	{
		int num = tagNames.IndexOf(relativePath);
		if (num >= 0)
		{
			return tags[num];
		}
		return "";
	}

	private string BuildTagStringFromSet(HashSet<string> selectedTags)
	{
		return string.Concat(buttons.Where(delegate(ImageButton button)
		{
			return selectedTags.Contains(button.name);
		}).Select(delegate(ImageButton button)
		{
			return button.name;
		}));
	}

	private HashSet<string> GetTagSetForPath(string relativePath)
	{
		string tagString = GetCachedTagsForPath(relativePath);
		return buttons.Where(delegate(ImageButton button)
		{
			return !string.IsNullOrWhiteSpace(button.name) && tagString.Contains(button.name);
		}).Select(delegate(ImageButton button)
		{
			return button.name;
		}).ToHashSet(StringComparer.Ordinal);
	}

	private void ShowCommonTagsForBrowserSelection()
	{
		if (selectedBrowserMediaPaths.Count == 0)
		{
			setTags("");
			return;
		}
		HashSet<string> commonTags = null;
		foreach (string path in selectedBrowserMediaPaths)
		{
			HashSet<string> pathTags = GetTagSetForPath(path);
			if (commonTags == null)
			{
				commonTags = pathTags;
			}
			else
			{
				commonTags.IntersectWith(pathTags);
			}
		}
		setTags(BuildTagStringFromSet(commonTags ?? new HashSet<string>(StringComparer.Ordinal)));
	}

	public void ApplyBrowserSelectionTagChange(ImageButton changedButton, bool selected)
	{
		if (!browserMultiSelectionMode || changedButton == null || string.IsNullOrWhiteSpace(changedButton.name))
		{
			return;
		}
		List<string> selectedPaths = selectedBrowserMediaPaths.ToList();
		List<string> radioSiblingTags = buttons.Where(delegate(ImageButton button)
		{
			return selected && changedButton.radioButton > 0 && button != changedButton && button.radioButton == changedButton.radioButton && !string.IsNullOrWhiteSpace(button.name);
		}).Select(delegate(ImageButton button)
		{
			return button.name;
		}).ToList();

		if (selected)
		{
			if (radioSiblingTags.Count > 0)
			{
				mediaCatalog.ApplyBulkTags(selectedPaths, radioSiblingTags, BulkTagOperationMode.RemoveSpecific, GetKnownTagsInOrder());
			}
			mediaCatalog.ApplyBulkTags(selectedPaths, new[] { changedButton.name }, BulkTagOperationMode.Merge, GetKnownTagsInOrder());
		}
		else
		{
			mediaCatalog.ApplyBulkTags(selectedPaths, new[] { changedButton.name }, BulkTagOperationMode.RemoveSpecific, GetKnownTagsInOrder());
		}

		foreach (string path in selectedPaths)
		{
			HashSet<string> pathTags = GetTagSetForPath(path);
			if (selected)
			{
				foreach (string siblingTag in radioSiblingTags)
				{
					pathTags.Remove(siblingTag);
				}
				pathTags.Add(changedButton.name);
			}
			else
			{
				pathTags.Remove(changedButton.name);
			}
			UpdateLocalTagCache(path, BuildTagStringFromSet(pathTags));
		}
		ShowCommonTagsForBrowserSelection();
	}

	public void setAmounts(int difference)
	{
		if (forced)
		{
			setForcedTagAmount(getForcedAmount() + difference);
		}
		if (hwScreen != null && hwScreen.activeTask.changeAmount(difference) < 1)
		{
			base.NavigationService.GoBack();
			displayShowTaskBtn(show: false);
		}
	}

	private void saveImageTags()
	{
		if (images.Count <= 0)
		{
			return;
		}
		string text = BuildCurrentTagString();
		string text2 = images[currentImagePointer % images.Count];
		SessionTraceLogger.Info("tag-save", "image path=" + text2 + " tagsLength=" + text.Length);
		mediaCatalog.SetTags(text2, text);
		UpdateLocalTagCache(text2, text);
	}

	private void saveVideoTags()
	{
		if (videos.Count <= 0)
		{
			return;
		}
		string text = BuildCurrentTagString();
		string text2 = videos[currentVideoPointer % videos.Count];
		SessionTraceLogger.Info("tag-save", "video path=" + text2 + " tagsLength=" + text.Length);
		mediaCatalog.SetTags(text2, text);
		UpdateLocalTagCache(text2, text);
	}

	private void setTags(string tagString)
	{
		foreach (ImageButton button in buttons)
		{
			button.setActive(tagString.Contains(button.textBox.Text));
		}
		setVolume();
	}

	private void setTagsByGroup(string tagString)
	{
		playClickSound();
		foreach (ImageButton button in buttons)
		{
			if (tagString.Contains(button.textBox.Text) && button.isActive() == "")
			{
				setAmounts(-1);
			}
			if (!tagString.Contains(button.textBox.Text) && button.isActive() != "")
			{
				setAmounts(1);
			}
		}
		setTags(tagString);
		setVolume();
	}

	public string getTagsOfCurrentImage()
	{
		string text = "";
		foreach (ImageButton button in buttons)
		{
			if (button.isActive() != "")
			{
				text = text + button.textBox.Text + " ";
			}
		}
		return text;
	}

	public void setVolume()
	{
		if (videos.Count <= 0)
		{
			return;
		}
		string text = "";
		if (typeButton)
		{
			text = GetCachedTagsForPath(videos[currentVideoPointer % videos.Count]);
		}
		else
		{
			text = BuildCurrentTagString();
		}
		if (text.Contains("Always Mute"))
		{
			currentVideo.IsMuted = true;
		}
		else if (text.Contains("Always Audio"))
		{
			currentVideo.IsMuted = false;
		}
		else
		{
			currentVideo.IsMuted = shouldBeMuted;
		}
		if (text.Contains("Increase Volume"))
		{
			currentVideo.Volume = 0.8;
		}
		else if (text.Contains("Reduce Volume"))
		{
			currentVideo.Volume = 0.2;
		}
		else
		{
			currentVideo.Volume = 0.5;
		}
	}

	private void clickNeutral()
	{
		if (typeButton)
		{
			setImage();
			saveImageTags();
		}
		else
		{
			setVideo();
			saveVideoTags();
		}
	}

	private void clickNext(object sender, RoutedEventArgs e)
	{
		playClickSound();
		if (typeButton)
		{
			saveImageTags();
			currentImagePointer++;
			setImage();
		}
		else
		{
			saveVideoTags();
			currentVideoPointer++;
			setVideo();
		}
	}

	private void clickPrev(object sender, RoutedEventArgs e)
	{
		playClickSound();
		if (typeButton)
		{
			saveImageTags();
			currentImagePointer--;
			if (currentImagePointer < 0)
			{
				currentImagePointer += images.Count();
			}
			setImage();
		}
		else
		{
			saveVideoTags();
			currentVideoPointer--;
			if (currentVideoPointer < 0)
			{
				currentVideoPointer += videos.Count();
			}
			setVideo();
		}
	}

	private void clickUntagged(object sender, RoutedEventArgs e)
	{
		playClickSound();
		if (typeButton)
		{
			saveImageTags();
			for (int i = 1; i < images.Count; i++)
			{
				if (!tagNames.Contains(images[(i + currentImagePointer) % images.Count]))
				{
					currentImagePointer = (i + currentImagePointer) % images.Count;
					setImage();
					break;
				}
			}
			return;
		}
		saveVideoTags();
		for (int j = 1; j < videos.Count; j++)
		{
			if (!tagNames.Contains(videos[(j + currentVideoPointer) % videos.Count]))
			{
				currentVideoPointer = (j + currentVideoPointer) % videos.Count;
				setVideo();
				break;
			}
		}
	}

	private void setImage()
	{
		base.Dispatcher.Invoke(delegate
		{
			if (images.Count > 0)
			{
				bool flag = true;
				for (int i = 0; i < tagNames.Count; i++)
				{
					if (tagNames[i] == images[currentImagePointer % images.Count])
					{
						flag = false;
						setTags(tags[i]);
					}
				}
				if (flag)
				{
					foreach (ImageButton button in buttons)
					{
						button.setActive(isActive: false);
					}
				}
				Task.Run((Action)Transform);
				pathOfFile.Content = images[currentImagePointer % images.Count];
			}
		});
	}

	private void Transform()
	{
		string imagePath = images[currentImagePointer % images.Count];
		string fullPath = RuntimePaths.ResolveRuntimePath(imagePath);
		try
		{
			if (!ImageFileSafety.IsSafeForWpfDecode(fullPath, out string unsafeReason))
			{
				SessionTraceLogger.Info("tagger-preview-skip", "Skipped unsafe preview image: " + imagePath + " reason=" + unsafeReason);
				base.Dispatcher.Invoke(delegate
				{
					currentImage.Source = null;
				});
				return;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
			bitmapImage.UriSource = new Uri(fullPath);
			bitmapImage.DecodePixelHeight = Math.Max(1, (int)base.ActualHeight);
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			int pixelHeight = bitmapImage.PixelHeight;
			int pixelWidth = bitmapImage.PixelWidth;
			double actualHeight = imageTaggerBottom.ActualHeight;
			double actualWidth = imageTaggerBottom.ActualWidth;
			double num = 1.0;
			if (actualHeight != 0.0)
			{
				num = ((!((double)pixelHeight / actualHeight > (double)pixelWidth / actualWidth)) ? (actualWidth / (double)pixelWidth) : (actualHeight / (double)pixelHeight));
			}
			TransformedBitmap transform = new TransformedBitmap();
			transform.BeginInit();
			transform.Source = bitmapImage;
			transform.Transform = new ScaleTransform(num, num);
			transform.EndInit();
			transform.Freeze();
			base.Dispatcher.Invoke(delegate
			{
				currentImage.Source = transform;
			});
		}
		catch (Exception ex)
		{
			SessionTraceLogger.Error("tagger-preview-decode", "Failed to load preview image: " + imagePath, ex);
			SessionTraceLogger.Memory("tagger-preview-decode", "after preview decode failure");
		}
	}

	private void setVideo()
	{
		if (videos.Count <= 0)
		{
			return;
		}
		bool flag = true;
		for (int i = 0; i < tagNames.Count; i++)
		{
			if (tagNames[i] == videos[currentVideoPointer % videos.Count])
			{
				flag = false;
				setTags(tags[i]);
				break;
			}
		}
		if (flag)
		{
			foreach (ImageButton button in buttons)
			{
				button.setActive(isActive: false);
			}
		}
		currentImage.Source = null;
		currentImage.Visibility = Visibility.Collapsed;
		currentVideo.Stop();
		currentVideo.Source = null;
		string relativePath = videos[currentVideoPointer % videos.Count];
		if (TrySetCurrentVideoSource(relativePath))
		{
			currentVideo.Visibility = Visibility.Visible;
		}
		else
		{
			currentVideo.Visibility = Visibility.Collapsed;
		}
		pathOfFile.Content = relativePath;
	}

	private bool TrySetCurrentVideoSource(string relativePath)
	{
		currentVideoRelativePath = relativePath ?? "";
		try
		{
			string fullPath = RuntimePaths.ResolveRuntimePath(currentVideoRelativePath);
			if (!File.Exists(fullPath))
			{
				SessionTraceLogger.Error("tagger-video", "Video file not found relative=" + currentVideoRelativePath + " full=" + fullPath);
				return false;
			}
			SessionTraceLogger.Info("tagger-video", "load relative=" + currentVideoRelativePath + " full=" + fullPath);
			currentVideo.Source = new Uri(fullPath, UriKind.Absolute);
			return true;
		}
		catch (Exception ex)
		{
			SessionTraceLogger.Error("tagger-video", "Failed to set video source relative=" + currentVideoRelativePath, ex);
			currentVideo.Source = null;
			return false;
		}
	}

	private void getImages()
	{
		images = mediaCatalog.GetActiveImagePaths();
	}

	public string[] searchSubFolders(string currentDirectory)
	{
		List<string> list = new List<string>();
		string[] directories = Directory.GetDirectories(currentDirectory);
		for (int i = 0; i < directories.Length; i++)
		{
			list.AddRange(searchSubFolders(directories[i]));
		}
		list.AddRange(Directory.GetFiles(currentDirectory));
		return list.ToArray();
	}

	private void getVideos()
	{
		videos = mediaCatalog.GetActiveVideoPaths();
	}

	public List<string> getImagesWithTag(string[] requiredTags, string[] disallowedTags)
	{
		return mediaCatalog.GetTaggedMedia(requiredTags, disallowedTags);
	}

	public string[] getTaggedImages()
	{
		return tagNames.Where(mediaCatalog.IsImagePath).ToArray();
	}

	public string[] getTaggedVideos()
	{
		return tagNames.Where(mediaCatalog.IsVideoPath).ToArray();
	}

	public List<string> getTaggedVideo(string lookedForTag)
	{
		return mediaCatalog.GetTaggedVideoPaths(lookedForTag);
	}

	public List<string> getTaggedImage(string lookedForTag)
	{
		return mediaCatalog.GetTaggedImagePaths(lookedForTag);
	}

	public List<string> allTaggedWith(string tagString)
	{
		string[] array = tagString.Split(',');
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text != "")
			{
				if (text.StartsWith('!'))
				{
					list2.Add(text.Split('!')[1]);
				}
				else
				{
					list.Add(text);
				}
			}
		}
		if (!list.Contains("Caption"))
		{
			list2.Add("Caption");
		}
		return getImagesWithTag(list.ToArray(), list2.ToArray());
	}

	public bool hasManyUntaggedImages()
	{
		if ((double)tagNames.Count / (double)images.Concat(videos).ToArray().Length < 0.5)
		{
			return true;
		}
		return false;
	}

	public int GetAvailableImageCount()
	{
		return mediaCatalog.GetActiveImagePaths().Count;
	}

	public List<string> activeTags(string lookedForTag)
	{
		string tagString = mediaCatalog.GetTags(lookedForTag);
		List<string> result = new List<string>();
		foreach (ImageButton button in buttons)
		{
			if (tagString.Contains(button.textBox.Text))
			{
				result.Add(button.textBox.Text);
			}
		}
		return result;
	}

	private void btnBackClick(object sender, RoutedEventArgs e)
	{
		backToMenu();
	}

	public void displayShowTaskBtn(bool show, HomeworkScreen hwScreen = null)
	{
		if (show)
		{
			this.hwScreen = hwScreen;
			backBtnTask.Visibility = Visibility.Visible;
		}
		else
		{
			this.hwScreen = null;
			backBtnTask.Visibility = Visibility.Collapsed;
		}
	}

	public int getForcedTagAmount()
	{
		return forcedTagAmount;
	}

	private void showTaskClick(object sender, RoutedEventArgs e)
	{
		playClickSound();
		if (typeButton)
		{
			saveImageTags();
		}
		else
		{
			saveVideoTags();
		}
		base.NavigationService.GoBack();
	}

	public void backToMenu()
	{
		playClickSound();
		if (typeButton)
		{
			saveImageTags();
		}
		else
		{
			saveVideoTags();
		}
		base.NavigationService.GoBack();
		parent.resumeSession();
	}

	private void updateType()
	{
		if (typeButton)
		{
			saveVideoTags();
			typeBtn.Content = "Image";
			getImages();
			currentVideo.Source = null;
			currentVideo.Visibility = Visibility.Collapsed;
			currentImage.Visibility = Visibility.Visible;
		}
		else
		{
			saveImageTags();
			typeBtn.Content = "Video";
			getVideos();
			currentVideo.Visibility = Visibility.Visible;
			currentImage.Visibility = Visibility.Collapsed;
		}
		clickNeutral();
	}

	private void typeBtn_Click(object sender, RoutedEventArgs e)
	{
		playClickSound();
		typeButton = !typeButton;
		updateType();
	}

	private void folderBtn_Click(object sender, RoutedEventArgs e)
	{
		playClickSound();
		base.Dispatcher.Invoke(delegate
		{
			string folderPath = "";
			if (typeButton)
			{
				if (images.Count > 0)
				{
					string currentImage = images[currentImagePointer % images.Count];
					folderPath = Path.GetDirectoryName(RuntimePaths.ResolveRuntimePath(currentImage));
				}
				else
				{
					folderPath = RuntimePaths.ImagesDir;
				}
			}
			else
			{
				if (videos.Count > 0)
				{
					string currentVideo = videos[currentVideoPointer % videos.Count];
					folderPath = Path.GetDirectoryName(RuntimePaths.ResolveRuntimePath(currentVideo));
				}
				else
				{
					folderPath = RuntimePaths.VideosDir;
				}
			}
			if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
			{
				Process.Start(Environment.GetEnvironmentVariable("WINDIR") + "\\explorer.exe", folderPath);
			}
		});
	}

	private void currentVideo_MediaOpened(object sender, RoutedEventArgs e)
	{
		base.Dispatcher.BeginInvoke(new Action(delegate
		{
			if (!typeButton && currentVideo.Source != null)
			{
				currentVideo.Position = TimeSpan.Zero;
				currentVideo.Play();
			}
		}), System.Windows.Threading.DispatcherPriority.Background);
	}

	private void currentVideo_MediaEnded(object sender, RoutedEventArgs e)
	{
		currentVideo.Position = new TimeSpan(0L);
	}

	private void currentVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
	{
		SessionTraceLogger.Error("tagger-video", "MediaElement failed relative=" + currentVideoRelativePath + " source=" + (currentVideo.Source?.LocalPath ?? ""), e.ErrorException);
		currentVideo.Stop();
		currentVideo.Source = null;
		currentVideo.Visibility = Visibility.Collapsed;
	}

	private void createGroup_Click(object sender, RoutedEventArgs e)
	{
		SessionTraceLogger.Info("tagger-overlay", "toggle group overlay active=" + (activeTaggerGroup != null));
		SessionTraceLogger.Memory("tagger-overlay", "before group toggle");
		playClickSound();
		if (activeBulkTagger != null)
		{
			OverlayHost.Children.Remove(activeBulkTagger);
			activeBulkTagger = null;
		}
		if (activeTaggerGroup != null)
		{
			OverlayHost.Children.Remove(activeTaggerGroup);
			activeTaggerGroup = null;
			return;
		}
		activeTaggerGroup = new ImageTaggerGroup(this);
		OverlayHost.Children.Add(activeTaggerGroup);
		SessionTraceLogger.Memory("tagger-overlay", "after group open");
	}

	private void bulkTagging_Click(object sender, RoutedEventArgs e)
	{
		SessionTraceLogger.Info("tagger-overlay", "toggle bulk overlay active=" + (activeBulkTagger != null) + " selected=" + selectedBrowserMediaPaths.Count);
		SessionTraceLogger.Memory("tagger-overlay", "before bulk toggle");
		playClickSound();
		if (activeTaggerGroup != null)
		{
			OverlayHost.Children.Remove(activeTaggerGroup);
			activeTaggerGroup = null;
		}
		if (activeBulkTagger != null)
		{
			OverlayHost.Children.Remove(activeBulkTagger);
			activeBulkTagger = null;
			return;
		}
		activeBulkTagger = new ImageTaggerBulkTagger(this);
		OverlayHost.Children.Add(activeBulkTagger);
		SessionTraceLogger.Memory("tagger-overlay", "after bulk open");
	}

	public void removeTaggerGroup(ImageTaggerGroup imgTagGroup)
	{
		SessionTraceLogger.Info("tagger-overlay", "remove group overlay");
		OverlayHost.Children.Remove(imgTagGroup);
		if (ReferenceEquals(activeTaggerGroup, imgTagGroup))
		{
			activeTaggerGroup = null;
		}
	}

	public void removeBulkTagger(ImageTaggerBulkTagger bulkTagger)
	{
		SessionTraceLogger.Info("tagger-overlay", "remove bulk overlay");
		OverlayHost.Children.Remove(bulkTagger);
		if (ReferenceEquals(activeBulkTagger, bulkTagger))
		{
			activeBulkTagger = null;
		}
	}

	public IReadOnlyList<BulkTagCategoryDefinition> GetBulkTagCategories()
	{
		List<BulkTagCategoryDefinition> list = new List<BulkTagCategoryDefinition>();
		for (int i = 0; i < tagCategories.Count; i++)
		{
			string title = ((i < tagCategoryTitles.Count) ? tagCategoryTitles[i] : "Custom");
			List<string> list2 = tagCategories[i].Where(delegate(ImageButton button)
			{
				return button.grid.Visibility != Visibility.Hidden && !string.IsNullOrWhiteSpace(button.name);
			}).Select(delegate(ImageButton button)
			{
				return button.name;
			}).Distinct(StringComparer.Ordinal).ToList();
			if (list2.Count != 0)
			{
				list.Add(new BulkTagCategoryDefinition
				{
					Title = title,
					Tags = list2
				});
			}
		}
		return list;
	}

	public IReadOnlyList<string> GetKnownTagsInOrder()
	{
		return buttons.Where(delegate(ImageButton button)
		{
			return button.grid.Visibility != Visibility.Hidden && !string.IsNullOrWhiteSpace(button.name);
		}).Select(delegate(ImageButton button)
		{
			return button.name;
		}).Distinct(StringComparer.Ordinal).ToList();
	}

	public void SaveCurrentMediaTags()
	{
		if (browserMultiSelectionMode)
		{
			return;
		}
		if (typeButton)
		{
			saveImageTags();
		}
		else
		{
			saveVideoTags();
		}
	}

	public void RefreshBrowserAndTags()
	{
		SessionTraceLogger.Info("tagger-refresh", "refresh browser and tags start");
		SessionTraceLogger.Memory("tagger-refresh", "before refresh");
		reloadImagesVideosTags();
		mediaBrowser?.ReloadMedia();
		SessionTraceLogger.Memory("tagger-refresh", "after refresh requested");
	}

	public IReadOnlyList<string> GetSelectedBrowserMediaPaths()
	{
		return selectedBrowserMediaPaths.ToList();
	}

	public int ApplyBrowserTagFilter(MediaBrowserTagFilterMode mode, IEnumerable<string> selectedTags)
	{
		mediaBrowser?.ApplyTagFilter(mode, selectedTags);
		return mediaBrowser?.FilteredMediaCount ?? 0;
	}

	public int ClearBrowserTagFilter()
	{
		mediaBrowser?.ClearTagFilter();
		return mediaBrowser?.FilteredMediaCount ?? 0;
	}

	private void UpdateBrowserSelectionStatus()
	{
		if (selectionStatusText == null)
		{
			return;
		}
		selectionStatusText.Text = selectedBrowserMediaPaths.Count == 0 ? "Selected: 0" : "Selected: " + selectedBrowserMediaPaths.Count;
		bulkTaggingBtn.Content = selectedBrowserMediaPaths.Count == 0 ? "Operations" : "Operations (" + selectedBrowserMediaPaths.Count + ")";
		bulkMoveBtn.Content = selectedBrowserMediaPaths.Count == 0 ? "Move" : "Move (" + selectedBrowserMediaPaths.Count + ")";
		explorerViewBtn.Content = explorerViewEnabled ? "Preview" : "Explorer";
	}

	private async void bulkMove_Click(object sender, RoutedEventArgs e)
	{
		playClickSound();
		List<string> selectedPaths = GetSelectedBrowserMediaPaths().ToList();
		if (selectedPaths.Count == 0)
		{
			MessageBox.Show("Select one or more media files before bulk moving.", "OpenEdge", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		string destinationFolder = ChooseDestinationFolder();
		if (string.IsNullOrWhiteSpace(destinationFolder))
		{
			return;
		}
		MessageBoxResult confirmation = MessageBox.Show("Move " + selectedPaths.Count + " selected media file(s) to:\n" + destinationFolder + "\n\nTags will be preserved.", "Confirm bulk move", MessageBoxButton.OKCancel, MessageBoxImage.Question);
		if (confirmation != MessageBoxResult.OK)
		{
			return;
		}
		bulkMoveBtn.IsEnabled = false;
		BulkMoveResult result = null;
		try
		{
			SessionTraceLogger.Info("bulk-move", "start count=" + selectedPaths.Count + " destination=" + destinationFolder);
			SessionTraceLogger.Memory("bulk-move", "before save/move");
			SaveCurrentMediaTags();
			result = await Task.Run(delegate
			{
				return mediaCatalog.MoveMedia(selectedPaths, destinationFolder);
			});
			SessionTraceLogger.Info("bulk-move", "move complete requested=" + result.RequestedCount + " moved=" + result.MovedCount + " skipped=" + result.SkippedCount + " renamed=" + result.RenamedCount);
			SessionTraceLogger.Memory("bulk-move", "after file move before refresh");
		}
		catch (Exception ex)
		{
			SessionTraceLogger.Error("bulk-move", "file move failed destination=" + destinationFolder, ex);
			SessionTraceLogger.Memory("bulk-move", "after file move failure");
			MessageBox.Show(ex.Message, "Bulk move failed during file move", MessageBoxButton.OK, MessageBoxImage.Error);
			bulkMoveBtn.IsEnabled = true;
			return;
		}

		try
		{
			RefreshBrowserAndTags();
			selectedBrowserMediaPaths.Clear();
			UpdateBrowserSelectionStatus();
			SessionTraceLogger.Memory("bulk-move", "after refresh");
			MessageBox.Show("Move complete.\n\nRequested: " + result.RequestedCount + "\nMoved: " + result.MovedCount + "\nSkipped: " + result.SkippedCount + "\nCollision renamed: " + result.RenamedCount, "OpenEdge", MessageBoxButton.OK, MessageBoxImage.Information);
		}
		catch (Exception ex)
		{
			SessionTraceLogger.Error("bulk-move-refresh", "post-move refresh failed after successful file move", ex);
			SessionTraceLogger.Memory("bulk-move-refresh", "after refresh failure");
			MessageBox.Show("Files were moved, but refreshing the browser failed.\n\nRequested: " + result.RequestedCount + "\nMoved: " + result.MovedCount + "\nSkipped: " + result.SkippedCount + "\nCollision renamed: " + result.RenamedCount + "\n\nRefresh error:\n" + ex.Message, "Bulk move completed with refresh error", MessageBoxButton.OK, MessageBoxImage.Warning);
		}
		finally
		{
			bulkMoveBtn.IsEnabled = true;
		}
	}

	private string ChooseDestinationFolder()
	{
		BulkMoveDestinationDialog dialog = new BulkMoveDestinationDialog(mediaCatalog.GetSources())
		{
			Owner = Window.GetWindow(this)
		};
		if (dialog.ShowDialog() == true)
		{
			return dialog.SelectedDestinationPath;
		}
		return "";
	}

	private void explorerViewBtn_Click(object sender, RoutedEventArgs e)
	{
		playClickSound();
		SessionTraceLogger.Info("explorer-view", "toggle from=" + explorerViewEnabled);
		SessionTraceLogger.Memory("explorer-view", "before toggle");
		explorerViewEnabled = !explorerViewEnabled;
		ApplyExplorerViewState();
		UpdateBrowserSelectionFromBrowser();
	}

	private void ApplyExplorerViewState()
	{
		SessionTraceLogger.Info("explorer-view", "apply enabled=" + explorerViewEnabled);
		Grid.SetColumnSpan(BrowsePanel, explorerViewEnabled ? 2 : 1);
		PreviewPanel.Visibility = explorerViewEnabled ? Visibility.Collapsed : Visibility.Visible;
		if (mediaBrowser != null)
		{
			if (explorerViewEnabled)
			{
				mediaBrowser.EnableExplorerLayout(150);
			}
			else
			{
				mediaBrowser.DisableExplorerLayout();
			}
		}
		UpdateBrowserSelectionStatus();
	}

	private void pathOfFile_Click(object sender, RoutedEventArgs e)
	{
		playClickSound();
		base.Dispatcher.Invoke(delegate
		{
			if (typeButton && images.Count > 0)
			{
				Clipboard.SetText(images[currentImagePointer % images.Count]);
			}
			else if (!typeButton && videos.Count > 0)
			{
				Clipboard.SetText(videos[currentVideoPointer % videos.Count]);
			}
			else
			{
				Clipboard.Clear();
			}
			DoubleAnimation animation = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(2000.0));
			copiedText.BeginAnimation(UIElement.OpacityProperty, animation);
		});
	}

	public void playClickSound()
	{
		parent.playClickSound();
	}
}
