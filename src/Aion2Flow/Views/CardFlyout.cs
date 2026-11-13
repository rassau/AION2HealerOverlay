using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cloris.Aion2Flow.Views;

// A flyout on an overlay card: a message (the history's), or a box to type a short text in (a history entry's name, a
// twin snapshot's note).
internal static class CardFlyout
{
    public static Flyout Create(Control content, PlacementMode placement = PlacementMode.BottomEdgeAlignedLeft)
    {
        var card = new Border
        {
            Child = content,
            Padding = new Thickness(8)
        };
        card.Classes.Add("SettingsCard");
        return new Flyout
        {
            Content = card,
            Placement = placement
        };
    }

    // Enter, or clicking elsewhere, keeps the text (keep gets it when it changed); only Escape drops it.
    public static void EditText(Control anchor, string? text, int maxLength, Action<string?> keep, double minWidth = 228,
        PlacementMode placement = PlacementMode.BottomEdgeAlignedLeft)
    {
        var textBox = new TextBox
        {
            Text = text,
            MinWidth = minWidth,
            MaxLength = maxLength
        };
        var flyout = Create(textBox, placement);
        var cancelled = false;
        textBox.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key is not (Key.Enter or Key.Escape))
                return;

            cancelled = args.Key == Key.Escape;
            args.Handled = true;
            flyout.Hide();
        }, RoutingStrategies.Tunnel);
        flyout.Opened += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };
        flyout.Closed += (_, _) =>
        {
            if (!cancelled && textBox.Text != text)
                keep(textBox.Text);
        };
        flyout.ShowAt(anchor);
    }
}
