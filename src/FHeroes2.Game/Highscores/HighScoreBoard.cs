using FHeroes2.Assets.Maps;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Highscores;

/// <summary>Which of the two tables is on screen, the <c>isCampaign</c> flag of the reference.</summary>
public enum HighScoreMode
{
    /// <summary>The table of finished single maps, <c>GameMode::HIGHSCORES_STANDARD</c>.</summary>
    Standard,

    /// <summary>The table of finished campaigns, <c>GameMode::HIGHSCORES_CAMPAIGN</c>.</summary>
    Campaign,
}

/// <summary>
/// One high score screen session: the two tables of <see cref="HighScoreDataContainer"/>, which of them is shown, and the
/// steps of <c>Game::DisplayHighScores()</c> that are not drawing
/// (<c>game_highscores.cpp:245-476</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把容器当文件级静态量（<c>highScoreDataContainer</c>），只在 <c>!isInternalUpdate</c> 时读一次文件，然后在循环里
/// 换表重画。端口把「读一次、换表、登记、存盘」这四件事收进这个类：界面层只管画，宿主只管把输入送进来。
/// </para>
/// <para>
/// <b>评分沿用参考的两条公式</b>：单张地图是 <c>Game::GetRating() * getGameOverScoreFactor() / 100</c>，战役是
/// <c>daysPassed * getCampaignDifficultyPercent() / 100</c>。两个乘数都是纯函数，测试可以直接钉住边界。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：参考只拿最后一行比较，不额外要求评分大于 0；端口照抄同一个比较，不加参考没有的条件。
/// </para>
/// </remarks>
public sealed class HighScoreBoard
{
    /// <summary>What the reference writes when the player confirms an empty name, <c>_( "Unknown Hero" )</c>.</summary>
    public const string DefaultPlayerName = "Unknown Hero";

    /// <summary>How many characters the name dialog takes, the <c>15</c> of <c>game_highscores.cpp:290</c>.</summary>
    public const int PlayerNameLength = 15;

    /// <summary>How many rows a table keeps, <c>highscoreMaximumEntries</c>.</summary>
    public const int MaximumEntries = 10;

    private readonly HighScoreDataContainer _container;
    private readonly string _fileName;

    /// <summary>Creates a session over <paramref name="fileName"/>.</summary>
    /// <param name="fileName">
    /// The file to read and write, normally <c>fheroes2.hgs</c> in <see cref="GameSave.GetSaveDir"/>; omitted for that path.
    /// </param>
    /// <param name="container">The tables to use, or a fresh one.</param>
    /// <remarks>
    /// 文件名在构造时定下来，是为了让测试可以指向临时目录：默认值走 <see cref="GameSave.GetSaveDir"/>，那会读设置文件。
    /// </remarks>
    public HighScoreBoard(string? fileName = null, HighScoreDataContainer? container = null)
    {
        _fileName = fileName ?? Path.Combine(GameSave.GetSaveDir(), HighScoreDataContainer.FileName);
        _container = container ?? new HighScoreDataContainer();
    }

    /// <summary>The tables this session reads and writes, <c>highScoreDataContainer</c>.</summary>
    public HighScoreDataContainer Container => _container;

    /// <summary>The file the tables live in.</summary>
    public string FileName => _fileName;

    /// <summary>The table on screen, the <c>isCampaign</c> argument of <c>openHighScores()</c>.</summary>
    public HighScoreMode Mode { get; set; } = HighScoreMode.Standard;

    /// <summary>The rows of the table on screen, <c>getHighScoresStandard()</c> or <c>getHighScoresCampaign()</c>.</summary>
    public IReadOnlyList<HighscoreData> Entries =>
        Mode == HighScoreMode.Campaign ? _container.HighScoresCampaign : _container.HighScoresStandard;

