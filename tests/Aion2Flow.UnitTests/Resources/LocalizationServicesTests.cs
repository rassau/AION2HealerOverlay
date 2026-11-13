using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Cloris.Aion2Flow.Localization;
using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Tests.Resources;

public sealed partial class LocalizationServicesTests
{
    [Fact]
    public void German_Interface_Has_Every_Text_With_Its_Placeholders()
    {
        var neutral = Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)!;
        var german = Strings.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(LanguageService.German), createIfNotExists: true, tryParents: false)!;

        foreach (DictionaryEntry entry in neutral)
        {
            var key = (string)entry.Key;
            var text = german.GetString(key);
            Assert.False(string.IsNullOrWhiteSpace(text), $"{key} has no German text");
            Assert.True(Placeholders((string)entry.Value!).SequenceEqual(Placeholders(text!)), $"{key}: placeholders differ");
        }
    }

    [Fact]
    public void German_Interface_And_Game_Names_Follow_The_Language()
    {
        var languageService = new LanguageService();
        try
        {
            languageService.SetLanguage(LanguageService.German);
            using var localization = new LocalizationService(languageService);
            using var resources = new GameResourceService(languageService);

            Assert.Equal("Bereit", localization["Status_Ready"]);
            Assert.Equal("Beschwörer", localization["CharacterClass_Elementalist"]);
            Assert.True(resources.TryResolveNpcCatalogEntry(2320171, out var berk));
            Assert.Equal("Vollendete Berk", berk.Name);
        }
        finally
        {
            languageService.SetLanguage(LanguageService.English);
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    private static string[] Placeholders(string text) => [.. PlaceholderPattern().Matches(text).Select(static m => m.Value).Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    [Fact]
    public void LocalizationService_Indexer_Updates_When_Language_Changes()
    {
        var languageService = new LanguageService();
        languageService.SetLanguage(LanguageService.English);
        languageService.SetLanguage(LanguageService.TraditionalChinese);
        using var localization = new LocalizationService(languageService);

        Assert.Equal("就緒", localization["Status_Ready"]);

        var changed = languageService.SetLanguage(LanguageService.English);

        Assert.True(changed);
        Assert.Equal("Ready", localization["Status_Ready"]);
    }

    [Fact]
    public void GameResourceService_Reloads_Skill_And_Npc_Names_When_Language_Changes()
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.Korean);
            using var resources = new GameResourceService(languageService);

            var koSkills = ResourceCatalog.Load(ResourceLanguage.Korean).Skills;
            var enSkills = ResourceCatalog.Load(ResourceLanguage.English).Skills;
            var koCatalog = ResourceCatalog.Load(ResourceLanguage.Korean).NpcCatalog;
            var enCatalog = ResourceCatalog.Load(ResourceLanguage.English).NpcCatalog;
            var koServerNames = ResourceCatalog.Load(ResourceLanguage.Korean).ServerNames;
            var enServerNames = ResourceCatalog.Load(ResourceLanguage.English).ServerNames;

            Assert.True(koSkills.TryGetValue(2011101, out var koSkill));
            Assert.True(enSkills.TryGetValue(2011101, out var enSkill));
            Assert.NotEqual(koSkill.Name, enSkill.Name);

            Assert.True(koCatalog.TryGetValue(2000002, out var koNpc));
            Assert.True(enCatalog.TryGetValue(2000002, out var enNpc));
            Assert.NotEqual(koNpc.Name, enNpc.Name);

            Assert.True(koServerNames.TryGetValue(1001, out var koServer));
            Assert.True(enServerNames.TryGetValue(1001, out var enServer));
            Assert.NotEqual(koServer.ServerName, enServer.ServerName);

            Assert.Equal(koSkill.Name, resources.ResolveSkillName(2011101));
            Assert.True(resources.TryResolveNpcCatalogEntry(2000002, out var initialNpc));
            Assert.Equal(koNpc.Name, initialNpc.Name);
            Assert.Equal(koServer.ServerName, resources.ResolveServerName(1001));
            Assert.Equal(koServer.ShortServerName, resources.ResolveShortServerName(1001));

            string? changedLanguage = null;
            resources.ResourcesChanged += (_, language) => changedLanguage = language;

            var switched = languageService.SetLanguage(LanguageService.English);

            Assert.True(switched);
            Assert.Equal(LanguageService.English, changedLanguage);
            Assert.Equal(enSkill.Name, resources.ResolveSkillName(2011101));
            Assert.True(resources.TryResolveNpcCatalogEntry(2000002, out var updatedNpc));
            Assert.Equal(enNpc.Name, updatedNpc.Name);
            Assert.Equal(enServer.ServerName, resources.ResolveServerName(1001));
            Assert.Equal(enServer.ShortServerName, resources.ResolveShortServerName(1001));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    [Theory]
    [InlineData(100014, "Fire Spirit: Basic Attack", "ICON_EL_SKILL_010.webp")]
    [InlineData(100018, "Fire Spirit: Basic Attack", "ICON_EL_SKILL_010.webp")]
    [InlineData(100024, "Water Spirit: Basic Attack", "ICON_EL_SKILL_011.webp")]
    [InlineData(100028, "Water Spirit: Basic Attack", "ICON_EL_SKILL_011.webp")]
    [InlineData(100034, "Wind Spirit: Basic Attack", "ICON_EL_SKILL_012.webp")]
    [InlineData(100048, "Earth Spirit: Basic Attack", "ICON_EL_SKILL_013.webp")]
    [InlineData(17040257, "Divine Punishment", "ICON_CL_SKILL_005.webp")]
    [InlineData(170402571, "Divine Punishment", "ICON_CL_SKILL_005.webp")]
    [InlineData(16030047, "Earth Tremor", "ICON_EL_SKILL_003.webp")]
    [InlineData(160300471, "Earth Tremor", "ICON_EL_SKILL_003.webp")]
    [InlineData(17440047, "Noble Aura", "ICON_CL_SKILL_046.webp")]
    [InlineData(17730001, "Empyrean Lord's Grace", "ICON_CL_SKILL_Passive_012.webp")]
    [InlineData(3001110, "Theostone: Charna's Root", "Icon_Item_Usable_Godstone_WP_r_004.webp")]
    [InlineData(30011101, "Theostone: Charna's Root", "Icon_Item_Usable_Godstone_WP_r_004.webp")]
    [InlineData(3000122, "Theostone: Charna's Acumen", "Icon_Item_Usable_Godstone_WP_r_016.webp")]
    [InlineData(17270040, "Salvation", "ICON_CL_SKILL_026.webp")]
    [InlineData(17270047, "Salvation", "ICON_CL_SKILL_026.webp")]
    [InlineData(17280010, "Power Burst", "ICON_CL_SKILL_027.webp")]
    [InlineData(17290000, "Absolution", "ICON_CL_SKILL_028.webp")]
    [InlineData(17420010, "Yustiel's Power", "ICON_CL_SKILL_042.webp")]
    [InlineData(1227237, "Attack", "ICON_TE_SKILL_001.webp")]
    [InlineData(1227265, "Wraith Surge", "ICON_TE_SKILL_001.webp")]
    [InlineData(1607415, "Attack", "ICON_TE_SKILL_001.webp")]
    [InlineData(1607400, "Attack", "ICON_TE_SKILL_001.webp")]
    public void GameResourceService_Resolves_Display_Names_And_Icons(int skillCode, string expectedName, string expectedIcon)
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.TraditionalChinese);
            using var resources = new GameResourceService(languageService);

            Assert.Equal(expectedName, resources.ResolveSkillName(skillCode));
            Assert.Equal(expectedIcon, resources.ResolveSkillIconAssetName(skillCode));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    [Fact]
    public void GameResourceService_Resolves_RowBase_For_SameFamily_Skill()
    {
        var languageService = new LanguageService();
        languageService.SetLanguage(LanguageService.TraditionalChinese);
        using var resources = new GameResourceService(languageService);

        Assert.Equal(13_160_000, resources.ResolveBaseSkillIdForCode(13_160_007));
        Assert.Equal(13_160_000, resources.ResolveBaseSkillIdForCode(13_160_000));
    }

    [Theory]
    [InlineData(11_250_000, true)]
    [InlineData(11_250_010, true)]
    [InlineData(2_210_103, false)]
    [InlineData(3_001_110, false)]
    public void GameResourceService_Identifies_Player_Profession_Skills(int skillCode, bool expected)
    {
        var languageService = new LanguageService();
        languageService.SetLanguage(LanguageService.TraditionalChinese);
        using var resources = new GameResourceService(languageService);

        Assert.Equal(expected, resources.IsPlayerProfessionSkill(skillCode));
    }

    [Theory]
    [InlineData(12130030, "Poach")]
    [InlineData(12780001, "Fury")]
    [InlineData(2210103, "Speed Scroll")]
    [InlineData(11190000, "Leaping Slam")]
    [InlineData(13130000, "Insignia Explosion")]
    [InlineData(13050000, "Flash Slice")]
    public void GameResourceService_Resolves_Runtime_Aura_And_Cooldown_RowBase_Ids(int resourceId, string expectedName)
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.TraditionalChinese);
            using var resources = new GameResourceService(languageService);

            Assert.Equal(expectedName, resources.ResolveSkillName(resourceId));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }
}
