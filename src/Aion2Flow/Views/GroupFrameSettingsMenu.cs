using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Services.Audio;
using Cloris.Aion2Flow.Services.Logging;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// The group frame's rows of the settings, filled into a menu: the settings' "Group frame" submenu and the frame's own gear.
internal sealed class GroupFrameSettingsMenu
{
    private readonly SettingsFlyoutViewModel _vm;
    private readonly MenuItem _enable;
    private readonly MenuItem _myBuffs;
    private readonly MenuItem _debuffs;
    private readonly MenuItem _otherBuffs;
    private readonly MenuItem _permanentBuffs;
    private readonly MenuItem _auraSide;
    private readonly MenuItem _shieldBar;
    private readonly MenuItem _shieldAnimation;
    private readonly MenuItem _deaths;
    private readonly MenuItem _arrowSide;
    private readonly MenuItem _distanceSide;
    private readonly MenuItem _greenMeters;
    private readonly MenuItem _orangeMeters;
    private readonly MenuItem _rangeMeters;
    private readonly MenuItem _cubes;
    private readonly MenuItem _alert;
    private readonly MenuItem _alertEnabled;
    private readonly MenuItem _alertSound;
    private readonly MenuItem _alertPlay;
    private readonly MenuItem _opacity;
    private readonly List<(string LabelKey, Func<int> Get, string Unit, TextBlock Label, Slider Slider, TextBlock Value)> _sliders = [];
    private string[]? _shownSounds;

    public GroupFrameSettingsMenu(SettingsFlyoutViewModel vm, ItemCollection items)
    {
        _vm = vm;
        _enable = Toggle(() => vm.GroupFrameEnabled = !vm.GroupFrameEnabled);
        _myBuffs = Toggle(() => vm.GroupFrameShowMyBuffs = !vm.GroupFrameShowMyBuffs);
        _debuffs = Toggle(() => vm.GroupFrameShowDebuffs = !vm.GroupFrameShowDebuffs);
        _otherBuffs = Toggle(() => vm.GroupFrameShowOtherBuffs = !vm.GroupFrameShowOtherBuffs);
        _permanentBuffs = Toggle(() => vm.GroupFrameShowPermanentBuffs = !vm.GroupFrameShowPermanentBuffs);
        _auraSide = Toggle(() => vm.GroupFrameAuraSide = Next(vm.GroupFrameAuraSide));
        _shieldBar = Toggle(() => vm.GroupFrameShieldOnRight = !vm.GroupFrameShieldOnRight);
        _shieldAnimation = Toggle(() => vm.GroupFrameShieldAnimation = !vm.GroupFrameShieldAnimation);
        _deaths = Toggle(() => vm.GroupFrameShowDeaths = !vm.GroupFrameShowDeaths);
        _arrowSide = Toggle(() => vm.GroupFrameArrowSide = Next(vm.GroupFrameArrowSide));
        _distanceSide = Toggle(() => vm.GroupFrameDistanceSide = Next(vm.GroupFrameDistanceSide));
        _greenMeters = Meters(meters => vm.GroupFrameGreenMeters = meters);
        _orangeMeters = Meters(meters => vm.GroupFrameOrangeMeters = meters);
        _rangeMeters = Meters(meters => vm.GroupFrameRangeMeters = meters);
        // The Hidden Cube finder: a click goes off, shown, shown with a sound, and round again.
        _cubes = Toggle(() => vm.GroupFrameCubeFinder = vm.GroupFrameCubeFinder switch
        {
            GroupFrameCubeFinder.Off => GroupFrameCubeFinder.Show,
            GroupFrameCubeFinder.Show => GroupFrameCubeFinder.Sound,
            _ => GroupFrameCubeFinder.Off,
        });
        // The boss alert: on or off, which sound (the chime, the user's own, a new one from a file), how loud, and a try.
        _alert = Submenu();
        _alertEnabled = Toggle(() => vm.GroupFrameAlertEnabled = !vm.GroupFrameAlertEnabled);
        _alertSound = Submenu();
        _alertPlay = Toggle(vm.PlayAlertSound);
        _alert.Items.Add(_alertEnabled);
        _alert.Items.Add(_alertSound);
        AddSlider(_alert, "Settings_GroupFrame_AlertVolume", () => vm.GroupFrameAlertVolumePercent, percent => vm.GroupFrameAlertVolumePercent = percent, min: 0, max: 200);
        AddSlider(_alert, "Settings_GroupFrame_AlertQuiet", () => vm.GroupFrameAlertQuietSeconds, seconds => vm.GroupFrameAlertQuietSeconds = seconds, min: 0, max: 10, step: 1, unit: " s");
        _alert.Items.Add(_alertPlay);
        _opacity = Submenu();
        AddSlider(_opacity, "Settings_GroupFrame_HpBar", () => vm.GroupFrameHpOpacityPercent, percent => vm.GroupFrameHpOpacityPercent = percent);
        AddSlider(_opacity, "Settings_GroupFrame_AuraSide", () => vm.GroupFrameAuraOpacityPercent, percent => vm.GroupFrameAuraOpacityPercent = percent);
        AddSlider(_opacity, "Settings_GroupFrame_ArrowSide", () => vm.GroupFrameArrowOpacityPercent, percent => vm.GroupFrameArrowOpacityPercent = percent);
        AddSlider(_opacity, "Settings_GroupFrame_DistanceSide", () => vm.GroupFrameDistanceOpacityPercent, percent => vm.GroupFrameDistanceOpacityPercent = percent);
        foreach (var item in (MenuItem[])[_enable, _myBuffs, _debuffs, _otherBuffs, _permanentBuffs, _auraSide, _shieldBar, _shieldAnimation, _deaths, _arrowSide, _distanceSide, _greenMeters, _orangeMeters, _rangeMeters, _cubes, _alert, _opacity])
            items.Add(item);
        Refresh();
    }

