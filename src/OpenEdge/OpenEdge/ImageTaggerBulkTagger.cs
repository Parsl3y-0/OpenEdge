using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace OpenEdge;

public partial class ImageTaggerBulkTagger : Grid, IComponentConnector
{
	private const string TagSearchPlaceholder = "Search tags";

	private sealed class FolderNode
	{
		private readonly Dictionary<string, FolderNode> childrenByKey = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);

		public string DisplayName { get; }

		public string FullPath { get; }

		public bool IsSourceRoot { get; }

		public List<string> DirectMediaPaths { get; } = new List<string>();

		public List<FolderNode> Children { get; } = new List<FolderNode>();

		public CheckBox Selector { get; set; }

		public int TotalMediaCount { get; private set; }

		public FolderNode(string displayName, string fullPath, bool isSourceRoot)
		{
			DisplayName = displayName;
			FullPath = fullPath;
			IsSourceRoot = isSourceRoot;
		}

		public FolderNode GetOrAddChild(string name, string fullPath)
		{
			if (!childrenByKey.TryGetValue(name, out FolderNode value))
			{
				value = new FolderNode(name, fullPath, isSourceRoot: false);
				childrenByKey[name] = value;
				Children.Add(value);
			}
			return value;
		}

		public int RecalculateCounts()
		{
			TotalMediaCount = DirectMediaPaths.Count;
			foreach (FolderNode child in Children)
			{
				TotalMediaCount += child.RecalculateCounts();
			}
			return TotalMediaCount;
		}
	}

	private readonly ImageTagger tagger;

	private readonly MediaCatalogService mediaCatalog;

	private readonly List<FolderNode> rootNodes = new List<FolderNode>();

	private readonly HashSet<string> selectedTags = new HashSet<string>(StringComparer.Ordinal);

	private bool updatingFolderChecks;

	public ImageTaggerBulkTagger(ImageTagger tagger)
	{
		InitializeComponent();
		this.tagger = tagger;
		mediaCatalog = tagger.MediaCatalog;
		TagSearchBox.Text = TagSearchPlaceholder;
		TagSearchBox.Foreground = Brushes.Gray;
		TagSearchBox.GotFocus += TagSearchBox_GotFocus;
		TagSearchBox.LostFocus += TagSearchBox_LostFocus;
		LoadModes();
		RebuildFolderTree();
		RenderTagCategories();
		UpdateBrowserSelectionOption();
		UpdateStatusText();
	}

	private void LoadModes()
	{
		ModeCombo.Items.Add(CreateModeItem("Add / Merge Tags", BulkTagOperationMode.Merge));
		ModeCombo.Items.Add(CreateModeItem("Remove SPECIFIC Tags", BulkTagOperationMode.RemoveSpecific));
		ModeCombo.Items.Add(CreateModeItem("Keep ONLY Selected", BulkTagOperationMode.KeepOnlySelected));
		ModeCombo.Items.Add(CreateModeItem("Remove ALL Tags", BulkTagOperationMode.RemoveAll));
		ModeCombo.SelectedIndex = 0;
	}

	private void RebuildFolderTree()
	{
		rootNodes.Clear();
		FolderTree.Items.Clear();
		MediaCatalogSnapshot snapshot = mediaCatalog.Snapshot;
		Dictionary<string, MediaSourceDefinition> sourceLookup = mediaCatalog.GetSources().ToDictionary((MediaSourceDefinition source) => source.Id, StringComparer.OrdinalIgnoreCase);
		foreach (IGrouping<string, MediaItemRecord> sourceGroup in snapshot.ActiveItems.GroupBy((MediaItemRecord item) => item.SourceId).OrderBy((IGrouping<string, MediaItemRecord> group) => sourceLookup.TryGetValue(group.Key, out MediaSourceDefinition value) ? value.SortOrder : int.MaxValue))
		{
			if (!sourceLookup.TryGetValue(sourceGroup.Key, out MediaSourceDefinition source) || string.IsNullOrWhiteSpace(source.RootPath))
			{
				continue;
			}
			FolderNode folderNode = new FolderNode(source.Name, source.RootPath, isSourceRoot: true);
			foreach (MediaItemRecord item in sourceGroup.OrderBy((MediaItemRecord media) => media.FullPath, StringComparer.OrdinalIgnoreCase))
			{
				AddItemToFolderTree(folderNode, source.RootPath, item);
			}
			folderNode.RecalculateCounts();
			rootNodes.Add(folderNode);
			FolderTree.Items.Add(BuildTreeItem(folderNode));
		}
	}

	private void AddItemToFolderTree(FolderNode sourceNode, string sourceRoot, MediaItemRecord item)
	{
		string text = Path.GetDirectoryName(item.FullPath) ?? sourceRoot;
		if (!IsSameDirectoryOrChild(text, sourceRoot))
		{
			text = sourceRoot;
		}
		string relativePath = Path.GetRelativePath(sourceRoot, text);
		FolderNode folderNode = sourceNode;
		if (!string.Equals(relativePath, ".", StringComparison.OrdinalIgnoreCase))
		{
			foreach (string pathSegment in SplitPath(relativePath))
			{
				folderNode = folderNode.GetOrAddChild(pathSegment, Path.Combine(folderNode.FullPath, pathSegment));
			}
		}
		folderNode.DirectMediaPaths.Add(item.RelativePath);
	}

	private TreeViewItem BuildTreeItem(FolderNode node)
	{
		CheckBox checkBox = new CheckBox
		{
			Content = node.DisplayName + " (" + node.TotalMediaCount + ")",
			Foreground = Brushes.White,
			Margin = new Thickness(0.0, 2.0, 0.0, 2.0),
			Tag = node
		};
		checkBox.Checked += FolderCheckBox_CheckedChanged;
		checkBox.Unchecked += FolderCheckBox_CheckedChanged;
		node.Selector = checkBox;
		TreeViewItem treeViewItem = new TreeViewItem
		{
			Header = checkBox,
			Foreground = Brushes.White,
			IsExpanded = node.IsSourceRoot
		};
		foreach (FolderNode child in node.Children.OrderBy((FolderNode item) => item.DisplayName, StringComparer.OrdinalIgnoreCase))
		{
			treeViewItem.Items.Add(BuildTreeItem(child));
		}
		return treeViewItem;
	}

	private void FolderCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
	{
		if (updatingFolderChecks || !(sender is CheckBox checkBox) || !(checkBox.Tag is FolderNode folderNode))
		{
			return;
		}
		bool flag = checkBox.IsChecked == true;
		updatingFolderChecks = true;
		try
		{
			SetDescendantCheckedState(folderNode, flag);
		}
		finally
		{
			updatingFolderChecks = false;
		}
		UpdateStatusText();
	}

	private void SetDescendantCheckedState(FolderNode node, bool isChecked)
	{
		foreach (FolderNode child in node.Children)
		{
			if (child.Selector != null)
			{
				child.Selector.IsChecked = isChecked;
			}
			SetDescendantCheckedState(child, isChecked);
		}
	}

	private void CheckAll_Click(object sender, RoutedEventArgs e)
	{
		tagger.playClickSound();
		UseBrowserSelectionBox.IsChecked = false;
		SetAllNodesCheckedState(isChecked: true);
	}

	private void UncheckAll_Click(object sender, RoutedEventArgs e)
	{
		tagger.playClickSound();
		UseBrowserSelectionBox.IsChecked = false;
		SetAllNodesCheckedState(isChecked: false);
	}

	private void SetAllNodesCheckedState(bool isChecked)
	{
		updatingFolderChecks = true;
		try
		{
			foreach (FolderNode rootNode in rootNodes)
			{
				if (rootNode.Selector != null)
				{
					rootNode.Selector.IsChecked = isChecked;
				}
				SetDescendantCheckedState(rootNode, isChecked);
			}
		}
		finally
		{
			updatingFolderChecks = false;
		}
		UpdateStatusText();
	}

	private void TagSearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		RenderTagCategories();
	}

	private void TagSearchBox_GotFocus(object sender, RoutedEventArgs e)
	{
		if (TagSearchBox.Foreground == Brushes.Gray)
		{
			TagSearchBox.Text = "";
			TagSearchBox.Foreground = Brushes.White;
		}
	}

	private void TagSearchBox_LostFocus(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(TagSearchBox.Text))
		{
			TagSearchBox.Text = TagSearchPlaceholder;
			TagSearchBox.Foreground = Brushes.Gray;
		}
	}

	private void RenderTagCategories()
	{
		TagCategoriesHost.Children.Clear();
		string text = GetTagSearchText();
		foreach (BulkTagCategoryDefinition bulkTagCategory in tagger.GetBulkTagCategories())
		{
			List<string> list = bulkTagCategory.Tags.Where(delegate(string tag)
			{
				if (text.Length != 0)
				{
					return tag.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
				}
				return true;
			}).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			Expander expander = new Expander
			{
				Header = bulkTagCategory.Title + " (" + list.Count + ")",
				Foreground = Brushes.White,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
				IsExpanded = text.Length != 0 || bulkTagCategory.Title == "Expressions"
			};
			WrapPanel wrapPanel = new WrapPanel();
			foreach (string item in list)
			{
				wrapPanel.Children.Add(CreateTagCheckBox(item));
			}
			expander.Content = wrapPanel;
			TagCategoriesHost.Children.Add(expander);
		}
	}

	private CheckBox CreateTagCheckBox(string tag)
	{
		CheckBox checkBox = new CheckBox
		{
			Content = tag,
			Foreground = Brushes.White,
			Margin = new Thickness(0.0, 0.0, 14.0, 8.0),
			IsChecked = selectedTags.Contains(tag),
			Tag = tag
		};
		checkBox.Checked += TagCheckBox_CheckedChanged;
		checkBox.Unchecked += TagCheckBox_CheckedChanged;
		return checkBox;
	}

	private void TagCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
	{
		if (!(sender is CheckBox checkBox) || !(checkBox.Tag is string item))
		{
			return;
		}
		if (checkBox.IsChecked == true)
		{
			selectedTags.Add(item);
		}
		else
		{
			selectedTags.Remove(item);
		}
		UpdateStatusText();
	}

	private void MatchingFirstButton_Click(object sender, RoutedEventArgs e)
	{
		ApplyBrowserFilter(MediaBrowserTagFilterMode.MatchingFirst, "Sorted matching media first.");
	}

	private void MatchSelectedButton_Click(object sender, RoutedEventArgs e)
	{
		ApplyBrowserFilter(MediaBrowserTagFilterMode.MatchSelected, "Include filter: showing media with all selected tags.");
	}

	private void ExactMatchButton_Click(object sender, RoutedEventArgs e)
	{
		ApplyBrowserFilter(MediaBrowserTagFilterMode.ExactMatch, "Showing media whose tags exactly match the selection.");
	}

	private void MissingTagsButton_Click(object sender, RoutedEventArgs e)
	{
		ApplyBrowserFilter(MediaBrowserTagFilterMode.MissingAny, "Exclude filter: showing media that does not include all selected tags.");
	}

	private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
	{
		tagger.playClickSound();
		int count = tagger.ClearBrowserTagFilter();
		StatusText.Text = "Cleared browser tag filter. Showing " + count + " media file(s).";
	}

	private void ApplyBrowserFilter(MediaBrowserTagFilterMode mode, string message)
	{
		tagger.playClickSound();
		if (selectedTags.Count == 0)
		{
			MessageBox.Show("Select one or more tags before filtering the browser.", "OpenEdge", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		int count = tagger.ApplyBrowserTagFilter(mode, selectedTags);
		StatusText.Text = message + " " + count + " media file(s) visible.";
	}

	private async void ApplyButton_Click(object sender, RoutedEventArgs e)
	{
		tagger.playClickSound();
		HashSet<string> hashSet = CollectSelectedMediaPaths();
		if (hashSet.Count == 0)
		{
			MessageBox.Show("Select at least one browser file, folder, or subfolder before applying bulk tags.", "OpenEdge", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		BulkTagOperationMode selectedMode = GetSelectedMode();
		if ((selectedMode == BulkTagOperationMode.Merge || selectedMode == BulkTagOperationMode.RemoveSpecific) && selectedTags.Count == 0)
		{
			MessageBox.Show("Select at least one tag for this bulk operation.", "OpenEdge", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		ApplyButton.IsEnabled = false;
		StatusText.Text = "Applying bulk tag changes...";
		try
		{
			tagger.SaveCurrentMediaTags();
			List<string> list = hashSet.ToList();
			List<string> list2 = selectedTags.ToList();
			List<string> knownTagsInOrder = tagger.GetKnownTagsInOrder().ToList();
			int num = await Task.Run(delegate
			{
				return mediaCatalog.ApplyBulkTags(list, list2, selectedMode, knownTagsInOrder);
			});
			tagger.RefreshBrowserAndTags();
			StatusText.Text = "Updated " + num + " media file(s).";
			MessageBox.Show("Bulk tagging updated " + num + " media file(s).", "OpenEdge", MessageBoxButton.OK, MessageBoxImage.Information);
		}
		catch (Exception ex)
		{
			StatusText.Text = "Bulk tagging failed: " + ex.Message;
			MessageBox.Show(ex.Message, "Bulk tagging failed", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			ApplyButton.IsEnabled = true;
			UpdateStatusText();
		}
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		tagger.playClickSound();
		tagger.removeBulkTagger(this);
	}

	private HashSet<string> CollectSelectedMediaPaths()
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (UseBrowserSelectionBox.IsChecked == true)
		{
			foreach (string path in tagger.GetSelectedBrowserMediaPaths())
			{
				hashSet.Add(path);
			}
			return hashSet;
		}
		foreach (FolderNode rootNode in rootNodes)
		{
			AddSelectedMediaPaths(rootNode, hashSet);
		}
		return hashSet;
	}

	private void AddSelectedMediaPaths(FolderNode node, HashSet<string> destination)
	{
		if (node.Selector != null && node.Selector.IsChecked == true)
		{
			AddAllMediaPaths(node, destination);
			return;
		}
		foreach (FolderNode child in node.Children)
		{
			AddSelectedMediaPaths(child, destination);
		}
	}

	private static void AddAllMediaPaths(FolderNode node, HashSet<string> destination)
	{
		foreach (string directMediaPath in node.DirectMediaPaths)
		{
			destination.Add(directMediaPath);
		}
		foreach (FolderNode child in node.Children)
		{
			AddAllMediaPaths(child, destination);
		}
	}

	private void UpdateStatusText()
	{
		if (!ApplyButton.IsEnabled)
		{
			return;
		}
		int count = CollectSelectedMediaPaths().Count;
		if (UseBrowserSelectionBox.IsChecked == true)
		{
			StatusText.Text = count + " browser-selected media file(s). " + selectedTags.Count + " tag(s) selected. Filter buttons affect the browser list only.";
			return;
		}
		int num = CountCheckedFolders(rootNodes);
		StatusText.Text = num + " folder selections covering " + count + " media file(s). " + selectedTags.Count + " tag(s) selected. Filter buttons affect the browser list only.";
	}

	private void UseBrowserSelectionBox_CheckedChanged(object sender, RoutedEventArgs e)
	{
		UpdateStatusText();
	}

	private void UpdateBrowserSelectionOption()
	{
		int selectedCount = tagger.GetSelectedBrowserMediaPaths().Count;
		UseBrowserSelectionBox.Content = "Use browser selection (" + selectedCount + ")";
		UseBrowserSelectionBox.IsEnabled = selectedCount > 0;
		UseBrowserSelectionBox.IsChecked = selectedCount > 0;
	}

	private static int CountCheckedFolders(IEnumerable<FolderNode> nodes)
	{
		int num = 0;
		foreach (FolderNode node in nodes)
		{
			if (node.Selector != null && node.Selector.IsChecked == true)
			{
				num++;
			}
			num += CountCheckedFolders(node.Children);
		}
		return num;
	}

	private BulkTagOperationMode GetSelectedMode()
	{
		if (ModeCombo.SelectedItem is ComboBoxItem comboBoxItem && comboBoxItem.Tag is BulkTagOperationMode bulkTagOperationMode)
		{
			return bulkTagOperationMode;
		}
		return BulkTagOperationMode.Merge;
	}

	private static ComboBoxItem CreateModeItem(string label, BulkTagOperationMode mode)
	{
		return new ComboBoxItem
		{
			Content = label,
			Tag = mode,
			Background = new SolidColorBrush(Color.FromRgb(34, 34, 34)),
			Foreground = Brushes.White,
			FontFamily = new FontFamily("Times New Roman"),
			FontSize = 16.0
		};
	}

	private string GetTagSearchText()
	{
		string text = TagSearchBox.Text?.Trim() ?? "";
		if (TagSearchBox.Foreground == Brushes.Gray || text.Length == 0 || string.Equals(text, TagSearchPlaceholder, StringComparison.Ordinal))
		{
			return "";
		}
		return text;
	}

	private static IEnumerable<string> SplitPath(string path)
	{
		return path.Split(new char[2] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
	}

	private static bool IsSameDirectoryOrChild(string path, string parentPath)
	{
		if (string.Equals(path, parentPath, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string text = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		return path.StartsWith(text, StringComparison.OrdinalIgnoreCase);
	}
}