    /// <summary>
    /// Whether the file was read already, the <c>isInternalUpdate</c> flag of the reference's loop.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Reads the file once, <c>game_highscores.cpp:267-276</c>: a file that is missing or unreadable is replaced by the two
    /// default tables, and the next call does not try again.
    /// </summary>
    /// <returns><see langword="true"/> when the file itself was read.</returns>
    public bool Load()
    {
        if (IsLoaded)
        {
            return true;
        }

        IsLoaded = true;

        if (_container.Load(_fileName))
        {
            return true;
        }

        // 读不出来（第一次玩、文件坏了）就用默认榜，参考在这里 clear() 加两张默认表。
        PopulateDefaults();

        return false;
    }

    /// <summary>Empties both tables and fills them with the ten sample rows each, <c>:271-275</c>.</summary>
    public void PopulateDefaults()
    {
        _container.Clear();
        _container.PopulateStandardDefaultHighScores();
        _container.PopulateCampaignDefaultHighScores();
    }

    /// <summary>Writes both tables, the <c>highScoreDataContainer.save()</c> of <c>:340-341</c>.</summary>
    /// <returns><see langword="true"/> when the file was written completely.</returns>
    public bool Save() => _container.Save(_fileName);

    /// <summary>
    /// Whether a score still makes the table on screen, the guard of <c>:309-330</c>.
    /// </summary>
    /// <param name="rating">The score of the finished game.</param>
    /// <returns><see langword="true"/> when the score has to be registered.</returns>
    /// <remarks>
    /// 标准榜按分数降序，最后一行是下限，所以 <c>rating &gt;= back.rating</c> 才算上榜；战役榜按天数升序，最后一行是上限，
    /// 所以 <c>rating &lt;= back.rating</c> 才算。方向不同是参考的写法，不要统一。
    /// </remarks>
    public bool Accepts(uint rating)
    {
        IReadOnlyList<HighscoreData> entries = Entries;

        if (entries.Count == 0)
        {
            return true;
        }

        uint boundary = entries[^1].Rating;

        return Mode == HighScoreMode.Campaign ? rating <= boundary : rating >= boundary;
    }

    /// <summary>
    /// Places a finished game in the table on screen and answers where it landed, the
    /// <c>registerScoreStandard()</c> / <c>registerScoreCampaign()</c> of <c>:317-337</c>.
    /// </summary>
    /// <param name="scenarioName">The map or campaign name, <c>conf.getCurrentMapInfo().name</c> or the campaign name.</param>
    /// <param name="playerName">The name the player typed; blank becomes <see cref="DefaultPlayerName"/>.</param>
    /// <param name="dayCount">How many days the game lasted.</param>
    /// <param name="rating">The score, from <see cref="GetStandardRating"/> or <see cref="GetCampaignRating"/>.</param>
    /// <param name="mapSeed">The seed of the map, <c>world.GetMapSeed()</c>.</param>
    /// <param name="completionTime">When the game ended; the current time when omitted.</param>
    /// <returns>The row the game took, or <c>-1</c> when it fell off the table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="scenarioName"/> is <see langword="null"/>.</exception>
    public int Register(
        string scenarioName,
        string playerName,
        uint dayCount,
        uint rating,
        uint mapSeed,
        uint? completionTime = null)
    {
        ArgumentNullException.ThrowIfNull(scenarioName);

        HighscoreData data = new(
            HighscoreData.EnglishLanguageAbbreviation,
            NormalizePlayerName(playerName),
            scenarioName,
            completionTime ?? HighscoreData.GenerateCompletionTime(),
            dayCount,
            rating,
            mapSeed);

        return Mode == HighScoreMode.Campaign
            ? _container.RegisterScoreCampaign(data)
            : _container.RegisterScoreStandard(data);
    }

    /// <summary>
    /// The name written into a row, <c>game_highscores.cpp:289-294</c>.
    /// </summary>
    /// <param name="playerName">The typed name, possibly blank.</param>
    /// <returns>The name to store.</returns>
    /// <remarks>
    /// 参考只判 <c>empty()</c>：取消输入框之后变量仍是空串，于是写成 <c>Unknown Hero</c>。端口不额外 trim 或截断，
    /// 长度上限由输入框负责（<see cref="PlayerNameLength"/>）。
    /// </remarks>
    public static string NormalizePlayerName(string? playerName) =>
        string.IsNullOrEmpty(playerName) ? DefaultPlayerName : playerName;

