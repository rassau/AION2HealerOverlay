using Avalonia.Controls;
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

// The group frame's settings by section: its tab in the settings window has them all, the Audio tab the alert, the Visuals
// tab its size and opacities.
[Flags]
internal enum GroupFrameRows
{
    Enable = 1,
    // Buffs, debuffs, sides, shields, deaths and the distance marks.
    Display = 2,
    // The boss alert: on or off, the sound, how loud, the quiet time, and a try.
    Alert = 8,
    // The frame's size and its parts' opacities.
    Visuals = 16,
    Clickable = 32,
}

// The group frame's settings rows of the chosen sections, filled into a menu of the settings window.
internal sealed class GroupFrameSettingsMenu : SettingsRows
{
    private MenuItem? _alertSound;
    private string[]? _shownSounds;

    public GroupFrameSettingsMenu(SettingsFlyoutViewModel vm, ItemCollection items, GroupFrameRows rows) : base(vm, items)
    {
        if (rows.HasFlag(GroupFrameRows.Enable))
            OnOff("Settings_SkillMonitor_Enable", () => vm.GroupFrameEnabled, on => vm.GroupFrameEnabled = on);

        if (rows.HasFlag(GroupFrameRows.Display))
        {
            ShowHide("Settings_GroupFrame_MyBuffs", () => vm.GroupFrameShowMyBuffs, on => vm.GroupFrameShowMyBuffs = on);
            ShowHide("Settings_GroupFrame_Debuffs", () => vm.GroupFrameShowDebuffs, on => vm.GroupFrameShowDebuffs = on);
            ShowHide("Settings_GroupFrame_OtherBuffs", () => vm.GroupFrameShowOtherBuffs, on => vm.GroupFrameShowOtherBuffs = on);
            ShowHide("Settings_GroupFrame_PermanentBuffs", () => vm.GroupFrameShowPermanentBuffs, on => vm.GroupFrameShowPermanentBuffs = on);
            Toggle("Settings_GroupFrame_AuraSide", () => vm.GroupFrameAuraSide = Next(vm.GroupFrameAuraSide), () => Side(vm.GroupFrameAuraSide), static () => false);
            Toggle("Settings_GroupFrame_ShieldBar", () => vm.GroupFrameShieldOnRight = !vm.GroupFrameShieldOnRight,
                () => Side(vm.GroupFrameShieldOnRight ? GroupFrameSide.Right : GroupFrameSide.Left), static () => false);
            OnOff("Settings_GroupFrame_ShieldAnimation", () => vm.GroupFrameShieldAnimation, on => vm.GroupFrameShieldAnimation = on);
            ShowHide("Settings_GroupFrame_Deaths", () => vm.GroupFrameShowDeaths, on => vm.GroupFrameShowDeaths = on);
            Toggle("Settings_GroupFrame_ArrowSide", () => vm.GroupFrameArrowSide = Next(vm.GroupFrameArrowSide), () => Side(vm.GroupFrameArrowSide), static () => false);
            Toggle("Settings_GroupFrame_DistanceSide", () => vm.GroupFrameDistanceSide = Next(vm.GroupFrameDistanceSide), () => Side(vm.GroupFrameDistanceSide), static () => false);
            Meters("Settings_GroupFrame_DistanceGreen", meters => vm.GroupFrameGreenMeters = meters, () => vm.GroupFrameGreenMeters);
            Meters("Settings_GroupFrame_DistanceOrange", meters => vm.GroupFrameOrangeMeters = meters, () => vm.GroupFrameOrangeMeters);
            Meters("Settings_GroupFrame_DistanceRange", meters => vm.GroupFrameRangeMeters = meters, () => vm.GroupFrameRangeMeters);
        }

        // The boss alert: on or off, which sound (the chime, the user's own, a new one from a file), how loud (the cube
        // finder's sound too), and a try.
        if (rows.HasFlag(GroupFrameRows.Alert))
        {
            OnOff("Settings_GroupFrame_AlertEnabled", () => vm.GroupFrameAlertEnabled, on => vm.GroupFrameAlertEnabled = on);
            var sound = _alertSound = Row();
            Add(sound, () =>
            {
                SettingsFlyoutView.SetRowHeader(sound, vm.Localization["Settings_GroupFrame_AlertSound"], SoundName());
                RefreshSounds();
            });
            Slider("Settings_GroupFrame_AlertVolume", () => vm.GroupFrameAlertVolumePercent, percent => vm.GroupFrameAlertVolumePercent = percent, min: 0, max: 200);
            Slider("Settings_GroupFrame_AlertQuiet", () => vm.GroupFrameAlertQuietSeconds, seconds => vm.GroupFrameAlertQuietSeconds = seconds, min: 0, max: 10, step: 1, unit: " s");
            Toggle("Settings_GroupFrame_AlertPlay", vm.PlayAlertSound, static () => string.Empty, static () => false);
        }

        if (rows.HasFlag(GroupFrameRows.Visuals))
        {
            Slider("Settings_Visuals_Scale", () => vm.GroupFrameScalePercent, percent => vm.GroupFrameScalePercent = percent, min: 50, max: 200);
            Slider("Settings_Visuals_HpOpacity", () => vm.GroupFrameHpOpacityPercent, percent => vm.GroupFrameHpOpacityPercent = percent);
            Slider("Settings_Visuals_AuraOpacity", () => vm.GroupFrameAuraOpacityPercent, percent => vm.GroupFrameAuraOpacityPercent = percent);
            Slider("Settings_Visuals_ArrowOpacity", () => vm.GroupFrameArrowOpacityPercent, percent => vm.GroupFrameArrowOpacityPercent = percent);
            Slider("Settings_Visuals_DistanceOpacity", () => vm.GroupFrameDistanceOpacityPercent, percent => vm.GroupFrameDistanceOpacityPercent = percent);
        }

        if (rows.HasFlag(GroupFrameRows.Clickable))
            Clickable(SettingsFlyoutViewModel.GroupFrameKey);
        Refresh();
    }

