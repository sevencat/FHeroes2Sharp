using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// What finishing a scenario does to the campaign, the accounting half of <c>CompleteCampaignScenario()</c>
/// (<c>game_campaign.cpp:1160-1282</c>) plus the scenario list of the selection screen.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>CompleteCampaignScenario</c> 里混着三件事：记账（完成记录、天数、奖励物、携带部队）、过场（上一关的影片）、
/// 结局（片尾与高分榜）。端口只把**记账**搬进来，另外两件要影片与高分榜，留在界面那一块。
/// </para>
/// <para>
/// 记账里唯一有副作用、又不属于「改存档」的一步是「非最后一关就存一次档」（参考的
/// <c>Game::SaveCompletedCampaignScenario()</c>）：端口没有写文件的责任，改成在返回值里告诉调用方「该存盘了」。
/// </para>
/// </remarks>
public static class CampaignProgress
{
    /// <summary>
    /// Records that the running scenario is over, <c>CompleteCampaignScenario()</c> (<c>game_campaign.cpp:1160-1282</c>).
    /// </summary>
    /// <param name="world">The world of the finished scenario, for its day count and the human kingdom's heroes.</param>
    /// <param name="isLoadingSaveFile">
    /// True when the campaign is being resumed from a save file of the next scenario: the finished one was already
    /// recorded back when the save was written, so nothing is recorded again.
    /// </param>
    /// <returns>Where the campaign stands now, see <see cref="CampaignScenarioCompletion"/>.</returns>
    /// <remarks>
    /// <para>
    /// 顺序照参考：先记完成记录与天数，再逐条发这一关的奖励物，最后决定「战役结束」还是「下一关是哪一关」。
    /// 这三段也是分开的公开方法，因为参考在第二段之前插了一次写档（<c>:1169-1171</c>），而写档必须发生在
    /// “当前关已经记进完成列表、但还没挪到下一关”的那一刻（<c>game_io.cpp:261-265</c> 靠这个判据认出“这一关打完了”）。
    /// 需要写档的调用方按 <see cref="RecordFinishedScenario"/> → 写档 → <see cref="CollectScenarioAwards"/> →
    /// <see cref="AdvanceScenario"/> 的顺序调；不需要写档的（读档补奖励）直接从第三段开始。
    /// </para>
    /// <para>
    /// 本方法是不写档的便捷入口：把三段按顺序串起来，用于只关心最终状态的场合。
    /// </para>
    /// </remarks>
    public static CampaignScenarioCompletion CompleteScenario(World world, bool isLoadingSaveFile)
    {
        ArgumentNullException.ThrowIfNull(world);

        bool shouldSave = !isLoadingSaveFile && RecordFinishedScenario(world);

        CollectScenarioAwards(world);

        return AdvanceScenario(shouldSave);
    }

    /// <summary>
    /// Records the finished scenario and its day count, the first block of <c>CompleteCampaignScenario()</c>
    /// (<c>game_campaign.cpp:1165-1172</c>).
    /// </summary>
    /// <param name="world">The world of the finished scenario.</param>
    /// <returns>True when an inter-scenario save has to be written, i.e. a scenario follows this one.</returns>
    /// <remarks>
    /// <para>
    /// 调用之后 <see cref="CampaignSaveData.CurrentScenarioInfoId"/> 仍指着刚打完的这一关，
    /// <see cref="CampaignSaveData.GetLastCompletedScenarioInfoId"/> 已经指向它——这正是「下一关」存档要写下的状态
    /// （<c>game_io.cpp:261-265</c>）。写档要在 <see cref="AdvanceScenario"/> 之前完成。
    /// </para>
    /// <para>
    /// 非最后一关才有「另一关等着读」的存档要写；最后一关走结局，没有下一关可存。
    /// </para>
    /// </remarks>
    public static bool RecordFinishedScenario(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        CampaignSaveData saveData = CampaignSaveData.Get();
        CampaignData campaignData = CampaignData.GetCampaignData(saveData.CampaignId);

        saveData.AddCurrentMapToFinished();
        saveData.AddDaysPassed(world.DayCount);

        return !campaignData.IsLastScenario(saveData.GetLastCompletedScenarioInfoId());
    }