    public void Refresh()
    {
        var vm = _vm;
        SettingsFlyoutView.RefreshToggleMenuItem(_enable, vm.Localization["Settings_SkillMonitor_Enable"], vm.GroupFrameEnabledDisplay, vm.GroupFrameEnabled);
        SettingsFlyoutView.RefreshToggleMenuItem(_myBuffs, vm.Localization["Settings_GroupFrame_MyBuffs"], ShowHide(vm.GroupFrameShowMyBuffs), vm.GroupFrameShowMyBuffs);
        SettingsFlyoutView.RefreshToggleMenuItem(_debuffs, vm.Localization["Settings_GroupFrame_Debuffs"], ShowHide(vm.GroupFrameShowDebuffs), vm.GroupFrameShowDebuffs);
        SettingsFlyoutView.RefreshToggleMenuItem(_otherBuffs, vm.Localization["Settings_GroupFrame_OtherBuffs"], ShowHide(vm.GroupFrameShowOtherBuffs), vm.GroupFrameShowOtherBuffs);
        SettingsFlyoutView.RefreshToggleMenuItem(_permanentBuffs, vm.Localization["Settings_GroupFrame_PermanentBuffs"], ShowHide(vm.GroupFrameShowPermanentBuffs), vm.GroupFrameShowPermanentBuffs);
        SettingsFlyoutView.RefreshToggleMenuItem(_auraSide, vm.Localization["Settings_GroupFrame_AuraSide"], Side(vm.GroupFrameAuraSide), false);
        SettingsFlyoutView.RefreshToggleMenuItem(_shieldBar, vm.Localization["Settings_GroupFrame_ShieldBar"], Side(vm.GroupFrameShieldOnRight ? GroupFrameSide.Right : GroupFrameSide.Left), false);
        SettingsFlyoutView.RefreshToggleMenuItem(_shieldAnimation, vm.Localization["Settings_GroupFrame_ShieldAnimation"], OnOff(vm.GroupFrameShieldAnimation), vm.GroupFrameShieldAnimation);
        SettingsFlyoutView.RefreshToggleMenuItem(_deaths, vm.Localization["Settings_GroupFrame_Deaths"], ShowHide(vm.GroupFrameShowDeaths), vm.GroupFrameShowDeaths);
        SettingsFlyoutView.RefreshToggleMenuItem(_arrowSide, vm.Localization["Settings_GroupFrame_ArrowSide"], Side(vm.GroupFrameArrowSide), false);
        SettingsFlyoutView.RefreshToggleMenuItem(_distanceSide, vm.Localization["Settings_GroupFrame_DistanceSide"], Side(vm.GroupFrameDistanceSide), false);
        RefreshMeters(_greenMeters, "Settings_GroupFrame_DistanceGreen", vm.GroupFrameGreenMeters);
        RefreshMeters(_orangeMeters, "Settings_GroupFrame_DistanceOrange", vm.GroupFrameOrangeMeters);
        RefreshMeters(_rangeMeters, "Settings_GroupFrame_DistanceRange", vm.GroupFrameRangeMeters);
        SettingsFlyoutView.RefreshToggleMenuItem(_cubes, vm.Localization["Settings_GroupFrame_Cubes"], vm.GroupFrameCubeFinder switch
        {
            GroupFrameCubeFinder.Off => OnOff(false),
            GroupFrameCubeFinder.Show => ShowHide(true),
            _ => vm.Localization["Settings_GroupFrame_CubesSound"],
        }, vm.GroupFrameCubeFinder != GroupFrameCubeFinder.Off);
        var sound = vm.GroupFrameAlertSound switch
        {
            null => vm.Localization["Settings_GroupFrame_AlertChime"],
            AlertSounds.KnockName => vm.Localization["Settings_GroupFrame_AlertKnock"],
            AlertSounds.BlipName => vm.Localization["Settings_GroupFrame_AlertBlip"],
            var name => name,
        };
        _alert.Header = SettingsFlyoutView.CreateRowHeader(vm.Localization["Settings_GroupFrame_Alert"], vm.GroupFrameAlertEnabled ? sound : OnOff(false));
        SettingsFlyoutView.RefreshToggleMenuItem(_alertEnabled, vm.Localization["Settings_GroupFrame_AlertEnabled"], OnOff(vm.GroupFrameAlertEnabled), vm.GroupFrameAlertEnabled);
        _alertSound.Header = SettingsFlyoutView.CreateRowHeader(vm.Localization["Settings_GroupFrame_AlertSound"], sound);
        RefreshSounds();
        SettingsFlyoutView.RefreshToggleMenuItem(_alertPlay, vm.Localization["Settings_GroupFrame_AlertPlay"], string.Empty, false);
        _opacity.Header = SettingsFlyoutView.CreateRowHeader(vm.Localization["Settings_OverlayOpacity"], string.Empty);
        foreach (var (labelKey, get, unit, label, slider, value) in _sliders)
        {
            label.Text = vm.Localization[labelKey];
            slider.Value = get();
            value.Text = $"{get()}{unit}";
        }
    }

