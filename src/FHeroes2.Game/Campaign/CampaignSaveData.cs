using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Army;
using FHeroes2.Game.System;

using TroopArmy = FHeroes2.Game.Army.Army;

// 同一个文件里的属性也叫 CampaignId，写全名会打架，这里给常量表一个别名。
using CampaignIds = FHeroes2.Game.Campaign.CampaignId;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// The difficulty of a campaign, <c>enum CampaignDifficulty : int32_t</c>
/// (<c>src/fheroes2/campaign/campaign_savedata.h:41-46</c>).
/// </summary>
/// <remarks>
/// 只有三档，且与地图难度不是一套东西：战役难度只影响最终评分，不影响地图里的怪物强度。
/// </remarks>
public static class CampaignDifficulty
{
    /// <summary>Easier than the original, worth 125 percent, <c>Decreased</c>.</summary>
    public const int Decreased = -1;

    /// <summary>The original difficulty, worth 100 percent, <c>Default</c>.</summary>
    public const int Default = 0;

    /// <summary>Harder than the original, worth 75 percent, <c>Increased</c>.</summary>
    public const int Increased = 1;
}

/// <summary>
/// Everything the game remembers between two scenarios of a campaign,
/// <c>class CampaignSaveData</c> (<c>campaign_savedata.h:48-149</c> and <c>campaign_savedata.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 战役是「一个存档管全篇」：进哪一关、每个已经打完的关用了多少天、选的是哪个奖励（三选一那个）、拿到过哪些奖励、
/// 上一关带过来的部队、敌人让分多少，全在这一个对象里。它跟着存档一起写盘，所以序列化字段的次序是格式的一部分。
/// </para>
/// <para>
/// 参考把它做成进程级单例（<c>Get()</c>）——同一时刻只可能玩一个战役，端口沿用，因为它和
/// <see cref="Settings.Get"/> 一样是「一局游戏一份」的状态，塞进参数会把每个入口都染上战役的味道。
/// </para>
/// </remarks>
public sealed class CampaignSaveData
{
    private static readonly Lazy<CampaignSaveData> Instance = new(() => new CampaignSaveData());

    private readonly List<ScenarioInfoId> _finishedMaps = [];

    private readonly List<uint> _daysPassed = [];

    private readonly List<int> _bonusesForFinishedMaps = [];

    private readonly List<int> _obtainedCampaignAwards = [];

    private readonly List<Troop> _carryOverTroops = [];

    private ScenarioInfoId _currentScenarioInfoId = ScenarioInfoId.NotStarted;

    private int _currentScenarioBonusId = -1;

    private int _difficulty = CampaignDifficulty.Default;

    private int _minDifficulty = CampaignDifficulty.Default;

    private CampaignSaveData()
    {
    }

    /// <summary>The scenarios already beaten, <c>getFinishedMaps()</c>.</summary>
    public IReadOnlyList<ScenarioInfoId> FinishedMaps => _finishedMaps;

    /// <summary>The campaign being played, <c>getCampaignID()</c>.</summary>
    public int CampaignId => _currentScenarioInfoId.CampaignId;

    /// <summary>The scenario being played, <c>getCurrentScenarioID()</c>.</summary>
    public int CurrentScenarioId => _currentScenarioInfoId.ScenarioId;

    /// <summary>Both identifiers at once, <c>getCurrentScenarioInfoId()</c>.</summary>
    public ScenarioInfoId CurrentScenarioInfoId => _currentScenarioInfoId;

    /// <summary>The bonus chosen for the current scenario, <c>getCurrentScenarioBonusId()</c>.</summary>
    public int CurrentScenarioBonusId => _currentScenarioBonusId;

    /// <summary>True before the first scenario of the campaign was finished, <c>isStarting()</c>.</summary>
    public bool IsStarting => _finishedMaps.Count == 0;

    /// <summary>How many days the whole campaign has taken so far, <c>getDaysPassed()</c>.</summary>
    public uint DaysPassed
    {
        get
        {
            uint total = 0;
            foreach (uint days in _daysPassed)
            {
                total += days;
            }

            return total;
        }
    }

