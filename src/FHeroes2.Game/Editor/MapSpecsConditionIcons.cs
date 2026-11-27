using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The icons of the map object rows: <c>getTownIcon()</c> (<c>editor_map_specs_window.cpp:170-191</c>) and
/// <c>getHeroIcon()</c> (<c>:112-168</c>), together with <c>getCastleLeftFlagIcnIndex()</c>
/// (<c>ui_castle.cpp:196-220</c>).
/// </summary>
/// <remarks>
/// <para>
/// 面板里的「选中的城镇」不是一张现成图，而是三张拼出来的：<c>LOCATORS</c>（邪恶界面用 <c>LOCATORE</c>）第 23 帧
/// 的空底座、底座身体（<c>getCastleIcnIndex</c>，城堡 9-14 / 城镇 15-20，随机种族 25/26），以及左右两面
/// <c>FLAG32</c> 旗帜（每种颜色占两帧，中立城用 12/13）。
/// </para>
/// <para>
/// 拼法与参考逐像素一致：旗帜距顶 5 像素、底座身体从左边旗宽 + 6、距顶 4 像素处贴上；整图宽 = 底座宽 + 两旗宽 + 4。
/// </para>
/// <para>
/// 参考另外有个 <c>getHeroIcon()</c>（英雄选择列表用），它把头像塞进同样的城堡框里，本类只做条件面板要用的城镇版。
/// </para>
/// </remarks>
public static class MapSpecsConditionIcons
{
    /// <summary>Vertical offset of both flags, <c>:181-183</c>.</summary>
    public const int FlagOffsetY = 5;

    /// <summary>Horizontal gap between the left flag and the plate, <c>leftFlag.width() + 2</c> (<c>:182</c>).</summary>
    public const int PlateOffsetX = 2;

    /// <summary>Horizontal gap between the plate and the right flag, <c>:183</c>.</summary>
    public const int RightFlagGap = 4;

    /// <summary>Horizontal offset of the castle body inside the plate, <c>leftFlag.width() + 6</c> (<c>:188</c>).</summary>
    public const int BodyOffsetX = 6;

    /// <summary>Vertical offset of the castle body inside the plate, <c>:188</c>.</summary>
    public const int BodyOffsetY = 4;

    /// <summary>Frame of <c>LOCATORS</c> that draws the plate of a town, <c>:175</c>.</summary>
    public const int TownPlateFrame = 23;

    /// <summary>Frame of <c>LOCATORS</c> that draws the plate of a hero icon, <c>:118</c>.</summary>
    public const int HeroPlateFrame = 22;

    /// <summary>Column of the <c>NGEXTRA</c> race portrait the small hero icon takes, <c>:164</c>.</summary>
    public const int HeroPortraitSourceX = 17;

    /// <summary>Row of the <c>NGEXTRA</c> race portrait the small hero icon takes, <c>:164</c>.</summary>
    public const int HeroPortraitSourceY = 10;

    /// <summary>Width of the small hero portrait, <c>:164</c>.</summary>
    public const int HeroPortraitWidth = 30;

    /// <summary>Height of the small hero portrait, <c>:164</c>.</summary>
    public const int HeroPortraitHeight = 22;

