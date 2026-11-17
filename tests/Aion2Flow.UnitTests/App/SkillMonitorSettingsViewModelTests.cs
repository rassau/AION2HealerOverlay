using Avalonia.Threading;
using Cloris.Aion2Flow.SceneRuntime.Twins;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class SkillMonitorSettingsViewModelTests
{
    [Fact]
    public void DefaultSelectionIncludesEveryRowBaseSkillForBuffsCooldownsAndDebuffs()
    {
        using var fixture = new SkillMonitorSettingsFixture();

        var skills = fixture.ViewModel.Groups.SelectMany(static group => group.Skills).ToArray();

        Assert.NotEmpty(skills);
        Assert.Equal(skills.Length, skills.Select(static skill => skill.SkillId).Distinct().Count());
        Assert.All(skills, static skill =>
        {
            Assert.True(skill.IsBuffSelected);
            Assert.True(skill.IsCooldownSelected);
            Assert.True(skill.IsDebuffSelected);
        });
        Assert.True(fixture.Settings.Current.SkillMonitorBuffSelectAll);
        Assert.Empty(fixture.Settings.Current.SkillMonitorBuffSkillIds);
        Assert.True(fixture.Settings.Current.SkillMonitorCooldownSelectAll);
        Assert.Empty(fixture.Settings.Current.SkillMonitorCooldownSkillIds);
        Assert.True(fixture.Settings.Current.SkillMonitorDebuffSelectAll);
        Assert.Empty(fixture.Settings.Current.SkillMonitorDebuffSkillIds);
        Assert.False(SkillMonitorSelection.IncludesBuff(fixture.Settings.Current, fixture.Resources, 2_210_103));
        Assert.False(SkillMonitorSelection.IncludesCooldown(fixture.Settings.Current, fixture.Resources, 2_210_103));
        Assert.False(SkillMonitorSelection.IncludesDebuff(fixture.Settings.Current, fixture.Resources, 2_210_103));
    }

    [Fact]
    public void DebuffSelectionStaysIndependentOfBuffsAndCooldowns()
    {
        using var fixture = new SkillMonitorSettingsFixture();
        var skills = fixture.ViewModel.Groups.SelectMany(static group => group.Skills).ToArray();

        skills[0].IsDebuffSelected = false;

        var settings = fixture.Settings.Current;
        Assert.False(settings.SkillMonitorDebuffSelectAll);
        Assert.Equal(skills.Length - 1, settings.SkillMonitorDebuffSkillIds.Count);
        Assert.True(settings.SkillMonitorBuffSelectAll && settings.SkillMonitorCooldownSelectAll);
        Assert.True(skills[0].IsBuffSelected && skills[0].IsCooldownSelected);
        Assert.False(SkillMonitorSelection.IncludesDebuff(settings, fixture.Resources, skills[0].SkillId));
        Assert.True(SkillMonitorSelection.IncludesDebuff(settings, fixture.Resources, skills[1].SkillId));
        Assert.Equal($"Debuffs {skills.Length - 1}/{skills.Length}", fixture.ViewModel.DebuffSelectionSummary);

        var changedProperties = new List<string?>();
        skills[1].PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        fixture.ViewModel.ClearAllDebuffsCommand.Execute(null);

        Assert.False(fixture.Settings.Current.SkillMonitorDebuffSelectAll);
        Assert.Empty(fixture.Settings.Current.SkillMonitorDebuffSkillIds);
        Assert.True(fixture.Settings.Current.SkillMonitorBuffSelectAll);
        Assert.All(skills, static skill => Assert.False(skill.IsDebuffSelected));
        Assert.Equal([nameof(SkillMonitorSkillOption.IsDebuffSelected)], changedProperties);

        fixture.ViewModel.SelectAllDebuffsCommand.Execute(null);

        Assert.True(fixture.Settings.Current.SkillMonitorDebuffSelectAll);
        Assert.Empty(fixture.Settings.Current.SkillMonitorDebuffSkillIds);
        Assert.All(skills, static skill => Assert.True(skill.IsDebuffSelected));
    }

    [Fact]
    public void IndividualBuffAndCooldownSelectionsRemainIndependent()
    {
        using var fixture = new SkillMonitorSettingsFixture();
        var skills = fixture.ViewModel.Groups.SelectMany(static group => group.Skills).ToArray();
        var excludedBuff = skills[0];
        var excludedCooldown = skills[1];

        excludedBuff.IsBuffSelected = false;

        Assert.False(fixture.Settings.Current.SkillMonitorBuffSelectAll);
        Assert.DoesNotContain(excludedBuff.SkillId, fixture.Settings.Current.SkillMonitorBuffSkillIds);
        Assert.Equal(skills.Length - 1, fixture.Settings.Current.SkillMonitorBuffSkillIds.Count);
        Assert.True(fixture.Settings.Current.SkillMonitorCooldownSelectAll);
        Assert.True(excludedBuff.IsCooldownSelected);
        Assert.False(SkillMonitorSelection.IncludesBuff(fixture.Settings.Current, fixture.Resources, excludedBuff.SkillId));
        Assert.True(SkillMonitorSelection.IncludesBuff(fixture.Settings.Current, fixture.Resources, excludedCooldown.SkillId));
        Assert.True(SkillMonitorSelection.IncludesCooldown(fixture.Settings.Current, fixture.Resources, excludedBuff.SkillId));

        excludedCooldown.IsCooldownSelected = false;

        Assert.False(fixture.Settings.Current.SkillMonitorCooldownSelectAll);
        Assert.DoesNotContain(excludedCooldown.SkillId, fixture.Settings.Current.SkillMonitorCooldownSkillIds);
        Assert.Equal(skills.Length - 1, fixture.Settings.Current.SkillMonitorCooldownSkillIds.Count);
        Assert.True(excludedCooldown.IsBuffSelected);
        Assert.False(SkillMonitorSelection.IncludesCooldown(fixture.Settings.Current, fixture.Resources, excludedCooldown.SkillId));
        Assert.True(SkillMonitorSelection.IncludesCooldown(fixture.Settings.Current, fixture.Resources, excludedBuff.SkillId));
    }

    [Fact]
    public void BuffAndCooldownSelectAllTogglesAreIndependent()
    {
        using var fixture = new SkillMonitorSettingsFixture();
        var firstSkill = fixture.ViewModel.Groups.SelectMany(static group => group.Skills).First();
        var changedProperties = new List<string?>();
        firstSkill.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        fixture.ViewModel.ClearAllBuffsCommand.Execute(null);

        Assert.False(fixture.Settings.Current.SkillMonitorBuffSelectAll);
        Assert.Empty(fixture.Settings.Current.SkillMonitorBuffSkillIds);
        Assert.True(fixture.Settings.Current.SkillMonitorCooldownSelectAll);
        Assert.All(
            fixture.ViewModel.Groups.SelectMany(static group => group.Skills),
            static skill =>
            {
                Assert.False(skill.IsBuffSelected);
                Assert.True(skill.IsCooldownSelected);
            });
        Assert.Contains(nameof(SkillMonitorSkillOption.IsBuffSelected), changedProperties);

        fixture.ViewModel.ClearAllCooldownsCommand.Execute(null);

        Assert.False(fixture.Settings.Current.SkillMonitorCooldownSelectAll);
        Assert.Empty(fixture.Settings.Current.SkillMonitorCooldownSkillIds);
        Assert.All(
            fixture.ViewModel.Groups.SelectMany(static group => group.Skills),
            static skill => Assert.False(skill.IsCooldownSelected));
        Assert.Contains(nameof(SkillMonitorSkillOption.IsCooldownSelected), changedProperties);

        fixture.ViewModel.SelectAllBuffsCommand.Execute(null);
        fixture.ViewModel.SelectAllCooldownsCommand.Execute(null);

        Assert.True(fixture.Settings.Current.SkillMonitorBuffSelectAll);
        Assert.True(fixture.Settings.Current.SkillMonitorCooldownSelectAll);
        Assert.All(
            fixture.ViewModel.Groups.SelectMany(static group => group.Skills),
            static skill =>
            {
                Assert.True(skill.IsBuffSelected);
                Assert.True(skill.IsCooldownSelected);
            });
    }

    // Hovering a skill: what it does, with the numbers at the player's level of it from their digital twin and whose level
    // that is; from level 1 to max while the overlay doesn't know who plays, and for skills the twin has no level for. A
    // row on show takes the levels once the game names the player, also from a twin that was in already (no lookup says so).
    [Fact]
    public void Skill_Tips_Take_The_Players_Level_From_Their_Twin()
    {
        AvaloniaTestHost.Run(() =>
        {
            const int light = 17410000;
            using var twins = new DigitalTwinService();
            twins.Preload([new DigitalTwin("Healer A", 1301, "Server", "Cleric", 45, 0, 0, DateTimeOffset.UnixEpoch, [], [], [new TwinSkill(light, "Light", 21, "Passive", true)], [], [], [])]);
            using var fixture = new SkillMonitorSettingsFixture(twins);
            var skills = fixture.ViewModel.Groups.SelectMany(static group => group.Skills).ToArray();
            var buff = skills.Single(static skill => skill.SkillId == light);
            var other = skills.First(static skill => skill.SkillId != light && SkillEffects.Get(skill.SkillId) is not null);

            Assert.Equal(buff.DisplayName, buff.Tip.Name);
            Assert.Contains("by 10.5–22.5%", buff.Tip.Effect);
            Assert.Equal("Numbers for skill level 1 to 25.", buff.Tip.EffectNote);

            var tipChanged = false;
            buff.PropertyChanged += (_, e) => tipChanged |= e.PropertyName == nameof(SkillMonitorSkillOption.Tip);
            fixture.Settings.Update(static settings => (settings.LocalPlayerServerId, settings.LocalPlayerNickname) = (1301, "Healer A"));
            Dispatcher.UIThread.RunJobs();

            Assert.True(tipChanged);
            Assert.Contains("by 20.5% and", buff.Tip.Effect);
            Assert.Equal("Numbers for skill level 21, Healer A's level on the official website.", buff.Tip.EffectNote);
            Assert.Equal($"Numbers for skill level 1 to {SkillEffects.Get(other.SkillId)!.MaxLevel}.", other.Tip.EffectNote);
        });
    }

    private sealed class SkillMonitorSettingsFixture : IDisposable
    {
        private readonly string _settingsPath = Path.Combine(
            Path.GetTempPath(),
            "Aion2Flow.Tests",
            $"skill-monitor-{Guid.NewGuid():N}.json");
        private readonly LocalizationService _localization;
        private readonly GameResourceService _resources;

        public SkillMonitorSettingsFixture(DigitalTwinService? twins = null)
        {
            var language = new LanguageService();
            language.SetLanguage(LanguageService.English);
            _localization = new LocalizationService(language);
            _resources = new GameResourceService(language);
            Settings = new SettingsService(_settingsPath);
            ViewModel = new SkillMonitorSettingsViewModel(_resources, Settings, _localization, twins);
        }

        public SettingsService Settings { get; }

        public GameResourceService Resources => _resources;

        public SkillMonitorSettingsViewModel ViewModel { get; }

        public void Dispose()
        {
            ViewModel.Dispose();
            _resources.Dispose();
            _localization.Dispose();
            File.Delete(_settingsPath);
        }
    }
}
