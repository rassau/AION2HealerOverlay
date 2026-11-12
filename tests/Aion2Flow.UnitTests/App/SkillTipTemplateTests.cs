using Avalonia.Controls;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class SkillTipTemplateTests
{
    // The template is the app's for every SkillTip, so it must take nothing else: an empty content area looking for a
    // template (the main window has some while it opens) must not get it.
    [Fact]
    public void The_Tip_Template_Takes_Skill_Tips_Only()
    {
        AvaloniaTestHost.Run(() =>
        {
            var template = SkillTipTemplate.Create();
            Assert.False(template.Match(null));
            Assert.False(template.Match("Light of Regeneration"));

            var tip = new SkillTip("Light of Regeneration", "Restores HP every 2s.", "Numbers for skill level 17.", null);
            Assert.True(template.Match(tip));
            var panel = Assert.IsType<StackPanel>(template.Build(tip));
            Assert.Equal(["Light of Regeneration", "Restores HP every 2s.", "Numbers for skill level 17."], panel.Children.OfType<TextBlock>().Select(static text => text.Text));
        });
    }
}
