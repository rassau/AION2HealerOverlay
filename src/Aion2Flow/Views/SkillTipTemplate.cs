using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

// How a SkillTip shows on hover, like the Buffs tab's: the name, what the skill does, the caster's specializations, and the
// note on the levels the numbers are for (faded). Registered for the whole app (see App.Initialize), so any ToolTip.Tip
// bound to a SkillTip shows this way. It takes SkillTips only: a typed FuncDataTemplate<SkillTip> also takes null, and as
// the app's template it then filled empty content areas while the main window opened, with nothing to show (a crash).
internal static class SkillTipTemplate
{
    public static FuncDataTemplate Create() => new(static data => data is SkillTip, static (data, _) => Build((SkillTip)data!));

    private static StackPanel Build(SkillTip tip)
    {
        var panel = new StackPanel { MaxWidth = 380, Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = tip.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        foreach (var (text, opacity) in new[] { (tip.Effect, 1d), (tip.Specializations, 1d), (tip.EffectNote, 0.65) })
        {
            if (!string.IsNullOrEmpty(text))
                panel.Children.Add(new TextBlock { Text = text, Opacity = opacity, TextWrapping = TextWrapping.Wrap });
        }

        return panel;
    }
}
