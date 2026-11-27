using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The in-place fix-up the reference applies to the two groups of wide player icons of <c>NGEXTRA.ICN</c>,
/// <c>game_assets.cpp:3948-3959</c> (the rest of <c>case ICN::NGEXTRA</c> is
/// <see cref="EditorPlayerIcons"/>'s appended frames).
/// </summary>
/// <remarks>
/// <para>
/// 原版的这两组图（27-33「电脑控制玩家」与 39-44「职业」）右侧多出一列 1 像素宽的杂边：旗标右缘是错的。
/// 参考的修法是「原位」的——解完归档立刻把第 58 列第 44-54 行抄到第 59 列，之后所有人拿到的都是改过的图
/// （<c>Copy( images[i], 58, 44, images[i], 59, 44, 1, 11 )</c>）。
/// </para>
/// <para>
/// 端口不能改归档缓存（<see cref="Assets"/> 的 <c>IcnSpriteFile</c> 被整局共用，多处的「只读」约定也写在
/// <see cref="Assets.GetImage"/> 的注释里），所以这里回一份改过的新图，由取帧的两条路各自套上：走
/// <see cref="Assets"/> 的 <see cref="Assets.GetImage"/>，以及直接读帧表的玩家面板。
/// </para>
/// <para>
/// 与参考一致的两个前提：表必须正好 82 帧（<c>if ( images.size() != 82 ) break;</c>），帧必须是 62×58
/// （两组里混着 62×45 的窄图，参考对它们只是跳过）。
/// </para>
/// </remarks>
public static class NgExtraWideIcons
{
    /// <summary>The archive entry the wide icons live in, <c>ICN::NGEXTRA</c>.</summary>
    public const string FileName = "NGEXTRA.ICN";

    /// <summary>Frame count the fix-up runs for, matching <see cref="EditorPlayerIcons.ArchiveFrameCount"/>.</summary>
    public const int ArchiveFrameCount = EditorPlayerIcons.ArchiveFrameCount;

    /// <summary>Width of a wide player icon; other frames of the table are 62×45 and are left alone.</summary>
    public const int IconWidth = 62;

    /// <summary>Height of a wide player icon.</summary>
    public const int IconHeight = 58;

    /// <summary>Column copied from, <c>58</c> in the reference's <c>Copy</c> call.</summary>
    public const int SourceColumn = 58;

    /// <summary>Column copied to, the one that stops being a stray border, <c>59</c>.</summary>
    public const int TargetColumn = 59;

    /// <summary>First row of the copied strip, <c>44</c>.</summary>
    public const int FirstRow = 44;

    /// <summary>Number of copied rows, <c>11</c>, i.e. rows 44 through 54.</summary>
    public const int RowCount = 11;

    /// <summary>First frame of the "computer controlled player" group, <c>27</c>.</summary>
    public const int FirstComputerFrame = 27;

    /// <summary>Number of frames in that group, <c>7</c> (27 through 33).</summary>
    public const int ComputerFrameCount = 7;

    /// <summary>First frame of the "class" group, <c>39</c>.</summary>
    public const int FirstClassFrame = 39;

    /// <summary>Number of frames in that group, <c>6</c> (39 through 44).</summary>
    public const int ClassFrameCount = 6;

    /// <summary>Whether <paramref name="fileName"/> is the table this class patches.</summary>
    /// <param name="fileName">Archive entry name, for example <c>NGEXTRA.ICN</c>.</param>
    /// <returns><see langword="true"/> for <c>NGEXTRA.ICN</c>, ignoring case.</returns>
    public static bool IsFileName(string fileName) =>
        string.Equals(fileName, FileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="frameId"/> belongs to one of the two patched groups.</summary>
    /// <param name="frameId">Zero based frame number.</param>
    /// <returns><see langword="true"/> for 27-33 and 39-44.</returns>
    public static bool IsPatchedFrame(int frameId) =>
        (frameId >= FirstComputerFrame && frameId < FirstComputerFrame + ComputerFrameCount)
        || (frameId >= FirstClassFrame && frameId < FirstClassFrame + ClassFrameCount);

    /// <summary>
    /// Returns the frame with the stray column removed, <c>game_assets.cpp:3948-3959</c>.
    /// </summary>
    /// <param name="archiveFrames">The frames of the archive; must be the untouched 82 frame table.</param>
    /// <param name="frameId">Frame number.</param>
    /// <returns>
    /// A copy of the frame with column <see cref="SourceColumn"/> written over column <see cref="TargetColumn"/>
    /// for rows <see cref="FirstRow"/> through <see cref="FirstRow"/> + <see cref="RowCount"/> - 1, or
    /// <see langword="null"/> when the frame is not one this fix-up touches, the table is not the expected size
    /// or the frame is not 62×58 — the caller then serves the archived frame as it is.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="archiveFrames"/> is <see langword="null"/>.</exception>
    public static Sprite? Patch(IReadOnlyList<Sprite> archiveFrames, int frameId)
    {
        ArgumentNullException.ThrowIfNull(archiveFrames);

        if (archiveFrames.Count != ArchiveFrameCount || !IsPatchedFrame(frameId))
        {
            return null;
        }

        Sprite source = archiveFrames[frameId];

        if (source.Width != IconWidth || source.Height != IconHeight)
        {
            // 参考对尺寸不对的帧直接跳过：那几张窄图（62×45）本来就没有这一列。
            return null;
        }

        // 抄的是同一张图的两列，改好的结果回新图：归档缓存不能被写脏。
        Sprite patched = source.Clone();

        ImageOps.Copy(
            patched,
            new Rect(SourceColumn, FirstRow, 1, RowCount),
            patched,
            new Point(TargetColumn, FirstRow));

        return patched;
    }
}
