using Avalonia.Controls;
using Cloris.Aion2Flow.SceneRuntime.Stores;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

[Flags]
internal enum FightGraphsRows
{
    Enable = 1,
    // The overview (in thousands or not, with the others' deaths or not) and its UHS, Taken and Rec. (the group's or the
    // user's own), the members' curves, their deaths, the metric of the best pull and new bests in the title line.
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
            OnOff("Settings_FightGraphs_OverviewThousands", () => vm.FightGraphsOverviewThousands, on => vm.FightGraphsOverviewThousands = on);
            ShowHide("Settings_FightGraphs_OverviewOtherDeaths", () => vm.FightGraphsOverviewOtherDeaths, on => vm.FightGraphsOverviewOtherDeaths = on);
            GroupValue("Settings_FightGraphs_Uhs", FightMetric.UnhealedHealth, () => vm.FightGraphsUhs, mode => vm.FightGraphsUhs = mode);
            GroupValue("Settings_FightGraphs_Taken", FightMetric.DamageTaken, () => vm.FightGraphsTaken, mode => vm.FightGraphsTaken = mode);
            GroupValue("Settings_FightGraphs_Rec", FightMetric.RecoveryTime, () => vm.FightGraphsRec, mode => vm.FightGraphsRec = mode);
            ShowHide("Settings_FightGraphs_Members", () => vm.FightGraphsShowMembers, on => vm.FightGraphsShowMembers = on);
            ShowHide("Settings_FightGraphs_OtherDeaths", () => vm.FightGraphsShowOtherDeaths, on => vm.FightGraphsShowOtherDeaths = on);
            OnOff("Settings_FightGraphs_Smooth", () => vm.FightGraphsSmoothCurves, on => vm.FightGraphsSmoothCurves = on);
            // The metric that picks a boss's best pull, the graphs' dotted line; the choices in a submenu of its own.
            var best = Row();
            Tip(best, "Settings_FightGraphs_BestMetricTip");
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
            OnOff("Settings_FightGraphs_NewBestsInTitle", () => vm.FightGraphsNewBestsInTitle, on => vm.FightGraphsNewBestsInTitle = on);
        }

        if (rows.HasFlag(FightGraphsRows.Visuals))
            Visuals(() => vm.FightGraphsScalePercent, percent => vm.FightGraphsScalePercent = percent, () => vm.FightGraphsOpacityPercent, percent => vm.FightGraphsOpacityPercent = percent);

        if (rows.HasFlag(FightGraphsRows.Clickable))
            Clickable(SettingsFlyoutViewModel.FightGraphsKey);
        Refresh();
    }

    // The group's value ("UHS*") or the user's own ("UHS"), ticked while it's the group's for the player's class; a click goes
    // through Auto, the group's and the user's own, like the meter's aggro column.
    private void GroupValue(string labelKey, FightMetric metric, Func<GroupValueMode> get, Action<GroupValueMode> set) =>
        Toggle(labelKey, () => set(get() switch
            {
                GroupValueMode.Auto => GroupValueMode.Group,
                GroupValueMode.Group => GroupValueMode.Own,
                _ => GroupValueMode.Auto,
            }),
            () => get() == GroupValueMode.Auto
                ? Vm.Localization["Settings_MainMetricVisibility_Auto"]
                : Vm.Localization[FightGraphsViewModel.ShortKeyOf(metric)] + (get() == GroupValueMode.Group ? "*" : string.Empty),
            () => BestRunKeys.UsesGroup(get(), Vm.LocalPlayerClass));
}