    private string ShowHide(bool shown) =>
        _vm.Localization[shown ? "Settings_MainMetricVisibility_Show" : "Settings_MainMetricVisibility_Hide"];

    private string OnOff(bool on) => _vm.Localization[on ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    private string Side(GroupFrameSide side) => _vm.Localization[side switch
    {
        GroupFrameSide.Left => "Settings_GroupFrame_ShieldBar_Left",
        GroupFrameSide.Off => "Settings_SkillMonitor_Off",
        _ => "Settings_GroupFrame_ShieldBar_Right",
    }];

    // A click goes left, right, off, and round again.
    private static GroupFrameSide Next(GroupFrameSide side) => side switch
    {
        GroupFrameSide.Left => GroupFrameSide.Right,
        GroupFrameSide.Right => GroupFrameSide.Off,
        _ => GroupFrameSide.Left,
    };

    // Labels and ticks come from Refresh.
    private static MenuItem Toggle(Action toggle) =>
        SettingsFlyoutView.CreateToggleMenuItem(string.Empty, string.Empty, false, (_, e) =>
        {
            toggle();
            e.Handled = true;
        });

    // A row with a submenu of its own; its header comes from Refresh.
    private static MenuItem Submenu()
    {
        var item = new MenuItem();
        item.Classes.AddRange(["FlyoutMenuItem", "FlyoutPanelRow", "SettingsRowItem"]);
        return item;
    }

    // A distance mark: its value in the row, the choices in a submenu of its own.
    private MenuItem Meters(Action<int> select)
    {
        var item = Submenu();
        SettingsFlyoutView.RebuildTaggedMenuItems(item, _vm.GroupFrameDistanceOptions, SettingsFlyoutViewModel.FormatMeters, static meters => meters, static _ => false, (sender, _) =>
        {
            if (sender is MenuItem { Tag: int meters })
                select(meters);
        });
        return item;
    }

    // A slider row in a submenu: the label, the slider (10-100% unless told otherwise) and its value. The frame follows
    // the slider while it moves, the settings are saved once it's let go, like the settings' own sliders. Only its texts
    // and value change on Refresh, so a drag goes on undisturbed.
    private void AddSlider(MenuItem parent, string labelKey, Func<int> get, Action<int> set, int min = 10, int max = 100, int step = 5, string unit = "%")
    {
        var label = new TextBlock();
        label.Classes.Add("SettingsRowLabel");
        var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true };
        slider.Classes.Add("SettingsScaleSlider");
        Grid.SetColumn(slider, 1);
        var value = new TextBlock();
        value.Classes.Add("SettingsScaleValue");
        Grid.SetColumn(value, 2);
        slider.ValueChanged += (_, e) => set((int)Math.Round(e.NewValue));
        slider.AddHandler(InputElement.PointerReleasedEvent, (_, _) => _vm.SaveOverlayOpacity(), RoutingStrategies.Bubble, handledEventsToo: true);
        slider.KeyUp += (_, _) => _vm.SaveOverlayOpacity();
        var row = new MenuItem
        {
            Header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10, Children = { label, slider, value } },
            StaysOpenOnClick = true,
        };
        row.Classes.AddRange(["FlyoutMenuItem", "FlyoutPanelRow", "SettingsRowItem"]);
        parent.Items.Add(row);
        _sliders.Add((labelKey, get, unit, label, slider, value));
    }

    private void RefreshMeters(MenuItem item, string labelKey, int meters)
    {
        item.Header = SettingsFlyoutView.CreateRowHeader(_vm.Localization[labelKey], SettingsFlyoutViewModel.FormatMeters(meters));
        SettingsFlyoutView.RefreshTaggedMenuCheckmarks<int>(item, value => value == meters);
    }

    // The built-in chime, knock and blip, the user's own sounds (each with a button to export it) and a row to add one from
    // a file. Built anew only when the list changes, as picking a sound leaves the list open.
    private void RefreshSounds()
    {
        var vm = _vm;
        if (_shownSounds is null || !_shownSounds.SequenceEqual(vm.GroupFrameAlertSounds))
        {
            _shownSounds = [.. vm.GroupFrameAlertSounds];
            _alertSound.Items.Clear();
            _alertSound.Items.Add(Sound(string.Empty, vm.Localization["Settings_GroupFrame_AlertChime"]));
            _alertSound.Items.Add(Sound(AlertSounds.KnockName, vm.Localization["Settings_GroupFrame_AlertKnock"]));
            _alertSound.Items.Add(Sound(AlertSounds.BlipName, vm.Localization["Settings_GroupFrame_AlertBlip"]));
            foreach (var name in _shownSounds)
                _alertSound.Items.Add(Sound(name, name));
            var add = new MenuItem { Header = vm.Localization["Settings_GroupFrame_AlertAdd"] };
            add.Classes.AddRange(["FlyoutMenuItem", "SettingsOptionItem"]);
            add.Click += AddClicked;
            _alertSound.Items.Add(add);
        }

        SettingsFlyoutView.RefreshTaggedMenuCheckmarks<string>(_alertSound, name => name == (vm.GroupFrameAlertSound ?? string.Empty));
    }

    // A sound to pick, the chime by the empty name: a click makes it the alert and plays it. The built-in ones ship with
    // the overlay, so only the user's own have an export button.
    private MenuItem Sound(string name, string label)
    {
        var text = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis };
        text.Classes.Add("SettingsRowLabel");
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { text } };
        if (!AlertSounds.IsBuiltIn(name))
        {
            var glyph = new Avalonia.Controls.Shapes.Path { Data = IconGeometries.Download };
            glyph.Classes.AddRange(["Glyph", "GlyphSm"]);
            var export = new Button { Content = glyph, Tag = name };
            export.Classes.Add("FlyoutInlineButton");
            ToolTip.SetTip(export, _vm.Localization["Settings_GroupFrame_AlertExport"]);
            export.Click += ExportClicked;
            Grid.SetColumn(export, 1);
            header.Children.Add(export);
        }

        var item = new MenuItem { Header = header, Tag = name, StaysOpenOnClick = true };
        item.Classes.AddRange(["FlyoutMenuItem", "SettingsOptionItem"]);
        item.Click += (_, e) =>
        {
            _vm.GroupFrameAlertSound = name.Length > 0 ? name : null;
            _vm.PlayAlertSound();
            e.Handled = true;
        };
        return item;
    }

    // The editor, over the window the menu is in (the meter's or the group frame's).
    private void AddClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AlertSoundEditorWindow.Open(_vm, (sender as Control)?.FindLogicalAncestorOfType<Window>());
    }

    // A copy of the sound where the user picks, to pass on: whoever gets it adds it with "Add from file…".
    private async void ExportClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: string name } button || button.FindLogicalAncestorOfType<Window>() is not { } owner)
            return;

        try
        {
            using var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _vm.Localization["Settings_GroupFrame_AlertExport"],
                SuggestedFileName = name + ".wav",
                DefaultExtension = "wav",
                FileTypeChoices = [new FilePickerFileType(_vm.Localization["Settings_GroupFrame_AlertFileType"]) { Patterns = ["*.wav"] }],
                ShowOverwritePrompt = true
            });
            if (file is null)
                return;

            await using var target = await file.OpenWriteAsync();
            if (target.CanSeek)
                target.SetLength(0);
            await using var source = File.OpenRead(_vm.AlertSounds.PathOf(name));
            await source.CopyToAsync(target);
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Exporting the alert sound '{name}' failed: {ex}");
        }
    }
}