    /// <summary>
    /// Grants the awards of the finished scenario, the middle block of <c>CompleteCampaignScenario()</c>
    /// (<c>game_campaign.cpp:1174-1238</c>).
    /// </summary>
    /// <param name="world">The world of the finished scenario, for the human kingdom's heroes.</param>
    /// <remarks>
    /// <para>
    /// 奖励物里有两条要跳过或特殊处理：消灭敌方英雄那条必须在战斗里发（<c>continue</c>），携带部队那条要先把人类王国里
    /// 最老的英雄的部队抄进存档。
    /// </para>
    /// <para>
    /// 「最老的英雄」= 参考 <c>humanKingdomHeroes.front()</c>，也就是雇佣时间最早、且还活着的那个。端口
    /// <see cref="GameKingdom.Heroes"/> 与参考的 <c>VecHeroes</c> 同为追加式列表，所以取下标 0 等价。
    /// </para>
    /// <para>
    /// 拿到宝物奖励后要试着组套：参考把「所有已拿到的宝物奖励」摊进一个临时背包，从存档里摘掉，组套，再把背包里剩下的
    /// 宝物按编号找回对应的奖励编号加回去。所以「三个泰坦之雷的零件」会变成一个「泰坦之雷」的奖励物。
    /// </para>
    /// <para>
    /// 这一段在写档之后跑（参考如此），所以「下一关」存档里没有本关奖励；读档补奖励时再跑一遍，见
    /// <c>GameHost.OpenLoadFile</c>。
    /// </para>
    /// </remarks>
    public static void CollectScenarioAwards(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        CampaignSaveData saveData = CampaignSaveData.Get();

        foreach (CampaignAwardData award in CampaignAwardData.GetCampaignAwardData(saveData.GetLastCompletedScenarioInfoId()))
        {
            if (award.Type == CampaignAwardData.AwardType.DefeatEnemyHero)
            {
                // 这条奖励只在战斗里打倒指定英雄时才发（CampaignSaveData.SetEnemyDefeatedAward）。
                continue;
            }

            if (award.Type == CampaignAwardData.AwardType.CarryOverForces)
            {
                IReadOnlyList<PlayerColor> humanColors = PlayerColors.ToList(Players.HumanColors());

                // 参考 assert( Count( humanColor ) == 1 )：战役只有一个人玩。
                if (humanColors.Count == 1)
                {
                    IReadOnlyList<Hero> heroes = world.GetKingdom(humanColors[0]).Heroes;

                    if (heroes.Count > 0)
                    {
                        saveData.SetCarryOverTroops(heroes[0].Army);
                    }
                }
            }

            saveData.AddCampaignAward(award.Id);

            if (award.Type == CampaignAwardData.AwardType.GetArtifact)
            {
                AssembleArtifactAwards(saveData);
            }
        }
    }

    /// <summary>
    /// Decides where the campaign goes next and moves the save there, the last block of
    /// <c>CompleteCampaignScenario()</c> (<c>game_campaign.cpp:1242-1281</c>).
    /// </summary>
    /// <param name="shouldSave">Whether the caller has written the inter-scenario save, carried into the result.</param>
    /// <returns>Where the campaign stands now, see <see cref="CampaignScenarioCompletion"/>.</returns>
    /// <remarks>
    /// 最后一关返回「已结束」，否则把当前关挪到参考挑出的下一关（<c>GetScenariosAfter().front()</c>）。
    /// 片尾、战役高分榜与上一关的过场影片属于界面那一块，不在这里。
    /// </remarks>
    public static CampaignScenarioCompletion AdvanceScenario(bool shouldSave)
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        CampaignData campaignData = CampaignData.GetCampaignData(saveData.CampaignId);
        ScenarioInfoId lastCompleted = saveData.GetLastCompletedScenarioInfoId();

        if (campaignData.IsLastScenario(lastCompleted))
        {
            return new CampaignScenarioCompletion(true, null, shouldSave);
        }

        ScenarioInfoId nextScenario = CampaignData.GetScenariosAfter(lastCompleted)[0];
        saveData.SetCurrentScenarioInfo(nextScenario);

