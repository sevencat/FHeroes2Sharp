using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The data half of <c>mapSpecificationsDialog()</c> (<c>editor_map_specs_window.cpp:1880-2580</c>): the
/// name and description of the scenario, its difficulty, which colours play and which of them a human may
/// pick, the victory and loss conditions, and the write back of all of it into the header of the map.
/// </summary>
/// <remarks>
/// <para>
/// 参考直接改 <c>Maps::Map_Format::MapFormat</c>，状态就是那个可变结构；本移植编辑的是 MP2，头部
/// <see cref="Mp2FileInfo"/> 是记录，于是这里做一层可变状态，<see cref="CreateInfo"/> 生成新的头部，
/// 由 <see cref="MapEditSession.ApplyMapInfo"/> 装回 <see cref="Mp2Map.Info"/>。绘制与事件循环不在这一类里（a7l.3.5）。
/// </para>
/// <para>
/// 四处与参考不同，都是 MP2 头部装不下 fh2m 的字段：
/// </para>
/// <list type="number">
/// <item>
/// 「占领某座城 / 击败某个英雄」的目标在 fh2m 里是「格子索引 + 颜色」（<c>:1010-1035</c>），MP2 只有坐标，
/// 所以这里只有 <see cref="VictoryTarget"/> 与 <see cref="LossTarget"/>。
/// </item>
/// <item>金额：fh2m 存枚数，MP2 存千枚（<c>maps_fileinfo.cpp:499-506</c>），见 <see cref="VictoryGoldInThousands"/>。</item>
/// <item>
/// 联盟：fh2m 是两个任意颜色集合，MP2 只存「第一方几个颜色」，双方都按颜色顺序取前 N 个，见
/// <see cref="VictorySideCount"/>。
/// </item>
/// <item>
/// 目标的候选：参考按「只有电脑的颜色」「只有人类的颜色」筛城镇与英雄（<c>:1013-1030</c>）。英雄在 MP2 里自带
/// 颜色（<see cref="Mp2MapHero.Color"/>），城镇的归属存在信息块里，编辑器不维护，而胜利条件也只记坐标，所以
/// <see cref="Towns"/> 不按归属筛；由此「占领城镇」的 AI 胜利位恒为 false——参考对非中立城镇也这么办，这里分不出中立。
/// </item>
/// </list>
/// <para>
/// 条件是否还站得住沿用参考的 <c>updateCondition</c>（<c>:879-935</c>）：目标不在候选里就换成第一个，候选为空就把
/// 条件降级成「击败所有敌人 / 失去一切」。参考在窗口打开时、每次点颜色后和确定前各跑一遍；本移植把它做成显式方法，
/// <see cref="CreateInfo"/> 里再跑一次兜底。
/// </para>
/// </remarks>
public sealed class MapSpecsState
{
    /// <summary>Granularity of the gold condition: the MP2 header stores thousands of pieces.</summary>
    public const int GoldThousand = 1000;

    /// <summary>
    /// Smallest gold target the reference offers, in thousands: its value widget starts at 10 000 pieces
    /// (<c>editor_map_specs_window.cpp:1000-1010</c>).
    /// </summary>
    public const int MinVictoryGoldInThousands = 10;

    /// <summary>Largest value of a condition parameter: both are two byte fields of the MP2 header.</summary>
    public const int MaxConditionParameter = ushort.MaxValue;

    /// <summary>
    /// The artifact value that stands for "any ultimate artifact", the 0 the original format keeps for it
    /// (<c>editor_map_specs_window.cpp:829-832</c>).
    /// </summary>
    public const int UltimateArtifact = 0;

    private readonly Mp2FileInfo _original;
    private readonly List<Mp2CastlePosition> _towns;
    private readonly List<Mp2MapHero> _heroes;
    private readonly List<string> _rumors = [];
    private readonly List<MapDailyEvent> _dailyEvents = [];

    private readonly MapTranslationState _translations;

