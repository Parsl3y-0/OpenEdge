using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace OpenEdge;

public partial class ModsPopup : Grid, IComponentConnector
{
	public bool open;

	private Page1 p1;

	private bool restartRequired;

	public ModsPopup()
	{
		InitializeComponent();
		Loaded += delegate
		{
			RefreshModList();
		};
	}

	public void setParent(Page1 p1)
	{
		this.p1 = p1;
	}

	private void Button_ClickX(object sender, RoutedEventArgs e)
	{
		closePopup();
	}

	public void closePopup()
	{
		open = false;
		p1.playClickSound();
		base.Visibility = Visibility.Collapsed;
	}

	public void openPopup()
	{
		open = true;
		p1.playClickSound();
		RefreshModList();
		base.Visibility = Visibility.Visible;
	}

	private void Button_Click_ModFolder(object sender, RoutedEventArgs e)
	{
		p1.playClickSound();
		ModService.EnsureModsDirectory();
		Process.Start(Environment.GetEnvironmentVariable("WINDIR") + "\\explorer.exe", RuntimePaths.ModsDir);
	}

	private void Refresh_Click(object sender, RoutedEventArgs e)
	{
		p1.playClickSound();
		RefreshModList();
	}

	private void ReloadApp_Click(object sender, RoutedEventArgs e)
	{
		p1.playClickSound();
		RestartApplication();
	}

	private void CreateExample_Click(object sender, RoutedEventArgs e)
	{
		p1.playClickSound();
		string path = ModService.CreateExampleMod();
		RefreshModList();
		MessageBox.Show("Example mod created at:\n" + path + "\n\nIt is disabled by default. Enable it here when you want to test it.", "OpenEdge Mods", MessageBoxButton.OK, MessageBoxImage.Information);
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		p1.playClickSound();
		Process.Start(new ProcessStartInfo
		{
			FileName = "https://discord.gg/jMYB7RU9r5",
			UseShellExecute = true
		});
	}

	private void RefreshModList()
	{
		if (ModsListPanel == null)
		{
			return;
		}
		ModService.EnsureModsDirectory();
		ModsListPanel.Children.Clear();
		var summaries = ModService.GetModSummaries();
		if (summaries.Count == 0)
		{
			ModsListPanel.Children.Add(new TextBlock
			{
				Text = "No mods found. Click Create Example Mod or put a mod folder inside:\n" + RuntimePaths.ModsDir,
				Foreground = Brushes.White,
				FontSize = 16.0,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(8.0)
			});
			StatusText.Text = "0 mods detected.";
			return;
		}
		foreach (ModSummary summary in summaries)
		{
			ModsListPanel.Children.Add(CreateModRow(summary));
		}
		int enabledCount = summaries.Count((ModSummary mod) => mod.Enabled && string.IsNullOrWhiteSpace(mod.Error));
		int warningCount = summaries.Count((ModSummary mod) => !string.IsNullOrWhiteSpace(mod.Error));
		UpdateStatus(summaries.Count + " mod(s) detected. " + enabledCount + " enabled. " + warningCount + " warning/error item(s).");
	}

