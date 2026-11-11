using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// The digital twin library (see DigitalTwinLibraryViewModel); one at a time, opened from the meter's title bar.
public partial class DigitalTwinLibraryWindow : Window
{
    private static DigitalTwinLibraryWindow? _open;

    public DigitalTwinLibraryWindow()
    {
        InitializeComponent();
    }

    // homeServerId: the player's own server, chosen to start with for adding manually.
    public static void Open(DigitalTwinLibrary library, DigitalTwinService twins, LocalizationService localization,
        IReadOnlyDictionary<int, string> serverNames, int? homeServerId, Window? owner)
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
        };
        _ = viewModel.LoadAsync();
        if (owner is null)
            _open.Show();
        else
            _open.Show(owner);
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
