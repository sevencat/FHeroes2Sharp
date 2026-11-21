using System.Diagnostics;

using FHeroes2.Core.Io;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Highscores;

/// <summary>
/// The two high score tables of the game, <c>fheroes2::HighScoreDataContainer</c>
/// (<c>src/fheroes2/game/highscores.h:88-122</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把标准战役（单张地图）与战役两种成绩各存一张表，每张最多十条，读写都走同一个压缩文件
/// <c>fheroes2.hgs</c>。表里一行的「怪兽」不是玩法数据，是给高分榜排名的插图：分数越高画得越强的兵种，用
/// <see cref="GetMonsterByRating"/> 反查。
/// </para>
/// <para>
/// 文件格式有两个版本：第一版没有语言字段、也没有魔数之外的头部；第二版给每行加了语言缩写。读的时候看魔数区分，写的
/// 时候只写第二版——参考也是这么做的。
/// </para>
/// </remarks>
public sealed class HighScoreDataContainer
{
    /// <summary>The name of the file in the save directory, <c>highScoreFileName</c> (<c>game_highscores.cpp:78</c>).</summary>
    public const string FileName = "fheroes2.hgs";

    /// <summary>The magic number of the first layout, <c>highscoreFileMagicValueV1</c>.</summary>
    private const uint MagicValueV1 = 0xBADC0DE;

    /// <summary>The magic number of the layout written today, <c>highscoreFileMagicValueV2</c>.</summary>
    private const uint MagicValueV2 = 0xBADC1DE;

    /// <summary>How many rows a table keeps, <c>highscoreMaximumEntries</c>.</summary>
    private const int MaximumEntries = 10;

    /// <summary>
    /// The order in which monsters illustrate a score, <c>monstersInRanking</c> (<c>highscores.cpp:43-75</c>).
    /// </summary>
    /// <remarks>
    /// 六十六个兵种按「分数从低到高」排列，相邻条目之间的分差由 <see cref="GetMonsterByRating"/> 里的步长决定：农民占
    /// 0 到 3 分，妖精 4 到 131 分……黑龙从 228 分起。
    /// </remarks>
    private static readonly MonsterType[] MonstersInRanking =
    [
        MonsterType.Peasant,
        MonsterType.Goblin,
        MonsterType.Sprite,
        MonsterType.Halfling,
        MonsterType.Centaur,
        MonsterType.Rogue,
        MonsterType.Skeleton,
        MonsterType.Orc,
        MonsterType.Zombie,
        MonsterType.Archer,
        MonsterType.Ranger,
        MonsterType.Boar,
        MonsterType.Dwarf,
        MonsterType.MutantZombie,
        MonsterType.OrcChief,
        MonsterType.Elf,
        MonsterType.Gargoyle,
        MonsterType.Pikeman,
        MonsterType.GrandElf,
        MonsterType.BattleDwarf,
        MonsterType.Nomad,
        MonsterType.VeteranPikeman,
        MonsterType.Wolf,
        MonsterType.Mummy,
        MonsterType.IronGolem,
        MonsterType.RoyalMummy,
        MonsterType.Ogre,
        MonsterType.Griffin,
        MonsterType.Swordsman,
        MonsterType.Druid,
        MonsterType.SteelGolem,
        MonsterType.MasterSwordsman,
        MonsterType.AirElement,
        MonsterType.GreaterDruid,
        MonsterType.FireElement,
        MonsterType.Ghost,
        MonsterType.Vampire,
        MonsterType.WaterElement,
        MonsterType.EarthElement,
        MonsterType.Roc,
        MonsterType.Minotaur,
        MonsterType.Cavalry,
        MonsterType.Troll,
        MonsterType.Mage,
        MonsterType.Medusa,
        MonsterType.Lich,
        MonsterType.OgreLord,
        MonsterType.MinotaurKing,
        MonsterType.Champion,
        MonsterType.WarTroll,
        MonsterType.VampireLord,
        MonsterType.Archmage,
        MonsterType.PowerLich,
        MonsterType.Unicorn,
        MonsterType.Hydra,
        MonsterType.Paladin,
        MonsterType.Genie,
        MonsterType.Crusader,
        MonsterType.Cyclops,
        MonsterType.Giant,
        MonsterType.Phoenix,
        MonsterType.BoneDragon,
        MonsterType.GreenDragon,
        MonsterType.RedDragon,
        MonsterType.Titan,
        MonsterType.BlackDragon,
    ];

    /// <summary>The rows of finished single maps, <c>_highScoresStandard</c>.</summary>
    private readonly List<HighscoreData> _highScoresStandard = [];

