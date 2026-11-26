using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Army;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

using TroopArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the campaign tables and the campaign state: the scenario graph of the six campaigns, the opening bonuses,
/// the awards and <see cref="CampaignSaveData"/>
/// (<c>campaign_data.cpp</c>, <c>campaign_scenariodata.cpp</c>, <c>campaign_savedata.cpp</c>).
/// </summary>
[Collection(CampaignCollection.Name)]
public sealed class CampaignTests
{
    /// <summary>The scenarios of each campaign, in scenario order.</summary>
    /// <param name="campaignId">The campaign.</param>
    /// <returns>The scenario count.</returns>
    private static int ScenarioCount(int campaignId) => CampaignData.GetCampaignData(campaignId).AllScenarios.Count;

    [Theory]
    [InlineData(CampaignId.Roland, 11)]
    [InlineData(CampaignId.Archibald, 12)]
    [InlineData(CampaignId.PriceOfLoyalty, 8)]
    [InlineData(CampaignId.Descendants, 8)]
    [InlineData(CampaignId.WizardsIsle, 4)]
    [InlineData(CampaignId.VoyageHome, 4)]
    public void CampaignsKeepTheirScenariosInOrder(int campaignId, int expectedCount)
    {
        CampaignData campaign = CampaignData.GetCampaignData(campaignId);

        Assert.Equal(campaignId, campaign.Id);
        Assert.Equal(expectedCount, campaign.AllScenarios.Count);

        for (int i = 0; i < campaign.AllScenarios.Count; ++i)
        {
            ScenarioData scenario = campaign.AllScenarios[i];

            Assert.Equal(new ScenarioInfoId(campaignId, i), scenario.ScenarioInfo);
            Assert.False(string.IsNullOrEmpty(scenario.ScenarioName));
            Assert.False(string.IsNullOrEmpty(scenario.Description));

            // 前两个战役的地图叫 .H2C，后四个叫 .HXC；端口统一存小写。
            string extension = Path.GetExtension(scenario.FileName);
            Assert.True(
                extension.Equals(".h2c", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".hxc", StringComparison.OrdinalIgnoreCase),
                $"Unexpected scenario map name '{scenario.FileName}'.");
            Assert.Equal(scenario.FileName.ToLowerInvariant(), scenario.FileName);
        }
    }

    [Theory]
    [InlineData(CampaignId.Roland)]
    [InlineData(CampaignId.Archibald)]
    [InlineData(CampaignId.PriceOfLoyalty)]
    [InlineData(CampaignId.Descendants)]
    [InlineData(CampaignId.WizardsIsle)]
    [InlineData(CampaignId.VoyageHome)]
    public void EveryCampaignStartsWithItsFirstScenario(int campaignId)
    {
        CampaignData campaign = CampaignData.GetCampaignData(campaignId);

        Assert.Equal([new ScenarioInfoId(campaignId, 0)], campaign.GetStartingScenarios());
        Assert.True(campaign.IsStartingScenario(new ScenarioInfoId(campaignId, 0)));
        Assert.False(campaign.IsStartingScenario(new ScenarioInfoId(campaignId, 1)));
    }

    [Fact]
    public void OnlyTheFinalScenariosHaveNoSuccessors()
    {
        // Roland 与 Archibald 的「叛变」关指向对方战役，所以它们不是终点。
        AssertLastScenarios(CampaignId.Roland, 9);
        AssertLastScenarios(CampaignId.Archibald, 10);
        AssertLastScenarios(CampaignId.PriceOfLoyalty, 7);
        AssertLastScenarios(CampaignId.Descendants, 7);
        AssertLastScenarios(CampaignId.WizardsIsle, 3);
        AssertLastScenarios(CampaignId.VoyageHome, 2, 3);
    }

