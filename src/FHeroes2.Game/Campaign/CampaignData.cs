using System.Diagnostics;

using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// One campaign: its scenarios in order and what opens up after each of them,
/// <c>class CampaignData</c> (<c>src/fheroes2/campaign/campaign_data.h:36-73</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把六个战役的数据写成六个工厂函数（<c>getRolandCampaignData()</c> 等），每个战役第一次被问到时构造一次并存进函数内的
/// <c>static</c>。端口用 <see cref="Lazy{T}"/> 做同样的事。
/// </para>
/// <para>
/// 关与关之间的跳转不是线性数组：Roland 第 4 关既能去第 5 关也能直接去「叛变」（第 11 关），
/// 「叛变」通关后接的是 Archibald 战役第 6 关。这些分支全写在场景表的 <c>nextScenarios</c> 里。
/// </para>
/// </remarks>
public sealed class CampaignData
{
    /// <summary>The campaign of the original game, one instance per campaign, <c>getCampaignData()</c>.</summary>
    private static readonly Lazy<CampaignData> RolandCampaign = new(static () => Build(CampaignId.Roland, Roland()));
    private static readonly Lazy<CampaignData> ArchibaldCampaign = new(static () => Build(CampaignId.Archibald, Archibald()));
    private static readonly Lazy<CampaignData> PriceOfLoyaltyCampaign = new(static () => Build(CampaignId.PriceOfLoyalty, PriceOfLoyalty()));
    private static readonly Lazy<CampaignData> DescendantsCampaign = new(static () => Build(CampaignId.Descendants, Descendants()));
    private static readonly Lazy<CampaignData> VoyageHomeCampaign = new(static () => Build(CampaignId.VoyageHome, VoyageHome()));
    private static readonly Lazy<CampaignData> WizardsIsleCampaign = new(static () => Build(CampaignId.WizardsIsle, WizardsIsle()));

    /// <summary>The scenario list, <c>_scenarios</c>.</summary>
    private readonly IReadOnlyList<ScenarioData> _scenarios;

    /// <summary>
    /// Creates a campaign, the reference's two setters (<c>setCampaignID</c> and <c>setCampaignScenarios</c>) in one step.
    /// </summary>
    /// <param name="campaignId">The campaign, <c>_campaignID</c>.</param>
    /// <param name="scenarios">The scenarios, <c>_scenarios</c>.</param>
    private CampaignData(int campaignId, IReadOnlyList<ScenarioData> scenarios)
    {
        Id = campaignId;
        _scenarios = scenarios;
    }

    /// <summary>
    /// The campaign, <c>_campaignID</c>.
    /// </summary>
    /// <remarks>名字带 <c>Id</c> 而不是 <c>CampaignId</c>：后者是常量类的名字，同域内同名会打架。</remarks>
    public int Id { get; }

    /// <summary>Every scenario of the campaign, <c>getAllScenarios()</c>.</summary>
    /// <remarks>顺序就是关号顺序：索引 <c>i</c> 永远是第 <c>i</c> 关。</remarks>
    public IReadOnlyList<ScenarioData> AllScenarios => _scenarios;

    /// <summary>
    /// The campaigns of the original game, <c>getCampaignData()</c> (<c>campaign_data.cpp:661-695</c>).
    /// </summary>
    /// <param name="campaignId">The campaign.</param>
    /// <returns>The campaign, empty after an assertion for an unknown id.</returns>
    public static CampaignData GetCampaignData(int campaignId) => campaignId switch
    {
        CampaignId.Roland => RolandCampaign.Value,
        CampaignId.Archibald => ArchibaldCampaign.Value,
        CampaignId.PriceOfLoyalty => PriceOfLoyaltyCampaign.Value,
        CampaignId.Descendants => DescendantsCampaign.Value,
        CampaignId.VoyageHome => VoyageHomeCampaign.Value,
        CampaignId.WizardsIsle => WizardsIsleCampaign.Value,
        _ => UnknownCampaign(),
    };

