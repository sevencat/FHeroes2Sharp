using FHeroes2.Core.Diagnostics;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// The key names a text support block of the selection screen quotes,
/// the <c>Game::getHotKeyNameByEventId()</c> calls of <c>outputCampaignScenarioInfoInTextSupportMode()</c>
/// (<c>game_campaign.cpp:1117-1137</c>).
/// </summary>
/// <remarks>
/// 参考这一屏直接问 UI 层要名字。端口的 <c>GameHotKeys</c> 在 UI 层，而 Game 层不能被 UI 引用，所以名字由调用方
/// 打包传进来，组合文本这件事就留在了 Game 层、可以单独测。
/// </remarks>
/// <param name="FirstBonus">The key of <c>CAMPAIGN_SELECT_FIRST_BONUS</c>.</param>
/// <param name="SecondBonus">The key of <c>CAMPAIGN_SELECT_SECOND_BONUS</c>.</param>
/// <param name="ThirdBonus">The key of <c>CAMPAIGN_SELECT_THIRD_BONUS</c>.</param>
/// <param name="Restart">The key of <c>CAMPAIGN_RESTART_SCENARIO</c>.</param>
/// <param name="Okay">The key of <c>DEFAULT_OKAY</c>.</param>
/// <param name="ViewIntro">The key of <c>CAMPAIGN_VIEW_INTRO</c>.</param>
/// <param name="SelectDifficulty">The key of <c>CAMPAIGN_SELECT_DIFFICULTY</c>.</param>
/// <param name="Cancel">The key of <c>DEFAULT_CANCEL</c>.</param>
public sealed record CampaignScenarioTextSupportKeys(
    string FirstBonus,
    string SecondBonus,
    string ThirdBonus,
    string Restart,
    string Okay,
    string ViewIntro,
    string SelectDifficulty,
    string Cancel);

/// <summary>
/// The text support output of the campaign selection screen,
/// <c>outputCampaignScenarioInfoInTextSupportMode()</c> (<c>game_campaign.cpp:1084-1138</c>).
/// </summary>
/// <remarks>
/// 只在 text support 模式打开时才有输出（参考的 <c>START_TEXT_SUPPORT_MODE</c>，<c>logging.h:197-201</c>），
/// 每行都是外部工具读的裸文本，不加日志级别前缀。
/// </remarks>
public static class CampaignScenarioTextSupport
{
    /// <summary>
    /// Writes the block of the selection screen, the <c>START_TEXT_SUPPORT_MODE</c> body of
    /// <c>outputCampaignScenarioInfoInTextSupportMode()</c>.
    /// </summary>
    /// <param name="saveData">The campaign being selected.</param>
    /// <param name="allowToRestart">Whether the screen restarts the current scenario instead of starting it.</param>
    /// <param name="keys">The key names to quote.</param>
    public static void Write(CampaignSaveData saveData, bool allowToRestart, CampaignScenarioTextSupportKeys keys)
    {
        TextSupport.WriteBlock(Describe(saveData, allowToRestart, keys));
    }

    /// <summary>
    /// Composes the lines of the block without the separators, so it can be checked without the console.
    /// </summary>
    /// <param name="saveData">The campaign being selected.</param>
    /// <param name="allowToRestart">Whether the screen restarts the current scenario instead of starting it.</param>
    /// <param name="keys">The key names to quote.</param>
    /// <returns>The lines of the block, in the order the reference prints them.</returns>
    public static List<string> Describe(
        CampaignSaveData saveData, bool allowToRestart, CampaignScenarioTextSupportKeys keys)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(keys);

        int campaignId = saveData.CampaignId;
        int scenarioId = saveData.CurrentScenarioId;
        ScenarioData scenario = CampaignData.GetCampaignData(campaignId).AllScenarios[scenarioId];

        List<string> lines =
        [
            "Scenario Information",
            $"'{CampaignAwardData.GetCampaignName(campaignId)}' campaign, scenario {scenarioId + 1}: {scenario.ScenarioName}",

            // :1097 的行尾带了一个 '\n'，所以描述之后还有一个空行。
            $"Description: {scenario.Description}",
            string.Empty,
        ];

        List<CampaignAwardData> awards = saveData.GetObtainedCampaignAwards();
        if (awards.Count == 0)
        {
            lines.Add("Awards: None");
        }
        else
        {
            lines.Add("Awards:");
            foreach (CampaignAwardData award in awards)
            {
                lines.Add($"- {award.GetName()} : {award.GetDescription()}");
            }
        }

        lines.Add("Bonuses:");
        IReadOnlyList<ScenarioBonusData> bonuses = scenario.Bonuses;
        foreach (ScenarioBonusData bonus in bonuses)
        {
            lines.Add($"- {bonus.GetName()} : {bonus.GetDescription()}");
        }

        // :1116-1124：短横线后是两个空格，与上面奖励行的单空格不同，照抄。
        if (bonuses.Count > 0)
        {
            lines.Add($"-  Press {keys.FirstBonus} to select the first bonus.");
        }

        if (bonuses.Count > 1)
        {
            lines.Add($"-  Press {keys.SecondBonus} to select the second bonus.");
        }

        if (bonuses.Count > 2)
        {
            lines.Add($"-  Press {keys.ThirdBonus} to select the third bonus.");
        }

        lines.Add(allowToRestart
            ? $"Press {keys.Restart} to Restart scenario."
            : $"Press {keys.Okay} to Start scenario.");
        lines.Add($"Press {keys.ViewIntro} to View Intro Video.");
        lines.Add($"Press {keys.SelectDifficulty} to Select / View Campaign Difficulty.");
        lines.Add($"Press {keys.Cancel} to Exit this dialog.");

        return lines;
    }
}
