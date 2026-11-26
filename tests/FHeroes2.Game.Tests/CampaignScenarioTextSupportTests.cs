using FHeroes2.Game.Campaign;
using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the text support block of the selection screen,
/// <c>outputCampaignScenarioInfoInTextSupportMode()</c> (<c>game_campaign.cpp:1084-1138</c>).
/// </summary>
[Collection(CampaignCollection.Name)]
public sealed class CampaignScenarioTextSupportTests
{
    /// <summary>Short stand-ins for the key names the reference asks the UI layer for.</summary>
    private static readonly CampaignScenarioTextSupportKeys Keys = new(
        FirstBonus: "1",
        SecondBonus: "2",
        ThirdBonus: "3",
        Restart: "R",
        Okay: "Enter",
        ViewIntro: "I",
        SelectDifficulty: "D",
        Cancel: "Esc");

    /// <summary>Every test starts from a clean campaign save, which is a process-wide singleton.</summary>
    public CampaignScenarioTextSupportTests()
    {
        CampaignSaveData.Get().Reset();
        Settings.Get().Players.Clear();
    }

    [Fact]
    public void TheHeaderNamesTheCampaignAndTheScenario()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

        List<string> lines = CampaignScenarioTextSupport.Describe(saveData, allowToRestart: false, Keys);

        Assert.Equal("Scenario Information", lines[0]);
        Assert.Equal("'Roland' campaign, scenario 1: Force of Arms", lines[1]);
        Assert.StartsWith("Description: ", lines[2], StringComparison.Ordinal);

        // :1097 的行尾多了一个 '\n'，所以描述后面必然还有一行空的。
        Assert.Equal(string.Empty, lines[3]);
    }

    /// <summary>
    /// 场景号从 1 开始数，与参考的 <c>scenarioId + 1</c> 一致（<c>:1096</c>）。
    /// </summary>
    [Fact]
    public void TheScenarioNumberIsOneBased()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 1));

        List<string> lines = CampaignScenarioTextSupport.Describe(saveData, allowToRestart: false, Keys);

        Assert.Contains("scenario 2:", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutAwardsTheListSaysNone()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

        List<string> lines = CampaignScenarioTextSupport.Describe(saveData, allowToRestart: false, Keys);

        Assert.Contains("Awards: None", lines);
        Assert.DoesNotContain("Awards:", lines);
    }

    [Fact]
    public void EachObtainedAwardIsOneLineWithItsNameAndDescription()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();

        // 罗兰第三关（编号 2）发「矮人联盟」，编号 0 是它的奖励。
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 2));
        saveData.AddCurrentMapToFinished();
        saveData.AddCampaignAward(0);
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 3));

        List<string> lines = CampaignScenarioTextSupport.Describe(saveData, allowToRestart: false, Keys);

        Assert.Contains("Awards:", lines);

        CampaignAwardData award = Assert.Single(saveData.GetObtainedCampaignAwards());
        Assert.Contains($"- {award.GetName()} : {award.GetDescription()}", lines);
    }

    [Fact]
    public void EveryBonusOfTheScenarioIsOneLineAndItsKeyIsQuotedOnlyWhenItExists()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

        IReadOnlyList<ScenarioBonusData> bonuses =
            CampaignData.GetCampaignData(CampaignId.Roland).AllScenarios[0].Bonuses;

        List<string> lines = CampaignScenarioTextSupport.Describe(saveData, allowToRestart: false, Keys);

        int header = lines.IndexOf("Bonuses:");
        Assert.True(header >= 0);

        for (int i = 0; i < bonuses.Count; ++i)
        {
            Assert.Equal($"- {bonuses[i].GetName()} : {bonuses[i].GetDescription()}", lines[header + 1 + i]);
        }

        // :1116-1124：短横线后是两个空格，提示只为真的存在的奖励出现。
        Assert.Equal(bonuses.Count > 0, lines.Contains("-  Press 1 to select the first bonus."));
        Assert.Equal(bonuses.Count > 1, lines.Contains("-  Press 2 to select the second bonus."));
        Assert.Equal(bonuses.Count > 2, lines.Contains("-  Press 3 to select the third bonus."));
    }

    [Fact]
    public void TheNormalScreenAsksForOkayAndTheRestartOneForRestart()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

        List<string> starting = CampaignScenarioTextSupport.Describe(saveData, allowToRestart: false, Keys);
        Assert.Equal(
            [
                "Press Enter to Start scenario.",
                "Press I to View Intro Video.",
                "Press D to Select / View Campaign Difficulty.",
                "Press Esc to Exit this dialog.",
            ],
            starting.TakeLast(4));

        List<string> restarting = CampaignScenarioTextSupport.Describe(saveData, allowToRestart: true, Keys);
        Assert.Equal(
            [
                "Press R to Restart scenario.",
                "Press I to View Intro Video.",
                "Press D to Select / View Campaign Difficulty.",
                "Press Esc to Exit this dialog.",
            ],
            restarting.TakeLast(4));
    }
}
