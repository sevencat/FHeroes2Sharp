using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.System;

/// <summary>
/// The way a map information block travels inside a save file, <c>operator&lt;&lt;</c> and
/// <c>operator&gt;&gt;( StreamBase &amp;, Maps::FileInfo &amp; )</c> (<c>maps_fileinfo.cpp:664-728</c>).
/// </summary>
/// <remarks>
/// <para>
/// 存档里的这块和 MP2 文件里那 428 字节的头完全是两套编码：MP2 是定长字段拼的，存档是逐个字段按类型写的，字符串带
/// 32 位长度前缀，两个条件参数按 <c>uint16</c> 写，颜色按 <c>uint8</c> 位集写。所以它不能复用
/// <see cref="Mp2FileInfo.ToHeaderBytes"/>，也不需要那 428 字节。
/// </para>
/// <para>
/// 两个尾巴按存档版本决定有无：1.1.03 之前没有 <c>mainLanguage</c>，1.1.15 之前没有 <c>creatorNotes</c>。读的时候
/// 版本由 <see cref="GameSave.VersionOfCurrentSaveFile"/> 给出——调用方必须先读文件头的版本号再调这里。
/// </para>
/// </remarks>
public static class SaveFileInfo
{
    /// <summary>The number of player slots of a map, <c>maxNumOfPlayers</c>.</summary>
    public const int MaxNumberOfPlayers = 6;

    /// <summary>
    /// Writes the map information, <c>Maps::operator&lt;&lt;( OStreamBase &amp;, const FileInfo &amp; )</c>
    /// (<c>maps_fileinfo.cpp:664-680</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="info">The map information.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="info"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 只写文件名不带路径：存档跟着地图走，换台机器读档时地图在哪由当时的设置决定（参考 666 行的注释）。
    /// </remarks>
    public static void Write(StreamBase stream, Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(info);

        stream.Write(Path.GetFileName(info.FilePath));
        stream.Write(info.Name);
        stream.Write(info.Description);
        stream.Write((ushort)info.Width);
        stream.Write((ushort)info.Height);
        stream.Write((byte)info.Difficulty);
        stream.Write((byte)MaxNumberOfPlayers);

        for (int player = 0; player < MaxNumberOfPlayers; ++player)
        {
            stream.Write((byte)info.PlayerRaces[player]);
            stream.Write((byte)info.PlayerUnions[player]);
        }

        stream.Write((byte)info.KingdomColors);
        stream.Write((byte)info.HumanPlayers);
        stream.Write((byte)info.ComputerPlayers);
        stream.Write((byte)info.RandomRacePlayers);
        stream.Write((byte)info.VictoryCondition);
        stream.Write(info.ComputerAlsoWins);
        stream.Write(info.AllowNormalVictory);
        stream.Write((ushort)info.VictoryConditionParameters[0]);
        stream.Write((ushort)info.VictoryConditionParameters[1]);
        stream.Write((byte)info.LossCondition);
        stream.Write((ushort)info.LossConditionParameters[0]);
        stream.Write((ushort)info.LossConditionParameters[1]);
        stream.Write(info.Timestamp);
        stream.Write(info.StartWithHeroInFirstCastle);
        stream.Write((int)info.Version);
        stream.Write(info.WorldDay);
        stream.Write(info.WorldWeek);
        stream.Write(info.WorldMonth);
        stream.Write(info.MainLanguage);
        stream.Write(info.CreatorNotes);
    }

