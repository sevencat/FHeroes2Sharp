using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.System;

/// <summary>
/// The way the settings travel inside a save file, <c>operator&lt;&lt;</c> and
/// <c>operator&gt;&gt;( IStreamBase &amp;, Settings &amp; )</c> (<c>settings.cpp:1280-1310</c>).
/// </summary>
/// <remarks>
/// <para>
/// Only five things are saved: the language, the map information, the difficulty, the game type and the player slots.
/// The volumes, the video mode, the speed of the heroes and every other preference stay in the configuration file —
/// a save belongs to a game, not to the machine that played it.
/// </para>
/// <para>
/// 语言读进 <see cref="LoadedFileLanguage"/> 而不是 <see cref="GameLanguage"/>：存档里的语言描述的是那张图的文本，
/// 覆盖玩家选的界面语言会让读档顺手把菜单也换了。
/// </para>
/// <para>
/// 1.1.01 之前这里还有一个整型字段，读完就丢；版本从 <c>GameSave.VersionOfCurrentSaveFile</c> 取，因为设置块在
/// <c>Game::Load</c> 里跟在文件头后面，头已经解析过了。
/// </para>
/// </remarks>
public sealed partial class Settings
{
    /// <summary>
    /// The map information written when no map has been chosen, <c>Maps::FileInfo()</c>.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>_currentMapInfo</c> 是值不是指针，所以「还没有地图」时写出去的是一个全空的头。端口的
    /// <see cref="CurrentMapFileInfo"/> 可空，这里用同一个全空实例顶替，保证字节数与参考一致。
    /// </remarks>
    private static readonly Mp2FileInfo NoMapFileInfo = new()
    {
        FilePath = string.Empty,
        Name = string.Empty,
        Description = string.Empty,
        Width = 0,
        Height = 0,
        Difficulty = GameDifficulty.Normal,
        Version = MapVersion.SuccessionWars,
    };

    /// <summary>
    /// Writes the settings, <c>operator&lt;&lt;( OStreamBase &amp;, const Settings &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(GameLanguage);
        SaveFileInfo.Write(stream, CurrentMapFileInfo ?? NoMapFileInfo);
        stream.Write((int)GameDifficulty);
        stream.Write((int)GameType);
        Players.Write(stream);
    }

    /// <summary>
    /// Reads the settings, <c>operator&gt;&gt;( IStreamBase &amp;, Settings &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 地图信息一定读成非空：参考读完之后 <c>getCurrentMapFileInfo()</c> 就能用，端口保持同样的保证，调用方不必再判空。
    /// </remarks>
    internal void Read(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        LoadedFileLanguage = stream.ReadString();
        CurrentMapFileInfo = SaveFileInfo.Read(stream, GameSave.VersionOfCurrentSaveFile);
        GameDifficulty = (GameDifficulty)stream.ReadInt32();
        GameType = (GameTypes)stream.ReadInt32();

        if (GameSave.VersionOfCurrentSaveFile < SaveFileFormat.PreRelease1_1_01)
        {
            // settings.cpp:1296-1299：1.1.01 之前这里还有一个整型字段，读完就丢。
            _ = stream.ReadInt32();
        }

        Players.Read(stream, GameSave.VersionOfCurrentSaveFile);
    }
}