	private UIElement CreateModRow(ModSummary summary)
	{
		Border border = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(135, 26, 22, 36)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(90, 138, 106, 176)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(8.0),
			Padding = new Thickness(12.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		StackPanel textPanel = new StackPanel();
		TextBlock title = new TextBlock
		{
			Text = summary.Name + (string.IsNullOrWhiteSpace(summary.Version) ? "" : "  v" + summary.Version),
			Foreground = Brushes.White,
			FontSize = 18.0,
			FontWeight = FontWeights.SemiBold
		};
		textPanel.Children.Add(title);
		string details = "ID: " + summary.Id;
		if (!string.IsNullOrWhiteSpace(summary.Author))
		{
			details += " · Author: " + summary.Author;
		}
		details += " · Priority: " + summary.Priority + " · Settings: " + summary.SettingCount + " · Tags: " + summary.TagCount + " · Contexts: " + summary.ContextCount + " · Hooks: " + summary.HookCount + " · Outcomes: " + summary.OutcomeCount + " · Lines: " + (summary.HasLines ? "yes" : "no");
		textPanel.Children.Add(new TextBlock
		{
			Text = details,
			Foreground = new SolidColorBrush(Color.FromRgb(200, 182, 240)),
			FontSize = 13.0,
			TextWrapping = TextWrapping.Wrap
		});
		if (!string.IsNullOrWhiteSpace(summary.Error))
		{
			textPanel.Children.Add(new TextBlock
			{
				Text = "Warning: " + summary.Error,
				Foreground = Brushes.Orange,
				FontSize = 13.0,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
			});
		}
		Grid.SetColumn(textPanel, 0);
		grid.Children.Add(textPanel);

		StackPanel actions = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		Button upButton = new Button
		{
			Content = "↑",
			ToolTip = "Move this mod earlier in priority order",
			Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			Tag = summary.Id
		};
		upButton.Click += MoveModUp_Click;
		actions.Children.Add(upButton);
		Button downButton = new Button
		{
			Content = "↓",
			ToolTip = "Move this mod later in priority order",
			Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
			Tag = summary.Id
		};
		downButton.Click += MoveModDown_Click;
		actions.Children.Add(downButton);
		CheckBox enabledBox = new CheckBox
		{
			Content = "Enabled",
			IsChecked = summary.Enabled,
			Foreground = Brushes.White,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
			Tag = summary.RootPath
		};
		enabledBox.Checked += EnabledBox_Changed;
		enabledBox.Unchecked += EnabledBox_Changed;
		actions.Children.Add(enabledBox);
		Button openButton = new Button
		{
			Content = "Open",
			Padding = new Thickness(12.0, 6.0, 12.0, 6.0),
			Tag = summary.RootPath
		};
		openButton.Click += OpenMod_Click;
		actions.Children.Add(openButton);
		Grid.SetColumn(actions, 1);
		grid.Children.Add(actions);

		border.Child = grid;
		return border;
	}

	private void MoveModUp_Click(object sender, RoutedEventArgs e)
	{
		MoveModPriority(sender, -1);
	}

	private void MoveModDown_Click(object sender, RoutedEventArgs e)
	{
		MoveModPriority(sender, 1);
	}

	private void MoveModPriority(object sender, int direction)
	{
		if (!(sender is Button button) || !(button.Tag is string modId))
		{
			return;
		}
		try
		{
			p1.playClickSound();
			ModService.MoveModPriority(modId, direction);
			restartRequired = true;
			RefreshModList();
			UpdateStatus("Updated mod priority order.");
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Unable to update mod priority", MessageBoxButton.OK, MessageBoxImage.Error);
			RefreshModList();
		}
	}

	private void EnabledBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!(sender is CheckBox checkBox) || !(checkBox.Tag is string path))
		{
			return;
		}
		try
		{
			ModService.SetModEnabled(path, checkBox.IsChecked == true);
			restartRequired = true;
			if (ReloadAppButton != null)
			{
				ReloadAppButton.IsEnabled = true;
			}
			UpdateStatus("Updated " + Path.GetFileName(path) + ".");
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Unable to update mod", MessageBoxButton.OK, MessageBoxImage.Error);
			RefreshModList();
		}
	}

	private void OpenMod_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button button && button.Tag is string path)
		{
			p1.playClickSound();
			Process.Start(Environment.GetEnvironmentVariable("WINDIR") + "\\explorer.exe", path);
		}
	}

	private void UpdateStatus(string prefix)
	{
		string restartText = restartRequired ? " Reload App is required for mod settings, tags, contexts, and line roots to refresh everywhere." : " Use Reload App after changing enabled mods.";
		StatusText.Text = prefix + restartText;
		if (ReloadAppButton != null)
		{
			ReloadAppButton.IsEnabled = restartRequired;
		}
	}

	private void RestartApplication()
	{
		try
		{
			string executablePath = Process.GetCurrentProcess().MainModule?.FileName;
			if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
			{
				executablePath = Assembly.GetEntryAssembly()?.Location;
			}
			if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
			{
				MessageBox.Show("Unable to find the OpenEdge executable to restart. Please close and reopen OpenEdge manually.", "Reload App", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = executablePath,
				UseShellExecute = true,
				WorkingDirectory = RuntimePaths.RuntimeRoot
			});
			Application.Current.Shutdown();
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message + "\n\nPlease close and reopen OpenEdge manually.", "Unable to reload app", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}