        return new CampaignScenarioCompletion(false, nextScenario, shouldSave);
    }

    /// <summary>
    /// The scenarios the selection screen lets the player click, <c>SelectCampaignScenario()</c>
    /// (<c>game_campaign.cpp:1471-1478</c>).
    /// </summary>
    /// <param name="saveData">The campaign save.</param>
    /// <param name="allowToRestart">True when the same scenario may be restarted, so only it is selectable.</param>
    /// <returns>The selectable scenarios, in campaign order.</returns>
    /// <remarks>
    /// 重开关卡时只有「正在打的这一关」可点；否则战役刚开头给的是所有起始关（原版罗兰与阿基巴德各有两个起始关），
    /// 打着打着给的是「上一关之后开放的那几关」。
    /// </remarks>
    public static List<ScenarioInfoId> GetSelectableScenarios(CampaignSaveData saveData, bool allowToRestart)
    {
        ArgumentNullException.ThrowIfNull(saveData);

        if (allowToRestart)
        {
            return [saveData.CurrentScenarioInfoId];
        }

        CampaignData campaignData = CampaignData.GetCampaignData(saveData.CampaignId);

        return saveData.IsStarting
            ? campaignData.GetStartingScenarios()
            : [.. CampaignData.GetScenariosAfter(saveData.GetLastCompletedScenarioInfoId())];
    }

    /// <summary>
    /// Swaps the obtained artifact awards for their assembled versions, the inner block of
    /// <c>CompleteCampaignScenario()</c> (<c>game_campaign.cpp:1204-1237</c>).
    /// </summary>
    /// <param name="saveData">The campaign save whose artifact awards are rewritten.</param>
    /// <remarks>
    /// 参考用一张「宝物编号 → 奖励编号」的表把奖励找回来：临时背包里最后剩下的每个宝物都要能对上一个奖励，组套之后的
    /// 成品（比如集齐零件后的泰坦之雷）也在表里——它的奖励编号来自
    /// <see cref="CampaignAwardData.GetExtraCampaignAwardData"/>，也就是「战役表里没写、但可以靠组套拿到」的那些奖励物。
    /// </remarks>
    private static void AssembleArtifactAwards(CampaignSaveData saveData)
    {
        var artifactAwardIds = new Dictionary<ArtifactType, int>();
        var bag = new ArtifactBag();

        // 注意：这里遍历的是「已经拿到的全部宝物奖励」，不只是这一关刚发的那个。
        foreach (CampaignAwardData award in saveData.GetObtainedCampaignAwards())
        {
            if (award.Type != CampaignAwardData.AwardType.GetArtifact)
            {
                continue;
            }

            artifactAwardIds.TryAdd((ArtifactType)award.SubType, award.Id);
            bag.Push((ArtifactType)award.SubType);
            saveData.RemoveCampaignAward(award.Id);
        }

        foreach (CampaignAwardData award in CampaignAwardData.GetExtraCampaignAwardData(saveData.CampaignId))
        {
            if (award.Type != CampaignAwardData.AwardType.GetArtifact)
            {
                continue;
            }

            artifactAwardIds.TryAdd((ArtifactType)award.SubType, award.Id);
        }

        bag.AssembleArtifactSets();

        foreach (ArtifactType artifact in bag.Slots)
        {
            if (artifact == ArtifactType.Unknown)
            {
                continue;
            }

            if (artifactAwardIds.TryGetValue(artifact, out int awardId))
            {
                saveData.AddCampaignAward(awardId);
            }
        }
    }
}

/// <summary>
/// Where the campaign stands after a scenario ended, <see cref="CampaignProgress.CompleteScenario"/>.
/// </summary>
/// <param name="IsCampaignFinished">True when the scenario was the last one of its campaign.</param>
/// <param name="NextScenario">The scenario the campaign moves on to, null when the campaign is over.</param>
/// <param name="ShouldSave">True when the caller has to write the inter-scenario save file.</param>
public readonly record struct CampaignScenarioCompletion(bool IsCampaignFinished, ScenarioInfoId? NextScenario, bool ShouldSave);