    /// <summary>
    /// Checks which scenarios of a campaign end it.
    /// </summary>
    /// <param name="campaignId">The campaign.</param>
    /// <param name="expected">The expected final scenarios.</param>
    private static void AssertLastScenarios(int campaignId, params int[] expected)
    {
        CampaignData campaign = CampaignData.GetCampaignData(campaignId);

        List<int> actual = [];
        for (int i = 0; i < campaign.AllScenarios.Count; ++i)
        {
            ScenarioInfoId info = new(campaignId, i);
            bool isLast = campaign.IsLastScenario(info);

            Assert.Equal(isLast, campaign.AllScenarios[i].NextScenarios.Count == 0);

            if (isLast)
            {
                actual.Add(i);
            }
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RolandBranchOpensBothTheNextScenarioAndTheBetrayal()
    {
        // Roland 第 4 关（Carator Mines）之后，除了正常推进，还多出一条「叛变」线。
        Assert.Equal(
            [new ScenarioInfoId(CampaignId.Roland, 4), new ScenarioInfoId(CampaignId.Roland, 10)],
            CampaignData.GetScenariosAfter(new ScenarioInfoId(CampaignId.Roland, 3)));
    }

    [Fact]
    public void BetrayalScenariosContinueInTheOtherCampaign()
    {
        // 叛变成功就换到对方战役的第 6 关：两个战役共用一张地图，跳转是跨战役的。
        Assert.Equal(
            [new ScenarioInfoId(CampaignId.Archibald, 5)],
            CampaignData.GetScenariosAfter(new ScenarioInfoId(CampaignId.Roland, 10)));

        Assert.Equal(
            [new ScenarioInfoId(CampaignId.Roland, 5)],
            CampaignData.GetScenariosAfter(new ScenarioInfoId(CampaignId.Archibald, 11)));
    }

    [Theory]
    [InlineData(CampaignId.Roland)]
    [InlineData(CampaignId.Archibald)]
    [InlineData(CampaignId.PriceOfLoyalty)]
    [InlineData(CampaignId.Descendants)]
    [InlineData(CampaignId.WizardsIsle)]
    [InlineData(CampaignId.VoyageHome)]
    public void EveryScenarioOffersTwoOrThreeOpeningBonuses(int campaignId)
    {
        for (int i = 0; i < ScenarioCount(campaignId); ++i)
        {
            List<ScenarioBonusData> bonuses = ScenarioBonusData.GetCampaignBonusData(new ScenarioInfoId(campaignId, i));

            Assert.InRange(bonuses.Count, 2, 3);

            foreach (ScenarioBonusData bonus in bonuses)
            {
                Assert.InRange(bonus.Type, ScenarioBonusData.BonusType.Resources, ScenarioBonusData.BonusType.StartingRaceAndArmy);
                Assert.NotEqual(0, bonus.Amount);
            }
        }
    }

    [Fact]
    public void DescendantsScenarioSixDeductsResources()
    {
        // 参考特意注释过：这一关的「奖励」是负数，别改成正值。
        ScenarioInfoId info = new(CampaignId.Descendants, 5);
        List<ScenarioBonusData> bonuses = ScenarioBonusData.GetCampaignBonusData(info);

        Assert.Equal(3, bonuses.Count);
        Assert.All(bonuses, bonus => Assert.Equal(ScenarioBonusData.BonusType.Resources, bonus.Type));
        Assert.All(bonuses, bonus => Assert.True(bonus.Amount < 0));

        Assert.Contains(bonuses, bonus => bonus.SubType == Resource.Gold && bonus.Amount == -1000);
        Assert.Contains(bonuses, bonus => bonus.SubType == Resource.Wood && bonus.Amount == -10);
        Assert.Contains(bonuses, bonus => bonus.SubType == Resource.Mercury && bonus.Amount == -2);
    }

    [Theory]
    [InlineData(CampaignId.Roland, 9, true)]
    [InlineData(CampaignId.Archibald, 5, true)]
    [InlineData(CampaignId.Archibald, 10, true)]
    [InlineData(CampaignId.Roland, 0, false)]
    [InlineData(CampaignId.Archibald, 0, false)]
    [InlineData(CampaignId.PriceOfLoyalty, 7, false)]
    public void AlliedComputerPlayersComeFromTheCampaignData(int campaignId, int scenarioId, bool allied)
    {
        Assert.Equal(allied, CampaignData.AreAllAiPlayersAllied(new ScenarioInfoId(campaignId, scenarioId)));
    }

    [Fact]
    public void CampaignAllianceMergesEveryComputerColor()
    {
        // Archibald 第五关：三色同图，人类是蓝色，两个电脑必须并成一方（campaign_data.cpp:741-745）。
        var mapInfo = new Mp2FileInfo
        {
            FilePath = "test.mp2",
            Name = "Test",
            Description = string.Empty,
            Width = 72,
            Height = 72,
            Difficulty = GameDifficulty.Normal,
            KingdomColors = PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red,
            HumanPlayers = PlayerColor.Blue,
            ComputerPlayers = PlayerColor.Green | PlayerColor.Red,
        };

        Mp2FileInfo merged =
            CampaignData.UpdateScenarioGameplayConditions(new ScenarioInfoId(CampaignId.Archibald, 5), mapInfo);

        PlayerColor computerSide = PlayerColor.Green | PlayerColor.Red;

        Assert.Equal(PlayerColor.Blue, merged.PlayerUnions[PlayerColors.IndexOf(PlayerColor.Blue)]);
        Assert.Equal(computerSide, merged.PlayerUnions[PlayerColors.IndexOf(PlayerColor.Green)]);
        Assert.Equal(computerSide, merged.PlayerUnions[PlayerColors.IndexOf(PlayerColor.Red)]);

        // 原表不动，参考也是在副本上改。
        Assert.Equal(PlayerColor.Green, mapInfo.PlayerUnions[PlayerColors.IndexOf(PlayerColor.Green)]);
    }

    [Fact]
    public void CampaignAllianceLeavesTheOtherScenariosUntouched()
    {
        var mapInfo = new Mp2FileInfo
        {
            FilePath = "test.mp2",
            Name = "Test",
            Description = string.Empty,
            Width = 72,
            Height = 72,
            Difficulty = GameDifficulty.Normal,
            KingdomColors = PlayerColor.Blue | PlayerColor.Red,
            HumanPlayers = PlayerColor.Blue,
            ComputerPlayers = PlayerColor.Red,
        };

        Mp2FileInfo result =
            CampaignData.UpdateScenarioGameplayConditions(new ScenarioInfoId(CampaignId.Archibald, 0), mapInfo);

        Assert.Same(mapInfo, result);
    }

    /// <summary>A map header of the shape the campaign scenarios have: three colours, one human.</summary>
    /// <param name="name">The map name, as read from the file.</param>
    /// <param name="version">The map format version.</param>
    /// <returns>The header.</returns>
    private static Mp2FileInfo ScenarioMap(string name, MapVersion version) => new()
    {
        FilePath = "test.h2c",
        Name = name,
        Description = "C#te",
        Width = 72,
        Height = 72,
        Difficulty = GameDifficulty.Normal,
        Version = version,
        KingdomColors = PlayerColor.Blue | PlayerColor.Green | PlayerColor.Red,
        HumanPlayers = PlayerColor.Blue,
        ComputerPlayers = PlayerColor.Green | PlayerColor.Red,
    };

    [Fact]
    public void APreparedScenarioMapBringsTheAllianceAndTheFrenchAccents()
    {
        // Archibald 第五关：法语资材 + 法语界面 + 老格式地图，两件事都要做（game_campaign.cpp:1606-1615）。
        Mp2FileInfo prepared = CampaignData.PrepareScenarioMap(
            new ScenarioInfoId(CampaignId.Archibald, 5),
            ScenarioMap("C#te", MapVersion.SuccessionWars),
            SupportedLanguage.French,
            SupportedLanguage.French);

        Assert.Equal("Côte", prepared.Name);
        Assert.Equal("Côte", prepared.Description);
        Assert.Equal(
            PlayerColor.Green | PlayerColor.Red,
            prepared.PlayerUnions[PlayerColors.IndexOf(PlayerColor.Green)]);
    }

    [Theory]
    [InlineData(SupportedLanguage.English, SupportedLanguage.French, MapVersion.SuccessionWars)]
    [InlineData(SupportedLanguage.French, SupportedLanguage.English, MapVersion.SuccessionWars)]
    [InlineData(SupportedLanguage.French, SupportedLanguage.French, MapVersion.Resurrection)]
    public void TheFrenchFixNeedsBothLanguagesAndALegacyMap(
        SupportedLanguage interfaceLanguage,
        SupportedLanguage resourceLanguage,
        MapVersion version)
    {
        // 缺一条就不动文本：中文界面开法语资材、法语界面开英文资材、法语界面开新格式地图，参考都不修。
        Mp2FileInfo prepared = CampaignData.PrepareScenarioMap(
            new ScenarioInfoId(CampaignId.Archibald, 5),
            ScenarioMap("C#te", version),
            interfaceLanguage,
            resourceLanguage);

        Assert.Equal("C#te", prepared.Name);

        // 联盟与语言无关，照样补上。
        Assert.Equal(
            PlayerColor.Green | PlayerColor.Red,
            prepared.PlayerUnions[PlayerColors.IndexOf(PlayerColor.Green)]);
    }

    [Fact]
    public void CampaignSaveDataTracksTheCurrentScenario()
    {
        CampaignSaveData save = CampaignSaveData.Get();
        save.Reset();

        try
        {
            Assert.True(save.IsStarting);
            Assert.Equal(ScenarioInfoId.NotStarted, save.CurrentScenarioInfoId);

            save.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0), 2);
            Assert.Equal(CampaignId.Roland, save.CampaignId);
            Assert.Equal(0, save.CurrentScenarioId);
            Assert.Equal(2, save.CurrentScenarioBonusId);

            save.AddCurrentMapToFinished();
            save.AddDaysPassed(30);

            Assert.False(save.IsStarting);
            Assert.Equal([new ScenarioInfoId(CampaignId.Roland, 0)], save.FinishedMaps);
            Assert.Equal(30u, save.DaysPassed);
            Assert.Equal(new ScenarioInfoId(CampaignId.Roland, 0), save.GetLastCompletedScenarioInfoId());

            // 重玩同一关不会在表里写第二条。
            save.AddCurrentMapToFinished();
            Assert.Single(save.FinishedMaps);
        }
        finally
        {
            save.Reset();
        }
    }