    /// <summary>
    /// Reads the map information, <c>Maps::operator&gt;&gt;( IStreamBase &amp;, FileInfo &amp; )</c>
    /// (<c>maps_fileinfo.cpp:682-728</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="saveFileVersion">The version in the file's header, already parsed.</param>
    /// <returns>The map information; a truncated stream leaves zero valued fields rather than throwing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考把 <c>kingdommax</c> 之后的多余玩家槽按「写多少读多少」处理：老存档可能只有四个槽，读到四个就停，剩下的保持
    /// 默认值。端口的 <see cref="Mp2FileInfo"/> 固定六个槽，所以少读的槽留默认值，多读的槽丢掉——与参考 701-704 行的
    /// 越界保护同一个意思。
    /// </remarks>
    public static Mp2FileInfo Read(StreamBase stream, SaveFileFormat saveFileVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string fileName = stream.ReadString();
        string name = stream.ReadString();
        string description = stream.ReadString();
        int width = stream.ReadUInt16();
        int height = stream.ReadUInt16();
        GameDifficulty difficulty = (GameDifficulty)stream.ReadUInt8();
        int players = stream.ReadUInt8();

        MapRace[] races = new MapRace[MaxNumberOfPlayers];
        PlayerColor[] unions = new PlayerColor[MaxNumberOfPlayers];

        for (int player = 0; player < players; ++player)
        {
            MapRace race = (MapRace)stream.ReadUInt8();
            PlayerColor union = (PlayerColor)stream.ReadUInt8();

            if (player < MaxNumberOfPlayers)
            {
                races[player] = race;
                unions[player] = union;
            }
        }

        PlayerColor kingdomColors = (PlayerColor)stream.ReadUInt8();
        PlayerColor humanPlayers = (PlayerColor)stream.ReadUInt8();
        PlayerColor computerPlayers = (PlayerColor)stream.ReadUInt8();
        PlayerColor randomRacePlayers = (PlayerColor)stream.ReadUInt8();
        MapVictoryCondition victoryCondition = (MapVictoryCondition)stream.ReadUInt8();
        bool computerAlsoWins = stream.ReadBool();
        bool allowNormalVictory = stream.ReadBool();
        int victoryParameter0 = stream.ReadUInt16();
        int victoryParameter1 = stream.ReadUInt16();
        MapLossCondition lossCondition = (MapLossCondition)stream.ReadUInt8();
        int lossParameter0 = stream.ReadUInt16();
        int lossParameter1 = stream.ReadUInt16();
        uint timestamp = stream.ReadUInt32();
        bool startWithHeroInFirstCastle = stream.ReadBool();
        MapVersion version = (MapVersion)stream.ReadInt32();
        uint worldDay = stream.ReadUInt32();
        uint worldWeek = stream.ReadUInt32();
        uint worldMonth = stream.ReadUInt32();

        // maps_fileinfo.cpp:711-725：两个尾巴各有自己的版本门。
        byte mainLanguage = saveFileVersion >= SaveFileFormat.Release1_1_03 ? stream.ReadUInt8() : (byte)0;
        string creatorNotes = saveFileVersion >= SaveFileFormat.Release1_1_15 ? stream.ReadString() : string.Empty;

        return new Mp2FileInfo
        {
            FilePath = fileName,
            Name = name,
            Description = description,
            Width = width,
            Height = height,
            Difficulty = difficulty,
            KingdomColors = kingdomColors,
            HumanPlayers = humanPlayers,
            ComputerPlayers = computerPlayers,
            RandomRacePlayers = randomRacePlayers,
            PlayerRaces = races,
            PlayerUnions = unions,
            VictoryCondition = victoryCondition,
            ComputerAlsoWins = computerAlsoWins,
            AllowNormalVictory = allowNormalVictory,
            VictoryConditionParameters = [victoryParameter0, victoryParameter1],
            LossCondition = lossCondition,
            LossConditionParameters = [lossParameter0, lossParameter1],
            Timestamp = timestamp,
            StartWithHeroInFirstCastle = startWithHeroInFirstCastle,
            Version = Enum.IsDefined(version) ? version : MapVersion.SuccessionWars,
            WorldDay = worldDay,
            WorldWeek = worldWeek,
            WorldMonth = worldMonth,
            MainLanguage = mainLanguage,
            CreatorNotes = creatorNotes,
        };
    }
}