    private GameDifficulty _difficulty;
    private PlayerColor _humanPlayers;
    private PlayerColor _computerPlayers;
    private MapVictoryCondition _victoryCondition;
    private MapLossCondition _lossCondition;
    private Point _victoryTarget;
    private Point _lossTarget;
    private int _victoryArtifact;
    private int _victoryGold;
    private int _victorySideCount;
    private int _lossDays;

    /// <summary>Builds the state from the header of <paramref name="map"/>.</summary>
    /// <remarks>构造时就降级文件里失效的条件，和参考两个条件面板的构造函数一致。</remarks>
    /// <param name="map">The map being edited.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
    public MapSpecsState(Mp2Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        Map = map;
        _original = map.Info;
        _towns = [.. map.Castles];
        _heroes = [.. map.Heroes];

        _translations = new MapTranslationState(
            MapTranslationState.DefaultLanguage,
            new MapTranslationText
            {
                Name = _original.Name,
                Description = _original.Description,
            });

        _difficulty = ClampDifficulty(_original.Difficulty);
        AvailablePlayers = _original.KingdomColors;
        _humanPlayers = _original.HumanPlayers & AvailablePlayers;
        _computerPlayers = _original.ComputerPlayers & AvailablePlayers;
        ComputerAlsoWins = _original.ComputerAlsoWins;
        AllowNormalVictory = _original.AllowNormalVictory;

        _victoryCondition = Enum.IsDefined(_original.VictoryCondition)
            ? _original.VictoryCondition
            : MapVictoryCondition.DefeatEveryone;
        _lossCondition = Enum.IsDefined(_original.LossCondition)
            ? _original.LossCondition
            : MapLossCondition.Everything;

        _victoryTarget = ReadPoint(_original.VictoryConditionParameters);
        _victoryArtifact = ReadParameter(_original.VictoryConditionParameters, 0);
        _victoryGold = ReadParameter(_original.VictoryConditionParameters, 0);
        _victorySideCount = _victoryCondition == MapVictoryCondition.DefeatOtherSide
            ? Math.Max(1, ReadParameter(_original.VictoryConditionParameters, 0))
            : DefaultSideCount();
        _lossTarget = ReadPoint(_original.LossConditionParameters);
        _lossDays = ReadParameter(_original.LossConditionParameters, 0);

        _ = UpdateVictoryCondition();
        _ = UpdateLossCondition();
    }

    /// <summary>
    /// The map being edited; the towns and heroes of the target lists come from it. Installing a new header does
    /// not touch the tiles, so a decoded terrain cache stays valid.
    /// </summary>
    public Mp2Map Map { get; }

