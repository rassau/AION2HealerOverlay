using Avalonia.Controls;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// The overlay frames' "Clickable without focus" toggles, filled into a menu: every frame by its name in the General tab,
// a frame's alone under that label in its own tab.
internal sealed class ClickableFramesMenu
{
    private readonly SettingsFlyoutViewModel _vm;
    private readonly bool _named;
    private readonly (string Frame, MenuItem Item)[] _rows;

    public ClickableFramesMenu(SettingsFlyoutViewModel vm, ItemCollection items, IReadOnlyList<string> frames, bool named)
    {
        _vm = vm;
        _named = named;
        _rows = [.. frames.Select(frame => (frame, SettingsRows.Toggle(() => vm.ToggleClickableWithoutFocus(frame))))];
        foreach (var (_, item) in _rows)
            items.Add(item);
        Refresh();
    }

    public void Refresh()
    {
        foreach (var (frame, item) in _rows)
        {
            var on = _vm.IsClickableWithoutFocus(frame);
            var label = _named ? NameOf(_vm, frame) : _vm.Localization["Settings_ClickableWithoutFocus"];
            SettingsFlyoutView.RefreshToggleMenuItem(item, label, _vm.Localization[on ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"], on);
            ToolTip.SetTip(item, _vm.Localization["Settings_ClickableWithoutFocusTip"]);
        }
    }

    private static string NameOf(SettingsFlyoutViewModel vm, string frame) => vm.Localization[frame switch
    {
        SettingsFlyoutViewModel.GroupFrameKey => "Settings_GroupFrame",
        SettingsFlyoutViewModel.FightGraphsKey => "Settings_FightGraphs",
        SettingsFlyoutViewModel.SkillMonitorKey => "Settings_SkillMonitor",
        SettingsFlyoutViewModel.TwinLibraryKey => "TwinLibrary_Title",
        _ => "Settings_ClickablePlayback",
    }];
}
