using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Cloris.Aion2Flow.Views;

public partial class GroupFrameWindow : Window
{
    private readonly AvaloniaFrameClockService _frameClock;
    private readonly LocalizationService _localization;
    private readonly UiScaleService _uiScale;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _persistPositionTimer;
    private bool _frameClockAttached;
    private bool _opened;
    private int _scalePercent = 100;
    private Control? _hovered;

    public GroupFrameWindow()
        : this(
            Ioc.Default.GetRequiredService<AvaloniaFrameClockService>(),
            Ioc.Default.GetRequiredService<LocalizationService>(),
            Ioc.Default.GetRequiredService<UiScaleService>(),
            Ioc.Default.GetRequiredService<SettingsService>())
    {
    }

    public GroupFrameWindow(AvaloniaFrameClockService frameClock, LocalizationService localization, UiScaleService uiScale, SettingsService settingsService)
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

    public GroupFrameWindow(GroupFrameViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public new GroupFrameViewModel DataContext
    {
        get => (GroupFrameViewModel)base.DataContext!;
        set => base.DataContext = value;
    }

    // Click-through while the game has focus, unless it's to take clicks then ("Clickable without focus"); drag it around
    // once the overlay has.
    public void SetAppFocusPresentation(bool appFocused, bool clickableWithoutFocus = false)
    {
        FrameRoot.Classes.Set("interactive", appFocused);
        NativeOverlayWindowStyles.SetInputTransparent(this, !appFocused && !clickableWithoutFocus);
    }

    // The frame's own size, apart from the main UI scale.
    public void SetScalePercent(int percent)
    {
        _scalePercent = percent;
        _uiScale.RefreshWindow(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplySides();
        ApplyOpacities();
        UpdateLayout();
        if (_settingsService.Current.GroupFramePosition is { } position)
            Position = new PixelPoint(AurasOnLeft ? position.X - PixelWidth(Bounds.Width) : position.X, position.Y);
        _opened = true;
        _uiScale.RegisterWindow(this, () => _scalePercent);
        PositionChanged += (_, _) => SchedulePersistPosition();
        DataContext.Settings.PropertyChanged += OnSettingsChanged;
        if (_frameClockAttached)
            return;

        _frameClockAttached = true;
        _frameClock.Frame += OnAnimationFrame;
        _frameClock.Attach(this);
    }

    // With the auras on the left the frame grows to the left: whatever its width does, its right edge holds, and with it
    // the HP bars. Nor does it get narrower again, which would move them each time a HoT ran out (and back with the
    // next): the room stays for the next auras. Only once open, as it starts out at the system's default size.
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_opened || !AurasOnLeft)
            return;

        FrameRoot.MinWidth = FrameRoot.DesiredSize.Width;
        var grown = PixelWidth(e.NewSize.Width) - PixelWidth(e.PreviousSize.Width);
        if (grown != 0)
            Position = new PixelPoint(Position.X - grown, Position.Y);
    }

    private bool AurasOnLeft => base.DataContext is GroupFrameViewModel { Settings.GroupFrameAuraSide: GroupFrameSide.Left };

    private int PixelWidth(double width) => (int)Math.Round(width * DesktopScaling);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _persistPositionTimer.Stop();
        PersistPosition();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        if (base.DataContext is GroupFrameViewModel viewModel)
            viewModel.Settings.PropertyChanged -= OnSettingsChanged;
        if (_frameClockAttached)
        {
            _frameClock.Frame -= OnAnimationFrame;
            _frameClock.Detach(this);
            _frameClockAttached = false;
        }

