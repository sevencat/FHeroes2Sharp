using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Cursor;

namespace FHeroes2.Game;

/// <summary>
/// The single entry point for archive images, the counterpart of the reference's
/// <c>Assets::getImage( const int icnId, const uint32_t index )</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/game/game_assets.h:33</c> namespace Assets（<c>getImage</c> 声明在 35 行，
/// <c>getImageCount</c> 在 36 行）；实现 <c>src/fheroes2/game/game_assets.cpp:5817</c>，
/// 进程级缓存 <c>_icnVsSprite</c> 在 <c>game_assets.cpp:65</c>（<c>std::vector&lt;std::vector&lt;Sprite&gt;&gt;</c>，
/// 按 ICN 号索引）。
/// </para>
/// <para>
/// 门面化见 ADR-057：<see cref="Current"/> 只在组合根赋值（<c>GameHost</c> 打开归档后设置、释放时清空），
/// 业务代码只读。参考没有「归档包」这一层，它的 ICN 在启动时全部解到 <c>_icnVsSprite</c>；本项目按需解码，
/// 用 <see cref="Current"/> 指向的 <see cref="AssetBundle"/> 取条目，解码结果按（文件名, 帧号）缓存。
/// </para>
/// <para>
/// 差异：参考的 <c>getImage</c> 在 ICN 号非法或帧号越界时返回 <c>errorImage</c>（一块画着错误的图），
/// 本项目返回 <see langword="null"/>——调用方要么接受空图，要么像 <c>Checkbox</c> 那样抛异常。
/// </para>
/// </remarks>
public static class Assets
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, IcnSpriteFile?> Files = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(string FileName, int FrameId), Sprite?> Images = new();
    private static readonly Dictionary<string, IcnSpriteFile?> BaseFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(string FileName, int FrameId), Sprite?> BaseImages = new();
    private static readonly Dictionary<string, Sprite?[]?> SyntheticFiles = new(StringComparer.OrdinalIgnoreCase);

    private static AssetBundle? _current;

    /// <summary>
    /// The archive bundle all images come from. Assigning it drops the cache, because frames decoded
    /// from the old bundle must not leak into the new one.
    /// </summary>
    /// <remarks>
    /// 参考：<c>src/fheroes2/game/game_assets.cpp:65</c> 的 <c>_icnVsSprite</c> 是进程级缓存，没有「换包」
    /// 的概念（归档在启动时打开、生命周期同进程）；本项目的 <see cref="AssetBundle"/> 可换，所以换包即清缓存。
    /// </remarks>
    public static AssetBundle? Current
    {
        get => _current;

        set
        {
            lock (SyncRoot)
            {
                _current = value;
                Files.Clear();
                Images.Clear();
                BaseFiles.Clear();
                BaseImages.Clear();
                SyntheticFiles.Clear();

                // The monochrome cursor frames are decoded here rather than by the archive reader, see CursorImages.
                CursorImages.ClearCache();
            }
        }
    }

    /// <summary>
    /// Decodes an ICN, or returns the cached copy. A missing or unreadable entry yields
    /// <see langword="null"/> and is remembered as such, so a broken archive is not re-read on every call.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>CELLWIN.ICN</c>.</param>
    /// <returns>The frames, or <see langword="null"/> when the archive has none.</returns>
    /// <remarks>
    /// 参考：<c>src/fheroes2/game/game_assets.cpp:655</c> <c>void loadICN( const int id )</c>——按 ICN 号
    /// 解出整组帧（<c>decodeICNSprite</c> 在 674 行与 729 行）并放进 <c>_icnVsSprite[id]</c>。参考里它是
    /// 内部函数，这里同样是私有。
    /// 差异：参考用 <c>ICN::</c> 枚举做键，本项目用归档内的文件名（<c>ICN::CELLWIN</c> 对应
    /// <c>CELLWIN.ICN</c>），因为枚举表还没移植。
    /// </remarks>
    private static IcnSpriteFile? GetIcn(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        lock (SyncRoot)
        {
            if (Files.TryGetValue(fileName, out IcnSpriteFile? cached))
            {
                return cached;
            }

            IcnSpriteFile? file = _current?.TryReadIcn(fileName);
            Files[fileName] = file;
            return file;
        }
    }

    /// <summary>
    /// One frame of an ICN, decoded once and shared afterwards — the reference's
    /// <c>Assets::getImage</c>. The returned sprite belongs to the cache: callers must not modify it.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>CELLWIN.ICN</c>.</param>
    /// <param name="frameId">Zero based frame number.</param>
    /// <returns>The frame, or <see langword="null"/> when the file or the frame is absent.</returns>
    /// <remarks>
    /// 参考：<c>src/fheroes2/game/game_assets.cpp:5817</c> Assets::getImage（越界返回 <c>errorImage</c>，见类注释的差异）。
    /// 归档之前先问 <see cref="GetSynthetic"/>：<c>EMPTY_*_BUTTON</c> 这类表是 <c>generateDefaultImages()</c>
    /// 现场拼的，没有归档条目，但调用方照样按文件名取。
    /// </remarks>
    public static Sprite? GetImage(string fileName, int frameId)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        if (frameId < 0)
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (Images.TryGetValue((fileName, frameId), out Sprite? cached))
            {
                return cached;
            }

            Sprite? sprite = GetSynthetic(fileName) is { } synthetic
                ? frameId < synthetic.Length ? synthetic[frameId] : null
                : GetArchiveFrame(fileName, frameId);

            Images[(fileName, frameId)] = sprite;
            return sprite;
        }
    }

    /// <summary>
    /// The archive entry (or run-time table) an ICN id stands for, i.e. what the reference's
    /// <c>icnFileNames</c> does before <c>loadICN()</c> asks for it.
    /// </summary>
    /// <param name="type">The ICN id.</param>
    /// <returns>The name <see cref="GetImage"/> takes, e.g. <c>MISC12.ICN</c>.</returns>
    /// <remarks>
    /// <see cref="IcnCatalog.GetFileName"/> 对 <see cref="IcnType.LastValidFileIcn"/> 之后的 id 一律回 <c>CUSTOM</c>
    /// （参考的 <c>icn.cpp:224</c> 也这样），但参考的 <c>icnFileNames</c> 表里这些 id 是有名字的，
    /// <c>loadICN</c> 才排得到 <c>generateDefaultImages()</c>。本项目把名字留在各自的生成器里
    /// （<see cref="Gui.SmallOkayCancelButtons"/>、<see cref="Gui.ScenarioInfoIcons"/>），由这里把 id 接过去。
    /// </remarks>
    public static string GetFileName(IcnType type) => type switch
    {
        IcnType.ButtonMapSelectGood => Gui.ScenarioInfoIcons.MapSelectGoodFileName,
        IcnType.ButtonMapSelectEvil => Gui.ScenarioInfoIcons.MapSelectEvilFileName,
        IcnType.ButtonSmallOkayGood => Gui.SmallOkayCancelButtons.OkayGoodFileName,
        IcnType.ButtonSmallOkayEvil => Gui.SmallOkayCancelButtons.OkayEvilFileName,
        IcnType.ButtonSmallCancelGood => Gui.SmallOkayCancelButtons.CancelGoodFileName,
        IcnType.ButtonSmallCancelEvil => Gui.SmallOkayCancelButtons.CancelEvilFileName,
        IcnType.ButtonExitPuzzleDimDoorGood => Gui.ExitButtons.PuzzleDimDoorGoodFileName,
        IcnType.ButtonExitPuzzleDimDoorEvil => Gui.ExitButtons.PuzzleDimDoorEvilFileName,
        IcnType.ButtonViewworldExitGood => Gui.ExitButtons.ViewWorldGoodFileName,
        IcnType.ButtonViewworldExitEvil => Gui.ExitButtons.ViewWorldEvilFileName,
        IcnType.MiniMonsterImage => Gui.MiniMonsterSprites.ImageFileName,
        IcnType.MiniMonsterShadow => Gui.MiniMonsterSprites.ShadowFileName,
        IcnType.DifficultyIconEasy => Gui.ScenarioInfoIcons.DifficultyEasyFileName,
        IcnType.DifficultyIconNormal => Gui.ScenarioInfoIcons.DifficultyNormalFileName,
        IcnType.DifficultyIconHard => Gui.ScenarioInfoIcons.DifficultyHardFileName,
        IcnType.DifficultyIconExpert => Gui.ScenarioInfoIcons.DifficultyExpertFileName,
        IcnType.DifficultyIconImpossible => Gui.ScenarioInfoIcons.DifficultyImpossibleFileName,
        IcnType.MetallicBorderedTextboxGood => Gui.ScenarioInfoIcons.MetallicTextboxGoodFileName,
        IcnType.MetallicBorderedTextboxEvil => Gui.ScenarioInfoIcons.MetallicTextboxEvilFileName,
        IcnType.StonebakEvil => Gui.EvilInterfaceImages.StonebakEvilFileName,
        IcnType.DroplislEvil => Gui.EvilInterfaceImages.DroplislEvilFileName,
        _ => IcnCatalog.GetFileName(type),
    };

    /// <summary>
    /// Number of frames in an ICN, the counterpart of <c>Assets::getImageCount</c>.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>FONT.ICN</c>.</param>
    /// <returns>The frame count, or 0 when the archive has no such entry.</returns>
    /// <remarks>
    /// 参考：<c>src/fheroes2/game/game_assets.h:36</c> 声明、<c>src/fheroes2/game/game_assets.cpp:5834</c>
    /// <c>uint32_t getImageCount( const int icnId )</c>（非法 ICN 号返回 0）。用它的是
    /// <c>ui_text.cpp:143</c> 的 <c>getCharacterLimit</c>——字体就是逐字符一帧的 ICN。
    /// 参考数的是 <c>loadICN()</c> 补完帧之后的表，所以 <c>NGEXTRA.ICN</c> 这里是 88 而不是归档里的 82。
    /// </remarks>
    public static int GetImageCount(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        lock (SyncRoot)
        {
            if (GetSynthetic(fileName) is { } synthetic)
            {
                return synthetic.Length;
            }

            // NGEXTRA 的帧数要算上 loadICN() 补出来的那六张，见 GetArchiveFrame。
            return Gui.EditorPlayerIcons.GetFrameCount(fileName, GetIcn(fileName)?.Count ?? 0);
        }
    }

    /// <summary>
    /// Whether <paramref name="fileName"/> is a table the engine assembles itself rather than an archive entry.
    /// </summary>
    /// <param name="fileName">Asset name, for example <c>LGNDXTRA.ICN</c> or <c>BUTTON_SMALL_OKAY_GOOD.ICN</c>.</param>
    /// <returns><see langword="true"/> when <see cref="GetSynthetic"/> builds the frames.</returns>
    /// <remarks>
    /// 归档侧的解码缓存（<c>IcnSpriteCache</c>）把查不到的名字记成「缺图」并告警；合成表本来就没有归档条目，
    /// 先问这里就不会为它们报假警。
    /// </remarks>
    public static bool IsRuntimeTable(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        lock (SyncRoot)
        {
            return GetSynthetic(fileName) is not null;
        }
    }

    /// <summary>
    /// The frames of an ICN the engine assembles itself, or <see langword="null"/> when
    /// <paramref name="fileName"/> is an ordinary archive entry.
    /// </summary>
    /// <remarks>
    /// 参考：<c>generateDefaultImages()</c>（<c>game_assets.cpp:749</c>）在 <c>loadICN()</c> 里先造图、再造不出来的
    /// 时候才读归档；本项目的「造图」集中在 <see cref="Gui.EmptyButtons"/>、
    /// <see cref="Gui.SmallOkayCancelButtons"/> 与 <see cref="Gui.ScenarioInfoIcons"/>。结果按文件名缓存，失败也记成
    /// <see langword="null"/>，免得每次取图都重算一遍。
    /// </remarks>
    private static Sprite?[]? GetSynthetic(string fileName)
    {
        if (SyntheticFiles.TryGetValue(fileName, out Sprite?[]? cached))
        {
            return cached;
        }

        Sprite?[]? frames = Gui.EmptyButtons.Build(fileName)
                             ?? Gui.SmallOkayCancelButtons.Build(fileName)
                             ?? Gui.ExitButtons.Build(fileName)
                             ?? Gui.MiniMonsterSprites.Build(fileName)
                             ?? Gui.EvilInterfaceImages.Build(fileName)
                             ?? Gui.ScenarioInfoIcons.Build(fileName);
        SyntheticFiles[fileName] = frames;

        return frames;
    }

    /// <summary>One frame straight out of the archives, the body of <see cref="GetImage"/> before the synthetic tables.</summary>
    private static Sprite? GetArchiveFrame(string fileName, int frameId)
    {
        IcnSpriteFile? file = GetIcn(fileName);

        if (file is null)
        {
            return null;
        }

        // 参考 loadICN() 解完归档先原位改图、再补帧（game_assets.cpp:3948-3989）：补出来的帧号超过归档帧数，
        // 两组 AI 玩家旗标（27-33、39-44）的杂边则是「原位」改的，所以归档帧也要过一遍。
        if (frameId >= file.Count)
        {
            return Gui.EditorPlayerIcons.Build(file.Sprites, frameId);
        }

        if (Gui.NgExtraWideIcons.IsFileName(fileName))
        {
            return Gui.NgExtraWideIcons.Patch(file.Sprites, frameId) ?? file.Sprites[frameId];
        }

        return file.Sprites[frameId];
    }

    /// <summary>
    /// Raw bytes of an archive entry, the counterpart of <c>AGG::getDataFromAggFile( name, false )</c>.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>FONT.ICN</c>.</param>
    /// <returns>The decompressed entry, or <see langword="null"/> when the archive has none.</returns>
    /// <remarks>
    /// 参考：<c>src/fheroes2/agg/agg.cpp:558</c>（<c>ignoreExpansion = false</c> 表示先找扩展包）。
    /// 字体代码拿它算 CRC32 判断这是哪国语言的资材——见 <see cref="ResourceLanguage"/>。不缓存原始字节：
    /// 调用方只有寥寥几处，而整份字体也就几十 KB。
    /// </remarks>
    public static byte[]? TryReadRaw(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        lock (SyncRoot)
        {
            return _current?.TryRead(fileName);
        }
    }

    /// <summary>
    /// One frame of an ICN taken from the base archive only, the counterpart of
    /// <c>AGG::getDataFromAggFile( name, true )</c> plus one decode.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>FONT.ICN</c>.</param>
    /// <param name="frameId">Zero based frame number.</param>
    /// <returns>The frame, or <see langword="null"/> when the base archive has none.</returns>
    /// <remarks>
    /// 只给 <c>replacePOLAssetWithSW</c> 用（<c>game_assets.cpp:2462</c>）：波兰资材把 <c>*</c> 画成了 <c>.</c>，
    /// 参考从<b>基础</b>归档里再解一次同一帧换回去，所以要绕过「先扩展包」的查找顺序。
    /// </remarks>
    public static Sprite? GetBaseArchiveImage(string fileName, int frameId)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        if (frameId < 0)
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (BaseImages.TryGetValue((fileName, frameId), out Sprite? cached))
            {
                return cached;
            }

            if (!BaseFiles.TryGetValue(fileName, out IcnSpriteFile? file))
            {
                file = _current?.BaseArchive?.TryRead(fileName) is { } data ? IcnSpriteFile.Parse(data) : null;
                BaseFiles[fileName] = file;
            }

            Sprite? sprite = file is not null && frameId < file.Count ? file.Sprites[frameId] : null;
            BaseImages[(fileName, frameId)] = sprite;

            return sprite;
        }
    }
}
