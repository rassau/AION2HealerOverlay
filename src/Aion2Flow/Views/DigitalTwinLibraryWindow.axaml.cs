using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// The digital twin library (see DigitalTwinLibraryViewModel); one at a time, opened from the meter's title bar.
public partial class DigitalTwinLibraryWindow : Window
{
    private static DigitalTwinLibraryWindow? _open;

    // Open (minimized too) or not, for the meter's navigation button, gold while it is.
    public static bool IsOpen => _open is not null;

    public static event Action? OpenChanged;

    public DigitalTwinLibraryWindow()
    {
        InitializeComponent();
    }

    // homeServerId: the player's own server, chosen to start with for adding manually; its size and opacity from the
    // settings' Visuals.
    public static void Open(DigitalTwinLibrary library, DigitalTwinService twins, LocalizationService localization,
        IReadOnlyDictionary<int, string> serverNames, int? homeServerId, SettingsFlyoutViewModel settings, UiScaleService uiScale, Window? owner)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var viewModel = new DigitalTwinLibraryViewModel(library, twins, localization, serverNames, homeServerId);
        _open = new DigitalTwinLibraryWindow { DataContext = viewModel };
        _open.Closed += (_, _) =>
        {
            viewModel.Dispose();
            _open = null;
            OpenChanged?.Invoke();
        };
        _ = viewModel.LoadAsync();
        if (owner is null)
            _open.Show();
        else
            _open.Show(owner);
        FrameLook.Attach(_open, settings, s => s.TwinLibraryOpacityPercent, s => s.TwinLibraryScalePercent, uiScale);
        _open.Settings = settings;
        FrameChrome.Attach(_open, _open.LibraryRoot, settings, SettingsFlyoutViewModel.TwinLibraryKey);
        FocusClickThrough.Attach(_open, SettingsFlyoutViewModel.TwinLibraryKey);
        OpenChanged?.Invoke();
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();

    // A snapshot's pencil: its note in a box under it (Enter or a click elsewhere keeps it, Esc drops it).
    private void NoteClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DigitalTwinLibraryRow row } button && DataContext is DigitalTwinLibraryViewModel library)
            CardFlyout.EditText(button, row.Note, 200, note => library.SetNote(row, note), minWidth: 320, placement: PlacementMode.BottomEdgeAlignedRight);
    }

    // The settings, for the eye (set by Open).
    internal SettingsFlyoutViewModel? Settings { get; set; }

    private void EyeClicked(object? sender, RoutedEventArgs e) => Settings?.ToggleMinimized(SettingsFlyoutViewModel.TwinLibraryKey);

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            WindowDrag.Move(this, e);
    }
}
