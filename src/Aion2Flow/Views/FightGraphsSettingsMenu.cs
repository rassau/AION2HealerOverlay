using Avalonia.Controls;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

[Flags]
internal enum FightGraphsRows
{
    Enable = 1,
    // The overview, the members' curves, their deaths and the metric of the best pull.
    Display = 2,
    Visuals = 4,
    Clickable = 8,
}

// The fight graphs' settings rows of the chosen sections, filled into a menu of the settings window.
internal sealed class FightGraphsSettingsMenu : SettingsRows
{
    public FightGraphsSettingsMenu(SettingsFlyoutViewModel vm, ItemCollection items, FightGraphsRows rows) : base(vm, items)
    {
        if (rows.HasFlag(FightGraphsRows.Enable))
            OnOff("Settings_SkillMonitor_Enable", () => vm.FightGraphsEnabled, on => vm.FightGraphsEnabled = on);

        if (rows.HasFlag(FightGraphsRows.Display))
        {
            ShowHide("Settings_FightGraphs_Overview", () => vm.FightGraphsShowOverview, on => vm.FightGraphsShowOverview = on);
            ShowHide("Settings_FightGraphs_Members", () => vm.FightGraphsShowMembers, on => vm.FightGraphsShowMembers = on);
            ShowHide("Settings_FightGraphs_OtherDeaths", () => vm.FightGraphsShowOtherDeaths, on => vm.FightGraphsShowOtherDeaths = on);
            ToolTip.SetTip(OnOff("Settings_FightGraphs_Smooth", () => vm.FightGraphsSmoothCurves, on => vm.FightGraphsSmoothCurves = on),
                vm.Localization["Settings_FightGraphs_SmoothTip"]);
            // The metric that picks a boss's best pull, the graphs' dotted line; the choices in a submenu of its own.
            var best = Row();
            SettingsFlyoutView.RebuildTaggedMenuItems(best, Enum.GetValues<FightMetric>(), metric => vm.Localization[FightGraphsViewModel.ShortKeyOf(metric)],
                static metric => metric, static _ => false, (sender, _) =>
                {
                    if (sender is MenuItem { Tag: FightMetric metric })
                        vm.FightGraphsBestMetric = metric;
                });
            Add(best, () =>
            {
                SettingsFlyoutView.SetRowHeader(best, vm.Localization["Settings_FightGraphs_BestMetric"], vm.Localization[FightGraphsViewModel.ShortKeyOf(vm.FightGraphsBestMetric)]);
                SettingsFlyoutView.RefreshTaggedMenuCheckmarks<FightMetric>(best, metric => metric == vm.FightGraphsBestMetric);
            });
        }

        if (rows.HasFlag(FightGraphsRows.Visuals))
            Visuals(() => vm.FightGraphsScalePercent, percent => vm.FightGraphsScalePercent = percent, () => vm.FightGraphsOpacityPercent, percent => vm.FightGraphsOpacityPercent = percent);

        if (rows.HasFlag(FightGraphsRows.Clickable))
            Clickable(SettingsFlyoutViewModel.FightGraphsKey);
        Refresh();
    }
}
