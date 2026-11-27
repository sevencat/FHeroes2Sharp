using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The six <c>NGEXTRA</c> frames the editor draws and the archive does not have, the
/// <c>case ICN::NGEXTRA</c> half of the fix-ups in <c>loadICN()</c> (<c>game_assets.cpp:3940-3989</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>game_assets.cpp:3965-3989</c>。原版 <c>NGEXTRA.ICN</c> 只有 82 帧（0-81），编辑器却要用
/// 82-87 画「人类和电脑都能选」的玩家图标——六种颜色各一张。参考在解完归档后现场拼：底图取「只给电脑」
/// 的那一帧（3+i）并去掉变换层，用它的玩家颜色圈（<c>image()[252]</c>）把中间的旗面填满，再把「只有人类」
/// 的那一帧（9+i）的人类剪影按同一颜色抠出来，贴到右下（27,8）的画面上最后盖在（4,5）。
/// </para>
/// <para>
/// 端口把它挂进 <see cref="Assets"/> 的取帧路径（<c>GetImage</c> / <c>GetImageCount</c>）：归档里没有的帧号
/// 落到这里现拼，调用方（<see cref="Editor.MapSpecsPageArtwork"/>、<see cref="Editor.MapSpecsPageLayout"/>）
/// 照旧按文件名 + 帧号取，与参考「先 loadICN 再 getImage」的顺序等价。
/// </para>
/// <para>
/// 与参考一致的前提：归档必须正好 82 帧。参考那句 <c>if ( images.size() != 82 ) break;</c> 是「资材不对就别改」，
/// 这里同样只在 82 帧时补，多一帧少一帧都原样返回。
/// </para>
/// </remarks>
public static class EditorPlayerIcons
{
    /// <summary>The archive entry the extra frames belong to, <c>ICN::NGEXTRA</c>.</summary>
    public const string FileName = "NGEXTRA.ICN";

    /// <summary>Frame count of the untouched <c>NGEXTRA.ICN</c>; the fix-ups only run for exactly this many frames.</summary>
    public const int ArchiveFrameCount = 82;

    /// <summary>First frame this class builds, <c>82</c>.</summary>
    public const int FirstFrame = 82;

    /// <summary>Number of frames this class builds, one per player colour, <c>6</c>.</summary>
    public const int FrameCount = 6;

    /// <summary>Width of a player icon; the reference skips a colour whose computer frame is not this size.</summary>
    public const int IconWidth = 62;

    /// <summary>Height of a player icon.</summary>
    public const int IconHeight = 45;

    /// <summary>Frame of the "computer only" icon of the first colour, the base of the generated icons.</summary>
    private const int ComputerFrameBase = 3;

    /// <summary>Frame of the "human only" icon of the first colour, the source of the human silhouette.</summary>
    private const int HumanFrameBase = 9;

    /// <summary>
    /// Pixel of the computer icon whose colour is the player's colour, the reference's <c>image()[252]</c>
    /// of a 62 pixel wide frame, i.e. column 4 of row 4.
    /// </summary>
    private const int PlayerColorPixel = 252;

    /// <summary>
    /// Whether <paramref name="fileName"/> is the archive table this class appends frames to.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>NGEXTRA.ICN</c>.</param>
    /// <returns><see langword="true"/> for <c>NGEXTRA.ICN</c>, ignoring case.</returns>
    public static bool IsFileName(string fileName) =>
        string.Equals(fileName, FileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The frame count the table has once the extra icons are appended, <c>ASSETS::getImageCount()</c> after
    /// the reference's fix-ups.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>NGEXTRA.ICN</c>.</param>
    /// <param name="archiveFrameCount">Frame count the archive itself reported.</param>
    /// <returns>
    /// <c>88</c> for a 82 frame <c>NGEXTRA.ICN</c>, otherwise <paramref name="archiveFrameCount"/> unchanged.
    /// </returns>
    public static int GetFrameCount(string fileName, int archiveFrameCount) =>
        IsFileName(fileName) && archiveFrameCount == ArchiveFrameCount ? ArchiveFrameCount + FrameCount : archiveFrameCount;

    /// <summary>
    /// Builds one of the six editor icons out of the archived frames, <c>game_assets.cpp:3968-3989</c>.
    /// </summary>
    /// <param name="archiveFrames">The frames of the archive; must be the untouched 82 frame table.</param>
    /// <param name="frameId">Frame number, <c>82</c> through <c>87</c>.</param>
    /// <returns>
    /// The icon, or <see langword="null"/> when <paramref name="frameId"/> is outside the range this class builds,
    /// the table is not the expected size, or the colour's computer frame has another size (the reference leaves
    /// the frame empty in that case).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="archiveFrames"/> is <see langword="null"/>.</exception>
    public static Sprite? Build(IReadOnlyList<Sprite> archiveFrames, int frameId)
    {
        ArgumentNullException.ThrowIfNull(archiveFrames);

        if (archiveFrames.Count != ArchiveFrameCount || frameId < FirstFrame || frameId >= FirstFrame + FrameCount)
        {
            return null;
        }

        int colour = frameId - FirstFrame;
        Sprite computer = archiveFrames[ComputerFrameBase + colour];
        Sprite human = archiveFrames[HumanFrameBase + colour];

        if (computer.Width != IconWidth || computer.Height != IconHeight)
        {
            // 参考在这里 continue：那一帧就留成 resize 出来的空图，取图方画不出东西。
            return null;
        }

        // 参考先 _disableTransformLayer() 再 Copy，所以结果是一张不透明的单层图，位置也从 (0,0) 起算。
        Sprite icon = new(new PixelBuffer(IconWidth, IconHeight));
        ImageOps.Copy(computer, computer.Image.Bounds, icon, Point.Zero);

        // Fill 用的色号取自刚复制过来的那一帧，不是原帧：两者此刻相同，但顺序照抄不会错。
        byte playerColor = icon.Image[PlayerColorPixel % IconWidth, PlayerColorPixel / IconWidth];
        ImageOps.FillSprite(icon, new Rect(15, 8, 32, 30), playerColor);

        // 人类剪影：抄过来，把它的玩家颜色圈换成「跳过」，贴上去时背景自然透出底图。
        Sprite humanCut = new(33, 35);
        ImageOps.Copy(human, new Rect(15, 5, 33, 35), humanCut, Point.Zero);
        ImageOps.ReplaceColorIdByTransformId(humanCut, playerColor, 1);

        // 电脑图标的那半幅（右边），再把人像盖到左边。
        ImageOps.Copy(computer, new Rect(15, 8, 31, 30), icon, new Point(27, 8));
        ImageOps.BlitSprite(icon, new Point(4, 5), humanCut);

        return icon;
    }
}
