using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

// The Hidden Cube finder's own frame: placed anywhere, with its own size and opacity, and like the group frame
// see-through for the mouse while the game has focus.
public partial class HiddenCubesWindow : Window
{
    private readonly AvaloniaFrameClockService _frameClock;
    private readonly LocalizationService _localization;
    private readonly UiScaleService _uiScale;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _persistPositionTimer;
    private bool _frameClockAttached;

    public HiddenCubesWindow()
        : this(
            Ioc.Default.GetRequiredService<AvaloniaFrameClockService>(),
            Ioc.Default.GetRequiredService<LocalizationService>(),
            Ioc.Default.GetRequiredService<UiScaleService>(),
            Ioc.Default.GetRequiredService<SettingsService>())
    {
    }

    public HiddenCubesWindow(AvaloniaFrameClockService frameClock, LocalizationService localization, UiScaleService uiScale, SettingsService settingsService)
    {
        _frameClock = frameClock;
        _localization = localization;
        _uiScale = uiScale;
        _settingsService = settingsService;
        _persistPositionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _persistPositionTimer.Tick += (_, _) =>
        {
            _persistPositionTimer.Stop();
            PersistPosition();
        };
        InitializeComponent();
        _localization.LanguageChanged += OnLanguageChanged;
        OnLanguageChanged(null, EventArgs.Empty);
    }

    public new HiddenCubesViewModel DataContext
    {
        get => (HiddenCubesViewModel)base.DataContext!;
        set => base.DataContext = value;
    }

    // Click-through while the game has focus, unless it's to take clicks then ("Clickable without focus").
    public void SetAppFocusPresentation(bool appFocused, bool clickableWithoutFocus = false)
    {
        FrameRoot.Classes.Set("interactive", appFocused);
        NativeOverlayWindowStyles.SetInputTransparent(this, !appFocused && !clickableWithoutFocus);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_settingsService.Current.HiddenCubesPosition is { } position)
            Position = new PixelPoint(position.X, position.Y);
        PositionChanged += (_, _) =>
        {
            _persistPositionTimer.Stop();
            _persistPositionTimer.Start();
        };
        var settings = DataContext.Settings;
        FrameLook.Attach(this, settings, static s => s.HiddenCubesOpacityPercent, static s => s.HiddenCubesScalePercent, _uiScale);
        FrameChrome.Attach(this, FrameRoot, settings, SettingsFlyoutViewModel.HiddenCubesKey);
        if (_frameClockAttached)
            return;

        _frameClockAttached = true;
        _frameClock.Frame += OnAnimationFrame;
        _frameClock.Attach(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _persistPositionTimer.Stop();
        PersistPosition();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        if (_frameClockAttached)
        {
            _frameClock.Frame -= OnAnimationFrame;
            _frameClock.Detach(this);
            _frameClockAttached = false;
        }

        base.OnClosed(e);
    }

    private void OnAnimationFrame(object? sender, AvaloniaFrameEventArgs e) => DataContext.ProcessUiFrame();

    private void SettingsClicked(object? sender, RoutedEventArgs e) => SettingsWindow.Open(DataContext.Settings, SettingsTab.HiddenCubes, _uiScale);

    private void EyeClicked(object? sender, RoutedEventArgs e) => DataContext.Settings.ToggleMinimized(SettingsFlyoutViewModel.HiddenCubesKey);

    private void CloseClicked(object? sender, RoutedEventArgs e) => DataContext.Settings.HiddenCubesEnabled = false;

    private void OnLanguageChanged(object? sender, EventArgs e) => FrameTitle.Text = _localization["HiddenCubes_Title"];

    private void PersistPosition()
    {
        var corner = new System.Drawing.Point(Position.X, Position.Y);
        _settingsService.Update(settings => settings.HiddenCubesPosition = corner);
    }

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            WindowDrag.Move(this, e);
        }
    }
}