    /// <summary>The difficulty chosen for the campaign, <c>getDifficulty()</c>.</summary>
    public int Difficulty => _difficulty;

    /// <summary>The hardest difficulty the campaign was ever set to, <c>getMinDifficulty()</c>.</summary>
    /// <remarks>降难度不算数，所以存的是历史最小值。</remarks>
    public int MinDifficulty => _minDifficulty;

    /// <summary>The troops carried into the current scenario, <c>getCarryOverTroops()</c>.</summary>
    public IReadOnlyList<Troop> CarryOverTroops => _carryOverTroops;

    /// <summary>The one campaign state of this session, <c>CampaignSaveData::Get()</c>.</summary>
    /// <returns>The instance.</returns>
    public static CampaignSaveData Get() => Instance.Value;

    /// <summary>
    /// Records the difficulty, <c>setDifficulty()</c> (<c>campaign_savedata.h:99-103</c>).
    /// </summary>
    /// <param name="difficulty">The new difficulty.</param>
    /// <param name="resetMinDifficulty">Whether this is a fresh campaign, where the minimum starts over.</param>
    /// <remarks>
    /// 战役中途可以调难度，但评分按「最狠的那次」算，所以只有重开时才让最小值跟着走。
    /// </remarks>
    public void SetDifficulty(int difficulty, bool resetMinDifficulty)
    {
        _difficulty = difficulty;
        _minDifficulty = resetMinDifficulty ? difficulty : Math.Min(difficulty, _minDifficulty);
    }

    /// <summary>
    /// The difficulty as a percentage for the final rating, <c>getCampaignDifficultyPercent()</c>
    /// (<c>campaign_savedata.cpp:126-141</c>).
    /// </summary>
    /// <returns>125, 100 or 75.</returns>
    public uint GetCampaignDifficultyPercent() => _minDifficulty switch
    {
        CampaignDifficulty.Decreased => 125,
        CampaignDifficulty.Default => 100,
        CampaignDifficulty.Increased => 75,
        _ => UnknownDifficultyPercent(),
    };

    /// <summary>
    /// The awards the player currently owns, <c>getObtainedCampaignAwards()</c>
    /// (<c>campaign_savedata.cpp:143-163</c>).
    /// </summary>
    /// <returns>The awards, in the order the scenarios were finished.</returns>
    /// <remarks>
    /// 存的是奖励编号，这里按「打完的关」把编号翻译成完整奖励；额外奖励（全战役共用的那几条）排在最后。
    /// </remarks>
    public List<CampaignAwardData> GetObtainedCampaignAwards()
    {
        List<CampaignAwardData> obtainedAwards = [];

        foreach (ScenarioInfoId finished in _finishedMaps)
        {
            foreach (CampaignAwardData award in CampaignAwardData.GetCampaignAwardData(finished))
            {
                if (_obtainedCampaignAwards.Contains(award.Id))
                {
                    obtainedAwards.Add(award);
                }
            }
        }

        foreach (CampaignAwardData award in CampaignAwardData.GetExtraCampaignAwardData(_currentScenarioInfoId.CampaignId))
        {
            if (_obtainedCampaignAwards.Contains(award.Id))
            {
                obtainedAwards.Add(award);
            }
        }

        return obtainedAwards;
    }

    /// <summary>
    /// Chooses the scenario to play next, <c>setCurrentScenarioInfo()</c> (<c>campaign_savedata.cpp:71-77</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario.</param>
    /// <param name="bonusId">The bonus picked for it, -1 when none was picked.</param>
    public void SetCurrentScenarioInfo(ScenarioInfoId scenarioInfoId, int bonusId = -1)
    {
        Debug.Assert(scenarioInfoId.CampaignId >= 0 && scenarioInfoId.ScenarioId >= 0);

        _currentScenarioInfoId = scenarioInfoId;
        _currentScenarioBonusId = bonusId;
    }

