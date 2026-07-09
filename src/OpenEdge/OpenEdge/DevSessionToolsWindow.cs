using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace OpenEdge;

internal sealed class DevSessionToolsWindow : Window
{
	private readonly MainWindow mainWindow;
	private readonly TextBox hookNameBox;
	private readonly TextBox scriptNameBox;
	private readonly TextBlock statusText;

	public DevSessionToolsWindow(MainWindow mainWindow)
	{
		this.mainWindow = mainWindow;
		Title = "OpenEdge Dev Session Tools";
		Width = 520;
		Height = 430;
		MinWidth = 460;
		MinHeight = 360;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		Background = new SolidColorBrush(Color.FromRgb(24, 24, 28));
		Foreground = Brushes.White;

		StackPanel root = new StackPanel
		{
			Margin = new Thickness(18),
			Orientation = Orientation.Vertical
		};
		Content = root;

		root.Children.Add(new TextBlock
		{
			Text = "Dev/Admin Session Tools",
			FontSize = 22,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0, 0, 0, 8)
		});
		root.Children.Add(new TextBlock
		{
			Text = "Manual test triggers for mod hooks, scripts, and session flow. Actions are trace-logged under dev-tools.",
			TextWrapping = TextWrapping.Wrap,
			Foreground = Brushes.LightGray,
			Margin = new Thickness(0, 0, 0, 16)
		});

		UniformGrid quickGrid = new UniformGrid
		{
			Columns = 2,
			Margin = new Thickness(0, 0, 0, 12)
		};
		root.Children.Add(quickGrid);
		AddButton(quickGrid, "Method Picker", "Run the method picker immediately.", delegate { mainWindow.DevTriggerMethodPicker(); });
		AddButton(quickGrid, "Edge Opportunity", "Run the edgeOpportunity hook.", delegate { mainWindow.DevTriggerEdgeOpportunity(); });
		AddButton(quickGrid, "Orgasm Decision", "Run the orgasmDecision hook.", delegate { mainWindow.DevTriggerOrgasmDecision(); });
		AddButton(quickGrid, "Session End", "Run the sessionEnd hook/base ending now.", delegate { mainWindow.DevTriggerSessionEnd(); });

		root.Children.Add(CreateLabel("Run named hook"));
		hookNameBox = new TextBox
		{
			Text = "sessionIntro",
			Margin = new Thickness(0, 3, 0, 6)
		};
		root.Children.Add(hookNameBox);
		AddButton(root, "Run Hook", "Run the hook name above through the mod hook bus.", delegate { mainWindow.DevTriggerHook(hookNameBox.Text); });

		root.Children.Add(CreateLabel("Run named script"));
		scriptNameBox = new TextBox
		{
			Text = "ending",
			Margin = new Thickness(0, 3, 0, 6)
		};
		root.Children.Add(scriptNameBox);
		AddButton(root, "Run Script", "Run the script name above as a GenericScript.", delegate { mainWindow.DevTriggerScript(scriptNameBox.Text); });

		statusText = new TextBlock
		{
			Text = "Ready.",
			Foreground = Brushes.LightGreen,
			Margin = new Thickness(0, 14, 0, 0),
			TextWrapping = TextWrapping.Wrap
		};
		root.Children.Add(statusText);
	}

	private TextBlock CreateLabel(string text)
	{
		return new TextBlock
		{
			Text = text,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0, 8, 0, 0)
		};
	}

	private void AddButton(Panel parent, string label, string description, Action action)
	{
		Button button = new Button
		{
			Content = label,
			Margin = new Thickness(4),
			Padding = new Thickness(10, 6, 10, 6),
			ToolTip = description
		};
		button.Click += delegate
		{
			try
			{
				action();
				statusText.Text = "Triggered: " + label;
			}
			catch (Exception ex)
			{
				statusText.Foreground = Brushes.OrangeRed;
				statusText.Text = "Failed: " + ex.Message;
				SessionTraceLogger.Error("dev-tools", "trigger failed action=" + label, ex);
			}
		};
		parent.Children.Add(button);
	}
}
