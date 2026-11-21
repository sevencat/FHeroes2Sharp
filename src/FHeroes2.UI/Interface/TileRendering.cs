using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.UI.Interface;

/// <summary>
/// Turning map tiles into pixels, <c>Maps::redraw*</c> of
/// <c>maps/maps_tiles_render.cpp</c> (1 327 lines).
/// </summary>
/// <remarks>
/// <para>
/// <b>Status: signature-only.</b> This skeleton exists so that <see cref="GameArea"/> can be written against
/// the reference's call sites (<c>interface_gamearea.cpp:408-864</c>) without a temporary replacement at the
/// call site: every entry point here keeps the reference's name, parameter order and meaning, and throws
/// until the body lands. See ADR-197 for why the dependency is written first.
/// </para>
/// <para>
/// <b>Deviation: the file lives in the UI layer.</b> The reference file is in <c>maps/</c>, but every one of
/// its entry points takes <c>const Interface::GameArea &amp;</c> — the viewport the tile is drawn into — and
/// in this port <see cref="GameArea"/> is a <c>FHeroes2.UI</c> type that <c>FHeroes2.Game</c> must not
/// reference. The functions therefore live beside <see cref="GameArea"/> in <c>FHeroes2.UI.Interface</c>,
/// with the reference's <c>Maps::</c> qualifier dropped. The port of <c>editor/</c> (Game layer) is the one
/// place that will need a second home or an explicit geometry parameter instead of <see cref="GameArea"/>.
/// </para>
/// <para>
/// The anonymous namespace helpers of the reference (<c>renderObjectPart</c>, <c>renderMainObject</c>,
/// <c>PassableViewSurface</c>, <c>getHeroSpriteInfo</c>, <c>getFlagSpriteInfo</c>,
/// <c>getShadowSpriteInfo</c>, <c>getFrothSpriteInfo</c>, <c>isTileDirectRenderingRestricted</c>,
/// <c>isObjectPartDirectRenderingRestricted</c>, <c>doesHeroImageNeedToBeReflected</c>) are private and land
/// with the bodies — a private member with no implementation would only be dead code.
/// </para>
/// </remarks>
public static class TileRendering
{
    /// <summary>
    /// Draws the ground tile of an empty map cell, <c>redrawEmptyTile()</c>
    /// (<c>maps_tiles_render.cpp:629-645</c>).
    /// </summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="mp">Tile to draw.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static void RedrawEmptyTile(PixelBuffer dst, Point mp, GameArea area)
        => throw new NotImplementedException("Maps::redrawEmptyTile is not ported yet (maps_tiles_render.cpp:629).");

    /// <summary>
    /// Draws the ghosts flying over the map (the Summon Elements animation), <c>redrawFlyingGhostsOnMap()</c>
    /// (<c>maps_tiles_render.cpp:648-664</c>).
    /// </summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="pos">Tile the ghosts are drawn on.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="isEditor">Whether the map editor is drawing.</param>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static void RedrawFlyingGhostsOnMap(PixelBuffer dst, Point pos, GameArea area, bool isEditor)
        => throw new NotImplementedException("Maps::redrawFlyingGhostsOnMap is not ported yet (maps_tiles_render.cpp:648).");

    /// <summary>
    /// Draws one top level object part of a tile, <c>redrawTopLayerObject()</c>
    /// (<c>maps_tiles_render.cpp:667-689</c>).
    /// </summary>
    /// <param name="tile">Tile the part belongs to.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="isPuzzleDraw">Whether the puzzle map is being drawn.</param>
    /// <param name="pos">Tile position.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="part">The part to draw.</param>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static void RedrawTopLayerObject(Tile tile, PixelBuffer dst, bool isPuzzleDraw, Point pos, GameArea area, ObjectPart part)
        => throw new NotImplementedException("Maps::redrawTopLayerObject is not ported yet (maps_tiles_render.cpp:667).");

    /// <summary>
    /// Draws the fog over a tile, <c>drawFog()</c> (<c>maps_tiles_render.cpp:692-913</c>).
    /// </summary>
    /// <param name="tile">Tile to cover.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static void DrawFog(Tile tile, PixelBuffer dst, GameArea area)
        => throw new NotImplementedException("Maps::drawFog is not ported yet (maps_tiles_render.cpp:692).");

    /// <summary>
    /// Draws the passability overlay of the map editor on one tile, <c>redrawPassable()</c>
    /// (<c>maps_tiles_render.cpp:916-929</c>).
    /// </summary>
    /// <param name="tile">Tile to annotate.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="friendColors">Colors whose passability the current player shares.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="isEditor">Whether the map editor is drawing.</param>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static void RedrawPassable(Tile tile, PixelBuffer dst, PlayerColor friendColors, GameArea area, bool isEditor)
        => throw new NotImplementedException("Maps::redrawPassable is not ported yet (maps_tiles_render.cpp:916).");

