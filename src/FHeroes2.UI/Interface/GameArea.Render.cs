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
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The render passes of the map viewport, <c>gui/interface_gamearea.cpp</c> (<c>:66-326</c> for the helpers and
/// <c>:404-1321</c> for <c>Redraw()</c> and the passes it drives).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the pieces of an object that do not fit into the object's own tile in seven
/// <c>std::map&lt;Point, std::deque&lt;ObjectRenderingInfo&gt;&gt;</c> buckets and draws them bucket by bucket. The
/// port keeps the same buckets in <see cref="TileUnfitRenderObjectInfo"/>; a <see cref="LinkedList{T}"/> stands in for
/// the deque (both append and prepend, both iterate front to back), but a <see cref="Dictionary{TKey,TValue}"/> does
/// not sort its keys the way <c>std::map</c> does, so <see cref="RenderImagesOnTiles"/> re-creates the reference's
/// y-then-x order before drawing.
/// </para>
/// <para>
/// The hero lookups of the reference go through the globals <c>GetFocusHeroes()</c> and
/// <c>AdventureMap::Get()</c>; the port asks <see cref="InterfaceFocus.GetFocusHeroes"/> instead, which is the same
/// query through the port's own focus holder.
/// </para>
/// </remarks>
public sealed partial class GameArea
{
    /// <summary>
    /// The parts of the objects around the viewport that stick out of their own tile, bucketed by the tile they
    /// have to be drawn on, <c>Interface::TileUnfitRenderObjectInfo</c> (<c>gui/interface_gamearea.cpp:72-84</c>).
    /// </summary>
    /// <remarks>
    /// The buckets are also what fixes the draw order inside one tile: objects whose visible part lies below their
    /// own tile go under the objects of that tile, low priority hero pieces go under everything, and the shadows
    /// always come last.
    /// </remarks>
    private sealed class TileUnfitRenderObjectInfo
    {
        /// <summary>Parts on the ground layer of their target tile, <c>bottomImages</c>.</summary>
        public Dictionary<Point, LinkedList<ObjectRenderingInfo>> BottomImages { get; } = [];

        /// <summary>Parts below the ground layer of their target tile, <c>bottomBackgroundImages</c>.</summary>
        public Dictionary<Point, LinkedList<ObjectRenderingInfo>> BottomBackgroundImages { get; } = [];

        /// <summary>Parts above the ground layer of their target tile, <c>topImages</c>.</summary>
        public Dictionary<Point, LinkedList<ObjectRenderingInfo>> TopImages { get; } = [];

        /// <summary>Moving hero parts that render under everything, <c>lowPriorityBottomImages</c>.</summary>
        public Dictionary<Point, LinkedList<ObjectRenderingInfo>> LowPriorityBottomImages { get; } = [];

        /// <summary>Moving hero parts that render over everything, <c>highPriorityBottomImages</c>.</summary>
        public Dictionary<Point, LinkedList<ObjectRenderingInfo>> HighPriorityBottomImages { get; } = [];

        /// <summary>The feet of a hero, drawn after its shadow, <c>heroBackgroundImages</c>.</summary>
        public Dictionary<Point, LinkedList<ObjectRenderingInfo>> HeroBackgroundImages { get; } = [];

        /// <summary>Shadows of everything, drawn last, <c>shadowImages</c>.</summary>
        public Dictionary<Point, LinkedList<ObjectRenderingInfo>> ShadowImages { get; } = [];
    }

    /// <summary>
    /// Appends an entry to a bucket, <c>bucket[key].emplace_front()</c>.
    /// </summary>
    private static void PrependTo(
        Dictionary<Point, LinkedList<ObjectRenderingInfo>> bucket,
        Point key,
        ObjectRenderingInfo info)
    {
        if (!bucket.TryGetValue(key, out LinkedList<ObjectRenderingInfo>? list))
        {
            list = new LinkedList<ObjectRenderingInfo>();
            bucket[key] = list;
        }

        list.AddFirst(info);
    }

    /// <summary>
    /// Appends an entry to a bucket, <c>bucket[key].emplace_back()</c>.
    /// </summary>
    private static void AppendTo(
        Dictionary<Point, LinkedList<ObjectRenderingInfo>> bucket,
        Point key,
        ObjectRenderingInfo info)
    {
        if (!bucket.TryGetValue(key, out LinkedList<ObjectRenderingInfo>? list))
        {
            list = new LinkedList<ObjectRenderingInfo>();
            bucket[key] = list;
        }

        list.AddLast(info);
    }

    /// <summary>
    /// Buckets the pieces of a static object, <c>populateStaticTileUnfitObjectInfo()</c>
    /// (<c>gui/interface_gamearea.cpp:86-138</c>).
    /// </summary>
    /// <param name="tileUnfit">Buckets to fill.</param>
    /// <param name="imageInfo">Pieces of the object body.</param>
    /// <param name="shadowInfo">Pieces of the object shadow.</param>
    /// <param name="offset">Tile the object stands on.</param>
    /// <param name="alphaValue">Opacity to draw the object with.</param>
    /// <param name="fogDirection">Fog directions of the object's tile.</param>
    private static void PopulateStaticTileUnfitObjectInfo(
        TileUnfitRenderObjectInfo tileUnfit,
        List<ObjectRenderingInfo> imageInfo,
        List<ObjectRenderingInfo> shadowInfo,
        Point offset,
        byte alphaValue,
        Direction fogDirection)
    {
        foreach (ObjectRenderingInfo objectInfo in imageInfo)
        {
            ObjectRenderingInfo info = objectInfo;
            Point imagePos = info.TileOffset;
            info.AlphaValue = alphaValue;

            if (imagePos.Y > 0)
            {
                if (imagePos.X < 0)
                {
                    PrependTo(tileUnfit.BottomBackgroundImages, imagePos + offset, info);
                }
                else
                {
                    AppendTo(tileUnfit.BottomBackgroundImages, imagePos + offset, info);
                }
            }
            else if (imagePos.Y == 0)
            {
                if (imagePos.X < 0)
                {
                    PrependTo(tileUnfit.BottomImages, imagePos + offset, info);
                }
                else
                {
                    AppendTo(tileUnfit.BottomImages, imagePos + offset, info);
                }
            }
            else
            {
                if (offset.Y == 0 && (fogDirection & Direction.Top) == Direction.Top)
                {
                    // Do not render object over the top map border if there is a nearby fog.
                    continue;
                }

                if (imagePos.X < 0)
                {
                    PrependTo(tileUnfit.TopImages, imagePos + offset, info);
                }
                else
                {
                    AppendTo(tileUnfit.TopImages, imagePos + offset, info);
                }
            }
        }

        // Static object's shadows are always on the same layer.
        foreach (ObjectRenderingInfo objectInfo in shadowInfo)
        {
            ObjectRenderingInfo info = objectInfo;
            Point imagePos = info.TileOffset + offset;

            // Shadows outside the game area should not be rendered (objects cast shadows in top-right direction).
            if (imagePos.X < 0 || imagePos.Y < 0)
            {
                continue;
            }

            info.AlphaValue = alphaValue;

            AppendTo(tileUnfit.ShadowImages, imagePos, info);
        }
    }

    /// <summary>
    /// Buckets the pieces of an object that is drawn below the ground layer of its target tile,
    /// <c>populateStaticTileUnfitBackgroundObjectInfo()</c> (<c>gui/interface_gamearea.cpp:140-157</c>).
    /// </summary>
    private static void PopulateStaticTileUnfitBackgroundObjectInfo(
        TileUnfitRenderObjectInfo tileUnfit,
        List<ObjectRenderingInfo> imageInfo,
        Point offset,
        byte alphaValue)
    {
        foreach (ObjectRenderingInfo objectInfo in imageInfo)
        {
            ObjectRenderingInfo info = objectInfo;
            Point imagePos = info.TileOffset;
            info.AlphaValue = alphaValue;

            if (imagePos.Y > 0)
            {
                PrependTo(tileUnfit.BottomBackgroundImages, imagePos + offset, info);
            }
            else if (imagePos.Y == 0)
            {
                PrependTo(tileUnfit.BottomImages, imagePos + offset, info);
            }
            else
            {
                PrependTo(tileUnfit.TopImages, imagePos + offset, info);
            }
        }
    }

    /// <summary>
    /// Buckets the pieces of a hero and of its shadow, <c>populateHeroObjectInfo()</c>
    /// (<c>gui/interface_gamearea.cpp:159-293</c>).
    /// </summary>
    /// <param name="tileUnfit">Buckets to fill.</param>
    /// <param name="hero">Hero to place.</param>
    /// <param name="fogDirection">Fog directions of the hero's tile.</param>
    private static void PopulateHeroObjectInfo(TileUnfitRenderObjectInfo tileUnfit, Hero hero, Direction fogDirection)
    {
        MapGeometry geometry = new(World.Current.Width, World.Current.Height);

        Point heroPos = geometry.GetPoint(hero.MapIndex);
        Point nextHeroPos = heroPos;

        bool movingHero = hero.IsMoveEnabled;
        if (movingHero)
        {
            HeroPath path = hero.Path;
            Debug.Assert(!path.IsEmpty, "A moving hero always has a path.");

            if (!path.IsEmpty)
            {
                nextHeroPos = geometry.GetPoint(geometry.GetDirectionIndex(hero.MapIndex, path.FrontDirection));
            }
        }

        // A castle's road south from a castle should actually be level 3 but it is level 2 causing a hero's horse legs
        // to be truncated. In order to render the legs properly the bottom part of the hero sprite has to be drawn
        // after the castle's road. This only happens when a hero stands in a castle.
        CastleTown? castle = World.Current.GetCastleEntrance(heroPos);
        bool isHeroInCastle = castle is not null && castle.TileIndex == hero.MapIndex;

        byte heroAlphaValue = hero.AlphaValue;
        int worldHeight = World.Current.Height;

        List<ObjectRenderingInfo> spriteInfo = [.. TileRendering.GetHeroSpritesPerTile(hero)];
        List<ObjectRenderingInfo> spriteShadowInfo = [.. TileRendering.GetHeroShadowSpritesPerTile(hero)];

        foreach (ObjectRenderingInfo objectInfo in spriteInfo)
        {
            ObjectRenderingInfo info = objectInfo;
            Point imagePos = info.TileOffset;
            info.AlphaValue = heroAlphaValue;

            if (movingHero && imagePos.Y == 0)
            {
                if (nextHeroPos.Y > heroPos.Y && nextHeroPos.X > heroPos.X && imagePos.X > 0)
                {
                    // The hero moves south-east. We need to render it over everything.
                    AppendTo(tileUnfit.HighPriorityBottomImages, imagePos + heroPos, info);
                    continue;
                }

                if (nextHeroPos.Y > heroPos.Y && nextHeroPos.X < heroPos.X && imagePos.X < 0)
                {
                    // The hero moves south-west. We need to render it over everything.
                    AppendTo(tileUnfit.HighPriorityBottomImages, imagePos + heroPos, info);
                    continue;
                }

                if (nextHeroPos.Y < heroPos.Y && nextHeroPos.X < heroPos.X && imagePos.X < 0)
                {
                    // The hero moves north-west. We need to render it under all other objects.
                    AppendTo(tileUnfit.LowPriorityBottomImages, imagePos + heroPos, info);
                    continue;
                }

                if (nextHeroPos.Y < heroPos.Y && nextHeroPos.X > heroPos.X && imagePos.X > 0)
                {
                    // The hero moves north-east. We need to render it under all other objects.
                    AppendTo(tileUnfit.LowPriorityBottomImages, imagePos + heroPos, info);
                    continue;
                }
            }

            if (movingHero && imagePos.Y == 1)
            {
                if (nextHeroPos.Y > heroPos.Y && nextHeroPos.X > heroPos.X && imagePos.X > 0)
                {
                    // The hero moves south-east. We need to render it over everything.
                    AppendTo(tileUnfit.BottomImages, imagePos + heroPos, info);
                    continue;
                }

                if (nextHeroPos.Y > heroPos.Y && nextHeroPos.X < heroPos.X && imagePos.X < 0)
                {
                    // The hero moves south-west. We need to render it over everything.
                    AppendTo(tileUnfit.BottomImages, imagePos + heroPos, info);
                    continue;
                }
            }

            if (movingHero && imagePos.Y == -1)
            {
                if (nextHeroPos.Y < heroPos.Y && nextHeroPos.X < heroPos.X && imagePos.X < 0)
                {
                    // The hero moves north-west. We need to render it under all other objects.
                    AppendTo(tileUnfit.BottomImages, imagePos + heroPos, info);
                    continue;
                }

                if (nextHeroPos.Y < heroPos.Y && nextHeroPos.X > heroPos.X && imagePos.X > 0)
                {
                    // The hero moves north-east. We need to render it under all other objects.
                    AppendTo(tileUnfit.BottomImages, imagePos + heroPos, info);
                    continue;
                }
            }

            if (imagePos.Y > 0 && !isHeroInCastle)
            {
                // Hero's horse or boat should not be rendered over the bottom map border.
                if (heroPos.Y + imagePos.Y >= worldHeight)
                {
                    continue;
                }

                // The very bottom part of the hero (or hero on boat) image should not be rendered before its shadow,
                // so it is placed in the extra bucket.
                if (imagePos.X < 0)
                {
                    PrependTo(tileUnfit.HeroBackgroundImages, imagePos + heroPos, info);
                }
                else
                {
                    AppendTo(tileUnfit.HeroBackgroundImages, imagePos + heroPos, info);
                }
            }
            else if (imagePos.Y == 0 || (isHeroInCastle && imagePos.Y > 0))
            {
                if (imagePos.X < 0)
                {
                    PrependTo(tileUnfit.BottomImages, imagePos + heroPos, info);
                }
                else
                {
                    AppendTo(tileUnfit.BottomImages, imagePos + heroPos, info);
                }
            }
            else
            {
                if (heroPos.Y == 0 && (fogDirection & Direction.Top) == Direction.Top)
                {
                    // Do not render hero's flag over the top map border if there is a nearby fog.
                    continue;
                }

                if (imagePos.X < 0)
                {
                    PrependTo(tileUnfit.TopImages, imagePos + heroPos, info);
                }
                else
                {
                    AppendTo(tileUnfit.TopImages, imagePos + heroPos, info);
                }
            }
        }

        foreach (ObjectRenderingInfo objectInfo in spriteShadowInfo)
        {
            ObjectRenderingInfo info = objectInfo;
            Point imagePos = info.TileOffset + heroPos;

            // Shadows outside the game area should not be rendered.
            if (imagePos.X < 0 || imagePos.Y < 0 || imagePos.Y >= worldHeight)
            {
                continue;
            }

            info.AlphaValue = heroAlphaValue;

            AppendTo(tileUnfit.ShadowImages, imagePos, info);
        }
    }

    /// <summary>
    /// Draws every bucket of a layer, <c>renderImagesOnTiles()</c> (<c>gui/interface_gamearea.cpp:295-304</c>).
    /// </summary>
    /// <remarks>
    /// <c>std::map</c> walks its keys sorted by row and then by column, which decides the draw order of overlapping
    /// sprites; the port's dictionary does not, so the keys are sorted here.
    /// </remarks>
    private void RenderImagesOnTiles(
        PixelBuffer output,
        Dictionary<Point, LinkedList<ObjectRenderingInfo>> images)
    {
        foreach (KeyValuePair<Point, LinkedList<ObjectRenderingInfo>> bucket in images.OrderBy(entry => entry.Key.Y).ThenBy(entry => entry.Key.X))
        {
            foreach (ObjectRenderingInfo info in bucket.Value)
            {
                if (MapSprites.GetSprite(info.IcnId, info.IcnIndex) is not { } sprite)
                {
                    continue;
                }

                BlitOnTile(
                    output,
                    sprite,
                    info.Area,
                    info.ImageOffset.X,
                    info.ImageOffset.Y,
                    bucket.Key,
                    info.IsFlipped,
                    info.AlphaValue);
            }
        }
    }

    /// <summary>
    /// Whether a top layer object continues on the tile below it, <c>isTallTopLayerObject()</c>
    /// (<c>gui/interface_gamearea.cpp:306-325</c>).
    /// </summary>
    /// <param name="x">Column of the object's tile.</param>
    /// <param name="y">Row of the object's tile.</param>
    /// <param name="uid">Unique id of the object.</param>
    /// <returns><see langword="true"/> when the tile below holds a part of the same object.</returns>
    private static bool IsTallTopLayerObject(int x, int y, uint uid)
    {
        if (y + 1 >= World.Current.Height)
        {
            // There is nothing below so it's not a tall object.
            return false;
        }

        // There is a tile below the current.
        Tile tileBelow = World.Current.GetTile(x, y + 1);

        foreach (ObjectPart lowerPart in tileBelow.TopObjectParts)
        {
            // Flags are put over the objects as a top object part and have the same UID but they do not make the
            // object taller.
            if (lowerPart.Uid == uid && lowerPart.IcnType != Mp2ObjectIcnType.Flag32)
            {
                // This is a tall object.
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Draws the viewport, <c>GameArea::Redraw()</c> (<c>gui/interface_gamearea.cpp:408-805</c>).
    /// </summary>
    /// <remarks>
    /// The passes are, in this order: terrain, the ground and background objects, the objects that stick out of their
    /// tile below it, the shadows, the hero's feet, the ground objects, the rest of the tile-unfit objects, the top
    /// layer objects (tall ones last), the flying ghosts, the hero's route and finally either the passability overlay
    /// or the fog with the town silhouettes under it.
    /// </remarks>
    /// <param name="dst">Frame buffer the viewport is drawn into.</param>
    /// <param name="flag">Which layers to draw, <c>RedrawLevelType</c>.</param>
    /// <param name="isPuzzleDraw">Whether the puzzle map is being drawn.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dst"/> is <see langword="null"/>.</exception>
    public void Redraw(PixelBuffer dst, RedrawLevelType flag, bool isPuzzleDraw = false)
    {
        ArgumentNullException.ThrowIfNull(dst);

        MapGeometry geometry = new(World.Current.Width, World.Current.Height);

        Rect tileRoi = VisibleTileRoi;

        int maxX = tileRoi.X + tileRoi.Width;
        int maxY = tileRoi.Y + tileRoi.Height;
        int worldWidth = World.Current.Width;
        int worldHeight = World.Current.Height;

        bool renderFog = (flag & RedrawLevelType.Fog) == RedrawLevelType.Fog;

        // Render terrain.
        for (int y = 0; y < tileRoi.Height; ++y)
        {
            int tileY = tileRoi.Y + y;

            if (tileY < 0 || tileY >= worldHeight)
            {
                for (int x = tileRoi.X; x < maxX; ++x)
                {
                    TileRendering.RedrawEmptyTile(dst, new Point(x, tileY), this);
                }
            }
            else
            {
                for (int x = tileRoi.X; x < maxX; ++x)
                {
                    if (x < 0 || x >= worldWidth)
                    {
                        TileRendering.RedrawEmptyTile(dst, new Point(x, tileY), this);
                    }
                    else
                    {
                        Tile tile = World.Current.GetTile(x, tileY);

                        // Do not render terrain on the tiles fully covered with the fog.
                        if (!renderFog || tile.FogDirection != Directions.All)
                        {
                            if (TileRendering.GetTileSurface(tile) is { } surface)
                            {
                                DrawTile(dst, surface, new Point(x, tileY));
                            }
                        }
                    }
                }
            }
        }

        int minX = Math.Max(tileRoi.X, 0);
        int minY = Math.Max(tileRoi.Y, 0);
        maxX = Math.Min(maxX, worldWidth);
        maxY = Math.Min(maxY, worldHeight);

        if (minX >= maxX || minY >= maxY)
        {
            // This can't be true! Please check your code changes as we shouldn't have an empty area.
            Debug.Assert(false, "The visible tile area is empty.");
            return;
        }

        // Each tile can contain multiple object parts or sprites. Each object part has its own level or in other
        // words layer of rendering. We need to use a correct order of levels to render objects on tiles. The levels:
        // 0 - main and action objects like mines, forest, castle and etc.
        // 1 - background objects like lakes or bushes.
        // 2 - shadows and some special objects like castle's entrance road.
        // 3 - roads, water flaws and cracks. Essentially everything what is a part of terrain.
        // The correct order of levels is 3 --> 1 --> 2 --> 0.
        //
        // There are also two groups of objects: ground objects (bottom layer) and high objects (top layer). High
        // objects are the parts of the objects which are taller than 1 tile, a castle for example. All ground
        // objects are drawn first.
        //
        // However, there are objects which appear to be more than 1 tile (32 x 32 pixels) in size such as heroes,
        // monsters and boats. To render all these 'special' objects a copy of the object sprite stacks is made for
        // every tile they reach, extra sprites are added and then they are rendered. These are called tile-unfit
        // objects, the rest are tile-fit objects.
        //
        // Fading animation can be applied for both of them: a tile-fit object is drawn with the alpha value of its
        // UID, a tile-unfit object gets its alpha value when the ObjectRenderingInfo instances are created.

        bool drawHeroes = (flag & RedrawLevelType.Heroes) == RedrawLevelType.Heroes;

        TileUnfitRenderObjectInfo tileUnfit = new();

        // Run through all visible tiles and find all tile-unfit objects. Extra tiles from the right and the bottom
        // are covered as well because these objects are usually bigger than 1x1 tiles.
        int roiToRenderMinX = Math.Max(minX - 1, 0);
        int roiToRenderMinY = Math.Max(minY - 1, 0);
        int roiToRenderMaxX = Math.Min(maxX + 2, worldWidth);
        int roiToRenderMaxY = Math.Min(maxY + 2, worldHeight);

        bool isEditor = _interface.IsEditor;

        List<Point> ghostAnimationPos = [];

        for (int posY = roiToRenderMinY; posY < roiToRenderMaxY; ++posY)
        {
            int offset = posY * worldWidth;

            for (int posX = roiToRenderMinX; posX < roiToRenderMaxX; ++posX)
            {
                int tileIndex = offset + posX;
                Tile tile = World.Current.GetTile(tileIndex);

                Mp2ObjectType objectType = tile.MainObjectType;

                // Objects which are fully under the fog are skipped.
                Direction fogDirection = renderFog ? tile.FogDirection : Direction.Unknown;
                bool isTileUnderFog = fogDirection == Directions.All;

                switch (objectType)
                {
                    case Mp2ObjectType.Hero:
                    {
                        if (isEditor)
                        {
                            Hero? hero = tile.GetHero(World.Current);
                            if (hero is null || !hero.IsShipMaster)
                            {
                                byte alphaValue = GetObjectAlphaValue(tileIndex, Mp2ObjectType.Hero);

                                IReadOnlyList<ObjectRenderingInfo> spriteInfo = TileRendering.GetEditorHeroSpritesPerTile(tile);

                                PopulateStaticTileUnfitObjectInfo(
                                    tileUnfit,
                                    [.. spriteInfo],
                                    [],
                                    new Point(posX, posY),
                                    alphaValue,
                                    fogDirection);
                            }
                            else
                            {
                                PopulateHeroObjectInfo(tileUnfit, hero, fogDirection);
                            }

                            continue;
                        }

                        if (!drawHeroes)
                        {
                            continue;
                        }

                        Hero? mapHero = tile.GetHero(World.Current);
                        Debug.Assert(mapHero is not null, "A hero tile holds no hero.");

                        if (mapHero is null)
                        {
                            continue;
                        }

                        // Heroes and boats covered with the fog are not rendered. Boats are 2 tiles high so for a
                        // hero on a boat the info has to be populated one tile lower than the fog. While moving an
                        // enemy hero can go out of the fog, so it is rendered one step before leaving the fog.
                        if (isTileUnderFog
                            && (!mapHero.IsShipMaster
                                || posY == 0
                                || World.Current.GetTile(tileIndex - worldWidth).FogDirection == Directions.All)
                            && (!mapHero.IsMoveEnabled || World.Current.GetTile(mapHero.Path.FrontIndex).FogDirection == Directions.All))
                        {
                            continue;
                        }

                        PopulateHeroObjectInfo(tileUnfit, mapHero, fogDirection);

                        // The type is updated as the tile can hold an object under the hero.
                        objectType = tile.GetMainObjectType(World.Current, false);

                        break;
                    }

                    case Mp2ObjectType.Monster:
                    {
                        if (isPuzzleDraw || isTileUnderFog)
                        {
                            continue;
                        }

                        byte alphaValue = GetObjectAlphaValue(tileIndex, Mp2ObjectType.Monster);

                        IReadOnlyList<ObjectRenderingInfo> spriteInfo = TileRendering.GetMonsterSpritesPerTile(tile, isEditor);
                        IReadOnlyList<ObjectRenderingInfo> spriteShadowInfo = TileRendering.GetMonsterShadowSpritesPerTile(tile, isEditor);

                        PopulateStaticTileUnfitObjectInfo(
                            tileUnfit,
                            [.. spriteInfo],
                            [.. spriteShadowInfo],
                            new Point(posX, posY),
                            alphaValue,
                            fogDirection);

                        continue;
                    }

                    case Mp2ObjectType.Boat:
                    {
                        // Boats are 2 tiles high so the info has to be populated for a boat one tile lower than the
                        // fog.
                        // There is no tile above the first row, so the "upper tile" counts as being under fog.
                        bool isUpperTileUnderFog = posY == 0 || World.Current.GetTile(tileIndex - worldWidth).FogDirection == Directions.All;

                        if (!drawHeroes || (isTileUnderFog && isUpperTileUnderFog))
                        {
                            // Boats can be occupied by heroes so they are considered as the same objects.
                            continue;
                        }

                        byte alphaValue = GetObjectAlphaValue(tileIndex, Mp2ObjectType.Boat);

                        IReadOnlyList<ObjectRenderingInfo> spriteInfo = TileRendering.GetBoatSpritesPerTile(tile);
                        IReadOnlyList<ObjectRenderingInfo> spriteShadowInfo = TileRendering.GetBoatShadowSpritesPerTile(tile);

                        PopulateStaticTileUnfitObjectInfo(
                            tileUnfit,
                            [.. spriteInfo],
                            [.. spriteShadowInfo],
                            new Point(posX, posY),
                            alphaValue,
                            fogDirection);

                        continue;
                    }

                    default:
                        break;
                }

                // These are parts of original action objects which must be rendered under heroes.
                if (!isPuzzleDraw
                    && (objectType == Mp2ObjectType.AbandonedMine
                        || (objectType == Mp2ObjectType.Mine && tile.GetMineSpellId() == SpellType.Haunt)))
                {
                    // To render the ghosts of an abandoned mine correctly the tile above has to be checked for fog.
                    bool areGhostsUnderFog = renderFog && isTileUnderFog && World.Current.GetTile(tileIndex - worldWidth).FogDirection == Directions.All;
                    if (areGhostsUnderFog)
                    {
                        // The current tile and the one above it are both fully under the fog.
                        continue;
                    }

                    ghostAnimationPos.Add(new Point(posX, posY));
                }
                else if (objectType == Mp2ObjectType.Mine && !isTileUnderFog)
                {
                    IReadOnlyList<ObjectRenderingInfo> spriteInfo = TileRendering.GetMineGuardianSpritesPerTile(tile);
                    if (spriteInfo.Count > 0)
                    {
                        byte alphaValue = GetObjectAlphaValue(tile.MainObjectPart.Uid);

                        PopulateStaticTileUnfitBackgroundObjectInfo(tileUnfit, [.. spriteInfo], new Point(posX, posY), alphaValue);
                    }
                }
            }
        }

        // Render all terrain and background layer objects.
        for (int y = minY; y < maxY; ++y)
        {
            int offset = y * worldWidth;

            for (int x = minX; x < maxX; ++x)
            {
                Tile tile = World.Current.GetTile(x + offset);

                if (renderFog && tile.FogDirection == Directions.All)
                {
                    continue;
                }

                // Draw roads, rivers and cracks, then the background objects.
                TileRendering.RedrawBottomLayerObjects(tile, dst, isPuzzleDraw, this, (byte)ObjectLayerType.Terrain);
                TileRendering.RedrawBottomLayerObjects(tile, dst, isPuzzleDraw, this, (byte)ObjectLayerType.Background);
            }
        }

        // Draw the lower part of the tile-unfit objects.
        RenderImagesOnTiles(dst, tileUnfit.BottomBackgroundImages);

        for (int y = minY; y < maxY; ++y)
        {
            int offset = y * worldWidth;

            for (int x = minX; x < maxX; ++x)
            {
                Tile tile = World.Current.GetTile(x + offset);

                if (renderFog && tile.FogDirection == Directions.All)
                {
                    continue;
                }

                TileRendering.RedrawBottomLayerObjects(tile, dst, isPuzzleDraw, this, (byte)ObjectLayerType.Shadow);
            }
        }

        // Draw all shadows of the tile-unfit objects.
        RenderImagesOnTiles(dst, tileUnfit.ShadowImages);

        // Draw the lower part of the hero's sprite, including the boat sprite when it is controlled by a hero.
        RenderImagesOnTiles(dst, tileUnfit.HeroBackgroundImages);

        // Low priority images are drawn before any other object on their tile.
        RenderImagesOnTiles(dst, tileUnfit.LowPriorityBottomImages);

        for (int y = minY; y < maxY; ++y)
        {
            int offset = y * worldWidth;

            for (int x = minX; x < maxX; ++x)
            {
                Tile tile = World.Current.GetTile(x + offset);

                if (renderFog && tile.FogDirection == Directions.All)
                {
                    continue;
                }

                // TODO: some action objects have tiles above which are still on the bottom layer. These images must
                // be drawn last.
                TileRendering.RedrawBottomLayerObjects(tile, dst, isPuzzleDraw, this, (byte)ObjectLayerType.Object);
            }
        }

        // Draw the middle part of the tile-unfit sprites.
        RenderImagesOnTiles(dst, tileUnfit.BottomImages);

        // High priority images are drawn after any other object on their tile.
        RenderImagesOnTiles(dst, tileUnfit.HighPriorityBottomImages);

        List<(ObjectPart Part, int TileIndex)> topLayerTallObjects = [];

        // The ROI is expanded to render very tall objects properly (1 tile left and right, 2 tiles below): the ghosts
        // of an abandoned mine, the flag of the alchemist lab and others.
        int roiExtraObjectsMaxX = Math.Min(maxX + 1, worldWidth);

        for (int y = minY; y < roiToRenderMaxY; ++y)
        {
            int offset = y * worldWidth;

            for (int x = roiToRenderMinX; x < roiExtraObjectsMaxX; ++x)
            {
                int tileIndex = x + offset;
                Tile tile = World.Current.GetTile(tileIndex);

                if (renderFog && tile.FogDirection == Directions.All)
                {
                    // Do not render parts fully covered with fog.
                    continue;
                }

                // Some objects are taller than 2 tiles so their top layer sprites must be drawn at the very end. To
                // find them every level 2 object is checked against the objects of the tile below it: an object that
                // continues down there is tall.
                foreach (ObjectPart part in tile.TopObjectParts)
                {
                    if (IsTallTopLayerObject(x, y, part.Uid))
                    {
                        topLayerTallObjects.Add((part, tileIndex));
                    }
                    else
                    {
                        TileRendering.RedrawTopLayerObject(tile, dst, isPuzzleDraw, new Point(x, y), this, part);
                    }
                }
            }
        }

        // Draw the upper part of the tile-unfit sprites.
        RenderImagesOnTiles(dst, tileUnfit.TopImages);

        // Draw the top part of the tall objects.
        foreach ((ObjectPart part, int tileIndex) in topLayerTallObjects)
        {
            TileRendering.RedrawTopLayerObject(
                World.Current.GetTile(tileIndex),
                dst,
                isPuzzleDraw,
                geometry.GetPoint(tileIndex),
                this,
                part);
        }

        // Draw the flying ghosts of haunted and abandoned mines over all other objects.
        foreach (Point pos in ghostAnimationPos)
        {
            TileRendering.RedrawFlyingGhostsOnMap(dst, pos, this, isEditor);
        }

        // Draw the hero's route. It is drawn on top of everything.
        bool drawRoutes = (flag & RedrawLevelType.Routes) != 0;
        Hero? currentHero = drawHeroes ? InterfaceFocus.GetFocusHeroes() : null;

        if (drawRoutes && currentHero is not null && currentHero.Path.IsShow)
        {
            HeroPath path = currentHero.Path;
            int greenColorSteps = path.GetAllowedSteps(currentHero.MovePoints);
            int pathfinding = (int)currentHero.GetLevelSkill(SecondarySkill.Pathfinding);

            int currentStepIndex = 0;
            if (currentHero.IsMoveEnabled && currentHero.Direction == path.FrontDirection)
            {
                // Do not draw the first path mark when the hero (or boat) moves along the path.
                ++currentStepIndex;
                --greenColorSteps;
            }

            // Not all arrows and their shadows fit in one tile: one more tile is considered so that everything is
            // rendered properly.
            Rect extendedVisibleRoi = new(tileRoi.X - 1, tileRoi.Y - 1, tileRoi.Width + 2, tileRoi.Height + 2);

            for (int i = currentStepIndex; i < path.Count; ++i)
            {
                RouteStep currentStep = path.Steps[i];
                int nextStepIndex = i + 1;

                Point mp = geometry.GetPoint(currentStep.Index);

                --greenColorSteps;

                if (!extendedVisibleRoi.Contains(mp))
                {
                    // The mark is on a tile outside the drawing area. Just skip it.
                    continue;
                }

                uint routeSpriteIndex = 0;
                if (nextStepIndex < path.Count)
                {
                    Tile tile = World.Current.GetTile(currentStep.Index);
                    uint cost = tile.IsRoad
                        ? MapGrounds.RoadPenalty
                        : MapGrounds.GetPenalty(tile.Ground, (SkillLevel)pathfinding);

                    routeSpriteIndex = (uint)HeroPath.GetIndexSprite(currentStep.Direction, path.Steps[nextStepIndex].Direction, (int)cost);
                }

                IcnType routeIcnId = greenColorSteps < 0 ? IcnType.Routered : IcnType.Route;

                if (MapSprites.GetSprite(routeIcnId, (int)routeSpriteIndex) is not { } routeSprite)
                {
                    continue;
                }

                BlitOnTile(dst, routeSprite, routeSprite.OffsetX - 12, routeSprite.OffsetY + 2, mp, false, 255);
            }
        }

        bool drawPassabilities = (flag & RedrawLevelType.Passabilities) != 0;

        if (drawPassabilities)
        {
            PlayerColor friendColors = Players.FriendColors();

            for (int y = minY; y < maxY; ++y)
            {
                int offset = y * worldWidth;

                for (int x = minX; x < maxX; ++x)
                {
                    TileRendering.RedrawPassable(World.Current.GetTile(x + offset), dst, friendColors, this, isEditor);
                }
            }
        }
        else if (renderFog)
        {
            bool drawTowns = (flag & RedrawLevelType.Towns) != 0;

            for (int y = minY; y < maxY; ++y)
            {
                int offset = y * worldWidth;

                for (int x = minX; x < maxX; ++x)
                {
                    Tile tile = World.Current.GetTile(x + offset);

                    if (tile.FogDirection != Direction.Unknown)
                    {
                        TileRendering.DrawFog(tile, dst, this);

                        if (drawTowns)
                        {
                            TileRendering.DrawByObjectIcnType(tile, dst, this, Mp2ObjectIcnType.ObjnTwba);

                            Mp2ObjectType objectType = tile.GetMainObjectType(World.Current, false);
                            // The reference tests OBJ_CASTLE and OBJ_NON_ACTION_CASTLE; the port's action bit is
                            // Mp2ObjectType.ActionObjectType, so the pair is Castle / ActionCastle.
                            if (objectType == Mp2ObjectType.Castle || objectType == Mp2ObjectType.ActionCastle)
                            {
                                TileRendering.DrawByObjectIcnType(tile, dst, this, Mp2ObjectIcnType.ObjnTown);
                            }
                        }
                    }
                }
            }
        }

        UpdateObjectAnimationInfo();
    }

    /// <summary>
    /// Repaints only the fog of war, <c>GameArea::redrawOnlyFog()</c> (<c>gui/interface_gamearea.cpp:807-835</c>).
    /// </summary>
    /// <param name="dst">Frame buffer the viewport is drawn into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dst"/> is <see langword="null"/>.</exception>
    public void RedrawOnlyFog(PixelBuffer dst)
    {
        ArgumentNullException.ThrowIfNull(dst);

        Rect tileRoi = VisibleTileRoi;

        int maxX = tileRoi.X + tileRoi.Width;
        int worldWidth = World.Current.Width;
        int worldHeight = World.Current.Height;

        for (int y = 0; y < tileRoi.Height; ++y)
        {
            int tileY = tileRoi.Y + y;

            for (int x = tileRoi.X; x < maxX; ++x)
            {
                if (tileY < 0 || tileY >= worldHeight || x < 0 || x >= worldWidth)
                {
                    TileRendering.RedrawEmptyTile(dst, new Point(x, tileY), this);
                }
                else if (MapSprites.GetTileImage(TilType.Clof32, (x + tileY) % 4, 0) is { } fogSurface)
                {
                    DrawTile(dst, fogSurface, new Point(x, tileY));
                }
            }
        }
    }

    /// <summary>
    /// Draws the selection of a tile area, <c>GameArea::renderTileAreaSelect()</c>
    /// (<c>gui/interface_gamearea.cpp:837-863</c>).
    /// </summary>
    /// <param name="dst">Frame buffer the selection is drawn into.</param>
    /// <param name="startTile">First tile of the area, <c>-1</c> when unused.</param>
    /// <param name="endTile">Last tile of the area, <c>-1</c> when unused.</param>
    /// <param name="isActionObject">Whether the area belongs to an action object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dst"/> is <see langword="null"/>.</exception>
    public void RenderTileAreaSelect(PixelBuffer dst, int startTile, int endTile, bool isActionObject)
    {
        ArgumentNullException.ThrowIfNull(dst);

        if (startTile < 0 || endTile < 0)
        {
            return;
        }

        MapGeometry geometry = new(World.Current.Width, World.Current.Height);

        Point startTileOffset = GetRelativeTilePosition(geometry.GetPoint(startTile));
        Point endTileOffset = GetRelativeTilePosition(geometry.GetPoint(endTile));

        int startX = Math.Min(startTileOffset.X, endTileOffset.X);
        int startY = Math.Min(startTileOffset.Y, endTileOffset.Y);
        int sizeX = GameBorder.TileWidth + Math.Abs(startTileOffset.X - endTileOffset.X);
        int sizeY = GameBorder.TileWidth + Math.Abs(startTileOffset.Y - endTileOffset.Y);

        Rect imageRoi = new(startX, startY, sizeX, sizeY);
        Rect overlappedRoi = _windowRoi.Intersect(imageRoi);

        int limitedLineWidth = Math.Min(2, overlappedRoi.Width);
        int limitedLineHeight = Math.Min(2, overlappedRoi.Height);

        byte color = isActionObject ? (byte)115 : (byte)181;

        ImageOps.FillRect(dst, new Rect(overlappedRoi.X, overlappedRoi.Y, overlappedRoi.Width, limitedLineHeight), color);
        ImageOps.FillRect(dst, new Rect(overlappedRoi.X, overlappedRoi.Y + 2, limitedLineWidth, overlappedRoi.Height - 4), color);
        ImageOps.FillRect(
            dst,
            new Rect(overlappedRoi.X, overlappedRoi.Y + overlappedRoi.Height - limitedLineHeight, overlappedRoi.Width, limitedLineHeight),
            color);
        ImageOps.FillRect(
            dst,
            new Rect(overlappedRoi.X + overlappedRoi.Width - limitedLineWidth, overlappedRoi.Y + 2, limitedLineWidth, overlappedRoi.Height - 4),
            color);
    }

    /// <summary>
    /// Renders the area around an ultimate artifact, <c>GameArea::GenerateUltimateArtifactAreaSurface()</c>
    /// (<c>gui/interface_gamearea.cpp:908-937</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference copies the live viewport and re-centres the copy on the artifact, so that the real viewport keeps
    /// its position. The port creates a viewport over the same interface for the same reason — its size is immediately
    /// overwritten by <see cref="SetAreaPosition"/>, and nothing but the tile offsets of the copy is ever read again.
    /// </para>
    /// <para>
    /// The reference calls <c>_disableTransformLayer()</c> on the result before drawing into it. The port's
    /// <see cref="PixelBuffer"/> has no transform layer — it is always the reference's non-transformed form — so that
    /// call has no counterpart.
    /// </para>
    /// </remarks>
    /// <param name="index">Tile index of the artifact.</param>
    /// <param name="offset">Offset of the area to render, in tiles.</param>
    /// <returns>The rendered map fragment, <c>448x448</c> pixels, tinted with the tan palette.</returns>
    public PixelBuffer GenerateUltimateArtifactAreaSurface(int index, Point offset)
    {
        MapGeometry geometry = new(World.Current.Width, World.Current.Height);

        if (!geometry.IsValidAbsIndex(index))
        {
            GameLog.Warn($"The ultimate artifact is not found on index {index}.");

            return new PixelBuffer(0, 0);
        }

        PixelBuffer result = new(448, 448);

        GameArea gameArea = new(_interface);
        gameArea.SetAreaPosition(0, 0, result.Width, result.Height);

        Point artifactPosition = geometry.GetPoint(index);

        gameArea.SetCenter(artifactPosition + offset);
        gameArea.Redraw(result, RedrawLevelType.Objects, true);

        if (MapSprites.GetSprite(IcnType.Route, 0) is { } marker)
        {
            Point markerPos = gameArea.GetRelativeTilePosition(artifactPosition)
                              - gameArea.MiddlePoint
                              - new Point(gameArea.Roi.X, gameArea.Roi.Y)
                              + new Point(result.Width / 2, result.Height / 2);

            ImageOps.Blit(
                result,
                new Point(markerPos.X, markerPos.Y + 8),
                marker.Image,
                new Rect(0, 0, marker.Width, marker.Height));
        }

        ImageOps.ApplyPalette(result, PaletteId.Tan);

        return result;
    }

    /// <summary>
    /// Advances every queued object animation by one frame and drops the finished ones,
    /// <c>GameArea::updateObjectAnimationInfo()</c> (<c>gui/interface_gamearea.cpp:1223-1233</c>).
    /// </summary>
    /// <remarks>
    /// Erasing an entry runs its destructor in the reference, and <c>~ObjectFadingOutInfo()</c> is what takes the
    /// faded object off its tile. The port has no destructor to run, so the removal is done right where the entry
    /// leaves the queue.
    /// </remarks>
    private void UpdateObjectAnimationInfo()
    {
        for (int i = 0; i < _animationInfo.Count;)
        {
            BaseObjectAnimationInfo info = _animationInfo[i];

            if (!info.Update())
            {
                ++i;
                continue;
            }

            _animationInfo.RemoveAt(i);

            if (info is ObjectFadingOutInfo fadingOut)
            {
                fadingOut.RemoveObjectFromTile();
            }
        }
    }

    /// <summary>
    /// Runs one animation, <c>GameArea::runSingleObjectAnimation()</c>
    /// (<c>gui/interface_gamearea.cpp:1257-1276</c>).
    /// </summary>
    /// <remarks>
    /// The reference enqueues the animation and then spins its own blocking loop — wait for the delay, pump events,
    /// redraw and present — until the animation has finished. The port has neither a global event pump nor a
    /// <c>Display</c> singleton: frames are driven by the scene (<c>AdventureMapScene</c> calls <see cref="Redraw"/>
    /// every frame, and the end of <see cref="Redraw"/> already calls the animation update). This method therefore
    /// only enqueues, and the animation finishes over the following frames. A caller that needs "finish before
    /// moving on" has to express that ordering in the scene.
    /// </remarks>
    /// <param name="info">Animation to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public void RunSingleObjectAnimation(BaseObjectAnimationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        AddObjectAnimationInfo(info);
    }
}