    /// <summary>
    /// The size-and-time multiplier of a standard map score, <c>Game::getGameOverScoreFactor()</c>
    /// (<c>game.cpp:397-439</c>).
    /// </summary>
    /// <param name="size">The size class of the map, <c>Settings::Get().getCurrentMapInfo().width</c>.</param>
    /// <param name="dayCount">The days the game lasted, <c>world.CountDay()</c>.</param>
    /// <returns>140 (small) down to 20 (very slow game), the factor the map rating is multiplied by.</returns>
    /// <remarks>
    /// 小地图系数大，拖得越久掉得越多：六十天以内原样扣，之后分段放缓，六百天以上一律扣 180。未知尺寸的系数是 0，
    /// 于是 <c>daysFactor</c> 也是 0，结果 200——参考的 <c>default</c> 分支就是这个效果。
    /// </remarks>
    public static uint GetGameOverScoreFactor(MapSize size, uint dayCount)
    {
        uint mapSizeFactor = size switch
        {
            MapSize.Small => 140u,
            MapSize.Medium => 100u,
            MapSize.Large => 80u,
            MapSize.ExtraLarge => 60u,
            _ => 0u,
        };

        uint daysFactor = dayCount * mapSizeFactor / 100;

        uint daysScore = daysFactor switch
        {
            <= 60 => daysFactor,
            <= 120 => (daysFactor / 2) + 30,
            <= 360 => (daysFactor / 4) + 60,
            <= 600 => (daysFactor / 8) + 105,
            _ => 180,
        };

        return 200 - daysScore;
    }

    /// <summary>
    /// The score of a finished single map, <c>Game::GetRating() * Game::getGameOverScoreFactor() / 100</c>
    /// (<c>game_highscores.cpp:322</c>).
    /// </summary>
    /// <param name="info">The map information, for its own difficulty and its size.</param>
    /// <param name="difficulty">The difficulty the player picked.</param>
    /// <param name="dayCount">The days the game lasted.</param>
    /// <returns>The score.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public static uint GetStandardRating(Mp2FileInfo info, GameDifficulty difficulty, uint dayCount)
    {
        ArgumentNullException.ThrowIfNull(info);

        return GetStandardRating(
            GameRating.GetRating(info, difficulty), MapSizes.FromWidth(info.Width), dayCount);
    }

    /// <summary>
    /// The same product without the map block, so a test can drive it with a score and a size alone.
    /// </summary>
    /// <param name="mapRating">The scenario rating, <c>Game::GetRating()</c>.</param>
    /// <param name="size">The size class of the map.</param>
    /// <param name="dayCount">The days the game lasted.</param>
    /// <returns>The score.</returns>
    public static uint GetStandardRating(uint mapRating, MapSize size, uint dayCount) =>
        mapRating * GetGameOverScoreFactor(size, dayCount) / 100;

    /// <summary>
    /// The score of a finished campaign, <c>daysPassed * getCampaignDifficultyPercent() / 100</c>
    /// (<c>game_highscores.cpp:302-304</c>).
    /// </summary>
    /// <param name="saveData">The campaign state, for the days and the hardest difficulty.</param>
    /// <returns>The score.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="saveData"/> is <see langword="null"/>.</exception>
    public static uint GetCampaignRating(CampaignSaveData saveData)
    {
        ArgumentNullException.ThrowIfNull(saveData);

        return GetCampaignRating(saveData.DaysPassed, saveData.GetCampaignDifficultyPercent());
    }

    /// <summary>The campaign product itself, <c>daysPassed * percent / 100</c>.</summary>
    /// <param name="daysPassed">The days the whole campaign took.</param>
    /// <param name="difficultyPercent">125, 100 or 75 for a decreased, default or increased campaign.</param>
    /// <returns>The score.</returns>
    public static uint GetCampaignRating(uint daysPassed, uint difficultyPercent) =>
        daysPassed * difficultyPercent / 100;
}
