using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.System;

/// <summary>
/// The save-file side of <c>src/fheroes2/game/game_io.cpp</c>: the header magic, the two automatic slot names, the
/// naming rules for a slot and the version of the file being read.
/// </summary>
/// <remarks>
/// <para>
/// 参考在这个文件里干两件事：一是拼名字（自动存档、快速存档、由地图名派生的存档名、按游戏类型选扩展名），二是整份
/// 存档的读写（<c>HeaderSAV</c> 加 <c>World</c>、<c>Settings</c>、<c>GameOver::Result</c> 压缩写盘）。
/// </para>
/// <para>
/// <b>端口只落了第一件事。</b>第二件事要两块现在还缺的地基：<c>World</c>、<c>Settings</c>、<c>GameOver::Result</c>
/// 的序列化（三个类型都还没有 <c>Write</c>/<c>Read</c>），以及「用户数据目录」这个概念（参考的 <c>GetSaveDir()</c> 走
/// <c>System::GetDataDirectory( "fheroes2" )</c>，端口连设置文件都没有落盘）。所以六个读写入口按计划先留签名：
/// 文档写清依赖什么，方法体抛 <see cref="NotImplementedException"/>，等存档阶段再补，不在调用方写临时替代。
/// </para>
/// <para>
/// 扩展名不是装饰：同一个 <c>.sav</c> 目录里三种玩法的存档混放，读档界面靠扩展名筛出「这种玩法能读的档」，
/// <see cref="GetSaveFileExtension(GameTypes)"/> 的判断顺序也因此不能重排。
/// </para>
/// </remarks>
public static class GameSave
{
    /// <summary>The first two bytes of every save file, <c>saveFileMagicNumber = 0xFF03</c>.</summary>
    public const ushort SaveFileMagicNumber = 0xFF03;

    /// <summary>The slot the game writes before every turn, <c>autoSaveName</c>.</summary>
    public const string AutoSaveName = "AUTOSAVE";

    /// <summary>The slot the player writes with the quick save key, <c>quickSaveName</c>.</summary>
    public const string QuickSaveName = "QUICKSAVE";

    /// <summary>
    /// The name used when the map name sanitizes to nothing, <c>"newgame"</c> of <c>GetSaveFileBaseName()</c>.
    /// </summary>
    private const string EmptyBaseName = "newgame";

    /// <summary>
    /// The format of the file being read or written, <c>versionOfCurrentSaveFile</c>.
    /// </summary>
    /// <remarks>
    /// 读老存档时必须先把它设成文件里的版本，各对象的 <c>Read</c> 才能按版本决定字段有无；写的时候总是设成
    /// <see cref="SaveFileFormatInfo.Current"/>。
    /// </remarks>
    public static SaveFileFormat VersionOfCurrentSaveFile { get; set; } = SaveFileFormatInfo.Current;

    /// <summary>
    /// The path of the last file saved or loaded, <c>lastSaveName</c>.
    /// </summary>
    /// <remarks>「继续游戏」与「保存」对话框用它作默认位置。</remarks>
    public static string LastSaveName { get; set; } = string.Empty;

    /// <summary>
    /// The directory the saves live in, <c>Game::GetSaveDir()</c> (<c>game_io.cpp:377-380</c>).
    /// </summary>
    /// <returns>Never returns; the port has no user data directory yet.</returns>
    /// <exception cref="NotImplementedException">
    /// Always. 需要先有 <c>System::GetDataDirectory( "fheroes2" )</c> 的对应物。
    /// </exception>
    public static string GetSaveDir() =>
        throw new NotImplementedException(
            "GetSaveDir needs the per-user data directory of System::GetDataDirectory( \"fheroes2\" ), which the port has not built yet.");

    /// <summary>
    /// Derives a file name from the current map, <c>Game::GetSaveFileBaseName()</c> (<c>game_io.cpp:382-400</c>).
    /// </summary>
    /// <returns>The sanitized map name, <c>"newgame"</c> when nothing printable is left.</returns>
    /// <remarks>
    /// 四步清洗，顺序不能换：先把非 ASCII 字符换成感叹号（保留「这张图有外文名」的痕迹），再删掉不可打印字符，再把剩下的
    /// 非字母数字（感叹号除外）换成下划线——先删后换，否则不可打印字符会先变成下划线留下来。
    /// </remarks>
    public static string GetSaveFileBaseName()
    {
        Mp2FileInfo? fileInfo = Settings.Get().CurrentMapFileInfo;
        string mapName = fileInfo?.Name ?? string.Empty;

        global::System.Text.StringBuilder baseName = new(mapName.Length);

        foreach (char character in mapName)
        {
            // 参考按 unsigned char 判断，端口照字节语义走，避免把宽字符当成可打印。
            byte code = character > 0xFF ? (byte)'!' : (byte)character;

            if (code > 127)
            {
                baseName.Append('!');
            }
            else if (IsPrintable(code))
            {
                baseName.Append(code != '!' && !IsAlphanumeric(code) ? '_' : character);
            }
        }

        return baseName.Length == 0 ? EmptyBaseName : baseName.ToString();
    }

    /// <summary>
    /// The extension of the type of game being played, <c>Game::GetSaveFileExtension()</c>
    /// (<c>game_io.cpp:402-405</c>).
    /// </summary>
    /// <returns>One of <c>.sav</c>, <c>.savc</c>, <c>.savh</c>, <c>.savm</c>.</returns>
    public static string GetSaveFileExtension() => GetSaveFileExtension(Settings.Get().GameType);