    [Fact]
    public void CampaignDifficultyKeepsTheHardestChoice()
    {
        CampaignSaveData save = CampaignSaveData.Get();
        save.Reset();

        try
        {
            save.SetDifficulty(3, resetMinDifficulty: true);
            Assert.Equal(3, save.Difficulty);
            Assert.Equal(3, save.MinDifficulty);

            // 中途改难度时，评分用的最小值取两者中更小的那个（参考用 std::min）。
            save.SetDifficulty(CampaignDifficulty.Decreased, resetMinDifficulty: false);
            Assert.Equal(CampaignDifficulty.Decreased, save.Difficulty);
            Assert.Equal(CampaignDifficulty.Decreased, save.MinDifficulty);
            Assert.Equal(125u, save.GetCampaignDifficultyPercent());

            save.SetDifficulty(CampaignDifficulty.Increased, resetMinDifficulty: false);
            Assert.Equal(CampaignDifficulty.Increased, save.Difficulty);
            Assert.Equal(CampaignDifficulty.Decreased, save.MinDifficulty);

            // 重开战役时最小值跟着重来。
            save.SetDifficulty(CampaignDifficulty.Increased, resetMinDifficulty: true);
            Assert.Equal(CampaignDifficulty.Increased, save.MinDifficulty);
            Assert.Equal(75u, save.GetCampaignDifficultyPercent());
        }
        finally
        {
            save.Reset();
        }
    }