    private string Side(GroupFrameSide side) => Vm.Localization[side switch
    {
        GroupFrameSide.Left => "Settings_GroupFrame_ShieldBar_Left",
        GroupFrameSide.Off => "Settings_SkillMonitor_Off",
        _ => "Settings_GroupFrame_ShieldBar_Right",
    }];

    private string SoundName() => Vm.GroupFrameAlertSound switch
    {
        null => Vm.Localization["Settings_GroupFrame_AlertChime"],
        AlertSounds.KnockName => Vm.Localization["Settings_GroupFrame_AlertKnock"],
        AlertSounds.BlipName => Vm.Localization["Settings_GroupFrame_AlertBlip"],
        var name => name,
    };

    // A click goes left, right, off, and round again.
    private static GroupFrameSide Next(GroupFrameSide side) => side switch
    {
        GroupFrameSide.Left => GroupFrameSide.Right,
        GroupFrameSide.Right => GroupFrameSide.Off,
        _ => GroupFrameSide.Left,
    };

    // A distance mark: its value in the row, the choices in a submenu of its own.
    private void Meters(string labelKey, Action<int> select, Func<int> get)
    {
        var item = Row();
        SettingsFlyoutView.RebuildTaggedMenuItems(item, Vm.GroupFrameDistanceOptions, SettingsFlyoutViewModel.FormatMeters, static meters => meters, static _ => false, (sender, _) =>
        {
            if (sender is MenuItem { Tag: int meters })
                select(meters);
        });
        Add(item, () =>
        {
            var meters = get();
            SettingsFlyoutView.SetRowHeader(item, Vm.Localization[labelKey], SettingsFlyoutViewModel.FormatMeters(meters));
            SettingsFlyoutView.RefreshTaggedMenuCheckmarks<int>(item, value => value == meters);
        });
    }

    // The built-in chime, knock and blip, the user's own sounds (each with a button to export it) and a row to add one from
    // a file. Built anew only when the list changes, as picking a sound leaves the list open.
    private void RefreshSounds()
    {
        if (_alertSound is not { } list)
            return;

        var vm = Vm;
        if (_shownSounds is null || !_shownSounds.SequenceEqual(vm.GroupFrameAlertSounds))
        {
            _shownSounds = [.. vm.GroupFrameAlertSounds];
            list.Items.Clear();
            list.Items.Add(Sound(string.Empty, vm.Localization["Settings_GroupFrame_AlertChime"]));
            list.Items.Add(Sound(AlertSounds.KnockName, vm.Localization["Settings_GroupFrame_AlertKnock"]));
            list.Items.Add(Sound(AlertSounds.BlipName, vm.Localization["Settings_GroupFrame_AlertBlip"]));
            foreach (var name in _shownSounds)
                list.Items.Add(Sound(name, name));
            var add = new MenuItem { Header = vm.Localization["Settings_GroupFrame_AlertAdd"] };
            add.Classes.AddRange(["FlyoutMenuItem", "SettingsOptionItem"]);
            add.Click += AddClicked;
            list.Items.Add(add);
        }

        SettingsFlyoutView.RefreshTaggedMenuCheckmarks<string>(list, name => name == (vm.GroupFrameAlertSound ?? string.Empty));
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
            ToolTip.SetTip(export, Vm.Localization["Settings_GroupFrame_AlertExport"]);
            export.Click += ExportClicked;
            Grid.SetColumn(export, 1);
            header.Children.Add(export);
        }

        var item = new MenuItem { Header = header, Tag = name, StaysOpenOnClick = true };
        item.Classes.AddRange(["FlyoutMenuItem", "SettingsOptionItem"]);
        item.Click += (_, e) =>
        {
            Vm.GroupFrameAlertSound = name.Length > 0 ? name : null;
            Vm.PlayAlertSound();
            e.Handled = true;
        };
        return item;
    }

    // The editor, over the settings window.
    private void AddClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AlertSoundEditorWindow.Open(Vm, (sender as Control)?.FindLogicalAncestorOfType<Window>());
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
                Title = Vm.Localization["Settings_GroupFrame_AlertExport"],
                SuggestedFileName = name + ".wav",
                DefaultExtension = "wav",
                FileTypeChoices = [new FilePickerFileType(Vm.Localization["Settings_GroupFrame_AlertFileType"]) { Patterns = ["*.wav"] }],
                ShowOverwritePrompt = true
            });
            if (file is null)
                return;

            await using var target = await file.OpenWriteAsync();
            if (target.CanSeek)
                target.SetLength(0);
            await using var source = File.OpenRead(Vm.AlertSounds.PathOf(name));
            await source.CopyToAsync(target);
        }
        catch (Exception ex)
        {
            AppLog.Write(AppLogLevel.Warning, $"Exporting the alert sound '{name}' failed: {ex}");
        }
    }
}