    /// <summary>
    /// What opens up after a scenario, <c>getScenariosAfter()</c> (<c>campaign_data.cpp:599-606</c>).
    /// </summary>
    /// <param name="scenarioInfo">The scenario.</param>
    /// <returns>The scenarios that become available, empty for the last ones.</returns>
    public static IReadOnlyList<ScenarioInfoId> GetScenariosAfter(ScenarioInfoId scenarioInfo)
    {
        IReadOnlyList<ScenarioData> scenarios = GetCampaignData(scenarioInfo.CampaignId).AllScenarios;

        Debug.Assert(scenarioInfo.ScenarioId >= 0 && scenarioInfo.ScenarioId < scenarios.Count);

        return scenarioInfo.ScenarioId >= 0 && scenarioInfo.ScenarioId < scenarios.Count
            ? scenarios[scenarioInfo.ScenarioId].NextScenarios
            : [];
    }

    /// <summary>
    /// Whether the scenario hands the player over to another campaign, <c>isBetrayalScenario()</c>
    /// (<c>game_campaign.cpp:122-132</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario.</param>
    /// <returns>True when one of the scenarios that follow it belongs to a different campaign.</returns>
    /// <remarks>
    /// <para>
    /// 「叛变关」是原版里两段战役之间的切换点（罗兰与阿基巴德各自的第六关）：打完它，下一关的战役号就变了。
    /// 参考拿它决定三件事——选关界面上图标画哪一张、进关前是否清空已得奖励、以及奖励物里的「携带部队」是否照常带走。
    /// </para>
    /// <para>
    /// 参考把这个函数放在 <c>game_campaign.cpp</c> 的匿名命名空间里，但它只查战役表、与界面无关，端口放进
    /// <see cref="CampaignData"/>，让界面和流程都能用。
    /// </para>
    /// </remarks>
    public static bool IsBetrayalScenario(ScenarioInfoId scenarioInfoId)
    {
        foreach (ScenarioInfoId next in GetScenariosAfter(scenarioInfoId))
        {
            if (next.CampaignId != scenarioInfoId.CampaignId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A scenario is a starting one when no other scenario leads to it,
    /// <c>isStartingScenario()</c> (<c>campaign_data.cpp:621-632</c>).
    /// </summary>
    /// <param name="scenarioInfo">The scenario.</param>
    /// <returns>True when nothing has to be finished first.</returns>
    public bool IsStartingScenario(ScenarioInfoId scenarioInfo)
    {
        foreach (ScenarioData scenario in _scenarios)
        {
            foreach (ScenarioInfoId next in scenario.NextScenarios)
            {
                if (next == scenarioInfo)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The scenarios a player may pick first, <c>getStartingScenarios()</c> (<c>campaign_data.cpp:608-619</c>).
    /// </summary>
    /// <returns>The starting scenarios in scenario order.</returns>
    public List<ScenarioInfoId> GetStartingScenarios()
    {
        List<ScenarioInfoId> startingScenarios = [];

        foreach (ScenarioData scenario in _scenarios)
        {
            ScenarioInfoId info = new(Id, scenario.ScenarioId);
            if (IsStartingScenario(info))
            {
                startingScenarios.Add(info);
            }
        }

        return startingScenarios;
    }

    /// <summary>
    /// Whether every map of the campaign is on disk, <c>isAllCampaignMapsPresent()</c> (<c>campaign_data.cpp:634-642</c>).
    /// </summary>
    /// <param name="rootDirectories">The directories to search, usually the data directory.</param>
    /// <returns>False as soon as one map is missing.</returns>
    public bool IsAllCampaignMapsPresent(IEnumerable<string> rootDirectories)
    {
        ArgumentNullException.ThrowIfNull(rootDirectories);

        foreach (ScenarioData scenario in _scenarios)
        {
            if (scenario.FindMapFile(rootDirectories) is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the scenario ends the campaign, <c>isLastScenario()</c> (<c>campaign_data.cpp:644-654</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario.</param>
    /// <returns>True when nothing follows it.</returns>
    public bool IsLastScenario(ScenarioInfoId scenarioInfoId)
    {
        Debug.Assert(_scenarios.Count > 0);

        foreach (ScenarioData scenario in _scenarios)
        {
            if (scenario.ScenarioInfo == scenarioInfoId && scenario.NextScenarios.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether all computer players of the scenario are in one alliance with each other,
    /// the first half of <c>updateScenarioGameplayConditions()</c> (<c>campaign_data.cpp:699-721</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario.</param>
    /// <returns>True for the three scenarios that hard-code the alliance.</returns>
    /// <remarks>
    /// Roland 第 10 关（最后决战）与 Archibald 第 6、11 关的敌人是铁板一块，地图文件里却没写联盟——原版把这些条件编在战役数据里。
    /// </remarks>
    public static bool AreAllAiPlayersAllied(ScenarioInfoId scenarioInfoId)
    {
        return scenarioInfoId.CampaignId switch
        {
            CampaignId.Roland => scenarioInfoId.ScenarioId == 9,
            CampaignId.Archibald => scenarioInfoId.ScenarioId is 5 or 10,
            CampaignId.PriceOfLoyalty or CampaignId.Descendants or CampaignId.VoyageHome or CampaignId.WizardsIsle => false,
            _ => UnknownCampaignAlliance(),
        };
    }

    /// <summary>
    /// Applies the alliance the campaign data gives on top of the map's own,
    /// <c>updateScenarioGameplayConditions()</c> (<c>campaign_data.cpp:697-747</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario.</param>
    /// <param name="mapInfo">The map header read from the scenario file.</param>
    /// <returns>The map header with the alliances of the computer players merged into one.</returns>
    /// <remarks>
    /// 参考把地图头里所有「含 AI 颜色、又不是人类自己那一方」的 <c>unions</c> 项改成全部 AI 颜色
    /// （<c>campaign_data.cpp:741-745</c>）；人类保持原样，于是这张图上的人类要同时打掉所有电脑才算赢。
    /// 这两关的人工联盟是原版编在战役数据里的：地图本身没写联盟，但敌人必须铁板一块。
    /// </remarks>
    public static Mp2FileInfo UpdateScenarioGameplayConditions(ScenarioInfoId scenarioInfoId, Mp2FileInfo mapInfo)
    {
        ArgumentNullException.ThrowIfNull(mapInfo);

        if (!AreAllAiPlayersAllied(scenarioInfoId))
        {
            return mapInfo;
        }

        // 参考要求这张图上恰好一个人类颜色与至少一个 AI 颜色，否则断言后放弃（campaign_data.cpp:724-737）。
        // 端口的 Mp2FileInfo 已经保证 HumanPlayers 非空；多于一个只能是地图被改过，照参考的「不动」处理。
        if (PlayerColors.Count(mapInfo.HumanPlayers) != 1)
        {
            Debug.Assert(false, "A campaign alliance scenario has exactly one human color.");

            return mapInfo;
        }

        PlayerColor human = mapInfo.HumanPlayers;
        PlayerColor aiColors = mapInfo.KingdomColors & ~human;

        if (aiColors == PlayerColor.None)
        {
            Debug.Assert(false, "A campaign alliance scenario has at least one computer color.");

            return mapInfo;
        }

        PlayerColor[] unions = [.. mapInfo.PlayerUnions];

        for (int index = 0; index < unions.Length; ++index)
        {
            if (unions[index] != human && (unions[index] & aiColors) != 0)
            {
                unions[index] = aiColors;
            }
        }

        return mapInfo with { PlayerUnions = unions };
    }

    /// <summary>
    /// Turns a scenario's freshly read map header into the one the campaign starts from, the block above
    /// <c>updateScenarioGameplayConditions()</c> in <c>Dialog::CampaignInfo</c> (<c>game_campaign.cpp:1604-1615</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario that is about to be played.</param>
    /// <param name="mapInfo">The header read from the scenario's map file.</param>
    /// <param name="interfaceLanguage">The language the interface runs in, <c>getCurrentLanguage()</c>.</param>
    /// <param name="resourceLanguage">The language version of the archives, <c>getResourceLanguage()</c>.</param>
    /// <returns>The header to hand to the players and the world.</returns>
    /// <remarks>
    /// <para>
    /// 两件事，参考在同一个分支里连着做：先把法语老地图被偷走的字符还原回名字与简介（游戏里其他地方的处理见
    /// <see cref="World.FixFrenchCharactersInStrings"/>），再补战役自己写死的联盟。
    /// </para>
    /// <para>
    /// 法语那两个条件是「界面说法语 **且** 资材是法语版」并且地图不是新格式——照参考原样，缺一不可：中文界面开
    /// 法语资材、或者法语界面开英文资材都不该动地图文本，否则标点会被换成重音字母。
    /// </para>
    /// </remarks>
    public static Mp2FileInfo PrepareScenarioMap(
        ScenarioInfoId scenarioInfoId,
        Mp2FileInfo mapInfo,
        SupportedLanguage interfaceLanguage,
        SupportedLanguage resourceLanguage)
    {
        ArgumentNullException.ThrowIfNull(mapInfo);

        Mp2FileInfo prepared = mapInfo;

        if (mapInfo.Version != MapVersion.Resurrection
            && interfaceLanguage == SupportedLanguage.French
            && resourceLanguage == SupportedLanguage.French)
        {
            prepared = prepared with
            {
                Name = MapTextCodec.FixFrenchCharacters(prepared.Name),
                Description = MapTextCodec.FixFrenchCharacters(prepared.Description),
            };
        }

        return UpdateScenarioGameplayConditions(scenarioInfoId, prepared);
    }

    /// <summary>Creates the campaign and freezes its scenario list.</summary>
    /// <param name="campaignId">The campaign.</param>
    /// <param name="scenarios">The scenarios in scenario order.</param>
    /// <returns>The campaign.</returns>
    private static CampaignData Build(int campaignId, List<ScenarioData> scenarios)
    {
        return new CampaignData(campaignId, scenarios);
    }

    /// <summary>The fallback of <see cref="GetCampaignData"/> for an unknown campaign.</summary>
    /// <returns>An empty campaign after an assertion.</returns>
    private static CampaignData UnknownCampaign()
    {
        Debug.Assert(false, "A campaign was added without its scenario table.");

        return new CampaignData(-1, []);
    }

    /// <summary>The fallback of <see cref="AreAllAiPlayersAllied"/> for an unknown campaign.</summary>
    /// <returns>False after an assertion.</returns>
    private static bool UnknownCampaignAlliance()
    {
        Debug.Assert(false, "A campaign was added without its gameplay conditions.");

        return false;
    }

    /// <summary>The scenarios of Roland's campaign, <c>getRolandCampaignData()</c> (<c>campaign_data.cpp:171-257</c>).</summary>
    /// <returns>Eleven scenarios.</returns>
    private static List<ScenarioData> Roland()
    {
        static ScenarioInfoId Info(int scenarioId) => new(CampaignId.Roland, scenarioId);

        return
        [
            new ScenarioData(
                Info(0), [Info(1)], "CAMPG01.H2C", "Force of Arms",
                "Roland needs you to defeat the lords near his castle to begin his war of rebellion against his brother. They are not allied with each other, so they will spend most of their time fighting with one another. Victory is yours when you have defeated all of their castles and heroes.",
                [[new CampaignVideoInfo("GOOD01.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD01V.SMK", VideoControl.Cutscene, 348, 420)]],
                []),
            new ScenarioData(
                Info(1), [Info(2), Info(3)], "CAMPG02.H2C", "Annexation",
                "The local lords refuse to swear allegiance to Roland, and must be subdued. They are wealthy and powerful, so be prepared for a tough fight. Capture all enemy castles to win.",
                [[new CampaignVideoInfo("GOOD02.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD02W.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("GOOD03.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD03QW.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(2), [Info(3)], "CAMPG03.H2C", "Save the Dwarves",
                "Your task is to defend the Dwarves against Archibald's forces. Capture all of the enemy towns and castles to win, and be sure not to lose all of the dwarf towns at once, or the enemy will have won.",
                [],
                [[new CampaignVideoInfo("GOOD04.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD04W.SMK", VideoControl.Audio)]],
                ScenarioVictoryCondition.Standard,
                ScenarioLossCondition.LoseAllSorceressVillages),
            new ScenarioData(
                Info(3), [Info(4), Info(10)], "CAMPG04.H2C", "Carator Mines",
                "You will face four allied enemies in a straightforward fight for resources and treasure. Capture all of the enemy castles for victory.",
                [],
                [[new CampaignVideoInfo("GOOD05.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD05V.SMK", VideoControl.Cutscene, 88, 345)]]),
            new ScenarioData(
                Info(4), [Info(5)], "CAMPG05.H2C", "Turning Point",
                "Your enemies are allied against you and start close by, so be ready to come out fighting. You will need to own all four castles in this small valley to win.",
                [],
                [[new CampaignVideoInfo("GOOD06.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD06AV.SMK", VideoControl.Cutscene, 280, 186)]]),
            new ScenarioData(
                Info(5), [Info(6), Info(7)], "CAMPG06.H2C", "Defender",
                "The Sorceress' guild of Noraston has requested Roland's aid against an attack from Archibald's allies. Capture all of the enemy castles to win, and don't lose Noraston, or you'll lose the scenario. (Hint: There is an enemy castle on an island in the ocean.)",
                [],
                [[new CampaignVideoInfo("GOOD07.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD07QW.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(6), [Info(8)], "CAMPG07.H2C", "The Gauntlet",
                "Gather as large an army as possible and capture the enemy castle within 8 weeks. You are opposed by only one enemy, but must travel a long way to get to the enemy castle. Any troops you have in your army at the end of this scenario will be with you in the final battle.",
                [],
                []),
            new ScenarioData(
                Info(7), [Info(8)], "CAMPG08.H2C", "The Crown",
                "Find the Crown before Archibald's heroes find it. Roland will need the Crown for the final battle against Archibald.",
                [],
                [],
                ScenarioVictoryCondition.ObtainUltimateCrown),
            new ScenarioData(
                Info(8), [Info(9)], "CAMPG09.H2C", "Corlagon's Defense",
                "Three allied enemies stand before you and victory, including Lord Corlagon. Roland is in a castle to the northwest, and you will lose if he falls to the enemy. Remember that capturing Lord Corlagon will ensure that he will not fight against you in the final scenario.",
                [[new CampaignVideoInfo("GOOD09.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD09W.SMK", VideoControl.Audio)]],
                []),
            new ScenarioData(
                Info(9), [], "CAMPG10.H2C", "Final Justice",
                "This is the final battle. Both you and your enemy are armed to the teeth, and all are allied against you. Capture Archibald to end the war!",
                [[new CampaignVideoInfo("GOOD10.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD10W.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("LIBRARY.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("LIBRARYW.SMK", VideoControl.Audio)]]),
            // 通关「叛变」之后接的是 Archibald 战役第 6 关：这是两个战役唯一的口子。
            new ScenarioData(
                Info(10), [new ScenarioInfoId(CampaignId.Archibald, 5)], "CAMPG05B.H2C", "Betrayal",
                "Switching sides leaves you with three castles against the enemy's one. This battle will be the easiest one you will face for the rest of the war...traitor.",
                [],
                [[new CampaignVideoInfo("EVIL06.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL06BW.SMK", VideoControl.Audio)]]),
        ];
    }

    /// <summary>The scenarios of Archibald's campaign, <c>getArchibaldCampaignData()</c>.</summary>
    /// <returns>The scenarios.</returns>
    private static List<ScenarioData> Archibald()
    {
        static ScenarioInfoId Info(int scenarioId) => new(CampaignId.Archibald, scenarioId);

        return
        [
            new ScenarioData(
                Info(0), [Info(1)], "CAMPE01.H2C", "First Blood",
                "King Archibald requires you to defeat the three enemies in this region. They are not allied with one another, so they will spend most of their energy fighting amongst themselves. You will win when you own all of the enemy castles and there are no more heroes left to fight.",
                [[new CampaignVideoInfo("EVIL01.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL01V.SMK", VideoControl.Cutscene, 366, 403)]],
                []),
            new ScenarioData(
                Info(1), [Info(2), Info(3)], "CAMPE02.H2C", "Barbarian Wars",
                "You must unify the barbarian tribes of the north by conquering them. As in the previous mission, the enemy is not allied against you, but they have more resources at their disposal. You will win when you own all of the enemy castles and there are no more heroes left to fight.",
                [[new CampaignVideoInfo("EVIL02.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL02W.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("EVIL03.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL03QW.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(2), [Info(4), Info(11)], "CAMPE03.H2C", "Necromancers",
                "Do-gooder wizards have taken the Necromancers' castle. You must retake it to achieve victory. Remember that while you start with a powerful army, you have no castle and must take one within 7 days, or lose this battle. (Hint: The nearest castle is to the southeast.)",
                [],
                [
                    [new CampaignVideoInfo("EVIL05.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL05AV.SMK", VideoControl.Cutscene, 87, 345)],
                    [new CampaignVideoInfo("EVIL05.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("RBETRAYV.SMK", VideoControl.Cutscene, 87, 345)],
                ]),
            new ScenarioData(
                Info(3), [Info(4), Info(11)], "CAMPE04.H2C", "Slay the Dwarves",
                "The dwarves need conquering before they can interfere in King Archibald's plans. Roland's forces have more than one hero and many towns to start with, so be ready for attack from multiple directions. You must capture all of the enemy towns and castles to claim victory.",
                [],
                [
                    [new CampaignVideoInfo("EVIL05.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL05BV.SMK", VideoControl.Cutscene, 87, 345)],
                    [new CampaignVideoInfo("EVIL05.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("RBETRAYV.SMK", VideoControl.Cutscene, 87, 345)],
                ]),
            new ScenarioData(
                Info(4), [Info(5)], "CAMPE05.H2C", "Turning Point",
                "Your enemies are allied against you and start close by, so be ready to come out fighting. You will need to own all four castles in this small valley to win.",
                [],
                [[new CampaignVideoInfo("EVIL06.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL06AW.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(5), [Info(6), Info(7)], "CAMPE06.H2C", "Rebellion",
                "You must put down a peasant revolt led by Roland's forces. All are allied against you, but you have Lord Corlagon, an experienced hero, to help you. Capture all enemy castles to win.",
                [],
                [[new CampaignVideoInfo("EVIL07.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL07W.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(6), [Info(7)], "CAMPE07.H2C", "Dragon Master",
                "There are two enemies allied against you in this mission. Both are well armed and seek to evict you from their island. Avoid them and capture Dragon City to win.",
                [],
                [[new CampaignVideoInfo("EVIL08.SMK", VideoControl.Cutscene)]],
                ScenarioVictoryCondition.CaptureDragonCity),
            new ScenarioData(
                Info(7), [Info(8), Info(9)], "CAMPE08.H2C", "Country Lords",
                "Your orders are to conquer the country lords that have sworn to serve Roland. All of the enemy castles are unified against you. Since you start without a castle, you must hurry to capture one before the end of the week. Capture all enemy castles for victory.",
                [],
                [[new CampaignVideoInfo("EVIL09.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL09W.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(8), [Info(10)], "CAMPE09.H2C", "The Crown",
                "Find the Crown before Roland's heroes find it. Archibald will need the Crown for the final battle against Roland.",
                [],
                [],
                ScenarioVictoryCondition.ObtainUltimateCrown),
            new ScenarioData(
                Info(9), [Info(10)], "CAMPE10.H2C", "Greater Glory",
                "Gather as large an army as possible and capture the enemy castle within 8 weeks. You are opposed by only one enemy, but must travel a long way to get to the enemy castle. Any troops you have in your army at the end of this scenario will be with you in the final battle.",
                [],
                []),
            new ScenarioData(
                Info(10), [], "CAMPE11.H2C", "Apocalypse",
                "This is the final battle. Both you and your enemy are armed to the teeth, and all are allied against you. Capture Roland to win the war, and be sure not to lose Archibald in the fight!",
                [[new CampaignVideoInfo("EVIL10.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("EVIL11W.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("PRISON.SMK", VideoControl.Cutscene)]]),
            // 与 Roland 的「叛变」对称：这一关通关后接 Roland 战役第 6 关。
            new ScenarioData(
                Info(11), [new ScenarioInfoId(CampaignId.Roland, 5)], "CAMPE05B.H2C", "Betrayal",
                "Switching sides leaves you with three castles against the enemy's one. This battle will be the easiest one you will face for the rest of the war...traitor.",
                [],
                [[new CampaignVideoInfo("GOOD06.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("GOOD06BV.SMK", VideoControl.Cutscene)]]),
        ];
    }

    /// <summary>The scenarios of Price of Loyalty, <c>getPriceOfLoyaltyCampaignData()</c>.</summary>
    /// <returns>The scenarios.</returns>
    private static List<ScenarioData> PriceOfLoyalty()
    {
        static ScenarioInfoId Info(int scenarioId) => new(CampaignId.PriceOfLoyalty, scenarioId);

        return
        [
            new ScenarioData(
                Info(0), [Info(1)], "CAMP1_01.HXC", "Uprising",
                "Subdue the unruly local lords in order to provide the Empire with facilities to operate in this region.",
                [[new CampaignVideoInfo("POL1.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL1.SMK", VideoControl.Audio)]],
                []),
            new ScenarioData(
                Info(1), [Info(2), Info(3)], "CAMP1_02.HXC", "Island of Chaos",
                "Eliminate all opposition in this area. Then the first piece of the artifact will be yours.",
                [[new CampaignVideoInfo("POL2.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL2.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("POL3.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL3.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(2), [Info(4)], "CAMP1_03.HXC", "Arrow's Flight",
                "The sorceresses to the northeast are rebelling! For the good of the empire you must quash their feeble uprising on your way to the mountains.",
                [],
                []),
            new ScenarioData(
                Info(3), [Info(5)], "CAMP1_04.HXC", "The Abyss",
                "Having prepared for your arrival, Kraeger has arranged for a force of necromancers to thwart your quest. You must capture the castle of Scabsdale before the first day of the third week, or the Necromancers will be too strong for you.",
                [],
                []),
            new ScenarioData(
                Info(4), [Info(5)], "CAMP1_05.HXC", "The Giant's Pass",
                "The barbarian despot in this area is, as yet, ignorant of your presence. Quickly, build up your forces before you are discovered and attacked! Secure the region by subduing all enemy forces.",
                [[new CampaignVideoInfo("POL4.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL4.SMK", VideoControl.Audio)]],
                []),
            new ScenarioData(
                Info(5), [Info(6), Info(7)], "CAMP1_06.HXC", "Aurora Borealis",
                "The Empire is weak in this region. You will be unable to completely subdue all forces in this area, so take what you can before reprisal strikes. Remember, your true goal is to claim the Helmet of Anduran.",
                [[new CampaignVideoInfo("POL5.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL5.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("POL6.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL6.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(6), [Info(7)], "CAMP1_07.HXC", "Betrayal's End",
                "For the good of the Empire, eliminate Kraeger.",
                [],
                [[new CampaignVideoInfo("POL7.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL7.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(7), [], "CAMP1_08.HXC", "Corruption's Heart",
                "At last, you have the opportunity and the facilities to rid the Empire of the necromancer's evil. Eradicate them completely, and you will be sung as a hero for all time.",
                [],
                [[new CampaignVideoInfo("POL8.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXPOL8.SMK", VideoControl.Audio)]]),
        ];
    }

    /// <summary>The scenarios of Descendants, <c>getDescendantsCampaignData()</c>.</summary>
    /// <returns>The scenarios.</returns>
    private static List<ScenarioData> Descendants()
    {
        static ScenarioInfoId Info(int scenarioId) => new(CampaignId.Descendants, scenarioId);

        return
        [
            new ScenarioData(
                Info(0), [Info(1)], "CAMP2_01.HXC", "Conquer and Unify",
                "Conquer and unite all the enemy tribes. Don't lose the hero Jarkonas, the forefather of all descendants.",
                [[new CampaignVideoInfo("DES9.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXDES9.SMK", VideoControl.Audio)]],
                []),
            new ScenarioData(
                Info(1), [Info(2), Info(3)], "CAMP2_02.HXC", "Border Towns",
                "Your rival, the Kingdom of Harondale, is attacking weak towns on your border! Recover from their first strike and crush them completely!",
                [[new CampaignVideoInfo("DES10.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXDES10.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("DES11.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXDES11.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(2), [Info(4)], "CAMP2_03.HXC", "The Wayward Son",
                "Find your wayward son Joseph who is rumored to be living in the desolate lands. Do it before the first day of the third month or it will be of no help to your family.",
                [],
                []),
            new ScenarioData(
                Info(3), [Info(4)], "CAMP2_04.HXC", "Crazy Uncle Ivan",
                "Rescue your crazy uncle Ivan. Find him before the first day of the fourth month or it will be of no help to your kingdom.",
                [],
                []),
            new ScenarioData(
                Info(4), [Info(5), Info(6)], "CAMP2_05.HXC", "The Southern War",
                "Destroy the barbarians who are attacking the southern border of your kingdom! Recover your fallen towns, and then invade the jungle kingdom. Leave no enemy standing.",
                [[new CampaignVideoInfo("DES12.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXDES12.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("DES13.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXDES13.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(5), [Info(7)], "CAMP2_06.HXC", "Ivory Gates",
                "Retake the castle of Ivory Gates, which has fallen due to treachery.",
                [],
                []),
            new ScenarioData(
                Info(6), [Info(7)], "CAMP2_07.HXC", "The Elven Lands",
                "Gain the favor of the elves. They will not allow trees to be chopped down, so we will send you wood every 2 weeks. You must complete your mission before the first day of the seventh month, or the kingdom will surely fall.",
                [],
                []),
            new ScenarioData(
                Info(7), [], "CAMP2_08.HXC", "The Epic Battle",
                "This is the final battle against your rival kingdom of Harondale. Eliminate everyone, and don't lose the hero Jarkonas VI.",
                [[new CampaignVideoInfo("DES14.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXDES14.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("DES15.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXDES15.SMK", VideoControl.Audio)]]),
        ];
    }

    /// <summary>The scenarios of Voyage Home, <c>getVoyageHomeCampaignData()</c>.</summary>
    /// <returns>The scenarios.</returns>
    private static List<ScenarioData> VoyageHome()
    {
        static ScenarioInfoId Info(int scenarioId) => new(CampaignId.VoyageHome, scenarioId);

        return
        [
            new ScenarioData(
                Info(0), [Info(1)], "CAMP4_01.HXC", "Stranded",
                "Capture the town on the island off the southeast shore in order to construct a boat and travel back towards the mainland. Do not lose the hero Gallavant.",
                [[new CampaignVideoInfo("VOY21.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXVOY21.SMK", VideoControl.Audio)]],
                []),
            new ScenarioData(
                Info(1), [Info(2), Info(3)], "CAMP4_02.HXC", "Pirate Isles",
                "Find and defeat Martine, the pirate leader, who resides in Pirates Cove. Do not lose Gallavant or your quest will be over.",
                [[new CampaignVideoInfo("VOY22.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXVOY22.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("VOY23.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXVOY23.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(2), [], "CAMP4_03.HXC", "King and Country",
                "Eliminate all the other forces who oppose the rule of Lord Alberon. Gallavant must not die.",
                [],
                [[new CampaignVideoInfo("VOY24.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXVOY24.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(3), [], "CAMP4_04.HXC", "Blood is Thicker",
                "Overthrow the entrenched monarchy of Lord Alberon, and claim all the land in your name. Gallavant must not die.",
                [],
                [[new CampaignVideoInfo("VOY25.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXVOY25.SMK", VideoControl.Audio)]]),
        ];
    }

    /// <summary>The scenarios of Wizard's Isle, <c>getWizardsIsleCampaignData()</c>.</summary>
    /// <returns>The scenarios.</returns>
    private static List<ScenarioData> WizardsIsle()
    {
        static ScenarioInfoId Info(int scenarioId) => new(CampaignId.WizardsIsle, scenarioId);

        return
        [
            new ScenarioData(
                Info(0), [Info(1)], "CAMP3_01.HXC", "The Shrouded Isles",
                "Your mission is to vanquish the warring mages in the magical Shrouded Isles. The completion of this task will give you a fighting chance against your rivals.",
                [[new CampaignVideoInfo("WIZ16.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXWIZ16.SMK", VideoControl.Audio)]],
                []),
            new ScenarioData(
                Info(1), [Info(2), Info(3)], "CAMP3_02.HXC", "The Eternal Scrolls",
                "The location of the great library has been discovered! You must make your way to it, and reclaim the city of Chronos in which it lies.",
                [[new CampaignVideoInfo("WIZ17.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXWIZ17.SMK", VideoControl.Audio)]],
                [[new CampaignVideoInfo("WIZ18.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXWIZ18.SMK", VideoControl.Audio)]]),
            new ScenarioData(
                Info(2), [Info(3)], "CAMP3_03.HXC", "Power's End",
                "Find the Orb of negation, which is said to be buried in this land. There are clues inscribed on stone obelisks which will help lead you to your prize. Find the Orb before the first day of the sixth month, or your rivals will surely have gotten to the fount before you.",
                [],
                [[new CampaignVideoInfo("WIZ19.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXWIZ19.SMK", VideoControl.Audio)]],
                ScenarioVictoryCondition.ObtainSphereNegation),
            new ScenarioData(
                Info(3), [], "CAMP3_04.HXC", "Fount of Wizardry",
                "You must take control of the castle of Magic, where the fount of wizardry lies. Do this and your victory will be supreme.",
                [],
                [[new CampaignVideoInfo("WIZ20.SMK", VideoControl.VideoLoop), new CampaignVideoInfo("MIXWIZ20.SMK", VideoControl.Audio)]]),
        ];
    }
}