        base.OnClosed(e);
    }

    private void OnAnimationFrame(object? sender, AvaloniaFrameEventArgs e)
    {
        DataContext.ProcessUiFrame();
        ShowToolTipUnderCursor();
    }

    // While the game has focus the frame lets the mouse through and never hears of it, so look where the cursor is and
    // open the tool tip of what's under it (an aura's name, a member's HP). Once the overlay has focus, Avalonia does that
    // itself.
    private void ShowToolTipUnderCursor()
    {
        Control? hovered = null;
        if (!FrameRoot.Classes.Contains("interactive") && NativeOverlayWindowStyles.TryGetCursorPosition(out var cursor))
        {
            var point = this.PointToClient(cursor);
            if (new Rect(Bounds.Size).Contains(point) && this.InputHitTest(point) is Visual hit)
                hovered = hit.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(control => ToolTip.GetTip(control) is not null);
        }

        if (ReferenceEquals(hovered, _hovered))
            return;

        if (_hovered is not null)
            ToolTip.SetIsOpen(_hovered, false);
        _hovered = hovered;
        if (hovered is not null)
            ToolTip.SetIsOpen(hovered, true);
    }

    // The gear: the settings at the frame's tab.
    private void SettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SettingsWindow.Open(DataContext.Settings, SettingsTab.GroupFrame, _uiScale);

    // The rows follow the sides. With the auras moved to or from the left the frame grows the other way from now on, and
    // the corner it keeps is the other one.
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsFlyoutViewModel.GroupFrameAuraSide) or nameof(SettingsFlyoutViewModel.GroupFrameArrowSide)
            or nameof(SettingsFlyoutViewModel.GroupFrameDistanceSide))
            ApplySides();
        if (e.PropertyName is nameof(SettingsFlyoutViewModel.GroupFrameHpOpacityPercent) or nameof(SettingsFlyoutViewModel.GroupFrameAuraOpacityPercent)
            or nameof(SettingsFlyoutViewModel.GroupFrameArrowOpacityPercent) or nameof(SettingsFlyoutViewModel.GroupFrameDistanceOpacityPercent))
            ApplyOpacities();
        if (e.PropertyName == nameof(SettingsFlyoutViewModel.GroupFrameAuraSide))
        {
            FrameRoot.MinWidth = 0;
            SchedulePersistPosition();
        }
    }

    // Where the auras, the arrow and the distance go (see the window's styles): a class for each that's left or off.
    private void ApplySides()
    {
        var settings = DataContext.Settings;
        Apply("auras", settings.GroupFrameAuraSide);
        Apply("arrow", settings.GroupFrameArrowSide);
        Apply("distance", settings.GroupFrameDistanceSide);

        void Apply(string part, GroupFrameSide side)
        {
            FrameRoot.Classes.Set(part + "Left", side == GroupFrameSide.Left);
            FrameRoot.Classes.Set(part + "Off", side == GroupFrameSide.Off);
        }
    }

    // How see-through the rows' parts are, 100% being their own look: the resources their Opacity comes from.
    private void ApplyOpacities()
    {
        var settings = DataContext.Settings;
        Resources["GroupFrameHpOpacity"] = settings.GroupFrameHpOpacityPercent / 100d;
        Resources["GroupFrameAuraOpacity"] = settings.GroupFrameAuraOpacityPercent / 100d;
        Resources["GroupFrameArrowOpacity"] = settings.GroupFrameArrowOpacityPercent / 100d;
        Resources["GroupFrameDistanceOpacity"] = settings.GroupFrameDistanceOpacityPercent / 100d;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        FrameTitle.Text = _localization["GroupFrame_Title"];
        FrameAlert.Text = _localization["GroupFrame_BossTarget"];
    }

    private void SchedulePersistPosition()
    {
        _persistPositionTimer.Stop();
        _persistPositionTimer.Start();
    }

    // Its top left corner, or with the auras on the left its top right one, which holds while it grows to the left.
    private void PersistPosition()
    {
        var corner = new System.Drawing.Point(AurasOnLeft ? Position.X + PixelWidth(Bounds.Width) : Position.X, Position.Y);
        _settingsService.Update(settings => settings.GroupFramePosition = corner);
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