    /// <summary>
    /// Adds the current scenario to the finished list, <c>addCurrentMapToFinished()</c>
    /// (<c>campaign_savedata.cpp:79-90</c>).
    /// </summary>
    /// <remarks>重玩同一关不会写两条：编号已经在表里就直接返回。</remarks>
    public void AddCurrentMapToFinished()
    {
        if (_finishedMaps.Contains(_currentScenarioInfoId))
        {
            return;
        }

        _finishedMaps.Add(_currentScenarioInfoId);
        _bonusesForFinishedMaps.Add(_currentScenarioBonusId);

        Debug.Assert(_finishedMaps.Count == _bonusesForFinishedMaps.Count);
    }

    /// <summary>Records how long the last finished scenario took, <c>addDaysPassed()</c>.</summary>
    /// <param name="days">The day count.</param>
    public void AddDaysPassed(uint days)
    {
        _daysPassed.Add(days);

        Debug.Assert(_daysPassed.Count == _finishedMaps.Count);
    }

    /// <summary>Gives the player an award, <c>addCampaignAward()</c>.</summary>
    /// <param name="awardId">The award identifier.</param>
    public void AddCampaignAward(int awardId) => _obtainedCampaignAwards.Add(awardId);

    /// <summary>Takes an award away, <c>removeCampaignAward()</c> (<c>campaign_savedata.cpp:49-52</c>).</summary>
    /// <param name="awardId">The award identifier.</param>
    /// <remarks>Price of Loyalty 会在过关时收走特定奖励，所以要能删。</remarks>
    public void RemoveCampaignAward(int awardId) => _obtainedCampaignAwards.RemoveAll(id => id == awardId);

    /// <summary>Takes every award away, <c>removeAllAwards()</c>.</summary>
    public void RemoveAllAwards() => _obtainedCampaignAwards.Clear();

    /// <summary>
    /// Keeps the troops of the finished scenario for the next one, <c>setCarryOverTroops()</c>
    /// (<c>campaign_savedata.cpp:111-118</c>).
    /// </summary>
    /// <param name="troops">The army that survived.</param>
    public void SetCarryOverTroops(TroopArmy troops)
    {
        ArgumentNullException.ThrowIfNull(troops);

        _carryOverTroops.Clear();
        _carryOverTroops.AddRange(troops.Slots);
    }

    /// <summary>Forgets the whole campaign, <c>reset()</c> (<c>campaign_savedata.cpp:98-109</c>).</summary>
    public void Reset()
    {
        _finishedMaps.Clear();
        _daysPassed.Clear();
        _bonusesForFinishedMaps.Clear();
        _obtainedCampaignAwards.Clear();
        _carryOverTroops.Clear();
        _currentScenarioInfoId = ScenarioInfoId.NotStarted;
        _currentScenarioBonusId = -1;
        _difficulty = CampaignDifficulty.Default;
        _minDifficulty = CampaignDifficulty.Default;
    }

    /// <summary>
    /// Records that the enemy hero of the scenario was defeated, <c>setEnemyDefeatedAward()</c>
    /// (<c>campaign_savedata.cpp:54-69</c>).
    /// </summary>
    /// <param name="heroId">The hero that was beaten.</param>
    /// <remarks>
    /// 只看当前关的第一条「击败敌方英雄」奖励：那一关只可能有一条，且奖励的副类型就是英雄编号。
    /// </remarks>
    public void SetEnemyDefeatedAward(int heroId)
    {
        foreach (CampaignAwardData award in CampaignAwardData.GetCampaignAwardData(_currentScenarioInfoId))
        {
            if (award.Type == CampaignAwardData.AwardType.DefeatEnemyHero)
            {
                if (award.SubType == heroId)
                {
                    AddCampaignAward(award.Id);
                }

                break;
            }
        }
    }

    /// <summary>
    /// The scenario finished most recently, <c>getLastCompletedScenarioInfoID()</c>
    /// (<c>campaign_savedata.cpp:120-124</c>).
    /// </summary>
    /// <returns>The last entry of the finished list.</returns>
    /// <remarks>只能在 <see cref="IsStarting"/> 为假时调用，参考用断言写明这一点。</remarks>
    public ScenarioInfoId GetLastCompletedScenarioInfoId()
    {
        Debug.Assert(_finishedMaps.Count > 0);

        return _finishedMaps[^1];
    }

