using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class SettingsFlyoutLayoutTests
{
    [Fact]
    public void SettingsSubMenuPopup_TouchesItsAnchorWithoutAHorizontalGap()
    {
        AvaloniaTestHost.Run(() =>
        {
            var style = new StyleInclude(new Uri("avares://Aion2Heal/"))
            {
                Source = new Uri("avares://Aion2Heal/Styles/OverlayTheme.axaml")
            };
            Application.Current!.Styles.Add(style);
            var parent = new MenuItem { Header = "Parent" };
            parent.Classes.Add("FlyoutMenuItem");
            parent.Classes.Add("FlyoutPanelRow");
            parent.Classes.Add("SettingsRowItem");
            parent.Items.Add(new MenuItem { Header = "Child" });
            var menu = new Menu { Items = { parent } };
            menu.Classes.Add("SettingsMenu");
            var window = new Window { Content = menu };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                parent.ApplyTemplate();

                var popup = Assert.Single(parent.GetTemplateDescendants().OfType<Popup>());
                Assert.Equal(PlacementMode.RightEdgeAlignedTop, popup.Placement);
                Assert.Equal(0, popup.HorizontalOffset);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
                Application.Current.Styles.Remove(style);
            }
        });
    }

    // A hover text wraps into a box at most 380 wide, with a little room around it (CommonTheme, in the window's theme as in
    // the app); a short one stays as narrow as its text and a line break stays where it is. A panel given as the tip keeps
    // its own layout: the style doesn't reach into it.
    [Fact]
    public void Text_Hover_Texts_Wrap_Into_A_Box_And_Panels_Keep_Their_Layout()
    {
        AvaloniaTestHost.Run(() =>
        {
            var text = string.Concat(Enumerable.Repeat("A long hover text runs on and on across the screen. ", 5));
            ToolTip[] tips =
            [
                new() { Content = text },
                new() { Content = "Close" },
                new() { Content = "20,580 / 24,212 HP\nShield: 1,200" },
                new() { Content = new StackPanel { Children = { new TextBlock { Text = text } } } },
            ];
            var buttons = tips.Select(static tip =>
            {
                var button = new Button { Content = "?" };
                ToolTip.SetTip(button, tip);
                return button;
            }).ToArray();
            var panel = new StackPanel();
            panel.Children.AddRange(buttons);
            var window = new Window { Width = 300, Height = 200, Content = panel };
            window.Styles.Add(new StyleInclude(new Uri("avares://Aion2Heal/")) { Source = new Uri("avares://Aion2Heal/Styles/OverlayTheme.axaml") });

            try
            {
                window.Show();
                var shown = new List<(TextWrapping Wrapping, Thickness Margin, double Width, int Lines)>();
                foreach (var (button, tip) in buttons.Zip(tips))
                {
                    ToolTip.SetIsOpen(button, true);
                    Dispatcher.UIThread.RunJobs();
                    tip.UpdateLayout();
                    var block = tip.GetLogicalDescendants().OfType<TextBlock>().Single();
                    shown.Add((block.TextWrapping, block.Margin, block.Bounds.Width, block.TextLayout.TextLines.Count));
                    ToolTip.SetIsOpen(button, false);
                    Dispatcher.UIThread.RunJobs();
                }

                var (wrapped, close, twoLines, inPanel) = (shown[0], shown[1], shown[2], shown[3]);
                Assert.Equal((TextWrapping.Wrap, new Thickness(4, 2)), (wrapped.Wrapping, wrapped.Margin));
                Assert.InRange(wrapped.Width, 300, 380);
                Assert.True(wrapped.Lines > 2, $"{wrapped.Lines} lines");
                Assert.True(close.Width < 100, $"{close.Width} wide");
                Assert.Equal(2, twoLines.Lines);
                Assert.True(twoLines.Width < 380, $"{twoLines.Width} wide");
                Assert.Equal((TextWrapping.NoWrap, new Thickness(0), 1), (inPanel.Wrapping, inPanel.Margin, inPanel.Lines));
                Assert.True(inPanel.Width > 380, $"{inPanel.Width} wide");
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
}
