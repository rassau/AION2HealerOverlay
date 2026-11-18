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

    // A scene that has the library closed.
    public static void CloseOpen() => _open?.Close();

    // The open library's view model (the tutorial shows HappyCat in it).
    internal static DigitalTwinLibraryViewModel? Shown => _open?.DataContext as DigitalTwinLibraryViewModel;

    public DigitalTwinLibraryWindow()
    {
        InitializeComponent();
    }

    // homeServerId: the player's own server, chosen to start with for adding manually; its size and opacity from the
    // settings' Visuals. activate: false when a scene opens it (a shortcut pressed in game, which keeps the focus). myself:
    // the player's character, for Update myself.
    public static void Open(DigitalTwinLibrary library, DigitalTwinService twins, LocalizationService localization,
        IReadOnlyDictionary<int, string> serverNames, int? homeServerId, SettingsFlyoutViewModel settings, UiScaleService uiScale, Window? owner,
        bool activate = true, Func<(int ServerId, string Name)?>? myself = null)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var viewModel = new DigitalTwinLibraryViewModel(library, twins, localization, serverNames, homeServerId, myself, settings);
        _open = new DigitalTwinLibraryWindow();
        // "Update myself on overlay start" and "Use last battle's skill levels" are settings (also rows of the settings'
        // twin library tab): their boxes have the settings, before the window gets its view model, so their bindings never
        // meet that one.
        _open.UpdateMyselfOnStartBox.DataContext = settings;
        _open.UseSeenLevelsBox.DataContext = settings;
        _open.DataContext = viewModel;
        _open.Closed += (_, _) =>
        {
            viewModel.Dispose();
            _open = null;
            OpenChanged?.Invoke();
        };
        _ = viewModel.LoadAsync();
        _open.ShowActivated = activate;
        if (owner is null)
            _open.Show();
        else
            _open.Show(owner);
        FrameLook.Attach(_open, settings, s => s.TwinLibraryOpacityPercent, s => s.TwinLibraryScalePercent, uiScale);
        _open.Settings = settings;
        FrameChrome.Attach(_open, _open.LibraryRoot, _open.TitleLine, settings, SettingsFlyoutViewModel.TwinLibraryKey);
        FrameChrome.AttachHand(_open, _open.HandButton, settings, SettingsFlyoutViewModel.TwinLibraryKey);
        // The gear: the settings at the library's tab.
        _open.GearButton.Click += (_, _) => SettingsWindow.Open(settings, SettingsTab.TwinLibrary, uiScale);
        FocusClickThrough.Attach(_open, SettingsFlyoutViewModel.TwinLibraryKey);
        TopmostFollower.Attach(_open, settings);
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
