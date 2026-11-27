using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The hero row of the condition panel, a port of <c>renderConditionsHeroIconAndName()</c>
/// (<c>editor_map_specs_window.cpp:243-281</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一行由四块拼成：<c>SWAPWIN</c> 第 0 帧里 (88, 66) 起 111x105 的英雄框、框左右两侧各一面城堡旗、
/// 框里 (5, 6) 的 101x93 头像，最后是框下 5 像素处的标题。
/// </para>
/// <para>
/// 参考用 <c>fheroes2::Blit</c>（带变换层）贴框与旗，用 <c>Copy</c> 贴头像——头像那张图没有透明像素，
/// 两种贴法结果一致，端口统一走 <see cref="ImageOps.BlitSprite(PixelBuffer, Point, Sprite, in Rect)"/>。
/// </para>
/// <para>
/// 头像分两支：自定义头像（<see cref="MapSpecsConditionHeroRowData.CustomPortraitId"/> 大于 0）取
/// <see cref="HeroPortraitIcn"/> 对应的大图；否则用 <see cref="HeroRacePortrait"/> 现画一张种族头像。
/// 端口的地图模型是 MP2，那里没有自定义头像，因此调用点正常都走第二支。
/// </para>
/// <para>
/// 参考对缺图一律 <c>assert</c>；端口在窗口框或旗帜缺失时返回 <see langword="false"/>（整行不画），
/// 头像缺失时只跳过头像，标题照画。
/// </para>
/// </remarks>
public static class MapSpecsConditionHeroRow
{
    /// <summary>Width of the hero frame, <c>heroFrameWidth</c> (<c>:247</c>).</summary>
    public const int FrameWidth = 111;

    /// <summary>Height of the hero frame, <c>heroFrameHeight</c> (<c>:248</c>).</summary>
    public const int FrameHeight = 105;

    /// <summary>Column of <c>SWAPWIN</c> frame 0 that the frame is cut from, <c>:252</c>.</summary>
    public const int FrameSourceX = 88;

    /// <summary>Row of <c>SWAPWIN</c> frame 0 that the frame is cut from, <c>:252</c>.</summary>
    public const int FrameSourceY = 66;

    /// <summary>Distance from the top of the panel area, <c>roi.y + 4</c> (<c>:250</c>).</summary>
    public const int TopOffset = 4;

    /// <summary>Vertical offset of both flags from the frame, <c>selectConditionRoi.y + 45</c> (<c>:258</c>).</summary>
    public const int FlagOffsetY = 45;

    /// <summary>The left flag sticks out of the frame by this many pixels, <c>selectConditionRoi.x - 21</c> (<c>:258</c>).</summary>
    public const int LeftFlagOverhang = 21;

    /// <summary>Gap between the frame and the right flag, <c>:259</c>.</summary>
    public const int RightFlagGap = 2;

    /// <summary>Horizontal offset of the portrait inside the frame, <c>:269</c>.</summary>
    public const int PortraitOffsetX = 5;

    /// <summary>Vertical offset of the portrait inside the frame, <c>:269</c>.</summary>
    public const int PortraitOffsetY = 6;

    /// <summary>Width of the portrait, <c>:272</c>.</summary>
    public const int PortraitWidth = 101;

    /// <summary>Height of the portrait, <c>:272</c>.</summary>
    public const int PortraitHeight = 93;

    /// <summary>Distance between the frame and the title, <c>:278</c>.</summary>
    public const int TitleGap = 5;

    /// <summary>
    /// The area the hero frame takes inside the panel area, <c>selectConditionRoi</c> (<c>:250</c>).
    /// </summary>
    /// <param name="roi">The panel area the row is drawn in.</param>
    /// <returns>The frame rectangle, centred horizontally.</returns>
    public static Rect GetFrameRoi(in Rect roi) =>
        new(roi.X + ((roi.Width - FrameWidth) / 2), roi.Y + TopOffset, FrameWidth, FrameHeight);

