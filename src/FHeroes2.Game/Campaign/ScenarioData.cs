using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// One scenario of a campaign: which map it loads, what it is called, what it says, which videos wrap it,
/// <c>class ScenarioData</c> (<c>src/fheroes2/campaign/campaign_scenariodata.h:121-175</c>).
/// </summary>
/// <remarks>
/// <para>
/// 奖励不在这里写，构造时按「战役 + 关号」从 <see cref="ScenarioBonusData.GetCampaignBonusData"/> 取：
/// 参考就是这么做的，奖励表只认关号，与场景表分开维护。
/// </para>
/// <para>
/// 文件名一律小写保存（参考在构造函数里 <c>StringLower</c>），因为光盘上的战役图有大写扩展名，
/// 而查找文件时不区分大小写。
/// </para>
/// </remarks>
public sealed class ScenarioData
{
    /// <summary>The name of the directory maps are searched in, <c>MapDirectoryName</c> of the port's catalog.</summary>
    private const string MapDirectoryName = "maps";

    /// <summary>
    /// Creates a scenario and pulls its rewards from the bonus table.
    /// </summary>
    /// <param name="scenarioInfo">Which campaign and which scenario.</param>
    /// <param name="nextScenarios">What opens up after this scenario, <c>_nextScenarios</c>.</param>
    /// <param name="fileName">The map file, <c>_fileName</c>.</param>
    /// <param name="scenarioName">The name shown in the campaign screen, <c>_scenarioName</c>.</param>
    /// <param name="description">The paragraph under the name, <c>_description</c>.</param>
    /// <param name="startScenarioVideoPlayback">What plays before the scenario, <c>_startScenarioVideoPlayback</c>.</param>
    /// <param name="endScenarioVideoPlayback">What plays after it, <c>_endScenarioVideoPlayback</c>.</param>
    /// <param name="victoryCondition">The extra victory condition, <c>_victoryCondition</c>.</param>
    /// <param name="lossCondition">The extra loss condition, <c>_lossCondition</c>.</param>
    public ScenarioData(
        ScenarioInfoId scenarioInfo,
        IReadOnlyList<ScenarioInfoId> nextScenarios,
        string fileName,
        string scenarioName,
        string description,
        IReadOnlyList<IReadOnlyList<CampaignVideoInfo>> startScenarioVideoPlayback,
        IReadOnlyList<IReadOnlyList<CampaignVideoInfo>> endScenarioVideoPlayback,
        ScenarioVictoryCondition victoryCondition = ScenarioVictoryCondition.Standard,
        ScenarioLossCondition lossCondition = ScenarioLossCondition.Standard)
    {
        ArgumentNullException.ThrowIfNull(nextScenarios);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(scenarioName);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(startScenarioVideoPlayback);
        ArgumentNullException.ThrowIfNull(endScenarioVideoPlayback);

        ScenarioInfo = scenarioInfo;
        NextScenarios = nextScenarios;
        Bonuses = ScenarioBonusData.GetCampaignBonusData(scenarioInfo);
        FileName = fileName.ToLowerInvariant();
        ScenarioName = scenarioName;
        Description = description;
        VictoryCondition = victoryCondition;
        LossCondition = lossCondition;
        StartScenarioVideoPlayback = startScenarioVideoPlayback;
        EndScenarioVideoPlayback = endScenarioVideoPlayback;
    }

    /// <summary>Which campaign and which scenario, <c>_scenarioInfo</c>.</summary>
    public ScenarioInfoId ScenarioInfo { get; }

    /// <summary>What opens up after this scenario, <c>_nextScenarios</c>.</summary>
    public IReadOnlyList<ScenarioInfoId> NextScenarios { get; }

    /// <summary>The rewards chosen at the start of the scenario, <c>_bonuses</c>.</summary>
    public IReadOnlyList<ScenarioBonusData> Bonuses { get; }

    /// <summary>The map file in lower case, <c>_fileName</c>.</summary>
    public string FileName { get; }

    /// <summary>The name shown in the campaign screen, <c>_scenarioName</c>.</summary>
    public string ScenarioName { get; }

    /// <summary>The paragraph under the name, <c>_description</c>.</summary>
    public string Description { get; }

    /// <summary>The extra victory condition, <c>_victoryCondition</c>.</summary>
    public ScenarioVictoryCondition VictoryCondition { get; }

    /// <summary>The extra loss condition, <c>_lossCondition</c>.</summary>
    public ScenarioLossCondition LossCondition { get; }

    /// <summary>What plays before the scenario, <c>_startScenarioVideoPlayback</c>.</summary>
    public IReadOnlyList<IReadOnlyList<CampaignVideoInfo>> StartScenarioVideoPlayback { get; }

    /// <summary>What plays after the scenario, <c>_endScenarioVideoPlayback</c>.</summary>
    public IReadOnlyList<IReadOnlyList<CampaignVideoInfo>> EndScenarioVideoPlayback { get; }

    /// <summary>The scenario number inside the campaign, <c>getScenarioID()</c>.</summary>
    public int ScenarioId => ScenarioInfo.ScenarioId;

    /// <summary>The campaign, <c>getCampaignId()</c>.</summary>
    public int CampaignId => ScenarioInfo.CampaignId;

    /// <summary>
    /// The file of the scenario map, or <see langword="null"/> when it is not on disk,
    /// <c>isMapFilePresent()</c> (<c>campaign_scenariodata.cpp:787-791</c>).
    /// </summary>
    /// <param name="rootDirectories">The directories to search, usually the data directory.</param>
    /// <returns>The full path of the map.</returns>
    /// <remarks>
    /// 参考用 <c>Maps::tryGetMatchingFile</c> 在数据目录里按名字找（跳过大小写差异）。端口把搜索目录显式传进来：
    /// 资材层没有全局设置对象，宿主持有数据目录。
    /// </remarks>
    public string? FindMapFile(IEnumerable<string> rootDirectories)
    {
        ArgumentNullException.ThrowIfNull(rootDirectories);

        // 战役地图是 .h2c/.hxc，不在可玩地图的扩展名白名单里，所以按名字找而不是过一遍地图列表。
        return MapCatalog.FindFile(rootDirectories, FileName);
    }

    /// <summary>
    /// Reads the map of the scenario, <c>loadMap()</c> (<c>campaign_scenariodata.cpp:793-806</c>).
    /// </summary>
    /// <param name="rootDirectories">The directories to search, usually the data directory.</param>
    /// <returns>The map header and path, or <see langword="null"/> when the file is missing or unreadable.</returns>
    public Mp2FileInfo? LoadMap(IEnumerable<string> rootDirectories)
    {
        string? filePath = FindMapFile(rootDirectories);
        if (filePath is null)
        {
            return null;
        }

        return Mp2FileInfo.TryRead(filePath, out Mp2FileInfo? info) ? info : null;
    }
}
