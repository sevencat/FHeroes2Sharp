using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The captions of the selected town and hero rows, a port of <c>getHeroTitle()</c> / <c>getTownTitle()</c>
/// (<c>editor_map_specs_window.cpp:193-241</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的标题是「带占位符的本地化字符串」：<c>[%{pos}]: %{name}, %{race} town</c>，再由
/// <c>StringReplace</c> 填位置与种族，最后交给 <c>getLocalizedStrings</c> 处理自定义名本身也是本地化串的情况。
/// 端口没有这一层，直接拼成同样的英文文本。
/// </para>
/// <para>
/// 位置写成 <c>x, y</c>：参考用 <c>tileIndex % mapWidth</c> 与 <c>tileIndex / mapWidth</c>
/// （<c>replacePosition</c>，<c>:193-196</c>），编辑器里作者看到的是坐标而不是编号。
/// </para>
/// <para>
/// 名字为空时用的是另一种句式——「某种族的英雄/城镇」而不是「某某，某族的英雄」，参考分了两条。
/// </para>
/// </remarks>
public static class MapSpecsConditionTitles
{
    /// <summary>
    /// The caption of a hero row.
    /// </summary>
    /// <param name="name">Custom name of the hero, empty when the author did not type one.</param>
    /// <param name="race">Race of the hero.</param>
    /// <param name="tileIndex">Tile the hero stands on.</param>
    /// <param name="mapWidth">Width of the map, needed to turn the tile index into a position.</param>
    /// <returns><c>[x, y]: Name, Race hero</c> or <c>[x, y]: Race hero</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    public static string GetHeroTitle(string name, MapRace race, int tileIndex, int mapWidth)
    {
        ArgumentNullException.ThrowIfNull(name);

        return string.IsNullOrEmpty(name)
            ? $"[{FormatPosition(tileIndex, mapWidth)}]: {RaceNames.Get(race)} hero"
            : $"[{FormatPosition(tileIndex, mapWidth)}]: {name}, {RaceNames.Get(race)} hero";
    }

    /// <summary>
    /// The caption of a town row.
    /// </summary>
    /// <param name="name">Custom name of the castle, empty when the author did not type one.</param>
    /// <param name="race">Race of the castle.</param>
    /// <param name="isTown">Whether the object is a town, i.e. has no fort yet; it only changes the noun.</param>
    /// <param name="tileIndex">Tile the castle stands on.</param>
    /// <param name="mapWidth">Width of the map, needed to turn the tile index into a position.</param>
    /// <returns><c>[x, y]: Name, Race town</c> (or <c>castle</c>), with the unnamed forms as well.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    public static string GetTownTitle(string name, MapRace race, bool isTown, int tileIndex, int mapWidth)
    {
        ArgumentNullException.ThrowIfNull(name);

        string kind = isTown ? "town" : "castle";

        return string.IsNullOrEmpty(name)
            ? $"[{FormatPosition(tileIndex, mapWidth)}]: {RaceNames.Get(race)} {kind}"
            : $"[{FormatPosition(tileIndex, mapWidth)}]: {name}, {RaceNames.Get(race)} {kind}";
    }

    /// <summary>
    /// The position a tile index stands for, <c>replacePosition</c> (<c>:193-196</c>).
    /// </summary>
    /// <param name="tileIndex">Index of the tile in the map array.</param>
    /// <param name="mapWidth">Width of the map.</param>
    /// <returns>The column and the row, separated by a comma and a space, as the caption shows them.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    public static string FormatPosition(int tileIndex, int mapWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);

        return $"{tileIndex % mapWidth}, {tileIndex / mapWidth}";
    }
}