    /// <summary>The rows of finished campaigns, <c>_highScoresCampaign</c>.</summary>
    private readonly List<HighscoreData> _highScoresCampaign = [];

    /// <summary>The rows of single maps, <c>getHighScoresStandard()</c>.</summary>
    public IReadOnlyList<HighscoreData> HighScoresStandard => _highScoresStandard;

    /// <summary>The rows of campaigns, <c>getHighScoresCampaign()</c>.</summary>
    public IReadOnlyList<HighscoreData> HighScoresCampaign => _highScoresCampaign;

    /// <summary>
    /// Places a finished single map in the table, <c>HighScoreDataContainer::registerScoreStandard()</c>
    /// (<c>highscores.cpp:219-222</c>).
    /// </summary>
    /// <param name="data">The row.</param>
    /// <returns>The place of the row in the table, or -1 when it did not make it.</returns>
    public int RegisterScoreStandard(HighscoreData data) => SaveEntry(data, _highScoresStandard, isCampaign: false);

    /// <summary>
    /// Places a finished campaign in the table, <c>HighScoreDataContainer::registerScoreCampaign()</c>
    /// (<c>highscores.cpp:224-227</c>).
    /// </summary>
    /// <param name="data">The row.</param>
    /// <returns>The place of the row in the table, or -1 when it did not make it.</returns>
    public int RegisterScoreCampaign(HighscoreData data) => SaveEntry(data, _highScoresCampaign, isCampaign: true);

    /// <summary>Empties both tables, <c>HighScoreDataContainer::clear()</c>.</summary>
    public void Clear()
    {
        _highScoresStandard.Clear();
        _highScoresCampaign.Clear();
    }

    /// <summary>
    /// Which monster illustrates a score, <c>HighScoreDataContainer::getMonsterByRating()</c>
    /// (<c>highscores.cpp:229-281</c>).
    /// </summary>
    /// <param name="rating">The score.</param>
    /// <returns>The monster.</returns>
    public static MonsterType GetMonsterByRating(uint rating)
    {
        List<(uint Threshold, MonsterType Monster)> table = BuildRatingTable();

        (uint threshold, MonsterType monster) = table[^1];
        if (rating >= threshold)
        {
            return monster;
        }

        foreach ((uint limit, MonsterType candidate) in table)
        {
            if (rating <= limit)
            {
                return candidate;
            }
        }

        Debug.Assert(false, "The rating table has no entry for the score.");

        return MonsterType.Peasant;
    }

    /// <summary>
    /// Which monster illustrates a campaign that lasted a number of days,
    /// <c>HighScoreDataContainer::getMonsterByDay()</c> (<c>highscores.cpp:283-341</c>).
    /// </summary>
    /// <param name="numOfDays">The day count.</param>
    /// <returns>The monster.</returns>
    /// <remarks>
    /// 战役没有分数，排名按天数：打得越快名次越高，所以兵种顺序反过来用——三百天以内是黑龙，五千八百天以上是农民。
    /// </remarks>
    public static MonsterType GetMonsterByDay(uint numOfDays)
    {
        List<(uint Threshold, MonsterType Monster)> table = BuildDayTable();

        (uint threshold, MonsterType monster) = table[^1];
        if (numOfDays >= threshold)
        {
            return monster;
        }

        foreach ((uint limit, MonsterType candidate) in table)
        {
            if (numOfDays <= limit)
            {
                return candidate;
            }
        }

        Debug.Assert(false, "The day table has no entry for the day count.");

        return MonsterType.Peasant;
    }

    /// <summary>
    /// Fills the single-map table with the ten sample rows, <c>HighScoreDataContainer::populateStandardDefaultHighScores()</c>
    /// (<c>highscores.cpp:343-359</c>).
    /// </summary>
    /// <remarks>
    /// 原版游戏的十个人名与地图名写死在参考里；它们的评分与天数刻意互不相同，正好覆盖十个兵种区间。
    /// </remarks>
    public void PopulateStandardDefaultHighScores()
    {
        uint currentTime = HighscoreData.GenerateCompletionTime();
        const string lang = HighscoreData.EnglishLanguageAbbreviation;

        RegisterScoreStandard(new HighscoreData(lang, "Lord Kilburn", "Beltway", currentTime, 70, 150, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Tsabu", "Deathgate", currentTime, 80, 140, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Sir Galant", "Enroth", currentTime, 90, 130, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Thundax", "Lost Continent", currentTime, 100, 120, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Lord Haart", "Mountain King", currentTime, 120, 110, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Ariel", "Pandemonium", currentTime, 140, 100, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Rebecca", "Terra Firma", currentTime, 160, 90, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Sandro", "The Clearing", currentTime, 180, 80, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Crodo", "Vikings!", currentTime, 200, 70, 0));
        RegisterScoreStandard(new HighscoreData(lang, "Barock", "Wastelands", currentTime, 240, 60, 0));
    }

