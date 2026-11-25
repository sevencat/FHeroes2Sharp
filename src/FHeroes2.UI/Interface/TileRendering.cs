using System.Diagnostics;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

namespace FHeroes2.UI.Interface;

/// <summary>
/// Turning map tiles into pixels, <c>Maps::redraw*</c> of
/// <c>maps/maps_tiles_render.cpp</c> (1 327 lines).
/// </summary>
/// <remarks>
/// <para>
/// <b>Status: the per tile drawing is complete.</b> Every entry point of the reference now has a body; the
/// remaining callers are on the <see cref="GameArea"/> side (<c>Redraw</c>), which is what turns these pieces
/// into the <c>TileUnfitRenderObjectInfo</c> layers. Entry points that are still unimplemented throw rather
/// than returning a placeholder, so a missing piece fails loudly at its own call site.
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
/// <c>isObjectPartDirectRenderingRestricted</c>, <c>doesHeroImageNeedToBeReflected</c>,
/// <c>GetMonsterSpriteIndices</c>) are private members of this class.
/// </para>
/// </remarks>
public static class TileRendering
{
    /// <summary>
    /// Draws the ground tile of an empty map cell, <c>redrawEmptyTile()</c>
    /// (<c>maps_tiles_render.cpp:629-645</c>).
    /// </summary>
    /// <remarks>
    /// Only the ring of tiles just outside the map is ever asked for: indices 0-19 of the stone tile set are the
    /// corners, and the four bands follow at 20, 24, 28 and 32 with the position inside the band picking the image.
    /// </remarks>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="mp">Tile to draw.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    public static void RedrawEmptyTile(PixelBuffer dst, Point mp, GameArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        int worldWidth = World.Current.Width;
        int worldHeight = World.Current.Height;

        int index;

        if (mp.Y == -1 && mp.X >= 0 && mp.X < worldWidth)
        {
            // Above the first row.
            index = 20 + (mp.X % 4);
        }
        else if (mp.X == worldWidth && mp.Y >= 0 && mp.Y < worldHeight)
        {
            // Right of the last column.
            index = 24 + (mp.Y % 4);
        }
        else if (mp.Y == worldHeight && mp.X >= 0 && mp.X < worldWidth)
        {
            // Below the last row.
            index = 28 + (mp.X % 4);
        }
        else if (mp.X == -1 && mp.Y >= 0 && mp.Y < worldHeight)
        {
            // Left of the first column.
            index = 32 + (mp.Y % 4);
        }
        else
        {
            // The corners, addressed by the distance from the map on both axes.
            index = ((Math.Abs(mp.Y) % 4) * 4) + (Math.Abs(mp.X) % 4);
        }