    /// <summary>
    /// Frame of <c>FLAG32</c> that draws the left flag of <paramref name="color"/>,
    /// <c>getCastleLeftFlagIcnIndex()</c> (<c>ui_castle.cpp:196-220</c>); the right flag is the next frame.
    /// </summary>
    /// <param name="color">Colour of the flag; <see cref="PlayerColor.None"/> is the neutral one.</param>
    /// <returns>The frame index, a multiple of two.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="color"/> is not a single player colour.</exception>
    /// <remarks>
    /// 参考对这种局面只有 <c>assert( 0 )</c> 之后返回 0（蓝色旗），这里抛异常：拼错的图比崩溃更难查。
    /// </remarks>
    public static int LeftFlagFrame(PlayerColor color) => color switch
    {
        PlayerColor.Blue => 0,
        PlayerColor.Green => 2,
        PlayerColor.Red => 4,
        PlayerColor.Yellow => 6,
        PlayerColor.Orange => 8,
        PlayerColor.Purple => 10,
        PlayerColor.None => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, "A flag belongs to exactly one player colour.")
    };

    /// <summary>
    /// Builds the icon of a town row: plate, castle body and two flags.
    /// </summary>
    /// <param name="race">Race of the castle, which picks the castle body frame.</param>
    /// <param name="color">Owner of the castle; <see cref="PlayerColor.None"/> for a neutral one.</param>
    /// <param name="hasFort">Whether the castle is fortified, i.e. a castle and not a town.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork (<c>LOCATORE</c>) is used.</param>
    /// <returns>
    /// The composed icon, or <see langword="null"/> when a frame is missing or the race has no frame in the data
    /// (see <see cref="IconBar.CastleFrame"/>); the caller then draws the row without an icon rather than a wrong one.
    /// </returns>
    public static Sprite? BuildTownIcon(MapRace race, PlayerColor color, bool hasFort, bool isEvilInterface)
    {
        string flagFile = Assets.GetFileName(IcnType.Flag32);
        string townFile = Assets.GetFileName(isEvilInterface ? IcnType.Locatore : IcnType.Locators);

        int leftFrame = LeftFlagFrame(color);

        Sprite? leftFlag = Assets.GetImage(flagFile, leftFrame);
        Sprite? rightFlag = Assets.GetImage(flagFile, leftFrame + 1);
        Sprite? plate = Assets.GetImage(townFile, TownPlateFrame);

        if (leftFlag is null || rightFlag is null || plate is null)
        {
            return null;
        }

        if (IconBar.CastleFrame(race, hasFort) is not { } bodyFrame)
        {
            return null;
        }

        if (Assets.GetImage(townFile, bodyFrame) is not { } body)
        {
            return null;
        }

        // 宽度按参考：底座 + 两旗 + 4 像素的空隙，高度取底座（旗帜比底座矮，挂在半空）。
        Sprite icon = new(
            plate.Image.Width + leftFlag.Image.Width + rightFlag.Image.Width + RightFlagGap, plate.Image.Height);
        icon.Reset();

        ImageOps.Copy(leftFlag, leftFlag.Image.Bounds, icon, new Point(0, FlagOffsetY));
        ImageOps.Copy(plate, plate.Image.Bounds, icon, new Point(leftFlag.Image.Width + PlateOffsetX, 0));
        ImageOps.Copy(
            rightFlag, rightFlag.Image.Bounds, icon,
            new Point(plate.Image.Width + leftFlag.Image.Width + RightFlagGap, FlagOffsetY));
        ImageOps.Copy(
            body, body.Image.Bounds, icon, new Point(leftFlag.Image.Width + BodyOffsetX, BodyOffsetY));

        return icon;
    }

    /// <summary>
    /// Builds the icon of a hero row: the hero plate, two flags and the small portrait of the race, a port of
    /// <c>getHeroIcon()</c> (<c>editor_map_specs_window.cpp:112-168</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与城镇版的差别只在「底座里塞什么」：塞的是 <c>NGEXTRA</c> 里该种族的 30x22 那块
    /// （参考 <c>Copy( ..., 17, 10, ..., 30, 22 )</c>）。
    /// </para>
    /// <para>
    /// 参考的另一条分支——地图上有自定义头像时贴 <c>MINIPORT</c>——端口没有对应状态：MP2 不存英雄元数据，
    /// 状态层给出的英雄行也只有种族与颜色，于是这里只画种族头像。
    /// </para>
    /// </remarks>
    /// <param name="race">Race of the hero, which picks the portrait frame.</param>
    /// <param name="color">Owner of the hero, which picks the two flags.</param>
    /// <param name="isEvilInterface">Whether the evil interface artwork (<c>LOCATORE</c>) is used.</param>
    /// <returns>
    /// The composed icon, or <see langword="null"/> when a frame is missing; the caller then draws the row without one.
    /// </returns>
    public static Sprite? BuildHeroIcon(MapRace race, PlayerColor color, bool isEvilInterface)
    {
        string flagFile = Assets.GetFileName(IcnType.Flag32);
        string townFile = Assets.GetFileName(isEvilInterface ? IcnType.Locatore : IcnType.Locators);

        int leftFrame = LeftFlagFrame(color);

        Sprite? leftFlag = Assets.GetImage(flagFile, leftFrame);
        Sprite? rightFlag = Assets.GetImage(flagFile, leftFrame + 1);
        Sprite? plate = Assets.GetImage(townFile, HeroPlateFrame);

        if (leftFlag is null || rightFlag is null || plate is null)
        {
            return null;
        }

        Sprite icon = new(
            plate.Image.Width + leftFlag.Image.Width + rightFlag.Image.Width + RightFlagGap, plate.Image.Height);
        icon.Reset();

        ImageOps.Copy(leftFlag, leftFlag.Image.Bounds, icon, new Point(0, FlagOffsetY));
        ImageOps.Copy(plate, plate.Image.Bounds, icon, new Point(leftFlag.Image.Width + PlateOffsetX, 0));
        ImageOps.Copy(
            rightFlag, rightFlag.Image.Bounds, icon,
            new Point(plate.Image.Width + leftFlag.Image.Width + RightFlagGap, FlagOffsetY));

        Point portraitAt = new(leftFlag.Image.Width + BodyOffsetX, BodyOffsetY);

        if (Assets.GetImage(Assets.GetFileName(IcnType.Ngextra), HeroPortraitFrame(race)) is { } racePortrait)
        {
            ImageOps.Copy(
                racePortrait, new Rect(HeroPortraitSourceX, HeroPortraitSourceY, HeroPortraitWidth, HeroPortraitHeight),
                icon, portraitAt);
        }

        return icon;
    }

    /// <summary>
    /// Frame of <c>NGEXTRA</c> that holds the small portrait of <paramref name="race"/>, the switch of
    /// <c>getHeroIcon()</c> (<c>:135-162</c>).
    /// </summary>
    /// <param name="race">Race of the hero.</param>
    /// <returns>The frame index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="race"/> has no small portrait; the reference asserts there.
    /// </exception>
    public static int HeroPortraitFrame(MapRace race) => race switch
    {
        MapRace.Knight => 51,
        MapRace.Barbarian => 52,
        MapRace.Sorceress => 53,
        MapRace.Warlock => 54,
        MapRace.Wizard => 55,
        MapRace.Necromancer => 56,
        MapRace.Random => 58,
        _ => throw new ArgumentOutOfRangeException(nameof(race), race, "This race has no small hero portrait.")
    };
}