    /// <summary>
    /// Fills the campaign table with the ten sample rows,
    /// <c>HighScoreDataContainer::populateCampaignDefaultHighScores()</c> (<c>highscores.cpp:361-377</c>).
    /// </summary>
    /// <remarks>两个战役交替排列，天数从六百到三千；战役的评分字段在这里与天数相同。</remarks>
    public void PopulateCampaignDefaultHighScores()
    {
        uint currentTime = HighscoreData.GenerateCompletionTime();
        const string lang = HighscoreData.EnglishLanguageAbbreviation;

        RegisterScoreCampaign(new HighscoreData(lang, "Antoine", "Roland", currentTime, 600, 600, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Astra", "Archibald", currentTime, 650, 650, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Agar", "Roland", currentTime, 700, 700, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Vatawna", "Archibald", currentTime, 750, 750, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Vesper", "Roland", currentTime, 800, 800, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Ambrose", "Archibald", currentTime, 850, 850, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Troyan", "Roland", currentTime, 900, 900, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Jojosh", "Archibald", currentTime, 1000, 1000, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Wrathmont", "Roland", currentTime, 2000, 2000, 0));
        RegisterScoreCampaign(new HighscoreData(lang, "Maximus", "Archibald", currentTime, 3000, 3000, 0));
    }

    /// <summary>
    /// Reads both tables, <c>HighScoreDataContainer::load()</c> (<c>highscores.cpp:131-201</c>).
    /// </summary>
    /// <param name="fileName">The file to read, normally <c>fheroes2.hgs</c> in the save directory.</param>
    /// <returns>True when the file was a high score file and was read to the end.</returns>
    /// <remarks>
    /// 表里不够十条就用默认行补齐，超过十条就截断——玩家删了文件或者手动改过也总能得到一张完整的榜。
    /// </remarks>
    public bool Load(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        using StreamFile fileStream = new();
        fileStream.SetBigEndian(true);

        if (!fileStream.OpenForReading(fileName))
        {
            return false;
        }

        StreamBuf hdata = new();
        hdata.SetBigEndian(true);

        if (!Compression.UnzipStream(fileStream, hdata))
        {
            return false;
        }

        uint magicValue = hdata.ReadUInt32();

        if (magicValue != MagicValueV1 && magicValue != MagicValueV2)
        {
            return false;
        }

        if (magicValue == MagicValueV1)
        {
            int size = hdata.ReadInt32();
            _highScoresStandard.Clear();
            for (int i = 0; i < size && !hdata.Fail; ++i)
            {
                HighscoreData data = new();
                data.LoadV1(hdata);
                _highScoresStandard.Add(data);
            }

            size = hdata.ReadInt32();
            _highScoresCampaign.Clear();
            for (int i = 0; i < size && !hdata.Fail; ++i)
            {
                HighscoreData data = new();
                data.LoadV1(hdata);
                _highScoresCampaign.Add(data);
            }
        }
        else
        {
            _highScoresStandard.Clear();
            _highScoresStandard.AddRange(hdata.ReadList(HighscoreData.Read));

            _highScoresCampaign.Clear();
            _highScoresCampaign.AddRange(hdata.ReadList(HighscoreData.Read));
        }

        if (hdata.Fail)
        {
            return false;
        }

        // 战役难度问世之前战役的评分恒为 0，那些旧记录改用天数当评分。
        foreach (HighscoreData data in _highScoresCampaign)
        {
            if (data.Rating == 0)
            {
                data.Rating = data.DayCount;
            }
        }

        if (_highScoresStandard.Count < MaximumEntries)
        {
            PopulateStandardDefaultHighScores();
        }
        else if (_highScoresStandard.Count > MaximumEntries)
        {
            _highScoresStandard.RemoveRange(MaximumEntries, _highScoresStandard.Count - MaximumEntries);
        }

        if (_highScoresCampaign.Count < MaximumEntries)
        {
            PopulateCampaignDefaultHighScores();
        }
        else if (_highScoresCampaign.Count > MaximumEntries)
        {
            _highScoresCampaign.RemoveRange(MaximumEntries, _highScoresCampaign.Count - MaximumEntries);
        }

        return true;
    }