        if (MapSprites.GetTileImage(TilType.Ston, index, 0) is { } image)
        {
            area.DrawTile(dst, image, mp);
        }
    }

    /// <summary>
    /// Draws the ghosts flying over the map (the Summon Elements animation), <c>redrawFlyingGhostsOnMap()</c>
    /// (<c>maps_tiles_render.cpp:648-664</c>).
    /// </summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="pos">Tile the ghosts are drawn on.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="isEditor">Whether the map editor is drawing.</param>
    /// <remarks>
    /// The sprite is taller than a tile on purpose: it is the animation of a mine being visited, which has to
    /// spill over the tile above. Only the top row needs the special case that clips it: a ghost whose frame
    /// reaches above the map must not be drawn over the border if the tile above it is still fogged.
    /// </remarks>
    public static void RedrawFlyingGhostsOnMap(PixelBuffer dst, Point pos, GameArea area, bool isEditor)
    {
        ArgumentNullException.ThrowIfNull(dst);
        ArgumentNullException.ThrowIfNull(area);

        if (MapSprites.GetSprite(IcnType.Objnhaun, (int)(GameAnimation.AdventureMapAnimationIndex % 15)) is not { } image)
        {
            return;
        }

        if (!isEditor && pos.Y == 1 && image.OffsetY < -32
            && (World.Current.GetTile(pos.X, pos.Y - 1).FogDirection & Direction.Top) == Direction.Top)
        {
            // The frame reaches into the map border itself; keep the part below the border.
            int cutY = 32 + image.OffsetY;
            Rect roi = new(0, -cutY, image.Width, image.Height + cutY);

            area.BlitOnTile(dst, image, roi, image.OffsetX, -32, pos, false, 255);
        }
        else
        {
            area.BlitOnTile(dst, image, image.OffsetX, image.OffsetY, pos, false, 255);
        }
    }

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
    /// <remarks>
    /// Two exclusions stack before the part is drawn. The puzzle map hides everything that is not terrain or a
    /// landmark, and a flag on the first row is clipped instead of drawn whole: the flag sprite hangs above its
    /// tile, so on the top row half of it would land on the map border — but only when the tile behind it is
    /// still fogged, otherwise the flag may spill into the visible neighbour.
    /// </remarks>
    public static void RedrawTopLayerObject(Tile tile, PixelBuffer dst, bool isPuzzleDraw, Point pos, GameArea area, ObjectPart part)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(dst);
        ArgumentNullException.ThrowIfNull(area);

        if (isPuzzleDraw && IsHiddenForPuzzle(tile.Ground, part.IcnType, part.IcnIndex))
        {
            return;
        }

        if (pos.Y == 0 && part.IcnType == Mp2ObjectIcnType.Flag32 && (tile.FogDirection & Direction.Top) == Direction.Top)
        {
            if (MapSprites.GetSprite(part.IcnType, part.IcnIndex) is not { } image)
            {
                return;
            }

            if (image.OffsetY < 0)
            {
                // Keep the part of the flag that is below the border.
                Rect roi = new(0, -image.OffsetY, image.Width, image.Height + image.OffsetY);

                area.BlitOnTile(dst, image, roi, image.OffsetX, 0, pos, false, 255);
            }
            else
            {
                area.BlitOnTile(dst, image, image.OffsetX, image.OffsetY, pos, false, 255);
            }
        }
        else
        {
            RenderObjectPart(dst, area, pos, part);
        }
    }

    /// <summary>
    /// Draws the fog over a tile, <c>drawFog()</c> (<c>maps_tiles_render.cpp:692-913</c>).
    /// </summary>
    /// <param name="tile">Tile to cover.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <remarks>
    /// The fog is drawn as a whole tile when every direction around it is hidden, and otherwise as one of the
    /// thirty-four sprites of <c>CLOP32.ICN</c> that draws only the edge the unexplored part of the map shares
    /// with the explored part. The reference picks that sprite with a long chain of exact direction tests; the
    /// order of the chain is part of the format because several masks overlap, so it is kept verbatim. The
    /// <c>revert</c> flag mirrors a sprite to cover the opposite edge instead of duplicating it in the file.
    /// </remarks>
    public static void DrawFog(Tile tile, PixelBuffer dst, GameArea area)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(dst);
        ArgumentNullException.ThrowIfNull(area);

        Direction fogDirection = tile.FogDirection;

        // The caller only asks for a fogged tile; a tile without the centre bit has nothing to cover.
        Debug.Assert((fogDirection & Direction.Center) != 0, "The fog is drawn for a tile that is not fogged.");

        Point mp = GetPoint(tile.Index);

        if (fogDirection == Directions.All)
        {
            if (MapSprites.GetTileImage(TilType.Clof32, (mp.X + mp.Y) % 4, 0) is { } wholeTile)
            {
                area.DrawTile(dst, wholeTile, mp);
            }

            return;
        }

        int index = 0;
        bool revert = false;

        if ((fogDirection & (Direction.Top | Direction.Bottom | Direction.Left | Direction.Right)) == 0)
        {
            index = 10;
        }
        else if (Directions.Contains(fogDirection, Direction.Top)
                 && (fogDirection & (Direction.Bottom | Direction.Left | Direction.Right)) == 0)
        {
            index = 6;
        }
        else if (Directions.Contains(fogDirection, Direction.Right)
                 && (fogDirection & (Direction.Top | Direction.Bottom | Direction.Left)) == 0)
        {
            index = 7;
        }
        else if (Directions.Contains(fogDirection, Direction.Left)
                 && (fogDirection & (Direction.Top | Direction.Bottom | Direction.Right)) == 0)
        {
            index = 7;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Direction.Bottom)
                 && (fogDirection & (Direction.Top | Direction.Left | Direction.Right)) == 0)
        {
            index = 8;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn)
                 && (fogDirection & (Direction.Left | Direction.Right)) == 0)
        {
            index = 9;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow)
                 && (fogDirection & (Direction.Top | Direction.Bottom)) == 0)
        {
            index = 29;
        }
        else if (fogDirection == (Directions.All & ~Direction.TopRight))
        {
            index = 15;
        }
        else if (fogDirection == (Directions.All & ~Direction.TopLeft))
        {
            index = 15;
            revert = true;
        }
        else if (fogDirection == (Directions.All & ~Direction.BottomRight))
        {
            index = 22;
        }
        else if (fogDirection == (Directions.All & ~Direction.BottomLeft))
        {
            index = 22;
            revert = true;
        }
        else if (fogDirection == (Directions.All & ~(Direction.TopRight | Direction.BottomRight)))
        {
            index = 16;
        }
        else if (fogDirection == (Directions.All & ~(Direction.TopLeft | Direction.BottomLeft)))
        {
            index = 16;
            revert = true;
        }
        else if (fogDirection == (Directions.All & ~(Direction.TopRight | Direction.BottomLeft)))
        {
            index = 17;
        }
        else if (fogDirection == (Directions.All & ~(Direction.TopLeft | Direction.BottomRight)))
        {
            index = 17;
            revert = true;
        }
        else if (fogDirection == (Directions.All & ~(Direction.TopLeft | Direction.TopRight)))
        {
            index = 18;
        }
        else if (fogDirection == (Directions.All & ~(Direction.BottomLeft | Direction.BottomRight)))
        {
            index = 23;
        }
        else if (fogDirection == (Directions.All & ~Directions.TopRightCorner))
        {
            index = 13;
        }
        else if (fogDirection == (Directions.All & ~Directions.TopLeftCorner))
        {
            index = 13;
            revert = true;
        }
        else if (fogDirection == (Directions.All & ~Directions.BottomRightCorner))
        {
            index = 14;
        }
        else if (fogDirection == (Directions.All & ~Directions.BottomLeftCorner))
        {
            index = 14;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Direction.Left | Direction.BottomLeft | Direction.Bottom)
                 && (fogDirection & (Direction.Top | Direction.Right)) == 0)
        {
            index = 11;
        }
        else if (Directions.Contains(fogDirection, Direction.Right | Direction.BottomRight | Direction.Bottom)
                 && (fogDirection & (Direction.Top | Direction.Left)) == 0)
        {
            index = 11;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Direction.Left | Direction.TopLeft | Direction.Top)
                 && (fogDirection & (Direction.Bottom | Direction.Right)) == 0)
        {
            index = 12;
        }
        else if (Directions.Contains(fogDirection, Direction.Right | Direction.TopRight | Direction.Top)
                 && (fogDirection & (Direction.Bottom | Direction.Left)) == 0)
        {
            index = 12;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom | Direction.Top | Direction.TopLeft)
                 && (fogDirection & (Direction.BottomLeft | Direction.BottomRight | Direction.TopRight)) == 0)
        {
            index = 19;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom | Direction.Top | Direction.TopRight)
                 && (fogDirection & (Direction.BottomLeft | Direction.BottomRight | Direction.TopLeft)) == 0)
        {
            index = 19;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom | Direction.Top | Direction.BottomLeft)
                 && (fogDirection & (Direction.TopRight | Direction.BottomRight | Direction.TopLeft)) == 0)
        {
            index = 20;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom | Direction.Top | Direction.BottomRight)
                 && (fogDirection & (Direction.TopRight | Direction.BottomLeft | Direction.TopLeft)) == 0)
        {
            index = 20;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom | Direction.Top)
                 && (fogDirection & Directions.AllCorners) == 0)
        {
            index = 21;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom | Direction.BottomLeft)
                 && (fogDirection & (Direction.Top | Direction.BottomRight)) == 0)
        {
            index = 24;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom | Direction.BottomRight)
                 && (fogDirection & (Direction.Top | Direction.BottomLeft)) == 0)
        {
            index = 24;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Direction.Left | Direction.TopLeft)
                 && (fogDirection & (Direction.Right | Direction.BottomLeft)) == 0)
        {
            index = 25;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Direction.Right | Direction.TopRight)
                 && (fogDirection & (Direction.Left | Direction.BottomRight)) == 0)
        {
            index = 25;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Direction.BottomLeft | Direction.Left)
                 && (fogDirection & (Direction.Right | Direction.TopLeft)) == 0)
        {
            index = 26;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Direction.BottomRight | Direction.Right)
                 && (fogDirection & (Direction.Left | Direction.TopRight)) == 0)
        {
            index = 26;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.TopLeft | Direction.Top)
                 && (fogDirection & (Direction.Bottom | Direction.TopRight)) == 0)
        {
            index = 30;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.TopRight | Direction.Top)
                 && (fogDirection & (Direction.Bottom | Direction.TopLeft)) == 0)
        {
            index = 30;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Direction.Bottom | Direction.Left)
                 && (fogDirection & (Direction.Top | Direction.Right | Direction.BottomLeft)) == 0)
        {
            index = 27;
        }
        else if (Directions.Contains(fogDirection, Direction.Bottom | Direction.Right)
                 && (fogDirection & (Direction.Top | Direction.Left | Direction.BottomRight)) == 0)
        {
            index = 27;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Direction.Left | Direction.Top)
                 && (fogDirection & (Direction.TopLeft | Direction.Right | Direction.Bottom)) == 0)
        {
            index = 28;
        }
        else if (Directions.Contains(fogDirection, Direction.Right | Direction.Top)
                 && (fogDirection & (Direction.TopRight | Direction.Left | Direction.Bottom)) == 0)
        {
            index = 28;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Top)
                 && (fogDirection & (Direction.Bottom | Direction.TopLeft | Direction.TopRight)) == 0)
        {
            index = 31;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Direction.Right)
                 && (fogDirection & (Direction.Left | Direction.TopRight | Direction.BottomRight)) == 0)
        {
            index = 32;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Direction.Left)
                 && (fogDirection & (Direction.Right | Direction.TopLeft | Direction.BottomLeft)) == 0)
        {
            index = 32;
            revert = true;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Direction.Bottom)
                 && (fogDirection & (Direction.Top | Direction.BottomLeft | Direction.BottomRight)) == 0)
        {
            index = 33;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Directions.BottomRow)
                 && (fogDirection & Direction.Top) == 0)
        {
            index = tile.Index % 2 != 0 ? 0 : 1;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterRow | Directions.TopRow)
                 && (fogDirection & Direction.Bottom) == 0)
        {
            index = tile.Index % 2 != 0 ? 4 : 5;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Directions.LeftColumn)
                 && (fogDirection & Direction.Right) == 0)
        {
            index = tile.Index % 2 != 0 ? 2 : 3;
        }
        else if (Directions.Contains(fogDirection, Directions.CenterColumn | Directions.RightColumn)
                 && (fogDirection & Direction.Left) == 0)
        {
            index = tile.Index % 2 != 0 ? 2 : 3;
            revert = true;
        }
        else
        {
            // A mask the format does not describe; cover the tile whole rather than leaving a hole in the fog.
            GameLog.Warn($"Invalid fog direction '{Directions.String(fogDirection)}' for tile {tile.Index}.");
            if (MapSprites.GetTileImage(TilType.Clof32, (mp.X + mp.Y) % 4, 0) is { } wholeTile)
            {
                area.DrawTile(dst, wholeTile, mp);
            }

            return;
        }

        if (MapSprites.GetSprite(IcnType.Clop32, index) is not { } sprite)
        {
            return;
        }

        int offsetX = revert ? GameBorder.TileWidth - sprite.OffsetX - sprite.Width : sprite.OffsetX;

        area.BlitOnTile(dst, sprite, offsetX, sprite.OffsetY, mp, revert, 255);
    }

    /// <summary>
    /// Draws the passability overlay of the map editor on one tile, <c>redrawPassable()</c>
    /// (<c>maps_tiles_render.cpp:916-929</c>).
    /// </summary>
    /// <param name="tile">Tile to annotate.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="friendColors">Colors whose passability the current player shares.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="isEditor">Whether the map editor is drawing.</param>
    public static void RedrawPassable(Tile tile, PixelBuffer dst, PlayerColor friendColors, GameArea area, bool isEditor)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(area);

        // The reference tints fogged tiles under WITH_DEBUG only, so the colour mask has no effect in a release build.
        _ = friendColors;

        bool isActionObject = isEditor
            ? Mp2ObjectTypes.IsActionObject(tile.MainObjectType)
            : Mp2ObjectTypes.IsInGameActionObject(tile.MainObjectType);

        if (isActionObject || tile.PassableDirections != Directions.All)
        {
            area.BlitOnTile(dst, GetPassableViewSurface(tile.PassableDirections, isActionObject), 0, 0, GetPoint(tile.Index), false, 255);
        }
    }

    /// <summary>Tile id to tile position, <c>Maps::GetPoint()</c>.</summary>
    /// <param name="tileIndex">Tile id.</param>
    /// <returns>The position of the tile.</returns>
    private static Point GetPoint(int tileIndex) => new(tileIndex % World.Current.Width, tileIndex / World.Current.Width);

    /// <summary>
    /// Draws one ground level object layer of a tile, <c>redrawBottomLayerObjects()</c>
    /// (<c>maps_tiles_render.cpp:932-978</c>).
    /// </summary>
    /// <param name="tile">Tile to draw.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="isPuzzleDraw">Whether the puzzle map is being drawn.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="level">Object layer, <c>0</c>…<c>3</c>.</param>
    /// <remarks>
    /// The object parts of the ground level are drawn first, then the main object of the tile, and finally the
    /// flags: the original game stores a flag as a ground part of the castle, but the flag has to cover the
    /// castle, so it is drawn after the main object. The reference collects the flags in a fixed array of
    /// sixteen to keep this method allocation free; the port keeps the same bound and documents the entry
    /// count instead of asserting only.
    /// </remarks>
    public static void RedrawBottomLayerObjects(Tile tile, PixelBuffer dst, bool isPuzzleDraw, GameArea area, byte level)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(dst);
        ArgumentNullException.ThrowIfNull(area);

        Debug.Assert(level <= 0x03, "The object layer is one of the four layers of the format.");

        Point mp = GetPoint(tile.Index);

        const int maxPostRenderPart = 16;
        Span<ObjectPart> postRenderingPart = stackalloc ObjectPart[maxPostRenderPart];
        int postRenderObjectCount = 0;

        foreach (ObjectPart part in tile.GroundObjectParts)
        {
            if ((byte)part.LayerType != level)
            {
                continue;
            }

            if (isPuzzleDraw && IsHiddenForPuzzle(tile.Ground, part.IcnType, part.IcnIndex))
            {
                continue;
            }

            if (part.IcnType == Mp2ObjectIcnType.Flag32)
            {
                // A tile cannot hold more than sixteen flags; the bound is the reference's.
                Debug.Assert(postRenderObjectCount < maxPostRenderPart, "More flags on one tile than the format allows.");

                if (postRenderObjectCount < maxPostRenderPart)
                {
                    postRenderingPart[postRenderObjectCount] = part;
                    ++postRenderObjectCount;
                }

                continue;
            }

            RenderObjectPart(dst, area, mp, part);
        }

        ObjectPart mainPart = tile.MainObjectPart;

        if (mainPart.IcnType != Mp2ObjectIcnType.Unknown
            && (byte)mainPart.LayerType == level
            && (!isPuzzleDraw || !IsHiddenForPuzzle(tile.Ground, mainPart.IcnType, mainPart.IcnIndex)))
        {
            RenderMainObject(dst, area, mp, tile);
        }

        for (int i = 0; i < postRenderObjectCount; ++i)
        {
            RenderObjectPart(dst, area, mp, postRenderingPart[i]);
        }
    }

    /// <summary>
    /// Draws every part of a tile that uses the given ICN tile set, <c>drawByObjectIcnType()</c>
    /// (<c>maps_tiles_render.cpp:981-999</c>).
    /// </summary>
    /// <param name="tile">Tile to draw.</param>
    /// <param name="output">Frame buffer.</param>
    /// <param name="area">Viewport the tile is drawn into.</param>
    /// <param name="objectIcnType">Tile set to draw the parts of.</param>
    /// <remarks>
    /// The passability overlay of the map editor redraws a single object when a hero moves: the caller has the
    /// tile, the frame buffer and the ICN of the object that changed, and this method finds every part of the
    /// tile that belongs to it — the ground level ones, the main object and the top level ones. The order
    /// matches the reference: ground parts, the main object, then the top level parts.
    /// </remarks>
    public static void DrawByObjectIcnType(Tile tile, PixelBuffer output, GameArea area, Mp2ObjectIcnType objectIcnType)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(area);

        Point tileOffset = GetPoint(tile.Index);

        foreach (ObjectPart part in tile.GroundObjectParts)
        {
            if (part.IcnType == objectIcnType)
            {
                RenderObjectPart(output, area, tileOffset, part);
            }
        }

        if (tile.MainObjectPart.IcnType == objectIcnType)
        {
            RenderMainObject(output, area, tileOffset, tile);
        }

        foreach (ObjectPart part in tile.TopObjectParts)
        {
            if (part.IcnType == objectIcnType)
            {
                RenderObjectPart(output, area, tileOffset, part);
            }
        }
    }

    /// <summary>
    /// The sprites of the monster standing on a tile, <c>getMonsterSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1002-1042</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <param name="isEditorMode">Whether the map editor is drawing.</param>
    /// <returns>One entry per sprite piece, empty when the tile has no monster.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetMonsterSpritesPerTile(Tile tile, bool isEditorMode)
    {
        ArgumentNullException.ThrowIfNull(tile);

        Debug.Assert(tile.MainObjectType == Mp2ObjectType.ActionMonster);

        uint monsterIndex = Tile.MonsterSpriteIndex(tile.GetMonster(World.Current));
        (uint primary, uint secondary) = GetMonsterSpriteIndices(tile, monsterIndex, isEditorMode);

        var output = new List<ObjectRenderingInfo>();
        AppendMonsterSpritePieces(output, IcnType.MiniMonsterImage, primary, secondary);

        return output;
    }

    /// <summary>
    /// The shadow sprites of the monster standing on a tile, <c>getMonsterShadowSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1045-1085</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <param name="isEditorMode">Whether the map editor is drawing.</param>
    /// <returns>One entry per shadow piece.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetMonsterShadowSpritesPerTile(Tile tile, bool isEditorMode)
    {
        ArgumentNullException.ThrowIfNull(tile);

        Debug.Assert(tile.MainObjectType == Mp2ObjectType.ActionMonster);

        uint monsterIndex = Tile.MonsterSpriteIndex(tile.GetMonster(World.Current));
        (uint primary, uint secondary) = GetMonsterSpriteIndices(tile, monsterIndex, isEditorMode);

        var output = new List<ObjectRenderingInfo>();
        AppendMonsterSpritePieces(output, IcnType.MiniMonsterShadow, primary, secondary);

        return output;
    }

    /// <summary>
    /// The sprites of the empty boat on a tile, <c>getBoatSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1088-1116</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry per sprite piece, empty when the tile has no boat.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetBoatSpritesPerTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        Debug.Assert(tile.MainObjectType == Mp2ObjectType.ActionBoat);

        uint spriteIndex = tile.MainObjectPart.IcnIndex == ObjectPart.EmptyIcnIndex ? 18u : tile.MainObjectPart.IcnIndex;
        bool isReflected = spriteIndex > 128;

        IcnType icnId = IcnType.Boat32;
        int icnIndex = (int)(spriteIndex % 128);

        var output = new List<ObjectRenderingInfo>();

        if (MapSprites.GetSprite(icnId, icnIndex) is not { } boatSprite)
        {
            return output;
        }

        // Boat sprites are stored as 2 sides of 14 sprites, the right side is the left one mirrored.
        Point boatSpriteOffset = new(
            isReflected ? GameBorder.TileWidth + 1 - boatSprite.OffsetX - boatSprite.Width : boatSprite.OffsetX,
            boatSprite.OffsetY + GameBorder.TileWidth - 11);

        AppendDividedSprite(output, icnId, icnIndex, boatSpriteOffset, boatSprite, isReflected);

        return output;
    }

    /// <summary>
    /// The shadow of the empty boat on a tile, <c>getBoatShadowSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1119-1143</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry per shadow piece.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetBoatShadowSpritesPerTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        Debug.Assert(tile.MainObjectType == Mp2ObjectType.ActionBoat);

        uint spriteIndex = tile.MainObjectPart.IcnIndex == ObjectPart.EmptyIcnIndex ? 18u : tile.MainObjectPart.IcnIndex;

        IcnType icnId = IcnType.Boatshad;
        int icnIndex = (int)(spriteIndex % 128);

        var output = new List<ObjectRenderingInfo>();

        if (MapSprites.GetSprite(icnId, icnIndex) is not { } shadowSprite)
        {
            return output;
        }

        Point shadowSpriteOffset = new(shadowSprite.OffsetX, GameBorder.TileWidth + shadowSprite.OffsetY - 11);

        AppendDividedSprite(output, icnId, icnIndex, shadowSpriteOffset, shadowSprite, false);

        return output;
    }

    /// <summary>
    /// The sprites of the guardian of a mine, <c>getMineGuardianSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1146-1181</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry per sprite piece, empty when the mine has no guardian.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetMineGuardianSpritesPerTile(World world, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tile);

        Debug.Assert(tile.MainObjectType == Mp2ObjectType.ActionMine);

        var output = new List<ObjectRenderingInfo>();

        SpellType spellId = tile.GetMineSpellId(world);

        switch (spellId)
        {
            case SpellType.SetEGuardian:
            case SpellType.SetAGuardian:
            case SpellType.SetFGuardian:
            case SpellType.SetWGuardian:
            {
                // The four guardians are stored back to back, in this order
                // (spell.h:45-48 — reordering the enum without this table would break the sprite ids).
                IcnType icnId = IcnType.Objnxtra;
                int icnIndex = (int)spellId - (int)SpellType.SetEGuardian;

                if (MapSprites.GetSprite(icnId, icnIndex) is not { } spriteMineGuardian)
                {
                    return output;
                }

                AppendDividedSprite(
                    output,
                    icnId,
                    icnIndex,
                    new Point(spriteMineGuardian.OffsetX, spriteMineGuardian.OffsetY),
                    spriteMineGuardian,
                    false);

                break;
            }

            default:
                break;
        }

        return output;
    }

    /// <summary>
    /// The sprites of a hero including the flag and the froth of a boat, <c>getHeroSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1184-1263</c>).
    /// </summary>
    /// <param name="hero">Hero to draw.</param>
    /// <returns>The body, the flag and — on water — the froth pieces.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetHeroSpritesPerTile(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        var output = new List<ObjectRenderingInfo>();

        bool reflect = DoesHeroImageNeedToBeReflected(hero.Direction);

        // The flag of a standing hero follows the adventure map animation instead of the movement one, so that
        // flags keep waving while the hero does not move. A hero on a boat has no waving flag at all.
        int flagFrameId = hero.SpriteIndex;
        if (!hero.IsMoveEnabled)
        {
            flagFrameId = hero.IsShipMaster ? 0 : (int)GameAnimation.AdventureMapAnimationIndex;
        }

        Point offset = new(0, hero.IsShipMaster ? -11 : -1);
        offset += hero.GetCurrentPixelOffset();

        (IcnType icnId, int icnIndex) = GetHeroSpriteInfo(hero, hero.SpriteIndex, false);

        if (MapSprites.GetSprite(icnId, icnIndex) is not { } spriteHero)
        {
            return output;
        }

        Point heroSpriteOffset = new(
            offset.X + (reflect ? GameBorder.TileWidth + 1 - spriteHero.OffsetX - spriteHero.Width : spriteHero.OffsetX),
            offset.Y + spriteHero.OffsetY + GameBorder.TileWidth);

        AppendDividedSprite(output, icnId, icnIndex, heroSpriteOffset, spriteHero, reflect);

        (Point flagOffset, IcnType flagIcnId, int flagIcnIndex) = GetFlagSpriteInfo(hero, flagFrameId, false);

        if (MapSprites.GetSprite(flagIcnId, flagIcnIndex) is not { } spriteFlag)
        {
            return output;
        }

        // The flag is drawn on the other side of the hero when the hero sprite is reflected.
        Point flagSpriteOffset = new(
            offset.X + (reflect ? GameBorder.TileWidth + 1 - spriteFlag.OffsetX - flagOffset.X - spriteFlag.Width : spriteFlag.OffsetX + flagOffset.X),
            offset.Y + spriteFlag.OffsetY + flagOffset.Y + GameBorder.TileWidth);

        AppendDividedSprite(output, flagIcnId, flagIcnIndex, flagSpriteOffset, spriteFlag, reflect);

        if (hero.IsShipMaster && hero.IsMoveEnabled && hero.IsInDeepOcean(World.Current))
        {
            (IcnType frothIcnId, int frothIcnIndex) = GetFrothSpriteInfo(hero, hero.SpriteIndex);

            if (MapSprites.GetSprite(frothIcnId, frothIcnIndex) is not { } spriteFroth)
            {
                return output;
            }

            Point frothSpriteOffset = new(
                offset.X + (reflect ? GameBorder.TileWidth - spriteFroth.OffsetX - spriteFroth.Width : spriteFroth.OffsetX),
                offset.Y + spriteFroth.OffsetY + GameBorder.TileWidth);

            AppendDividedSprite(output, frothIcnId, frothIcnIndex, frothSpriteOffset, spriteFroth, reflect);
        }

        return output;
    }

    /// <summary>
    /// The shadow of a hero, <c>getHeroShadowSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1266-1295</c>).
    /// </summary>
    /// <param name="hero">Hero to draw.</param>
    /// <returns>One entry per shadow piece.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetHeroShadowSpritesPerTile(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        var output = new List<ObjectRenderingInfo>();

        // A boat is drawn 11 pixels higher than a hero standing on the ground.
        Point offset = new(0, hero.IsShipMaster ? -11 : 0);
        offset += hero.GetCurrentPixelOffset();

        (IcnType icnId, int icnIndex) = GetShadowSpriteInfo(hero, hero.SpriteIndex);

        if (MapSprites.GetSprite(icnId, icnIndex) is not { } shadowSprite)
        {
            return output;
        }

        Point shadowSpriteOffset = new(
            offset.X + shadowSprite.OffsetX,
            offset.Y + shadowSprite.OffsetY + GameBorder.TileWidth);

        AppendDividedSprite(output, icnId, icnIndex, shadowSpriteOffset, shadowSprite, false);

        return output;
    }

    /// <summary>
    /// The sprite of the hero the map editor placed on a tile, <c>getEditorHeroSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1298-1321</c>).
    /// </summary>
    /// <param name="tile">Tile to inspect.</param>
    /// <returns>One entry, or an empty list when the tile has no hero.</returns>
    public static IReadOnlyList<ObjectRenderingInfo> GetEditorHeroSpritesPerTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var output = new List<ObjectRenderingInfo>();

        if (tile.MainObjectType != Mp2ObjectType.ActionHero)
        {
            return output;
        }

        IcnType icnId = IcnType.Minihero;
        int icnIndex = tile.MainObjectPart.IcnIndex;

        if (MapSprites.GetSprite(icnId, icnIndex) is not { } sprite)
        {
            return output;
        }

        // The editor hero is a 32x32 mini hero anchored so that its feet land on the bottom of the tile.
        Point spriteOffset = new(0, 32 - 50);

        AppendDividedSprite(output, icnId, icnIndex, spriteOffset, sprite, false);

        return output;
    }

    /// <summary>
    /// Where a monster sprite is anchored inside its tile, <c>monsterImageOffset</c>
    /// (<c>maps_tiles_render.cpp:58</c>).
    /// </summary>
    private static readonly Point MonsterImageOffset = new(16, 30);

    // The flag of a standing hero swings around its tile, the tables below are indexed by the movement frame
    // inside a tile (maps_tiles_render.cpp:508-517).
    private static readonly Point[] FlagOffsetTop =
    {
        new(0, 0), new(0, 2), new(0, 3), new(0, 2), new(0, 0), new(0, 1), new(0, 3), new(0, 2), new(0, 1),
    };

    private static readonly Point[] FlagOffsetBottom =
    {
        new(0, 0), new(0, -1), new(0, -2), new(0, 0), new(0, -1), new(0, -2), new(0, -3), new(0, 0), new(0, -1),
    };

    private static readonly Point[] FlagOffsetSideways =
    {
        new(0, 0), new(-1, 0), new(0, 0), new(1, 0), new(1, -1), new(2, -1), new(1, 0), new(0, 0), new(1, 0),
    };

    private static readonly Point[] FlagOffsetTopSideways =
    {
        new(0, 0), new(-1, 0), new(0, 0), new(-1, -1), new(-2, -1), new(-2, 0), new(-1, 0), new(0, 0), new(1, 0),
    };

    private static readonly Point[] FlagOffsetBottomSideways =
    {
        new(0, 0), new(-1, 0), new(0, -1), new(2, -2), new(0, -2), new(-1, -3), new(-1, -2), new(-1, -1), new(1, 0),
    };

    /// <summary>
    /// Splits a sprite into tile sized pieces and appends them to <paramref name="output"/>,
    /// the body of the eight <c>get*SpritesPerTile()</c> functions (<c>maps_tiles_render.cpp:1013-1017</c>).
    /// </summary>
    private static void AppendDividedSprite(
        List<ObjectRenderingInfo> output,
        IcnType icnId,
        int icnIndex,
        Point spriteOffset,
        Sprite sprite,
        bool isFlipped)
    {
        foreach (DividedSquare square in ImageOps.DivideImageBySquares(spriteOffset, sprite, GameBorder.TileWidth))
        {
            output.Add(new ObjectRenderingInfo(square.SquareId, square.ImageOffset, square.Area, icnId, icnIndex, isFlipped, 255));
        }
    }

    /// <summary>
    /// The shared body of <c>getMonsterSpritesPerTile()</c> and <c>getMonsterShadowSpritesPerTile()</c>
    /// (<c>maps_tiles_render.cpp:1005-1042</c>, <c>:1048-1085</c>); the reference repeats it once per ICN.
    /// </summary>
    private static void AppendMonsterSpritePieces(
        List<ObjectRenderingInfo> output,
        IcnType icnId,
        uint primaryIndex,
        uint secondaryIndex)
    {
        if (MapSprites.GetSprite(icnId, (int)primaryIndex) is not { } monsterSprite)
        {
            return;
        }

        Point monsterSpriteOffset = new(
            monsterSprite.OffsetX + MonsterImageOffset.X,
            monsterSprite.OffsetY + MonsterImageOffset.Y);

        AppendDividedSprite(output, icnId, (int)primaryIndex, monsterSpriteOffset, monsterSprite, false);

        if (secondaryIndex > 0 && MapSprites.GetSprite(icnId, (int)secondaryIndex) is { } secondaryMonsterSprite)
        {
            Point secondarySpriteOffset = new(
                secondaryMonsterSprite.OffsetX + MonsterImageOffset.X,
                secondaryMonsterSprite.OffsetY + MonsterImageOffset.Y);

            AppendDividedSprite(output, icnId, (int)secondaryIndex, secondarySpriteOffset, secondaryMonsterSprite, false);
        }
    }

    /// <summary>
    /// The two monster frames to draw, <c>GetMonsterSpriteIndices()</c> (<c>maps_tiles_render.cpp:292-334</c>).
    /// </summary>
    /// <remarks>
    /// A monster that a hero is about to attack turns towards the hero and shows its attack frame; every other
    /// monster animates on the adventure map. The two indices are the standing frame and, when animating, the
    /// frame of the shared animation sequence that this tile shows at the current animation step.
    /// </remarks>
    private static (uint Primary, uint Secondary) GetMonsterSpriteIndices(Tile tile, uint monsterIndex, bool isEditorMode)
    {
        int tileIndex = tile.Index;
        int attackerIndex = -1;

        if (!isEditorMode)
        {
            foreach (int index in World.Current.ScanAroundObject(tileIndex, Mp2ObjectType.ActionHero, false))
            {
                Hero? hero = World.Current.GetTile(index).GetHero(World.Current);
                if (hero is null)
                {
                    Debug.Assert(false, "A tile reported as a hero tile holds no hero.");
                    continue;
                }

                if (hero.AttackedMonsterTileIndex == tileIndex)
                {
                    attackerIndex = index;
                    break;
                }
            }
        }

        uint primary = monsterIndex * 9;
        uint secondary = 0;

        if (attackerIndex != -1)
        {
            primary += 7;

            Direction direction = World.Current.Geometry is { } geometry
                ? geometry.GetDirection(tileIndex, attackerIndex)
                : Direction.Unknown;

            if (direction is Direction.TopLeft or Direction.Left or Direction.BottomLeft)
            {
                primary += 1;
            }
        }
        else
        {
            Point mp = GetPoint(tileIndex);
            ReadOnlySpan<byte> sequence = MonsterAnimationSequence.Frames;

            // The reference multiplies two uint32 coordinates; the port widens the product so that a large map
            // cannot overflow into a negative remainder.
            long animationStep = GameAnimation.AdventureMapAnimationIndex + ((long)mp.X * mp.Y);

            secondary = monsterIndex * 9 + 1 + sequence[(int)(animationStep % sequence.Length)];
        }

        return (primary, secondary);
    }

    /// <summary>
    /// Whether a hero facing <paramref name="direction"/> has to be drawn mirrored,
    /// <c>doesHeroImageNeedToBeReflected()</c> (<c>maps_tiles_render.cpp:336-349</c>).
    /// </summary>
    private static bool DoesHeroImageNeedToBeReflected(Direction direction) =>
        direction is Direction.TopLeft or Direction.Left or Direction.BottomLeft;

    /// <summary>
    /// The ICN and frame of a hero, <c>getHeroSpriteInfo()</c> (<c>maps_tiles_render.cpp:351-428</c>).
    /// </summary>
    private static (IcnType IcnId, int IcnIndex) GetHeroSpriteInfo(Hero hero, int heroMovementIndex, bool isHeroChangingDirection)
    {
        IcnType icnId;

        if (hero.IsShipMaster)
        {
            icnId = IcnType.Boat32;
        }
        else
        {
            icnId = hero.Race switch
            {
                MapRace.Knight => IcnType.Kngt32,
                MapRace.Barbarian => IcnType.Barb32,
                MapRace.Sorceress => IcnType.Sorc32,
                MapRace.Warlock => IcnType.Wrlk32,
                MapRace.Wizard => IcnType.Wzrd32,
                MapRace.Necromancer => IcnType.Necr32,
                _ => UnknownHeroRaceIcn(hero.Race),
            };
        }

        int icnIndex = isHeroChangingDirection ? 45 : GetHeroDirectionBaseIndex(hero.Direction);

        return (icnId, icnIndex + (heroMovementIndex % Hero.FrameCountPerTile));
    }

    /// <summary>
    /// An unknown race has no hero sprite; the reference asserts in debug builds and keeps <c>ICN::UNKNOWN</c>,
    /// which draws nothing.
    /// </summary>
    private static IcnType UnknownHeroRaceIcn(MapRace race)
    {
        Debug.Assert(false, "Unknown hero race.");
        GameLog.Warn($"Unknown hero race {race} has no hero sprite.");

        return IcnType.Unknown;
    }

    /// <summary>
    /// The first frame of a hero facing <paramref name="direction"/>; the left facing directions share the frame
    /// of their mirrored right facing counterpart (<c>maps_tiles_render.cpp:394-424</c>).
    /// </summary>
    private static int GetHeroDirectionBaseIndex(Direction direction) => direction switch
    {
        Direction.Top => 0,
        Direction.TopRight => 9,
        Direction.Right => 18,
        Direction.BottomRight => 27,
        Direction.Bottom => 36,
        Direction.BottomLeft => 27,
        Direction.Left => 18,
        Direction.TopLeft => 9,
        _ => UnknownHeroDirectionIndex(direction),
    };

    /// <summary>
    /// An unknown direction keeps the first frame, the reference only logs (<c>maps_tiles_render.cpp:421-423</c>).
    /// </summary>
    private static int UnknownHeroDirectionIndex(Direction direction)
    {
        GameLog.Warn($"Unknown hero direction {(int)direction} was treated as facing the top.");

        return 0;
    }

    /// <summary>
    /// The ICN, frame and offset of a hero's flag, <c>getFlagSpriteInfo()</c>
    /// (<c>maps_tiles_render.cpp:430-544</c>).
    /// </summary>
    private static (Point FlagOffset, IcnType IcnId, int IcnIndex) GetFlagSpriteInfo(
        Hero hero,
        int heroMovementIndex,
        bool isHeroChangingDirection)
    {
        IcnType icnId = hero.Color switch
        {
            PlayerColor.Blue => hero.IsShipMaster ? IcnType.BBflg32 : IcnType.BFlag32,
            PlayerColor.Green => hero.IsShipMaster ? IcnType.GBflg32 : IcnType.GFlag32,
            PlayerColor.Red => hero.IsShipMaster ? IcnType.RBflg32 : IcnType.RFlag32,
            PlayerColor.Yellow => hero.IsShipMaster ? IcnType.YBflg32 : IcnType.YFlag32,
            PlayerColor.Orange => hero.IsShipMaster ? IcnType.OBflg32 : IcnType.OFlag32,
            PlayerColor.Purple => hero.IsShipMaster ? IcnType.PBflg32 : IcnType.PFlag32,
            _ => UnknownFlagColorIcn(hero.Color),
        };

        int icnIndex = isHeroChangingDirection ? 45 : GetHeroDirectionBaseIndex(hero.Direction);
        int frameId = heroMovementIndex % Hero.FrameCountPerTile;

        icnIndex += frameId;

        // A moving hero and a hero on a boat carry their flag with the sprite, only a standing hero gets a
        // separately animated flag that needs its own offset.
        if (hero.IsMoveEnabled || hero.IsShipMaster)
        {
            return (new Point(0, 0), icnId, icnIndex);
        }

        Point flagOffset = hero.Direction switch
        {
            Direction.Top => FlagOffsetTop[frameId],
            Direction.Bottom => FlagOffsetBottom[frameId],
            Direction.Right or Direction.Left => FlagOffsetSideways[frameId],
            Direction.TopRight or Direction.TopLeft => FlagOffsetTopSideways[frameId],
            Direction.BottomRight or Direction.BottomLeft => FlagOffsetBottomSideways[frameId],
            _ => UnknownFlagDirectionOffset(hero.Direction),
        };

        return (flagOffset, icnId, icnIndex);
    }

    /// <summary>
    /// An unknown color has no flag sprite; the reference only logs (<c>maps_tiles_render.cpp:456-458</c>).
    /// </summary>
    private static IcnType UnknownFlagColorIcn(PlayerColor color)
    {
        GameLog.Warn($"Unknown hero color {color} has no flag sprite.");

        return IcnType.Unknown;
    }

    /// <summary>
    /// An unknown direction keeps the unshifted flag, the reference only logs
    /// (<c>maps_tiles_render.cpp:540-542</c>).
    /// </summary>
    private static Point UnknownFlagDirectionOffset(Direction direction)
    {
        GameLog.Warn($"Unknown hero direction {(int)direction} has no flag offset.");

        return new Point(0, 0);
    }

    /// <summary>
    /// The ICN and frame of a hero's shadow, <c>getShadowSpriteInfo()</c> (<c>maps_tiles_render.cpp:546-584</c>).
    /// </summary>
    /// <remarks>
    /// The shadow atlas lays the left facing rows next to the right facing ones in a different order than the
    /// hero atlas, and a hero on a boat uses a different set of rows than a hero on the ground.
    /// </remarks>
    private static (IcnType IcnId, int IcnIndex) GetShadowSpriteInfo(Hero hero, int heroMovementIndex)
    {
        bool isOnBoat = hero.IsShipMaster;

        int icnIndex = hero.Direction switch
        {
            Direction.Top => 0,
            Direction.TopRight => 9,
            Direction.Right => 18,
            Direction.BottomRight => 27,
            Direction.Bottom => 36,
            Direction.BottomLeft => isOnBoat ? 45 : 77,
            Direction.Left => isOnBoat ? 54 : 68,
            Direction.TopLeft => isOnBoat ? 63 : 59,
            _ => UnknownHeroDirectionIndex(hero.Direction),
        };

        IcnType icnId = isOnBoat ? IcnType.Boatshad : IcnType.Shadow32;

        return (icnId, icnIndex + (heroMovementIndex % Hero.FrameCountPerTile));
    }

    /// <summary>
    /// The ICN and frame of the froth under a boat, <c>getFrothSpriteInfo()</c>
    /// (<c>maps_tiles_render.cpp:586-624</c>).
    /// </summary>
    private static (IcnType IcnId, int IcnIndex) GetFrothSpriteInfo(Hero hero, int heroMovementIndex)
    {
        int icnIndex = GetHeroDirectionBaseIndex(hero.Direction);

        return (IcnType.Froth, icnIndex + (heroMovementIndex % Hero.FrameCountPerTile));
    }

    /// <summary>
    /// Draws one ground level object part, <c>renderObjectPart()</c> (<c>maps_tiles_render.cpp:100-140</c>).
    /// </summary>
    /// <remarks>
    /// The part is drawn at the offset its sprite carries and, when the object part table knows an animation
    /// for it, its animated frame is drawn on top with the same opacity. The reference asserts that the sprite
    /// fits into one tile; the port does not (a debug assertion would fire on the flags the reference itself
    /// allows to be bigger), it only relies on <see cref="GameArea.BlitOnTile(PixelBuffer, Sprite, int, int, Point, bool, byte)"/>
    /// clipping.
    /// </remarks>
    /// <param name="output">Frame buffer.</param>
    /// <param name="area">Viewport the part is drawn into.</param>
    /// <param name="offset">Tile the part belongs to.</param>
    /// <param name="part">The part to draw.</param>
    private static void RenderObjectPart(PixelBuffer output, GameArea area, Point offset, ObjectPart part)
    {
        if (IsObjectPartDirectRenderingRestricted(part.IcnType))
        {
            // Either an invalid sprite or one that has to be divided into tiles to be drawn correctly.
            return;
        }

        byte alphaValue = area.GetObjectAlphaValue(part.Uid);

        if (MapSprites.GetSprite(part.IcnType, part.IcnIndex) is not { } sprite)
        {
            return;
        }

        area.BlitOnTile(output, sprite, sprite.OffsetX, sprite.OffsetY, offset, false, alphaValue);

        if (MapObjectCatalog.GetObjectPartByIcn(part.IcnType, part.IcnIndex) is not { } objectInfo
            || objectInfo.AnimationFrames == 0)
        {
            return;
        }

        int secondaryFrameIndex = part.IcnIndex + (int)(GameAnimation.AdventureMapAnimationIndex % objectInfo.AnimationFrames) + 1;

        if (MapSprites.GetSprite(part.IcnType, secondaryFrameIndex) is not { } animationSprite)
        {
            return;
        }

        area.BlitOnTile(output, animationSprite, animationSprite.OffsetX, animationSprite.OffsetY, offset, false, alphaValue);
    }

    /// <summary>
    /// Draws the main object of a tile, <c>renderMainObject()</c> (<c>maps_tiles_render.cpp:142-186</c>).
    /// </summary>
    /// <remarks>
    /// The same two blits as <see cref="RenderObjectPart"/>, with one quirk of the original game: the magic
    /// garden shows the frame of its visited state — one whole animation cycle further — when its metadata says
    /// it has already been visited.
    /// </remarks>
    /// <param name="output">Frame buffer.</param>
    /// <param name="area">Viewport the object is drawn into.</param>
    /// <param name="offset">Tile the object stands on.</param>
    /// <param name="tile">Tile whose main object is drawn.</param>
    private static void RenderMainObject(PixelBuffer output, GameArea area, Point offset, Tile tile)
    {
        ObjectPart part = tile.MainObjectPart;

        if (IsTileDirectRenderingRestricted(part.IcnType, tile.MainObjectType))
        {
            return;
        }

        byte alphaValue = area.GetObjectAlphaValue(part.Uid);

        if (MapSprites.GetSprite(part.IcnType, part.IcnIndex) is not { } sprite)
        {
            return;
        }

        area.BlitOnTile(output, sprite, sprite.OffsetX, sprite.OffsetY, offset, false, alphaValue);

        if (MapObjectCatalog.GetObjectPartByIcn(part.IcnType, part.IcnIndex) is not { } objectInfo
            || objectInfo.AnimationFrames == 0)
        {
            return;
        }

        int secondaryFrameIndex = part.IcnIndex + (int)(GameAnimation.AdventureMapAnimationIndex % objectInfo.AnimationFrames) + 1;

        if (objectInfo.ObjectType == Mp2ObjectType.ActionMagicGarden && tile.Metadata[1] == 0)
        {
            // A visited magic garden shows the single frame after the whole animation.
            secondaryFrameIndex = part.IcnIndex + objectInfo.AnimationFrames + 1;
        }

        if (MapSprites.GetSprite(part.IcnType, secondaryFrameIndex) is not { } animationSprite)
        {
            return;
        }

        area.BlitOnTile(output, animationSprite, animationSprite.OffsetX, animationSprite.OffsetY, offset, false, alphaValue);
    }

    /// <summary>
    /// Whether a part has to be skipped when drawn directly, <c>isObjectPartDirectRenderingRestricted()</c>
    /// (<c>maps_tiles_render.cpp:84-98</c>).
    /// </summary>
    /// <param name="icnType">Tile set of the part.</param>
    /// <returns><see langword="true"/> when the part has to be drawn by a dedicated renderer.</returns>
    private static bool IsObjectPartDirectRenderingRestricted(Mp2ObjectIcnType icnType) =>
        icnType is Mp2ObjectIcnType.Unknown or Mp2ObjectIcnType.Boat32 or Mp2ObjectIcnType.MiniHero
            or Mp2ObjectIcnType.Mons32;

    /// <summary>
    /// Whether the main object of a tile has to be skipped when drawn directly,
    /// <c>isTileDirectRenderingRestricted()</c> (<c>maps_tiles_render.cpp:65-82</c>).
    /// </summary>
    /// <remarks>
    /// The monster case is the one exception to "monsters are drawn by their own renderer": a random monster
    /// has no sprite of its own, so the map editor shows the generic one from <c>MONS32.ICN</c>.
    /// </remarks>
    /// <param name="icnType">Tile set of the main object.</param>
    /// <param name="objectType">Type of the main object.</param>
    /// <returns><see langword="true"/> when the object has to be drawn by a dedicated renderer.</returns>
    private static bool IsTileDirectRenderingRestricted(Mp2ObjectIcnType icnType, Mp2ObjectType objectType)
    {
        switch (icnType)
        {
        case Mp2ObjectIcnType.Unknown:
        case Mp2ObjectIcnType.Boat32:
        case Mp2ObjectIcnType.MiniHero:
            return true;
        case Mp2ObjectIcnType.Mons32:
            return objectType != Mp2ObjectType.RandomMonster
                && objectType != Mp2ObjectType.RandomMonsterWeak
                && objectType != Mp2ObjectType.RandomMonsterMedium
                && objectType != Mp2ObjectType.RandomMonsterStrong
                && objectType != Mp2ObjectType.RandomMonsterVeryStrong;
        default:
            return false;
        }
    }

    /// <summary>
    /// Whether an object part stays hidden on the puzzle map, <c>MP2::isHiddenForPuzzle()</c>
    /// (<c>mp2.cpp:186-222</c>).
    /// </summary>
    /// <remarks>
    /// The puzzle map only shows terrain and landmarks: sprites from the decorative tile sets, the campfire
    /// frames that spell out the reward, and the digging hole are all omitted. The list is the reference's and
    /// is written as the set of the tile sets that never show; everything else is decided by
    /// <see cref="DiggingHole.IsSprite"/>.
    /// </remarks>
    /// <param name="ground">Terrain of the tile.</param>
    /// <param name="icnType">Tile set of the part.</param>
    /// <param name="imageIndex">Frame of the part.</param>
    /// <returns><see langword="true"/> when the part must not be drawn on the puzzle map.</returns>
    private static bool IsHiddenForPuzzle(MapGround ground, Mp2ObjectIcnType icnType, byte imageIndex)
    {
        switch (icnType)
        {
        case Mp2ObjectIcnType.Unknown:
        case Mp2ObjectIcnType.Unused1:
        case Mp2ObjectIcnType.Unused2:
        case Mp2ObjectIcnType.Unused3:
        case Mp2ObjectIcnType.Unused4:
        case Mp2ObjectIcnType.Unused5:
        case Mp2ObjectIcnType.Boat32:
        case Mp2ObjectIcnType.Unused7:
        case Mp2ObjectIcnType.Unused8:
        case Mp2ObjectIcnType.Unused9:
        case Mp2ObjectIcnType.ObjnHaun:
        case Mp2ObjectIcnType.ObjnArti:
        case Mp2ObjectIcnType.Mons32:
        case Mp2ObjectIcnType.Unused13:
        case Mp2ObjectIcnType.Flag32:
        case Mp2ObjectIcnType.Unused15:
        case Mp2ObjectIcnType.Unused16:
        case Mp2ObjectIcnType.Unused17:
        case Mp2ObjectIcnType.Unused18:
        case Mp2ObjectIcnType.Unused19:
        case Mp2ObjectIcnType.MiniMon:
        case Mp2ObjectIcnType.MiniHero:
        case Mp2ObjectIcnType.ObjnRsrc:
            return true;
        case Mp2ObjectIcnType.ObjnMult:
            // The campfire frames.
            return imageIndex >= 124 && imageIndex <= 137;
        default:
            break;
        }

        return DiggingHole.IsSprite(ground, icnType, imageIndex);
    }

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
    public static PixelBuffer? GetTileSurface(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        return MapSprites.GetTileImage(TilType.Ground32, tile.TerrainImageIndex, tile.TerrainFlags & 0x3);
    }

    /// <summary>Passability overlays, <c>PassableViewSurface()</c> (<c>maps_tiles_render.cpp:188-277</c>).</summary>
    /// <remarks>
    /// One 31 by 31 frame per passability mask and action flag: red where a hero may not leave the tile, green where
    /// it may. The reference draws the frame into a fresh image and outlines the eight sides of the tile by thirds so
    /// that a diagonal connection only colours the part of the edge that belongs to it.
    /// </remarks>
    /// <param name="passable">Passability mask of the tile.</param>
    /// <param name="isActionObject">Whether the tile carries an action object, which shifts the green.</param>
    /// <returns>The overlay, owned by the cache.</returns>
    private static Sprite GetPassableViewSurface(Direction passable, bool isActionObject)
    {
        (int passableMask, bool actionObject) key = ((int)passable, isActionObject);

        if (PassableViewSurfaces.TryGetValue(key, out Sprite? cached))
        {
            return cached;
        }

        const int size = 31;
        const byte red = 0xBA;
        byte green = isActionObject ? (byte)115 : (byte)90;

        Sprite surface = new(size, size);

        // Every pixel starts transparent, so only the frame the reference paints becomes visible.
        if (surface.Transform is { } transform)
        {
            Array.Fill(transform, (byte)1);
        }

        if (passable == Direction.Unknown || passable == Direction.Center)
        {
            ImageOps.DrawBorder(surface.Image, red);
        }
        else if (passable == Directions.All)
        {
            ImageOps.DrawBorder(surface.Image, green);
        }
        else
        {
            // Thirds of an edge: the outer thirds belong to the diagonal neighbours, the middle third to the
            // straight one.
            const int quarter = 10;
            const int threeQuarters = 21;

            byte topLeftColor = (passable & Direction.TopLeft) != 0 ? green : red;
            byte topColor = (passable & Direction.Top) != 0 ? green : red;
            byte topRightColor = (passable & Direction.TopRight) != 0 ? green : red;
            byte leftColor = (passable & Direction.Left) != 0 ? green : red;
            byte rightColor = (passable & Direction.Right) != 0 ? green : red;
            byte bottomLeftColor = (passable & Direction.BottomLeft) != 0 ? green : red;
            byte bottomColor = (passable & Direction.Bottom) != 0 ? green : red;
            byte bottomRightColor = (passable & Direction.BottomRight) != 0 ? green : red;

            Span<byte> pixels = surface.Image.Pixels;

            // Horizontal edges.
            for (int x = 0; x < quarter; ++x)
            {
                pixels[x] = topLeftColor;
                pixels[x + ((size - 1) * size)] = bottomLeftColor;
            }

            for (int x = quarter; x < threeQuarters; ++x)
            {
                pixels[x] = topColor;
                pixels[x + ((size - 1) * size)] = bottomColor;
            }

            for (int x = threeQuarters; x < size; ++x)
            {
                pixels[x] = topRightColor;
                pixels[x + ((size - 1) * size)] = bottomRightColor;
            }

            // Vertical edges.
            for (int y = 0; y < quarter; ++y)
            {
                pixels[y * size] = topLeftColor;
                pixels[(size - 1) + (y * size)] = topRightColor;
            }

            for (int y = quarter; y < threeQuarters; ++y)
            {
                pixels[y * size] = leftColor;
                pixels[(size - 1) + (y * size)] = rightColor;
            }

            for (int y = threeQuarters; y < size; ++y)
            {
                pixels[y * size] = bottomLeftColor;
                pixels[(size - 1) + (y * size)] = bottomRightColor;
            }
        }

        if (surface.Transform is { } layer)
        {
            for (int x = 0; x < size; ++x)
            {
                layer[x] = 0;
                layer[x + ((size - 1) * size)] = 0;
            }

            for (int y = 1; y < size - 1; ++y)
            {
                layer[y * size] = 0;
                layer[(size - 1) + (y * size)] = 0;
            }
        }

        PassableViewSurfaces[key] = surface;

        return surface;
    }

    /// <summary>Overlays already built, keyed by the passability mask and the action flag.</summary>
    private static readonly Dictionary<(int PassableMask, bool IsActionObject), Sprite> PassableViewSurfaces = [];
}