    /// <summary>
    /// Paints the row: frame, flags, portrait and title.
    /// </summary>
    /// <param name="hero">The hero the row stands for.</param>
    /// <param name="mapWidth">Width of the map, needed by the title's position.</param>
    /// <param name="roi">The panel area the row is drawn in; the title is clipped to it.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    /// <param name="gamePalette">The game palette, used to build the race portrait.</param>
    /// <returns>
    /// <see langword="false"/> when <c>SWAPWIN</c> frame 0 or one of the flags is missing and nothing was drawn;
    /// <see langword="true"/> otherwise, even when only the portrait had to be skipped.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="output"/> or <paramref name="gamePalette"/> is <see langword="null"/>, or the hero has no name string.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    public static bool TryDraw(
        in MapSpecsConditionHeroRowData hero, int mapWidth, in Rect roi, PixelBuffer output, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(gamePalette);
        ArgumentNullException.ThrowIfNull(hero.Name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);

        string windowFile = GameAssets.GetFileName(IcnType.Swapwin);
        Sprite? frame = GameAssets.GetImage(windowFile, 0);

        if (frame is null || frame.Image.Width < FrameSourceX + FrameWidth
            || frame.Image.Height < FrameSourceY + FrameHeight)
        {
            return false;
        }

        int leftFlagFrame = MapSpecsConditionIcons.LeftFlagFrame(hero.Color);
        string flagFile = GameAssets.GetFileName(IcnType.Flag32);
        Sprite? leftFlag = GameAssets.GetImage(flagFile, leftFlagFrame);
        Sprite? rightFlag = GameAssets.GetImage(flagFile, leftFlagFrame + 1);

        if (leftFlag is null || rightFlag is null)
        {
            return false;
        }

        Rect row = GetFrameRoi(roi);

        // 子矩形贴图的落点是「图内原点」：要让 (88, 66) 起的 111x105 落在 row 上，偏移得减掉源原点。
        ImageOps.BlitSprite(
            output, new Point(row.X - FrameSourceX, row.Y - FrameSourceY), frame,
            new Rect(FrameSourceX, FrameSourceY, FrameWidth, FrameHeight));

        ImageOps.BlitSprite(output, new Point(row.X - LeftFlagOverhang, row.Y + FlagOffsetY), leftFlag);
        ImageOps.BlitSprite(output, new Point(row.X + row.Width + RightFlagGap, row.Y + FlagOffsetY), rightFlag);

        if (TryBuildPortrait(hero, gamePalette) is { } portrait)
        {
            ImageOps.BlitSprite(output, new Point(row.X + PortraitOffsetX, row.Y + PortraitOffsetY), portrait);
        }

        Text title = new(
            MapSpecsConditionTitles.GetHeroTitle(hero.Name, hero.Race, hero.TileIndex, mapWidth),
            FontType.NormalWhite());
        title.FitToOneRow(roi.Width);
        title.DrawInRoi(roi.X, row.Y + row.Height + TitleGap, roi.Width, output, roi);

        return true;
    }

    /// <summary>
    /// The portrait of the row, the two branches of <c>:266-273</c>.
    /// </summary>
    /// <param name="hero">The hero the portrait belongs to.</param>
    /// <param name="gamePalette">The game palette the race portrait is composed with.</param>
    /// <returns>The portrait, or <see langword="null"/> when the artwork is missing.</returns>
    private static Sprite? TryBuildPortrait(in MapSpecsConditionHeroRowData hero, Palette gamePalette)
    {
        if (hero.CustomPortraitId > 0)
        {
            return GameAssets.GetImage(GameAssets.GetFileName(HeroPortraitIcn.Get(hero.CustomPortraitId)), 0);
        }

        return GameAssets.Current is { } assets
            ? HeroRacePortrait.TryRender(assets, hero.Race, new Size(PortraitWidth, PortraitHeight), gamePalette)
            : null;
    }
}