    [Fact]
    public void ObtainedAwardsAreReportedForTheFinishedScenarios()
    {
        CampaignSaveData save = CampaignSaveData.Get();
        save.Reset();

        try
        {
            // Price of Loyalty 的战役级奖励：凑齐三件安德兰套装后合成战斗装束。
            List<CampaignAwardData> extraAwards = CampaignAwardData.GetExtraCampaignAwardData(CampaignId.PriceOfLoyalty);
            CampaignAwardData battleGarb = Assert.Single(extraAwards);

            Assert.Equal(CampaignAwardData.AwardType.GetArtifact, battleGarb.Type);

            save.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.PriceOfLoyalty, 0));
            Assert.Empty(save.GetObtainedCampaignAwards());

            save.AddCampaignAward(battleGarb.Id);
            Assert.Equal([battleGarb.Id], save.GetObtainedCampaignAwards().Select(award => award.Id));

            save.RemoveCampaignAward(battleGarb.Id);
            Assert.Empty(save.GetObtainedCampaignAwards());

            // Voyage Home 一关奖励都没有，参考专门写了注释说不是漏了。
            Assert.Empty(CampaignAwardData.GetCampaignAwardData(new ScenarioInfoId(CampaignId.VoyageHome, 0)));
        }
        finally
        {
            save.Reset();
        }
    }

    [Fact]
    public void AllianceMessagesAreTranslatedFromTheMonster()
    {
        string dwarf = CampaignAwardData.GetAllianceJoiningMessage((int)MonsterType.Dwarf);

        Assert.Equal(dwarf, CampaignAwardData.GetAllianceJoiningMessage((int)MonsterType.BattleDwarf));
        Assert.Contains("dwarves", dwarf, StringComparison.Ordinal);
        Assert.NotEqual(dwarf, CampaignAwardData.GetAllianceJoiningMessage((int)MonsterType.Ogre));

        // 「屠矮者」只对矮人有话说，参考的表里也只列了矮人。
        Assert.Contains("Dwarfbane", CampaignAwardData.GetBaneFleeingMessage((int)MonsterType.Dwarf), StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioInfoIdRoundTripsThroughAStream()
    {
        ScenarioInfoId info = new(CampaignId.Descendants, 3);

        StreamBuf stream = new();
        info.Write(stream);
        stream.Seek(0);

        Assert.Equal(info, ScenarioInfoId.Read(stream));
        Assert.Equal(CampaignId.Descendants, info.CampaignId);
        Assert.Equal(3, info.ScenarioId);
        Assert.NotEqual(ScenarioInfoId.NotStarted, info);
    }

    [Fact]
    public void CampaignMapsAreFoundRegardlessOfCase()
    {
        string directory = Path.Combine(Path.GetTempPath(), "fh2-campaign-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            CampaignData roland = CampaignData.GetCampaignData(CampaignId.Roland);
            ScenarioData first = roland.AllScenarios[0];

            Assert.Null(first.FindMapFile([directory]));
            Assert.Null(first.LoadMap([directory]));
            Assert.False(roland.IsAllCampaignMapsPresent([directory]));

            string filePath = Path.Combine(directory, first.FileName.ToLowerInvariant());
            File.WriteAllBytes(filePath, []);

            Assert.Equal(filePath, first.FindMapFile([directory]));

            // 别的地图还是没有，所以整个战役依旧不完整。
            Assert.False(roland.IsAllCampaignMapsPresent([directory]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CampaignSaveDataRoundTripsThroughAStream()
    {
        CampaignSaveData save = CampaignSaveData.Get();
        save.Reset();

        try
        {
            // 罗兰第三关有一个「矮人联盟」奖励：读回来要能按编号翻回完整奖励，编的编号会被过滤掉。
            ScenarioInfoId scenario = new(CampaignId.Roland, 2);
            CampaignAwardData award = Assert.Single(CampaignAwardData.GetCampaignAwardData(scenario));

            save.SetCurrentScenarioInfo(scenario, 1);
            save.AddCurrentMapToFinished();
            save.AddDaysPassed(42);
            save.AddCampaignAward(award.Id);

            save.SetDifficulty(CampaignDifficulty.Increased, resetMinDifficulty: true);

            TroopArmy army = new(MapRace.Knight);
            Assert.True(army.JoinTroop(MonsterType.Peasant, 88));
            save.SetCarryOverTroops(army);

            // 字段次序即格式（campaign_savedata.cpp:165-170），所以一个来回就够了：次序错位会在读的时候立刻看出来。
            StreamBuf stream = new();
            save.Write(stream);
            stream.Seek(0);

            save.Reset();
            Assert.True(save.IsStarting);

            save.Read(stream);

            Assert.Equal(CampaignId.Roland, save.CampaignId);
            Assert.Equal(2, save.CurrentScenarioId);
            Assert.Equal(1, save.CurrentScenarioBonusId);
            Assert.Equal([scenario], save.FinishedMaps);
            Assert.Equal(42u, save.DaysPassed);
            Assert.Equal([award.Id], save.GetObtainedCampaignAwards().Select(obtained => obtained.Id));
            Assert.Equal(CampaignDifficulty.Increased, save.Difficulty);
            Assert.Equal(CampaignDifficulty.Increased, save.MinDifficulty);

            // 携带部队存的是整个五格（参考写的是 Troops，不是压紧过的表），空格也跟着来回。
            Assert.Equal(TroopArmy.MaximumTroopCount, save.CarryOverTroops.Count);

            Troop carryOver = save.CarryOverTroops[0];
            Assert.Equal(MonsterType.Peasant, carryOver.Monster);
            Assert.Equal(88u, carryOver.Count);
            Assert.True(save.CarryOverTroops[1].IsEmpty);
        }
        finally
        {
            save.Reset();
        }
    }

    [Fact]
    public void TheCurrentScenarioDrawsItsConditionsFromTheCampaignTable()
    {
        CampaignSaveData save = CampaignSaveData.Get();
        save.Reset();

        try
        {
            // Roland 第三关是唯一一关「丢了所有女巫村就输」的，第八关要拿终极王冠才赢。
            save.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 2));
            Assert.Equal(ScenarioVictoryCondition.Standard, save.GetCurrentScenarioVictoryCondition());
            Assert.Equal(ScenarioLossCondition.LoseAllSorceressVillages, save.GetCurrentScenarioLossCondition());

            save.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 7));
            Assert.Equal(ScenarioVictoryCondition.ObtainUltimateCrown, save.GetCurrentScenarioVictoryCondition());
            Assert.Equal(ScenarioLossCondition.Standard, save.GetCurrentScenarioLossCondition());
        }
        finally
        {
            save.Reset();
        }
    }

    [Theory]
    [InlineData(CampaignId.Roland, 1, GameDifficulty.Easy)]
    [InlineData(CampaignId.Archibald, 1, GameDifficulty.Easy)]
    [InlineData(CampaignId.Descendants, 0, GameDifficulty.Easy)]
    [InlineData(CampaignId.Descendants, 5, GameDifficulty.Hard)]
    [InlineData(CampaignId.WizardsIsle, 3, GameDifficulty.Hard)]
    [InlineData(CampaignId.VoyageHome, 0, GameDifficulty.Easy)]
    [InlineData(CampaignId.PriceOfLoyalty, 0, GameDifficulty.Easy)]
    [InlineData(CampaignId.PriceOfLoyalty, 5, GameDifficulty.Hard)]
    [InlineData(CampaignId.PriceOfLoyalty, 6, GameDifficulty.Hard)]
    [InlineData(CampaignId.PriceOfLoyalty, 7, GameDifficulty.Expert)]
    public void TenScenariosHardCodeTheirMapDifficulty(int campaignId, int scenarioId, GameDifficulty expected)
    {
        CampaignSaveData save = CampaignSaveData.Get();
        save.Reset();

        try
        {
            save.SetCurrentScenarioInfo(new ScenarioInfoId(campaignId, scenarioId));

            Assert.Equal(expected, save.GetCurrentScenarioDifficultyLevel());
        }
        finally
        {
            save.Reset();
        }
    }

    [Fact]
    public void ScenariosWithoutAHardCodedDifficultyKeepTheMapOne()
    {
        CampaignSaveData save = CampaignSaveData.Get();
        save.Reset();

        try
        {
            // Roland 第一关没在表里，用地图表头里的难度。
            save.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

            Assert.Null(save.GetCurrentScenarioDifficultyLevel());
        }
        finally
        {
            save.Reset();
        }
    }
}
