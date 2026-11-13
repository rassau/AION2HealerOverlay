using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// Rows of the settings window filled into a menu, each with what brings it up to date (Refresh): toggles, submenus and
// sliders.
internal class SettingsRows(SettingsFlyoutViewModel vm, ItemCollection items)
{
    private readonly List<Action> _refreshes = [];

    protected SettingsFlyoutViewModel Vm { get; } = vm;

    protected ItemCollection Items { get; } = items;

    public void Refresh()
    {
        foreach (var refresh in _refreshes)
            refresh();
    }

    // A row and what keeps it up to date.
    public void Add(MenuItem item, Action refresh)
    {
        Items.Add(item);
        _refreshes.Add(refresh);
    }

    public MenuItem Toggle(string labelKey, Action toggle, Func<string> value, Func<bool> isChecked)
    {
        var item = Toggle(toggle);
        Add(item, () => SettingsFlyoutView.RefreshToggleMenuItem(item, Vm.Localization[labelKey], value(), isChecked()));
        return item;
    }

    public MenuItem OnOff(string labelKey, Func<bool> get, Action<bool> set) => Toggle(labelKey, () => set(!get()), () => OnOffText(get()), get);

    public MenuItem ShowHide(string labelKey, Func<bool> get, Action<bool> set) => Toggle(labelKey, () => set(!get()), () => ShowHideText(get()), get);

    // live: the setting follows the slider while it moves (saved once it's let go); otherwise it's set once let go.
    public void Slider(string labelKey, Func<int> get, Action<int> set, int min = 10, int max = 100, int step = 5, string unit = "%", bool live = true)
    {
        var (label, slider) = SliderRow(Vm, Items, set, min, max, step, unit, live);
        _refreshes.Add(() =>
        {
            label.Text = Vm.Localization[labelKey];
            slider.Value = get();
        });
    }

    // A frame's size and opacity (the Visuals).
    public void Visuals(Func<int> scale, Action<int> setScale, Func<int> opacity, Action<int> setOpacity)
    {
        Slider("Settings_Visuals_Scale", scale, setScale, min: 50, max: 200);
        Slider("Settings_OverlayOpacity", opacity, setOpacity, min: 20);
    }

    public void Clickable(params string[] frames) => _refreshes.Add(new ClickableFramesMenu(Vm, Items, frames, named: frames.Length > 1).Refresh);

    protected string OnOffText(bool on) => Vm.Localization[on ? "Settings_SkillMonitor_On" : "Settings_SkillMonitor_Off"];

    protected string ShowHideText(bool shown) => Vm.Localization[shown ? "Settings_MainMetricVisibility_Show" : "Settings_MainMetricVisibility_Hide"];

    // Labels and ticks come from Refresh.
    internal static MenuItem Toggle(Action toggle) =>
        SettingsFlyoutView.CreateToggleMenuItem(string.Empty, string.Empty, false, (_, e) =>
        {
            toggle();
            e.Handled = true;
        });

    // A row to click, or with a submenu of its own; its header comes from Refresh.
    internal static MenuItem Row()
    {
        var item = new MenuItem();
        item.Classes.AddRange(["FlyoutMenuItem", "FlyoutPanelRow", "SettingsRowItem"]);
        return item;
    }

    // A slider row: the label, the slider and its value; the label and the slider's place are left to the caller's
    // Refresh (which changes only those, so a drag goes on undisturbed). The settings are saved once it's let go, like
    // the other sliders.
    internal static (TextBlock Label, Slider Slider) SliderRow(SettingsFlyoutViewModel vm, ItemCollection items, Action<int> set, int min, int max, int step, string unit = "%", bool live = true)
    {
        var label = new TextBlock();
        label.Classes.Add("SettingsRowLabel");
        var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true };
        slider.Classes.Add("SettingsScaleSlider");
        Grid.SetColumn(slider, 1);
        var value = new TextBlock();
        value.Classes.Add("SettingsScaleValue");
        Grid.SetColumn(value, 2);
        int Value() => (int)Math.Round(slider.Value);
        void LetGo()
        {
            if (!live)
                set(Value());
            vm.SaveOverlayOpacity();
        }

        value.Text = $"{Value()}{unit}";
        slider.ValueChanged += (_, _) =>
        {
            value.Text = $"{Value()}{unit}";
            if (live)
                set(Value());
        };
        slider.AddHandler(InputElement.PointerReleasedEvent, (_, _) => LetGo(), RoutingStrategies.Bubble, handledEventsToo: true);
        slider.KeyUp += (_, _) => LetGo();
        var row = new MenuItem
        {
            Header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10, Children = { label, slider, value } },
            StaysOpenOnClick = true,
        };
        row.Classes.AddRange(["FlyoutMenuItem", "FlyoutPanelRow", "SettingsRowItem"]);
        items.Add(row);
        return (label, slider);
    }
}
