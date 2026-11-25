using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Io;
using FHeroes2.Game.Campaign;

namespace FHeroes2.Game.System;

/// <summary>
/// The save-file side of <c>src/fheroes2/game/game_io.cpp</c>: the header magic, the two automatic slot names, the
/// naming rules for a slot, the version of the file being read, and the whole file — writing one and reading it back.
/// </summary>
/// <remarks>
/// <para>
/// 参考在这个文件里干两件事：一是拼名字（自动存档、快速存档、由地图名派生的存档名、按游戏类型选扩展名），二是整份
/// 存档的读写（<c>HeaderSAV</c> 加 <c>World</c>、<c>Settings</c>、<c>GameOver::Result</c> 压缩写盘）。
/// </para>
/// <para>
/// <b>第二件事整份都能跑了</b>：明文段（魔数、版本串、<see cref="SaveFileHeader"/>）之后是一个压缩块，里面依次是
/// <see cref="World"/>、<c>Settings</c>、<see cref="GameOverResult"/>，最后是又一遍魔数当「数据完」标记
/// （<c>game_io.cpp:141-157</c>）。压缩块用 <see cref="Compression"/>，和参考的 <c>zlib</c> 逐字节一致。
/// </para>
/// <para>
/// <b>战役那一段也接上了</b>：战役存档在结局之后多一段 <see cref="CampaignSaveData"/>（<c>game_io.cpp:149-151</c>）。
/// 参考读的时候顺手判断「这一关是不是打完了」，打完就切到选关界面（<c>:257-265</c>）；端口没有 <c>GameMode</c>，
/// 只把进度读进 <see cref="CampaignSaveData.Get"/>，「要不要进选关」交给调用方看
/// <see cref="CampaignSaveData.IsStarting"/> 与 <see cref="CampaignSaveData.GetLastCompletedScenarioInfoId"/>。
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
    /// <returns><c>&lt;user data&gt;/files/save</c>; the directory may not exist yet.</returns>
    /// <remarks>
    /// 参考拼的是「用户数据目录 + <c>files/save</c>」，两级都不检查存在性：目录由启动时的
    /// <c>initDataDir()</c> 建好（<c>game_init.cpp:205-209</c>），读档时目录不存在就是「没有存档」。端口没有等价的启动
    /// 步骤，所以由 <see cref="Save"/> 在写盘前把目标目录建出来。
    /// </remarks>
    public static string GetSaveDir() =>
        Path.Combine(GameDataDirectory.Get("fheroes2"), "files", "save");

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
    /// <returns>Whether the file was written.</returns>
    public static bool AutoSave() => Save(Path.Combine(GetSaveDir(), AutoSaveName + GetSaveFileExtension()), autoSave: true);

    /// <summary>The file the quick save key writes, <c>Game::QuickSave()</c> (<c>game_io.cpp:111-114</c>).</summary>
    /// <returns>Whether the file was written.</returns>
    /// <remarks>
    /// 快速存档不是自动存档：参考传 <see langword="false"/>，「继续游戏」会把它当成最近一次的存档。
    /// </remarks>
    public static bool QuickSave() => Save(Path.Combine(GetSaveDir(), QuickSaveName + GetSaveFileExtension()));

    /// <summary>
    /// Writes the whole game, <c>Game::Save()</c> (<c>game_io.cpp:116-164</c>).
    /// </summary>
    /// <param name="filePath">The file to write.</param>
    /// <param name="autoSave">Whether this is the automatic slot, which does not become <see cref="LastSaveName"/>.</param>
    /// <returns>Whether the file was written.</returns>
    /// <remarks>
    /// <para>
    /// 明文段之后是<b>一个</b>压缩块，块里是「世界 + 设置 + 结局 + 魔数」，读完魔数才承认整份存档完整
    /// （<c>game_io.cpp:141-157</c>）。所以压缩块里的任何一个算子少读一个字节都会在后面暴出来，而不是悄悄错位。
    /// </para>
    /// <para>
    /// 写之前把 <see cref="VersionOfCurrentSaveFile"/> 设成当前版本：各算子的 <c>Write</c> 不看版本，但读回来的
    /// 同一进程里紧接着 <see cref="LoadSav2FileInfo"/> 会按这个值判断，留着旧版本会读错老格式的分支。
    /// </para>
    /// <para>
    /// 两处主动偏差：没有地图时直接拒写（参考会写一份 0x0 的世界），以及写盘前建出目标目录——参考的目录是
    /// <c>initDataDir()</c> 在启动时建的（<c>game_init.cpp:205-209</c>），端口没有那一步。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    public static bool Save(string filePath, bool autoSave = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Settings settings = Settings.Get();
        World world = World.Current;

        if (settings.CurrentMapFileInfo is not { } mapInfo || world.Width <= 0 || world.Height <= 0)
        {
            // 参考不检查：没有地图时它写的是一个 0x0 的世界。端口的世界序列化要求先有地图，所以在这里拦下，
            // 而不是让它抛出来。拦在开文件之前，免得留下一份 0 字节的存档把「能读的档」列表弄脏。
            GameLog.Error("There is no game to save.");

            return false;
        }

        using StreamFile file = new();

        file.SetBigEndian(true);

        if (!CreateSaveDirectory(filePath))
        {
            return false;
        }

        if (!file.OpenForWriting(filePath))
        {
            GameLog.Error($"Could not open the save file for writing: {filePath}");

            return false;
        }

        VersionOfCurrentSaveFile = SaveFileFormatInfo.Current;

        SaveFileFormat version = SaveFileFormatInfo.Current;

        file.Write(SaveFileMagicNumber);

        // 参考写的是 std::to_string(saveFileVersion)（game_io.cpp:131）——十进制数字而不是枚举名，读的时候整串丢掉（:186）。
        file.Write(((ushort)version).ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        file.Write((ushort)version);
        SaveFileHeader.Create(mapInfo, settings.GameType, world.Day, world.Week, world.Month).Write(file);

        if (file.Fail)
        {
            return false;
        }

        StreamBuf data = new();
        data.SetBigEndian(true);

        world.WriteTo(data);
        settings.Write(data);
        GameOverResult.Current.WriteTo(data);

        // 战役存档在结局之后多一段战役进度（game_io.cpp:149-151），只战役才写。
        if (settings.IsCampaignGameType)
        {
            CampaignSaveData.Get().Write(data);
        }

        // 数据块的收尾魔数：读的时候要靠它区分「文件在这里结束」和「文件被截断」。
        data.Write(SaveFileMagicNumber);

        if (data.Fail || !Compression.ZipStreamBuf(data, file))
        {
            return false;
        }

        if (!autoSave)
        {
            LastSaveName = filePath;
        }

        // 参考在成功时静默（game_io.cpp 只报失败）。端口补一条日志：失败路径本来就有 Error，成功却没有任何痕迹，
        // 冒烟脚本与玩家排查「到底写没写出去」只能靠猜。
        GameLog.Info($"The game was saved to '{filePath}'.");

        return true;
    }

    /// <summary>
    /// Reads a game back, <c>Game::Load()</c> (<c>game_io.cpp:166-305</c>).
    /// </summary>
    /// <param name="filePath">The file to read.</param>
    /// <returns>Whether the game was read; a world whose own block is broken is dropped instead of half-kept.</returns>
    /// <remarks>
    /// <para>
    /// 参考返回 <c>fheroes2::GameMode</c>，用它决定「读档后进游戏」还是「进战役下一关选择界」。端口没有 Game 层的
    /// <c>GameMode</c>（界面切换由 App 的 <c>SceneKind</c> 管），所以这里只回成功与否，切场景留给调用方——这是签名上
    /// 的已知偏差，不是临时兜底。
    /// </para>
    /// <para>
    /// 四道明文闸（文件能开、魔数、版本区间、玩法掩码）在读压缩块之前，和 <see cref="LoadSav2FileInfo"/> 一致；
    /// 压缩块解不开、<c>World</c>/<c>Settings</c> 读到一半、收尾魔数不对，都算整份存档坏了。
    /// </para>
    /// <para>
    /// 读完把 <see cref="World.MapInfo"/> 接上 <c>Settings</c> 里那份文件头：存档先写世界后写设置，世界读的时候还
    /// 拿不到它，而地图判定要用（见 <c>World.Conditions.cs</c>）。参考在这里还会把文件名经 <c>Settings::GetLastFile</c>
    /// 还原成完整路径（<c>game_io.cpp:275</c>），端口没有那张「上次用过的文件」表，路径就保留存档里的相对名。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    public static bool Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using StreamFile file = new();

        file.SetBigEndian(true);

        if (!file.OpenForReading(filePath))
        {
            GameLog.Error($"Could not open the save file for reading: {filePath}");

            return false;
        }

        if (file.ReadUInt16() != SaveFileMagicNumber)
        {
            GameLog.Error($"Not a save file: {filePath}");

            return false;
        }

        _ = file.ReadString();
        SaveFileFormat version = (SaveFileFormat)file.ReadUInt16();

        if (file.Fail)
        {
            GameLog.Error($"The header of the save file is incomplete: {filePath}");

            return false;
        }

        if (version > SaveFileFormatInfo.Current || version < SaveFileFormatInfo.LastSupported)
        {
            GameLog.Error($"Unsupported save format {version} in {filePath}.");

            return false;
        }

        VersionOfCurrentSaveFile = version;

        SaveFileHeader header = SaveFileHeader.Read(file);

        Settings settings = Settings.Get();

        if ((settings.GameType & header.GameType) == 0)
        {
            GameLog.Error($"The save file belongs to another type of game: {filePath}");

            return false;
        }

        StreamBuf data = new();
        data.SetBigEndian(true);

        if (!Compression.UnzipStream(file, data))
        {
            GameLog.Error($"The data of the save file is broken: {filePath}");

            return false;
        }

        if ((header.Requirements & SaveFileHeader.RequiresPolResources) != 0 && !settings.IsPriceOfLoyaltySupported)
        {
            GameLog.Error($"The save file needs \"The Price of Loyalty\" assets: {filePath}");

            return false;
        }

        data.Seek(0);

        World.Current.ReadFrom(data, version);
        settings.Read(data);

        if (!GameOverResult.Current.ReadFrom(data, version))
        {
            return false;
        }

        if (settings.IsCampaignGameType)
        {
            CampaignSaveData.Get().Read(data);
        }

        ushort endOfDataMarker = data.ReadUInt16();

        if (data.Fail || endOfDataMarker != SaveFileMagicNumber)
        {
            GameLog.Error($"The save file is corrupted: {filePath}");

            return false;
        }

        World.Current.MapInfo = settings.CurrentMapFileInfo;

        LastSaveName = filePath;

        return true;
    }

    /// <summary>
    /// Reads only the header of a save, <c>Game::LoadSAV2FileInfo()</c> (<c>game_io.cpp:307-355</c>).
    /// </summary>
    /// <param name="filePath">The file to read.</param>
    /// <param name="fileInfo">Receives the map information stored in the header.</param>
    /// <returns>Whether the file opened, carried the right magic, a supported version and a matching game type.</returns>
    /// <remarks>
    /// 四道闸照参考的顺序：魔数、版本区间、<c>fs.fail()</c>、玩法掩码。最后一道是「这个档不属于当前玩法」，读档界面靠它把
    /// 战役档从单图存档里筛掉。读到的 <see cref="Mp2FileInfo.FilePath"/> 是完整路径，不是头部里那个裸文件名。
    /// </remarks>
    public static bool LoadSav2FileInfo(string filePath, out Mp2FileInfo fileInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        fileInfo = null!;

        using StreamFile file = new();

        file.SetBigEndian(true);

        if (!file.OpenForReading(filePath))
        {
            return false;
        }

        ushort magicNumber = file.ReadUInt16();

        if (magicNumber != SaveFileMagicNumber)
        {
            return false;
        }

        _ = file.ReadString();
        SaveFileFormat version = (SaveFileFormat)file.ReadUInt16();

        if (version > SaveFileFormatInfo.Current || version < SaveFileFormatInfo.LastSupported)
        {
            return false;
        }

        VersionOfCurrentSaveFile = version;

        SaveFileHeader header = SaveFileHeader.Read(file);

        if (file.Fail || (Settings.Get().GameType & header.GameType) == 0)
        {
            return false;
        }

        fileInfo = header.Info with { FilePath = filePath };

        return true;
    }

    /// <summary>
    /// Writes the campaign scenario that was just finished,
    /// <c>Game::SaveCompletedCampaignScenario()</c> (<c>game_io.cpp:419-422</c>).
    /// </summary>
    /// <returns>Whether the file was written.</returns>
    /// <remarks>
    /// 文件名是「按地图名派生的基名 + <c>_Complete</c> + 扩展名」，后缀是参考硬写的，不是本地化文案。
    /// 这份档就是读档界面里「这一关已经打完、可以进下一关」的那个入口（<see cref="Load"/> 读战役进度后由调用方判断）。
    /// </remarks>
    public static bool SaveCompletedCampaignScenario() =>
        Save(Path.Combine(GetSaveDir(), GetSaveFileBaseName() + "_Complete" + GetSaveFileExtension()));

    /// <summary>
    /// The block between the version and the compressed game data, <c>HeaderSAV</c> (<c>game_io.cpp:64-103</c>).
    /// </summary>
    /// <param name="Requirements">The resource packs the save needs, <c>HeaderSAV::requirements</c>.</param>
    /// <param name="Info">The map information.</param>
    /// <param name="GameType">The type of game the save belongs to.</param>
    /// <remarks>
    /// 三个字段都是明文，只有后面那份游戏数据是压缩的，所以读档界面能只读头就把列表画出来。位标志
    /// <see cref="RequiresPolResources"/> 记的是「这张图用的不是原版战役的资源」，写的时候由
    /// <see cref="Create"/> 看地图版本置上；读的时候只有 <see cref="Load"/> 校验（<c>game_io.cpp:241-247</c>），
    /// <see cref="LoadSav2FileInfo"/> 不校验——列表里照样列出来，进不去的时候再报缺资源。
    /// </remarks>
    public readonly record struct SaveFileHeader(ushort Requirements, Mp2FileInfo Info, GameTypes GameType)
    {
        /// <summary>The save needs the Price of Loyalty resources, <c>HeaderSAV::REQUIRES_POL_RESOURCES</c>.</summary>
        public const ushort RequiresPolResources = 0x4000;

        /// <summary>
        /// Builds a header for the game being written, <c>HeaderSAV( fi, type, worldDay, worldWeek, worldMonth )</c>
        /// (<c>game_io.cpp:73-88</c>).
        /// </summary>
        /// <param name="info">The map information.</param>
        /// <param name="gameType">The type of game.</param>
        /// <param name="worldDay">The day of the world date.</param>
        /// <param name="worldWeek">The week of the world date.</param>
        /// <param name="worldMonth">The month of the world date.</param>
        /// <returns>The header, with the timestamp and the world date filled in.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// 时间戳取当下，参考用 <c>std::time</c>，端口用 <see cref="DateTimeOffset.UtcNow"/> 的秒数——两者都是 Unix 秒。
        /// </remarks>
        public static SaveFileHeader Create(Mp2FileInfo info, GameTypes gameType, uint worldDay, uint worldWeek, uint worldMonth)
        {
            ArgumentNullException.ThrowIfNull(info);

            ushort requirements = info.Version == MapVersion.SuccessionWars ? (ushort)0 : RequiresPolResources;

            return new SaveFileHeader(
                requirements,
                info with
                {
                    Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    WorldDay = worldDay,
                    WorldWeek = worldWeek,
                    WorldMonth = worldMonth,
                },
                gameType);
        }

        /// <summary>Writes the header, <c>operator&lt;&lt;( OStreamBase &amp;, const HeaderSAV &amp; )</c>.</summary>
        /// <param name="stream">The stream to write to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        public void Write(StreamBase stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            stream.Write(Requirements);
            SaveFileInfo.Write(stream, Info);
            stream.Write((int)GameType);
        }

        /// <summary>Reads the header, <c>operator&gt;&gt;( IStreamBase &amp;, HeaderSAV &amp; )</c>.</summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>The header; a failed stream leaves zero valued fields and the caller checks <see cref="StreamBase.Fail"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        public static SaveFileHeader Read(StreamBase stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            ushort requirements = stream.ReadUInt16();
            Mp2FileInfo info = SaveFileInfo.Read(stream, VersionOfCurrentSaveFile);
            GameTypes gameType = (GameTypes)stream.ReadInt32();

            return new SaveFileHeader(requirements, info, gameType);
        }
    }

    /// <summary>
    /// Creates the directory a save is about to be written into, standing in for <c>initDataDir()</c>
    /// (<c>game_init.cpp:196-210</c>).
    /// </summary>
    /// <remarks>
    /// 参考在启动时就把 <c>files/save</c> 建好（还有它的两级父目录），保存本身只管 <c>fopen</c>。端口没有等价的启动
    /// 代码，而 <c>FileStream</c> 不会替调用方建目录，所以这一步放到写盘前：第一次存档在干净的安装上也能成功。
    /// 失败的报错和 <see cref="StreamFile"/> 打开失败一样只是记日志、返回 <see langword="false"/>。
    /// </remarks>
    /// <param name="filePath">The save file about to be written.</param>
    /// <returns>Whether the directory exists afterwards.</returns>
    private static bool CreateSaveDirectory(string filePath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));

        if (string.IsNullOrEmpty(directory))
        {
            return true;
        }

        try
        {
            Directory.CreateDirectory(directory);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            GameLog.Error($"Could not create the save directory '{directory}': {ex.Message}");

            return false;
        }
    }

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