    /// <summary>Scenario name (<c>_mapName</c>, <c>:1970</c>); the header keeps <see cref="Mp2FileInfo.NameFieldLength"/> characters.</summary>
    /// <remarks>名字存在当前主语言的文本槽里，见 <see cref="Translations"/>。</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string Name
    {
        get => _translations.Current.Name;
        set => _translations.Current.Name = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Scenario description (<c>_description</c>, <c>:1980</c>); the header keeps <see cref="Mp2FileInfo.DescriptionFieldLength"/> characters.</summary>
    /// <remarks>与 <see cref="Name"/> 一样，存在当前主语言的文本槽里。</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string Description
    {
        get => _translations.Current.Description;
        set => _translations.Current.Description = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Notes the map maker left for whoever opens the map in the editor (<c>creatorNotes</c>), the "ABOUT" button of
    /// the window (<c>editor_map_specs_window.cpp:2370-2377</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Name"/> / <see cref="Description"/> 同一套路：存在当前主语言的文本槽里，长度上限是参考的
    /// <c>Map_Format::messageCharLimit</c>（999）。MP2 的 428 字节头部装不下它，写出去只留在
    /// <see cref="Mp2FileInfo.CreatorNotes"/> 上等 <c>.fh2m</c>。
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string CreatorNotes
    {
        get => _translations.Current.CreatorNotes;
        set => _translations.Current.CreatorNotes = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Custom rumors of the map (<c>rumors</c>), the list the rumors window edits
    /// (<c>editor_map_specs_window.cpp:2282-2289</c>).
    /// </summary>
    /// <remarks>
    /// 参考把传闻放在 <c>MapFormat</c> 上、按语言各存一份；端口的地图格式还没有这一层（随 <c>.fh2m</c> 进来），
    /// 所以先存主语言的那一份，MP2 文件也不写它。用 <see cref="SetRumors"/> 整份换掉，窗口拿到的是副本。
    /// </remarks>
    public IReadOnlyList<string> Rumors => _rumors;

    /// <summary>
    /// Daily events of the map (<c>dailyEvents</c>), the list the daily events window edits
    /// (<c>editor_map_specs_window.cpp:2290-2297</c>).
    /// </summary>
    /// <remarks>与 <see cref="Rumors"/> 同样先只存主语言的一份，整份换掉走 <see cref="SetDailyEvents"/>。</remarks>
    public IReadOnlyList<MapDailyEvent> DailyEvents => _dailyEvents;

    /// <summary>Replaces the rumor list, the write-back of the rumors window (<c>:2284-2286</c>).</summary>
    /// <param name="rumors">The rumors the window ended up with; copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rumors"/> is <see langword="null"/>.</exception>
    public void SetRumors(IReadOnlyList<string> rumors)
    {
        ArgumentNullException.ThrowIfNull(rumors);

        _rumors.Clear();
        _rumors.AddRange(rumors);
    }

    /// <summary>Replaces the daily event list, the write-back of the daily events window (<c>:2292-2294</c>).</summary>
    /// <param name="events">The events the window ended up with; each one is cloned, so the window keeps its own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public void SetDailyEvents(IReadOnlyList<MapDailyEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _dailyEvents.Clear();

        foreach (MapDailyEvent dailyEvent in events)
        {
            _dailyEvents.Add(dailyEvent.Clone());
        }
    }

    /// <summary>
    /// The languages of the map and the texts of the main one, the state the language window edits
    /// (<c>BaseMapFormat::mainLanguage</c> / <c>translations</c>).
    /// </summary>
    /// <remarks>
    /// 参考把语言放在 <c>MapFormat</c> 上，规格窗只改 <c>name</c> / <c>description</c>；端口没有 <c>MapFormat</c>，于是
    /// 名字与描述由这份状态托管，换语言后 <see cref="CreateInfo"/> 写出去的是新主语言的文本。
    /// </remarks>
    public MapTranslationState Translations => _translations;

    /// <summary>
    /// Difficulty (<c>:2120-2140</c>). The reference never offers <see cref="GameDifficulty.Impossible"/> — that is a
    /// player choice, not a map property — so the setter clamps to Easy…Expert.
    /// </summary>
    public GameDifficulty Difficulty
    {
        get => _difficulty;
        set => _difficulty = ClampDifficulty(value);
    }

    /// <summary>
    /// Colours that have a kingdom on the map (<c>availablePlayerColors</c>). Fixed for the lifetime of the window:
    /// the reference only draws a check box for an available colour, and the panel that adds kingdoms is elsewhere.
    /// </summary>
    public PlayerColor AvailablePlayers { get; }

    /// <summary>Colours a human may pick (<c>humanPlayerColors</c>).</summary>
    public PlayerColor HumanPlayers => _humanPlayers;

    /// <summary>Colours the computer may pick (<c>computerPlayerColors</c>).</summary>
    public PlayerColor ComputerPlayers => _computerPlayers;

    /// <summary>Colours only a human may pick (<c>:1022</c>).</summary>
    public PlayerColor HumanOnlyPlayers => _humanPlayers & ~_computerPlayers;

    /// <summary>Colours only the computer may pick (<c>:1020</c>).</summary>
    public PlayerColor ComputerOnlyPlayers => _computerPlayers & ~_humanPlayers;

    /// <summary>Victory condition (<c>_victoryConditionType</c>); an unknown value falls back to <see cref="MapVictoryCondition.DefeatEveryone"/>.</summary>
    public MapVictoryCondition VictoryCondition
    {
        get => _victoryCondition;
        set => _victoryCondition = Enum.IsDefined(value) ? value : MapVictoryCondition.DefeatEveryone;
    }

    /// <summary>Loss condition (<c>_lossConditionType</c>); an unknown value falls back to <see cref="MapLossCondition.Everything"/>.</summary>
    public MapLossCondition LossCondition
    {
        get => _lossCondition;
        set => _lossCondition = Enum.IsDefined(value) ? value : MapLossCondition.Everything;
    }

    /// <summary>Whether the victory condition also applies to the computer (<c>_isVictoryConditionApplicableForAI</c>).</summary>
    /// <remarks>Kept as the user set it; <see cref="CreateInfo"/> decides which conditions may honour it.</remarks>
    public bool ComputerAlsoWins { get; set; }

    /// <summary>Whether defeating everyone still counts as a victory (<c>_isNormalVictoryAllowed</c>).</summary>
    public bool AllowNormalVictory { get; set; }

    /// <summary>
    /// Target of <see cref="MapVictoryCondition.CaptureTown"/> and <see cref="MapVictoryCondition.KillHero"/>
    /// (<c>_townToCapture</c> / <c>_heroToKill</c>); a position, see the class remarks.
    /// </summary>
    public Point VictoryTarget
    {
        get => _victoryTarget;
        set => _victoryTarget = value;
    }

    /// <summary>
    /// Artifact of <see cref="MapVictoryCondition.ObtainArtifact"/> (<c>_artifactToFind</c>), the value the header
    /// stores; <see cref="UltimateArtifact"/> means "any ultimate artifact".
    /// </summary>
    /// <remarks>
    /// 参考的 <c>_victoryArtifactId</c> 用 <c>Artifact::EDITOR_ANY_ULTIMATE_ARTIFACT</c>（一个只存在于编辑器里的
    /// 伪 id）表示这一支，写文件时换成 0（<c>:985-987</c>）。神器选择窗还没移植，所以这里直接存文件里的值；
    /// 选择窗落地后把它的伪 id 映射到 <see cref="UltimateArtifact"/> 即可，写回不用改。
    /// </remarks>
    public int VictoryArtifact
    {
        get => _victoryArtifact;
        set => _victoryArtifact = Math.Clamp(value, 0, MaxConditionParameter);
    }

    /// <summary>
    /// Gold of <see cref="MapVictoryCondition.CollectGold"/> in thousands (<c>_goldAccumulationValue</c>). The
    /// reference works in pieces; the MP2 parameter is thousands, so <see cref="MinVictoryGoldInThousands"/> is its
    /// minimum of 10 000 pieces.
    /// </summary>
    public int VictoryGoldInThousands
    {
        get => _victoryGold;
        set => _victoryGold = Math.Clamp(value, 0, MaxConditionParameter);
    }

    /// <summary>
    /// How many of <see cref="AvailablePlayers"/> fight on the first side of
    /// <see cref="MapVictoryCondition.DefeatOtherSide"/>, the count the MP2 header stores.
    /// </summary>
    /// <remarks>
    /// 参考的两个联盟各是一个复选框面板，可以随意挑颜色；MP2 只存「前几个」，读文件时按颜色顺序把前 N 个算作第一方。
    /// 所以这里只留分界点：<see cref="VictorySide1"/> 是前 N 个，<see cref="VictorySide2"/> 是其余。
    /// </remarks>
    public int VictorySideCount
    {
        get => _victorySideCount;
        set => _victorySideCount = Math.Clamp(value, 1, MaxSideCount);
    }

    /// <summary>Colours of the first side of <see cref="MapVictoryCondition.DefeatOtherSide"/>.</summary>
    public PlayerColor VictorySide1
    {
        get
        {
            IReadOnlyList<PlayerColor> available = PlayerColors.ToList(AvailablePlayers);
            PlayerColor side1 = PlayerColor.None;
            for (int index = 0; index < available.Count && index < _victorySideCount; ++index)
            {
                side1 |= available[index];
            }

            return side1;
        }
    }

    /// <summary>Colours of the second side of <see cref="MapVictoryCondition.DefeatOtherSide"/>.</summary>
    public PlayerColor VictorySide2 => AvailablePlayers & ~VictorySide1;

    /// <summary>
    /// Target of <see cref="MapLossCondition.LoseTown"/> and <see cref="MapLossCondition.LoseHero"/>
    /// (<c>_townToLose</c> / <c>_heroToLose</c>).
    /// </summary>
    public Point LossTarget
    {
        get => _lossTarget;
        set => _lossTarget = value;
    }

    /// <summary>Days of <see cref="MapLossCondition.OutOfTime"/> (<c>_outOfTimeValue</c>), stored as the MP2 parameter.</summary>
    public int LossDays
    {
        get => _lossDays;
        set => _lossDays = Math.Clamp(value, 0, MaxConditionParameter);
    }

    /// <summary>Towns on the map in table order, the candidates of both town conditions.</summary>
    public IReadOnlyList<Mp2CastlePosition> Towns => _towns;

    /// <summary>All heroes on the map in tile order.</summary>
    public IReadOnlyList<Mp2MapHero> Heroes => _heroes;

    /// <summary>Heroes only the computer plays, the candidates of <see cref="MapVictoryCondition.KillHero"/>.</summary>
    public IReadOnlyList<Mp2MapHero> VictoryHeroes => GetHeroes(ComputerOnlyPlayers);

    /// <summary>Heroes only a human plays, the candidates of <see cref="MapLossCondition.LoseHero"/>.</summary>
    public IReadOnlyList<Mp2MapHero> LossHeroes => GetHeroes(HumanOnlyPlayers);

    /// <summary>The largest useful <see cref="VictorySideCount"/>: every available colour but one.</summary>
    public int MaxSideCount => Math.Max(1, PlayerColors.Count(AvailablePlayers) - 1);

    /// <summary>Whether <paramref name="color"/> has a kingdom on the map.</summary>
    public bool IsAvailable(PlayerColor color) => (AvailablePlayers & color) != 0;

    /// <summary>Whether a human may pick <paramref name="color"/>.</summary>
    public bool IsHumanPlayer(PlayerColor color) => (_humanPlayers & color) != 0;

    /// <summary>Whether the computer may pick <paramref name="color"/>.</summary>
    public bool IsComputerPlayer(PlayerColor color) => (_computerPlayers & color) != 0;

    /// <summary>Heroes of <paramref name="colors"/>, in tile order.</summary>
    public IReadOnlyList<Mp2MapHero> GetHeroes(PlayerColor colors) =>
        [.. _heroes.Where(hero => (colors & hero.Color) != 0)];

    /// <summary>Cycles one colour through the three states its check box can have, <c>:2417-2443</c>.</summary>
    /// <remarks>
    /// 人类与电脑都能选时点一下摘掉电脑；只有人类能选时，只有不止一个人类颜色才把这个颜色让给电脑（否则没人能玩）；
    /// 其余情况（只有电脑、或者谁也不选）一律变成「人类能选」。点完立刻校验两个条件，和参考在同一个循环里做的事一样。
    /// </remarks>
    /// <param name="color">Exactly one playable colour.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="color"/> is not exactly one playable colour.</exception>
    public void TogglePlayerColor(PlayerColor color)
    {
        if (PlayerColors.Count(color) != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(color), color, "A player colour must be exactly one playable colour.");
        }

        if (!IsAvailable(color))
        {
            return;
        }

        bool human = (_humanPlayers & color) != 0;
        bool computer = (_computerPlayers & color) != 0;

        if (human && computer)
        {
            _computerPlayers &= ~color;
        }
        else if (human)
        {
            if (PlayerColors.Count(_humanPlayers) > 1)
            {
                _humanPlayers &= ~color;
                _computerPlayers |= color;
            }
        }
        else
        {
            _humanPlayers |= color;
        }

        _ = UpdateVictoryCondition();
        _ = UpdateLossCondition();
    }

    /// <summary>Repairs the victory condition, <c>VictoryConditionUI::updateCondition()</c> (<c>:879-935</c>).</summary>
    /// <remarks>
    /// 与参考的一处小差别：参考在「换成第一个候选」时仍回 <c>false</c>（只在改条件类型时回 <c>true</c>），这里只要有
    /// 东西被改就回 <c>true</c>，因为调用方拿它决定要不要重画。
    /// </remarks>
    /// <returns>Whether the condition or its target had to be changed.</returns>
    public bool UpdateVictoryCondition()
    {
        switch (_victoryCondition)
        {
        case MapVictoryCondition.CaptureTown:
            if (_towns.Count == 0)
            {
                _victoryCondition = MapVictoryCondition.DefeatEveryone;
                return true;
            }

            if (Contains(_victoryTarget, _towns.Select(town => (town.X, town.Y))))
            {
                return false;
            }

            _victoryTarget = new Point(_towns[0].X, _towns[0].Y);
            return true;

        case MapVictoryCondition.KillHero:
        {
            IReadOnlyList<Mp2MapHero> heroes = VictoryHeroes;
            if (heroes.Count == 0)
            {
                _victoryCondition = MapVictoryCondition.DefeatEveryone;
                return true;
            }

            if (Contains(_victoryTarget, heroes.Select(hero => (hero.X, hero.Y))))
            {
                return false;
            }

            _victoryTarget = new Point(heroes[0].X, heroes[0].Y);
            return true;
        }

        case MapVictoryCondition.DefeatOtherSide:
        {
            int available = PlayerColors.Count(AvailablePlayers);
            if (available < 2)
            {
                _victoryCondition = MapVictoryCondition.DefeatEveryone;
                return true;
            }

            int clamped = Math.Clamp(_victorySideCount, 1, available - 1);
            if (clamped == _victorySideCount)
            {
                return false;
            }

            _victorySideCount = clamped;
            return true;
        }

        default:
            return false;
        }
    }

    /// <summary>
    /// Repairs the loss condition, <c>LossConditionUI::updateCondition()</c> (<c>:1682-1720</c>); the mirror of
    /// <see cref="UpdateVictoryCondition"/>.
    /// </summary>
    /// <returns>Whether the condition or its target had to be changed.</returns>
    public bool UpdateLossCondition()
    {
        switch (_lossCondition)
        {
        case MapLossCondition.LoseTown:
            if (_towns.Count == 0)
            {
                _lossCondition = MapLossCondition.Everything;
                return true;
            }

            if (Contains(_lossTarget, _towns.Select(town => (town.X, town.Y))))
            {
                return false;
            }

            _lossTarget = new Point(_towns[0].X, _towns[0].Y);
            return true;

        case MapLossCondition.LoseHero:
        {
            IReadOnlyList<Mp2MapHero> heroes = LossHeroes;
            if (heroes.Count == 0)
            {
                _lossCondition = MapLossCondition.Everything;
                return true;
            }

            if (Contains(_lossTarget, heroes.Select(hero => (hero.X, hero.Y))))
            {
                return false;
            }

            _lossTarget = new Point(heroes[0].X, heroes[0].Y);
            return true;
        }

        default:
            return false;
        }
    }

    /// <summary>
    /// The header this state stands for, the write back of the 「确定」 branch (<c>:2470-2520</c>). Install it
    /// with <see cref="MapEditSession.ApplyMapInfo"/> so the next save writes it too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只改建模过的字段：名称、描述、难度、三种颜色、两个条件与它们的参数、两个胜利开关。头部里剩下的字节
    /// （<see cref="Mp2FileInfo.HeaderBytes"/>）原样带过去。
    /// </para>
    /// <para>
    /// 两个胜利开关按条件落值（<c>getConditionMetadata</c>，<c>:949-998</c>）：只有「击败所有敌人」把它们都关掉；
    /// 「击败某个英雄」「击败另一方」的 AI 位恒关；「占领城镇」的 AI 位也关（见类注释）；其余两个条件照用户的勾选写。
    /// </para>
    /// <para>
    /// 生成前先跑一遍 <see cref="UpdateVictoryCondition"/> / <see cref="UpdateLossCondition"/>，写出去的头部不会
    /// 指向一座已经不在的城。参考在确定时不再校验，是因为它在每次点击里都已经校验过了。
    /// </para>
    /// </remarks>
    /// <returns>A new header; the original one is not modified.</returns>
    public Mp2FileInfo CreateInfo()
    {
        _ = UpdateVictoryCondition();
        _ = UpdateLossCondition();

        int[] victoryParameters;
        bool computerAlsoWins;
        bool allowNormalVictory;
        PlayerColor[] unions;

        switch (_victoryCondition)
        {
        case MapVictoryCondition.CaptureTown:
            victoryParameters = [ClampParameter(_victoryTarget.X), ClampParameter(_victoryTarget.Y)];
            computerAlsoWins = false;
            allowNormalVictory = AllowNormalVictory;
            unions = [.. PlayerColors.Ordered];
            break;

        case MapVictoryCondition.KillHero:
            victoryParameters = [ClampParameter(_victoryTarget.X), ClampParameter(_victoryTarget.Y)];
            computerAlsoWins = false;
            allowNormalVictory = false;
            unions = [.. PlayerColors.Ordered];
            break;

        case MapVictoryCondition.ObtainArtifact:
            victoryParameters = [_victoryArtifact, 0];
            computerAlsoWins = false;
            allowNormalVictory = AllowNormalVictory;
            unions = [.. PlayerColors.Ordered];
            break;

        case MapVictoryCondition.DefeatOtherSide:
            victoryParameters = [Math.Clamp(_victorySideCount, 1, MaxSideCount), 0];
            computerAlsoWins = false;
            allowNormalVictory = false;
            unions = Mp2FileInfo.FillUnions(VictorySide1, VictorySide2);
            break;

        case MapVictoryCondition.CollectGold:
            victoryParameters = [_victoryGold, 0];
            computerAlsoWins = ComputerAlsoWins;
            allowNormalVictory = AllowNormalVictory;
            unions = [.. PlayerColors.Ordered];
            break;

        default:
            victoryParameters = [0, 0];
            computerAlsoWins = false;
            allowNormalVictory = false;
            unions = [.. PlayerColors.Ordered];
            break;
        }

        int[] lossParameters = _lossCondition switch
        {
            MapLossCondition.LoseTown => [ClampParameter(_lossTarget.X), ClampParameter(_lossTarget.Y)],
            MapLossCondition.LoseHero => [ClampParameter(_lossTarget.X), ClampParameter(_lossTarget.Y)],
            MapLossCondition.OutOfTime => [_lossDays, 0],
            _ => [0, 0],
        };

        return _original with
        {
            Name = Name,
            Description = Description,
            CreatorNotes = CreatorNotes,
            Difficulty = _difficulty,
            KingdomColors = AvailablePlayers,
            HumanPlayers = _humanPlayers,
            ComputerPlayers = _computerPlayers,
            VictoryCondition = _victoryCondition,
            VictoryConditionParameters = victoryParameters,
            ComputerAlsoWins = computerAlsoWins,
            AllowNormalVictory = allowNormalVictory,
            LossCondition = _lossCondition,
            LossConditionParameters = lossParameters,
            PlayerUnions = unions,
        };
    }

    private static bool Contains(Point point, IEnumerable<(int X, int Y)> positions) =>
        positions.Any(position => position.X == point.X && position.Y == point.Y);

    private static int ClampParameter(int value) => Math.Clamp(value, 0, MaxConditionParameter);

    private static GameDifficulty ClampDifficulty(GameDifficulty difficulty) =>
        difficulty switch
        {
            GameDifficulty.Easy => GameDifficulty.Easy,
            GameDifficulty.Normal => GameDifficulty.Normal,
            GameDifficulty.Hard => GameDifficulty.Hard,
            _ => GameDifficulty.Expert,
        };

    private static int ReadParameter(IReadOnlyList<int> parameters, int index) =>
        index < parameters.Count ? parameters[index] : 0;

    private static Point ReadPoint(IReadOnlyList<int> parameters) =>
        new(ReadParameter(parameters, 0), ReadParameter(parameters, 1));

    private int DefaultSideCount() => Math.Max(1, PlayerColors.Count(AvailablePlayers) / 2);
}