    /// <summary>
    /// The extension for a type of game, <c>Game::GetSaveFileExtension( const int )</c>
    /// (<c>game_io.cpp:407-417</c>).
    /// </summary>
    /// <param name="gameType">The type of game.</param>
    /// <returns><c>.sav</c> for a single map, <c>.savc</c> for a campaign, <c>.savh</c> for hot seat, else <c>.savm</c>.</returns>
    /// <remarks>
    /// 判断顺序是参考的：先单图、再战役、再热座，最后落 <c>.savm</c>——这一档同时兜住网络、单场战斗与自动测试。
    /// </remarks>
    public static string GetSaveFileExtension(GameTypes gameType)
    {
        if ((gameType & GameTypes.Standard) != 0)
        {
            return ".sav";
        }

        if ((gameType & GameTypes.Campaign) != 0)
        {
            return ".savc";
        }

        if ((gameType & GameTypes.HotSeat) != 0)
        {
            return ".savh";
        }

        return ".savm";
    }

    /// <summary>The file the game writes before every turn, <c>Game::AutoSave()</c> (<c>game_io.cpp:106-109</c>).</summary>
    /// <returns>Never returns; save serialization is not ported yet.</returns>
    /// <exception cref="NotImplementedException">Always. 依赖 <see cref="Save"/>。</exception>
    public static bool AutoSave() => throw new NotImplementedException("AutoSave needs GameSave.Save, which needs the world serialization.");

    /// <summary>The file the quick save key writes, <c>Game::QuickSave()</c> (<c>game_io.cpp:111-114</c>).</summary>
    /// <returns>Never returns; save serialization is not ported yet.</returns>
    /// <exception cref="NotImplementedException">Always. 依赖 <see cref="Save"/>。</exception>
    public static bool QuickSave() => throw new NotImplementedException("QuickSave needs GameSave.Save, which needs the world serialization.");

    /// <summary>
    /// Writes the whole game, <c>Game::Save()</c> (<c>game_io.cpp:116-164</c>).
    /// </summary>
    /// <param name="filePath">The file to write.</param>
    /// <param name="autoSave">Whether this is the automatic slot, which does not become <see cref="LastSaveName"/>.</param>
    /// <returns>Never returns; save serialization is not ported yet.</returns>
    /// <exception cref="NotImplementedException">Always. 见类注释：缺 <c>World</c>/<c>Settings</c>/<c>GameOver::Result</c> 的序列化。</exception>
    public static bool Save(string filePath, bool autoSave = false) =>
        throw new NotImplementedException("GameSave.Save needs the serialization of World, Settings and GameOver.Result.");

    /// <summary>
    /// Reads a game back, <c>Game::Load()</c> (<c>game_io.cpp:166-305</c>).
    /// </summary>
    /// <param name="filePath">The file to read.</param>
    /// <returns>Never returns; save serialization is not ported yet.</returns>
    /// <exception cref="NotImplementedException">Always. 见类注释。</exception>
    /// <remarks>
    /// 参考返回 <c>fheroes2::GameMode</c>，用它决定「读档后进游戏」还是「进战役下一关选择界」。端口没有 Game 层的
    /// <c>GameMode</c>（界面切换由 App 的 <c>SceneKind</c> 管），所以这里只回成功与否，切场景留给调用方——这是签名上
    /// 的已知偏差，不是临时兜底。
    /// </remarks>
    public static bool Load(string filePath) =>
        throw new NotImplementedException("GameSave.Load needs the serialization of World, Settings and GameOver.Result.");

    /// <summary>
    /// Reads only the header of a save, <c>Game::LoadSAV2FileInfo()</c> (<c>game_io.cpp:307-355</c>).
    /// </summary>
    /// <param name="filePath">The file to read.</param>
    /// <param name="fileInfo">Receives the map information stored in the header.</param>
    /// <returns>Never returns; the save header needs the map info serialization.</returns>
    /// <exception cref="NotImplementedException">Always. 见类注释。</exception>
    public static bool LoadSav2FileInfo(string filePath, out Mp2FileInfo fileInfo) =>
        throw new NotImplementedException("LoadSav2FileInfo needs the serialization of Maps::FileInfo, which the port has no counterpart of.");

    /// <summary>
    /// Writes the campaign scenario that was just finished,
    /// <c>Game::SaveCompletedCampaignScenario()</c> (<c>game_io.cpp:419-422</c>).
    /// </summary>
    /// <returns>Never returns; save serialization is not ported yet.</returns>
    /// <exception cref="NotImplementedException">Always. 依赖 <see cref="Save"/> 与 <see cref="GetSaveDir"/>。</exception>
    public static bool SaveCompletedCampaignScenario() =>
        throw new NotImplementedException("SaveCompletedCampaignScenario needs GameSave.Save and GameSave.GetSaveDir.");

    /// <summary>Whether a byte is printable, <c>std::isprint</c> of the C locale.</summary>
    /// <param name="code">The byte.</param>
    /// <returns>True for 0x20 to 0x7E.</returns>
    private static bool IsPrintable(byte code) => code is >= 0x20 and <= 0x7E;

    /// <summary>Whether a printable byte is a letter or a digit, <c>std::isalnum</c> of the C locale.</summary>
    /// <param name="code">The byte.</param>
    /// <returns>True for ASCII letters and digits.</returns>
    private static bool IsAlphanumeric(byte code) =>
        code is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';
}
