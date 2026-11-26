using System.Diagnostics;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// 战役里要播哪些影片，<c>playPreviousScenarioVideo()</c> 与 <c>playCurrentScenarioVideo()</c>
/// (<c>game_campaign.cpp:781-828</c>) 里「挑文件」的那一半。
/// </summary>
/// <remarks>
/// <para>
/// 真正把画面放出来的活留给界面层（<c>GameHost.PlayVideo()</c>）：这里只回答「哪几个文件、按什么顺序」，
/// 于是冒烟可以只核对清单而不必真的解码影片，单元测试也不必准备 <c>.SMK</c>。
/// </para>
/// <para>
/// 两个函数都按参考的「一组一次 <c>Video::ShowVideo()</c>」取数据，端口把组摊平成一条文件名列表：
/// 组内多轨（例如 <c>GOOD01.SMK</c> 循环背景配 <c>GOOD01V.SMK</c> 叠加）本来要同时播，
/// 当前播放器只会逐个播一遍，偏差记在 ADR-257。
/// </para>
/// </remarks>
public static class CampaignVideoPlayback
{
    /// <summary>
    /// The clips that play after the scenario finished most recently, <c>playPreviousScenarioVideo()</c>
    /// (<c>game_campaign.cpp:781-805</c>).
    /// </summary>
    /// <param name="saveData">The save of the campaign.</param>
    /// <returns>The files in playback order, empty when nothing is to be played.</returns>
    /// <remarks>战役刚开始时没有「上一关」，参考在这一步直接返回。</remarks>
    public static IReadOnlyList<string> GetPreviousScenarioVideoFiles(CampaignSaveData saveData)
    {
        ArgumentNullException.ThrowIfNull(saveData);

        if (saveData.IsStarting)
        {
            return [];
        }

        return Flatten(saveData.GetLastCompletedScenarioInfoId(), scenario => scenario.EndScenarioVideoPlayback);
    }

    /// <summary>
    /// The clips that open the scenario the save sits on, <c>playCurrentScenarioVideo()</c>
    /// (<c>game_campaign.cpp:807-828</c>).
    /// </summary>
    /// <param name="saveData">The save of the campaign.</param>
    /// <returns>The files in playback order, empty when nothing is to be played.</returns>
    public static IReadOnlyList<string> GetCurrentScenarioVideoFiles(CampaignSaveData saveData)
    {
        ArgumentNullException.ThrowIfNull(saveData);

        return Flatten(saveData.CurrentScenarioInfoId, scenario => scenario.StartScenarioVideoPlayback);
    }

    /// <summary>Turns the play groups of one scenario into the flat list the player takes.</summary>
    /// <param name="scenarioInfoId">Which scenario of which campaign.</param>
    /// <param name="select">Picks the start or the end groups of that scenario.</param>
    /// <returns>The file names, empty when the scenario does not exist.</returns>
    private static IReadOnlyList<string> Flatten(
        ScenarioInfoId scenarioInfoId,
        Func<ScenarioData, IReadOnlyList<IReadOnlyList<CampaignVideoInfo>>> select)
    {
        CampaignData campaignData = CampaignData.GetCampaignData(scenarioInfoId.CampaignId);
        IReadOnlyList<ScenarioData> scenarios = campaignData.AllScenarios;

        // 参考用断言写死这一点（game_campaign.cpp:792-794、816）；数据表坏掉时不播，比抛出去更接近参考的 release 行为。
        if (scenarioInfoId.ScenarioId < 0 || scenarioInfoId.ScenarioId >= scenarios.Count)
        {
            Debug.Assert(false, $"Campaign {scenarioInfoId.CampaignId} has no scenario {scenarioInfoId.ScenarioId}.");

            return [];
        }

        List<string> files = [];

        foreach (IReadOnlyList<CampaignVideoInfo> group in select(scenarios[scenarioInfoId.ScenarioId]))
        {
            foreach (CampaignVideoInfo video in group)
            {
                // 参考对空文件名与 PLAY_NONE 的两条目直接放弃整组（game_video.cpp:124-134）；端口逐条播，
                // 于是逐条跳过——反正它们本来就不该出声也不该出画。
                if (video.Control == VideoControl.None || string.IsNullOrWhiteSpace(video.FileName))
                {
                    continue;
                }

                files.Add(video.FileName);
            }
        }

        return files;
    }
}
