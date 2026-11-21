using FHeroes2.Core.Io;

namespace FHeroes2.Game.Highscores;

/// <summary>
/// One row of the high score table, <c>fheroes2::HighscoreData</c> (<c>src/fheroes2/game/highscores.h:36-86</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把一局的成绩存成「谁、哪张图、什么时候打完、用了多少天、评分、地图种子」六项，其中玩家名与完成时间只是给人看的，
/// 去重靠后四项——同一张图、同样天数、同样评分、同样种子必然是同一次通关，换了个名字登录不算新纪录。
/// </para>
/// <para>
/// 语言缩写是记录写入时所处界面语言的两位代码（<c>"en"</c>、<c>"de"</c>…），读回来时用来判断这条成绩要不要换个字体
/// 显示。端口没有翻译层（ADR-172），写出去的一律是英文代号，读回来的值原样保留，不会被改写。
/// </para>
/// </remarks>
public sealed class HighscoreData
{
    /// <summary>
    /// The abbreviation written into the language field, <c>fheroes2::getLanguageAbbreviation( English )</c>.
    /// </summary>
    /// <remarks>
    /// 参考写的是当前界面语言；端口只有英文资材与英文文本，所以固定写英文，见类注释里关于 ADR-172 的说明。
    /// </remarks>
    public const string EnglishLanguageAbbreviation = "en";

    /// <summary>Creates an empty row, the reference's default constructor.</summary>
    public HighscoreData()
    {
    }

    /// <summary>
    /// Creates a row with every field given, the reference's six-argument constructor
    /// (<c>highscores.h:40-51</c>).
    /// </summary>
    /// <param name="languageAbbreviation">The language the game was in, <c>languageAbbreviation</c>.</param>
    /// <param name="playerName">The name the player typed, <c>playerName</c>.</param>
    /// <param name="scenarioName">The map or campaign name, <c>scenarioName</c>.</param>
    /// <param name="completionTime">When the game ended, in seconds since the epoch, <c>completionTime</c>.</param>
    /// <param name="dayCount">How many days the game lasted, <c>dayCount</c>.</param>
    /// <param name="rating">The score of the game, <c>rating</c>.</param>
    /// <param name="mapSeed">The seed of the map, <c>mapSeed</c>.</param>
    public HighscoreData(
        string languageAbbreviation,
        string playerName,
        string scenarioName,
        uint completionTime,
        uint dayCount,
        uint rating,
        uint mapSeed)
    {
        LanguageAbbreviation = languageAbbreviation;
        PlayerName = playerName;
        ScenarioName = scenarioName;
        CompletionTime = completionTime;
        DayCount = dayCount;
        Rating = rating;
        MapSeed = mapSeed;
    }

    /// <summary>The language the interface was in, <c>languageAbbreviation</c>.</summary>
    public string LanguageAbbreviation { get; set; } = string.Empty;

    /// <summary>The name the player typed, <c>playerName</c>.</summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>The name of the map or the campaign, <c>scenarioName</c>.</summary>
    public string ScenarioName { get; set; } = string.Empty;

    /// <summary>When the game ended, in seconds since the epoch, <c>completionTime</c>.</summary>
    public uint CompletionTime { get; set; }

    /// <summary>How many days the game lasted, <c>dayCount</c>.</summary>
    public uint DayCount { get; set; }

    /// <summary>
    /// The score, <c>rating</c>. Campaigns use the day count instead until the difficulty of the campaign was
    /// introduced, hence the fixing up in <see cref="HighScoreDataContainer.Load"/>.
    /// </summary>
    public uint Rating { get; set; }

    /// <summary>The seed of the map, <c>mapSeed</c>. It identifies a completion, so a reloaded game does not add a row.</summary>
    public uint MapSeed { get; set; }

    /// <summary>
    /// The current time, <c>HighscoreData::generateCompletionTime()</c> (<c>highscores.cpp:109-112</c>).
    /// </summary>
    /// <returns>Seconds since the Unix epoch.</returns>
    public static uint GenerateCompletionTime() => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// Reads the oldest layout, which has no language field,
    /// <c>HighscoreData::loadV1()</c> (<c>highscores.cpp:114-119</c>).
    /// </summary>
    /// <param name="stream">The decompressed high score file.</param>
    /// <remarks>
    /// 第一版把语言字段省了，读的时候补英文——那会儿界面只有英文。
    /// </remarks>
    public void LoadV1(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        LanguageAbbreviation = EnglishLanguageAbbreviation;
        PlayerName = stream.ReadString();
        ScenarioName = stream.ReadString();
        CompletionTime = stream.ReadUInt32();
        DayCount = stream.ReadUInt32();
        Rating = stream.ReadUInt32();
        MapSeed = stream.ReadUInt32();
    }

    /// <summary>
    /// Writes the row, <c>operator&lt;&lt;( OStreamBase &amp;, const HighscoreData &amp; )</c> (<c>highscores.cpp:121-124</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(LanguageAbbreviation);
        stream.Write(PlayerName);
        stream.Write(ScenarioName);
        stream.Write(CompletionTime);
        stream.Write(DayCount);
        stream.Write(Rating);
        stream.Write(MapSeed);
    }

    /// <summary>
    /// Reads the row, <c>operator&gt;&gt;( IStreamBase &amp;, HighscoreData &amp; )</c> (<c>highscores.cpp:126-129</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The row, zero valued when the stream ran out.</returns>
    public static HighscoreData Read(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new HighscoreData
        {
            LanguageAbbreviation = stream.ReadString(),
            PlayerName = stream.ReadString(),
            ScenarioName = stream.ReadString(),
            CompletionTime = stream.ReadUInt32(),
            DayCount = stream.ReadUInt32(),
            Rating = stream.ReadUInt32(),
            MapSeed = stream.ReadUInt32(),
        };
    }

    /// <summary>
    /// Whether two rows describe the same completion, <c>HighscoreData::operator==</c> (<c>highscores.h:61-65</c>).
    /// </summary>
    /// <param name="other">The row to compare with.</param>
    /// <returns>True when the map, the day count, the rating and the seed all match.</returns>
    /// <remarks>玩家名与完成时间明确不参与比较：同一次通关换个名字不该占两行。</remarks>
    public bool SameCompletion(HighscoreData? other) =>
        other is not null
        && ScenarioName == other.ScenarioName
        && DayCount == other.DayCount
        && Rating == other.Rating
        && MapSeed == other.MapSeed;

    /// <summary>The row as the reference prints it in a debug log.</summary>
    /// <returns>The player name, the map and the score.</returns>
    public override string ToString() => $"{PlayerName} / {ScenarioName} / {DayCount} days / {Rating}";
}