    /// <summary>
    /// Writes the campaign state, <c>operator&lt;&lt;( OStreamBase &amp;, const CampaignSaveData &amp; )</c>
    /// (<c>campaign_savedata.cpp:165-170</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <remarks>字段次序即格式：CampaignId、ScenarioId、奖励编号、已过地图、地图奖励、天数、已得奖励、携带部队、难度、最小难度。</remarks>
    public void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(_currentScenarioInfoId.CampaignId);
        stream.Write(_currentScenarioInfoId.ScenarioId);
        stream.Write(_currentScenarioBonusId);
        stream.WriteList(_finishedMaps, static (output, info) => info.Write(output));
        stream.WriteList(_bonusesForFinishedMaps, static (output, bonus) => output.Write(bonus));
        stream.WriteList(_daysPassed, static (output, days) => output.Write(days));
        stream.WriteList(_obtainedCampaignAwards, static (output, award) => output.Write(award));
        stream.WriteList(_carryOverTroops, static (output, troop) => troop.WriteTo(output));
        stream.Write(_difficulty);
        stream.Write(_minDifficulty);
    }

    /// <summary>
    /// Reads the campaign state, <c>operator&gt;&gt;( IStreamBase &amp;, CampaignSaveData &amp; )</c>
    /// (<c>campaign_savedata.cpp:172-210</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <remarks>
    /// 两处版本分支都是「把旧的单值摊成新表」：1.1.11.1 之前整个战役只存一个总天数，读回来时均摊成一关一天、余数加在最后
    /// 一关；1.1.08pre 之前没有「历史最小难度」，就用当前难度顶上。表长按已过关数补齐，防住手改过的存档。
    /// </remarks>
    public void Read(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _finishedMaps.Clear();
        _bonusesForFinishedMaps.Clear();
        _daysPassed.Clear();
        _obtainedCampaignAwards.Clear();
        _carryOverTroops.Clear();

        _currentScenarioInfoId = new ScenarioInfoId(stream.ReadInt32(), stream.ReadInt32());
        _currentScenarioBonusId = stream.ReadInt32();

        _finishedMaps.AddRange(stream.ReadList(ScenarioInfoId.Read));
        _bonusesForFinishedMaps.AddRange(stream.ReadList(static input => input.ReadInt32()));

        while (_bonusesForFinishedMaps.Count < _finishedMaps.Count)
        {
            _bonusesForFinishedMaps.Add(-1);
        }

        if (GameSave.VersionOfCurrentSaveFile < SaveFileFormat.Release1_1_11_1)
        {
            uint daysPassed = stream.ReadUInt32();

            for (int i = 0; i < _finishedMaps.Count; ++i)
            {
                _daysPassed.Add(1);
            }

            if (_daysPassed.Count > 0 && daysPassed > (uint)_daysPassed.Count)
            {
                _daysPassed[^1] += daysPassed - (uint)_daysPassed.Count;
            }
        }
        else
        {
            _daysPassed.AddRange(stream.ReadList(static input => input.ReadUInt32()));

            while (_daysPassed.Count < _finishedMaps.Count)
            {
                _daysPassed.Add(1);
            }
        }

        _obtainedCampaignAwards.AddRange(stream.ReadList(static input => input.ReadInt32()));
        _carryOverTroops.AddRange(stream.ReadList(static input =>
        {
            Troop troop = new();
            troop.ReadFrom(input);

            return troop;
        }));
        _difficulty = stream.ReadInt32();

        _minDifficulty = GameSave.VersionOfCurrentSaveFile < SaveFileFormat.PreRelease1_1_08
            ? _difficulty
            : stream.ReadInt32();
    }

    /// <summary>
    /// The victory condition of the scenario being played, <c>getCurrentScenarioVictoryCondition()</c>
    /// (<c>campaign_savedata.cpp:212-227</c>).
    /// </summary>
    /// <returns>The scenario's condition, or <see cref="ScenarioVictoryCondition.Standard"/> outside the table.</returns>
    /// <remarks>
    /// 只能在战役里调用。编号是从存档里读回来的，手改过的存档可能越界，参考那里断言之后退回 STANDARD，端口照做。
    /// </remarks>
    public ScenarioVictoryCondition GetCurrentScenarioVictoryCondition()
    {
        IReadOnlyList<ScenarioData> scenarios = CampaignData.GetCampaignData(_currentScenarioInfoId.CampaignId).AllScenarios;
        int scenarioId = _currentScenarioInfoId.ScenarioId;

        Debug.Assert(scenarioId >= 0 && scenarioId < scenarios.Count);

        return scenarioId >= 0 && scenarioId < scenarios.Count
            ? scenarios[scenarioId].VictoryCondition
            : ScenarioVictoryCondition.Standard;
    }

    /// <summary>
    /// The loss condition of the scenario being played, <c>getCurrentScenarioLossCondition()</c>
    /// (<c>campaign_savedata.cpp:229-244</c>).
    /// </summary>
    /// <returns>The scenario's condition, or <see cref="ScenarioLossCondition.Standard"/> outside the table.</returns>
    public ScenarioLossCondition GetCurrentScenarioLossCondition()
    {
        IReadOnlyList<ScenarioData> scenarios = CampaignData.GetCampaignData(_currentScenarioInfoId.CampaignId).AllScenarios;
        int scenarioId = _currentScenarioInfoId.ScenarioId;

        Debug.Assert(scenarioId >= 0 && scenarioId < scenarios.Count);

        return scenarioId >= 0 && scenarioId < scenarios.Count
            ? scenarios[scenarioId].LossCondition
            : ScenarioLossCondition.Standard;
    }

    /// <summary>
    /// The map difficulty a scenario is played on when the original hard-codes one,
    /// <c>getCurrentScenarioDifficultyLevel()</c> (<c>campaign_savedata.cpp:246-268</c>).
    /// </summary>
    /// <returns>The overridden difficulty, or <see langword="null"/> when the map's own difficulty stands.</returns>
    /// <remarks>
    /// 原版有十关的地图难度与玩家选的战役难度不一致，游戏把这十个例外写死在代码里。表外的关返回
    /// <see langword="null"/>，调用方就用地图表头里的难度。
    /// </remarks>
    public GameDifficulty? GetCurrentScenarioDifficultyLevel()
    {
        return AdjustedDifficultyLevels.TryGetValue(
            (_currentScenarioInfoId.CampaignId, _currentScenarioInfoId.ScenarioId),
            out GameDifficulty difficulty)
            ? difficulty
            : null;
    }

    /// <summary>
    /// The ten scenarios whose map difficulty is hard-coded in the original,
    /// <c>adjustedDifficultyLevels</c> (<c>campaign_savedata.cpp:248-260</c>).
    /// </summary>
    private static readonly Dictionary<(int Campaign, int Scenario), GameDifficulty> AdjustedDifficultyLevels = new()
    {
        [(CampaignIds.Roland, 1)] = GameDifficulty.Easy,
        [(CampaignIds.Archibald, 1)] = GameDifficulty.Easy,
        [(CampaignIds.Descendants, 0)] = GameDifficulty.Easy,
        [(CampaignIds.Descendants, 5)] = GameDifficulty.Hard,
        [(CampaignIds.WizardsIsle, 3)] = GameDifficulty.Hard,
        [(CampaignIds.VoyageHome, 0)] = GameDifficulty.Easy,
        [(CampaignIds.PriceOfLoyalty, 0)] = GameDifficulty.Easy,
        [(CampaignIds.PriceOfLoyalty, 5)] = GameDifficulty.Hard,
        [(CampaignIds.PriceOfLoyalty, 6)] = GameDifficulty.Hard,
        [(CampaignIds.PriceOfLoyalty, 7)] = GameDifficulty.Expert,
    };

    /// <summary>The table of <c>getCurrentScenarioDifficultyLevel()</c> and the fallback of an unknown difficulty.</summary>
    /// <returns>A percentage that keeps the rating sensible after a bug add a new difficulty.</returns>
    private static uint UnknownDifficultyPercent()
    {
        Debug.Assert(false, "A new campaign difficulty was added without updating the rating table.");

        return 100;
    }
}
