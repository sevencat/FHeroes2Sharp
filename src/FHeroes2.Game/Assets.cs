using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

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
    /// <remarks>参考：<c>src/fheroes2/game/game_assets.cpp:5817</c> Assets::getImage（越界返回 <c>errorImage</c>，见类注释的差异）。</remarks>
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

            IcnSpriteFile? file = GetIcn(fileName);
            Sprite? sprite = file is not null && frameId < file.Count ? file.Sprites[frameId] : null;
            Images[(fileName, frameId)] = sprite;
            return sprite;
        }
    }

    /// <summary>
    /// Number of frames in an ICN, the counterpart of <c>Assets::getImageCount</c>.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>FONT.ICN</c>.</param>
    /// <returns>The frame count, or 0 when the archive has no such entry.</returns>
    /// <remarks>
    /// 参考：<c>src/fheroes2/game/game_assets.h:36</c> 声明、<c>src/fheroes2/game/game_assets.cpp:5834</c>
    /// <c>uint32_t getImageCount( const int icnId )</c>（非法 ICN 号返回 0）。用它的是
    /// <c>ui_text.cpp:143</c> 的 <c>getCharacterLimit</c>——字体就是逐字符一帧的 ICN。
    /// </remarks>
    public static int GetImageCount(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        lock (SyncRoot)
        {
            return GetIcn(fileName)?.Count ?? 0;
        }
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