    /// <summary>
    /// Draws one ground level object layer of a tile, <c>redrawBottomLayerObjects()</c>
    /// (<c>maps_tiles_render.cpp:932-978</c>).
    /// </summary>
    /// <param name="tile">Tile to draw.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="isPuzzleDraw">Whether the puzzle map is being drawn.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="level">Object layer, <c>0</c>…<c>3</c>.</param>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static void RedrawBottomLayerObjects(Tile tile, PixelBuffer dst, bool isPuzzleDraw, GameArea area, byte level)
        => throw new NotImplementedException("Maps::redrawBottomLayerObjects is not ported yet (maps_tiles_render.cpp:932).");

    /// <summary>
    /// Draws every part of a tile that uses the given ICN tile set, <c>drawByObjectIcnType()</c>
    /// (<c>maps_tiles_render.cpp:981-999</c>).
    /// </summary>
    /// <param name="tile">Tile to draw.</param>
    /// <param name="output">Frame buffer.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="objectIcnType">Tile set to draw the parts of.</param>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static void DrawByObjectIcnType(Tile tile, PixelBuffer output, GameArea area, Mp2ObjectIcnType objectIcnType)
        => throw new NotImplementedException("Maps::drawByObjectIcnType is not ported yet (maps_tiles_render.cpp:981).");

    /// <summary>
    /// The sprites of the monster standing on a tile, <c>getMonsterSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1002-1042</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <param name="isEditorMode">Whether the map editor is drawing.</param>
    /// <returns>One entry per sprite piece, empty when the tile has no monster.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetMonsterSpritesPerTile(Tile tile, bool isEditorMode)
        => throw new NotImplementedException("Maps::getMonsterSpritesPerTile is not ported yet (maps_tiles_render.cpp:1002).");

    /// <summary>
    /// The shadow sprites of the monster standing on a tile, <c>getMonsterShadowSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1045-1085</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <param name="isEditorMode">Whether the map editor is drawing.</param>
    /// <returns>One entry per shadow piece.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetMonsterShadowSpritesPerTile(Tile tile, bool isEditorMode)
        => throw new NotImplementedException("Maps::getMonsterShadowSpritesPerTile is not ported yet (maps_tiles_render.cpp:1045).");

    /// <summary>
    /// The sprites of the empty boat on a tile, <c>getBoatSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1088-1116</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry per sprite piece, empty when the tile has no boat.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetBoatSpritesPerTile(Tile tile)
        => throw new NotImplementedException("Maps::getBoatSpritesPerTile is not ported yet (maps_tiles_render.cpp:1088).");

    /// <summary>
    /// The shadow of the empty boat on a tile, <c>getBoatShadowSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1119-1143</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry per shadow piece.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetBoatShadowSpritesPerTile(Tile tile)
        => throw new NotImplementedException("Maps::getBoatShadowSpritesPerTile is not ported yet (maps_tiles_render.cpp:1119).");

    /// <summary>
    /// The sprites of the guardian of a mine, <c>getMineGuardianSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1146-1181</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry per sprite piece, empty when the mine has no guardian.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetMineGuardianSpritesPerTile(Tile tile)
        => throw new NotImplementedException("Maps::getMineGuardianSpritesPerTile is not ported yet (maps_tiles_render.cpp:1146).");

    /// <summary>
    /// The sprites of a hero including the flag and the froth of a boat, <c>getHeroSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1184-1263</c>).
    /// </summary>
    /// <param name="hero">Hero to draw.</param>
    /// <returns>The body, the flag and — on water — the froth pieces.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetHeroSpritesPerTile(Hero hero)
        => throw new NotImplementedException("Maps::getHeroSpritesPerTile is not ported yet (maps_tiles_render.cpp:1184).");

    /// <summary>
    /// The shadow of a hero, <c>getHeroShadowSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1266-1295</c>).
    /// </summary>
    /// <param name="hero">Hero to draw.</param>
    /// <returns>One entry per shadow piece.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetHeroShadowSpritesPerTile(Hero hero)
        => throw new NotImplementedException("Maps::getHeroShadowSpritesPerTile is not ported yet (maps_tiles_render.cpp:1266).");

    /// <summary>
    /// The sprite of the hero the map editor placed on a tile, <c>getEditorHeroSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1298-1321</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry, or an empty list when the tile has no hero.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static IReadOnlyList<ObjectRenderingInfo> GetEditorHeroSpritesPerTile(Tile tile)
        => throw new NotImplementedException("Maps::getEditorHeroSpritesPerTile is not ported yet (maps_tiles_render.cpp:1298).");

    /// <summary>
    /// The terrain image of a tile, <c>getTileSurface()</c> (<c>maps_tiles_render.cpp:1323-1330</c>).
    /// </summary>
    /// <remarks>
    /// The reference returns <c>const fheroes2::Image &amp;</c> out of a cache it owns; the port has no global
    /// tile cache yet and <c>TerrainTileSet.GetTile</c> returns <see langword="null"/> when the tile set is
    /// missing, so the answer is nullable.
    /// </remarks>
    /// <param name="tile">Tile to look up.</param>
    /// <returns>The terrain image, or <see langword="null"/> when the tile set is not loaded.</returns>
    /// <exception cref="NotImplementedException">Not ported yet.</exception>
    public static PixelBuffer? GetTileSurface(Tile tile)
        => throw new NotImplementedException("Maps::getTileSurface is not ported yet (maps_tiles_render.cpp:1323).");
}