    /// <summary>
    /// Writes both tables, <c>HighScoreDataContainer::save()</c> (<c>highscores.cpp:203-217</c>).
    /// </summary>
    /// <param name="fileName">The file to write, normally <c>fheroes2.hgs</c> in the save directory.</param>
    /// <returns>True when the file was written completely.</returns>
    /// <remarks>写的一律是第二版格式，哪怕读进来的是第一版。</remarks>
    public bool Save(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        using StreamFile fileStream = new();
        fileStream.SetBigEndian(true);

        if (!fileStream.OpenForWriting(fileName))
        {
            return false;
        }

        StreamBuf hdata = new();
        hdata.SetBigEndian(true);

        hdata.Write(MagicValueV2);
        hdata.WriteList(_highScoresStandard, static (stream, data) => data.Write(stream));
        hdata.WriteList(_highScoresCampaign, static (stream, data) => data.Write(stream));

        return !hdata.Fail && Compression.ZipStreamBuf(hdata, fileStream);
    }

    /// <summary>
    /// Puts a row into a table, the anonymous <c>saveHighscoreEntry()</c> of <c>highscores.cpp:77-104</c>.
    /// </summary>
    /// <param name="data">The row.</param>
    /// <param name="entries">The table to change.</param>
    /// <param name="isCampaign">Whether the table is the campaign one, which sorts the other way.</param>
    /// <returns>The place of the row, or -1 when it fell off the table.</returns>
    /// <remarks>
    /// 同一次通关已经榜上有名时只替换那一行，不新增；新行插进去之后重排并截到十条，最后再看它还在不在。
    /// 标准图按分数从高到低，战役按天数从少到多——这是参考的排序方向，不要统一。
    /// </remarks>
    private static int SaveEntry(HighscoreData data, List<HighscoreData> entries, bool isCampaign)
    {
        ArgumentNullException.ThrowIfNull(data);

        int existing = entries.FindIndex(entry => entry.SameCompletion(data));
        if (existing >= 0)
        {
            entries[existing] = data;

            return existing;
        }

        entries.Add(data);

        if (isCampaign)
        {
            entries.Sort(static (first, second) => first.Rating.CompareTo(second.Rating));
        }
        else
        {
            entries.Sort(static (first, second) => second.Rating.CompareTo(first.Rating));
        }

        if (entries.Count > MaximumEntries)
        {
            entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);
        }

        return entries.FindIndex(entry => entry.SameCompletion(data));
    }

    /// <summary>
    /// Builds the score-to-monster table, the lambda of <c>highscores.cpp:231-261</c>.
    /// </summary>
    /// <returns>Thresholds in ascending order, each paired with the monster that covers up to it.</returns>
    /// <remarks>
    /// 步长只在四个位置变化：农民 3 分一格、妖精 4 分一格、大德鲁伊 3 分一格、黑龙 1 分一格，其余沿用上一步长。
    /// </remarks>
    private static List<(uint Threshold, MonsterType Monster)> BuildRatingTable()
    {
        List<(uint Threshold, MonsterType Monster)> result = [];
        uint threshold = 0;
        uint step = 0;

        foreach (MonsterType monster in MonstersInRanking)
        {
            step = monster switch
            {
                MonsterType.Peasant => 3,
                MonsterType.Goblin => 4,
                MonsterType.GreaterDruid => 3,
                MonsterType.BlackDragon => 1,
                _ => step,
            };

            threshold += step;
            result.Add((threshold, monster));
        }

        return result;
    }

    /// <summary>
    /// Builds the day-to-monster table, the lambda of <c>highscores.cpp:285-321</c>.
    /// </summary>
    /// <returns>Thresholds in ascending order, each paired with the monster that covers up to it.</returns>
    /// <remarks>
    /// 与评分表相反：从最强的兵种往回走，黑龙 300 天一格、泰坦 20 天、德鲁伊 100 天、战斗矮人 200 天、农民 1 天。
    /// </remarks>
    private static List<(uint Threshold, MonsterType Monster)> BuildDayTable()
    {
        List<(uint Threshold, MonsterType Monster)> result = [];
        uint threshold = 0;
        uint step = 0;

        for (int i = MonstersInRanking.Length - 1; i >= 0; --i)
        {
            MonsterType monster = MonstersInRanking[i];

            step = monster switch
            {
                MonsterType.BlackDragon => 300,
                MonsterType.Titan => 20,
                MonsterType.Druid => 100,
                MonsterType.BattleDwarf => 200,
                MonsterType.Peasant => 1,
                _ => step,
            };

            threshold += step;
            result.Add((threshold, monster));
        }

        return result;
    }
}
