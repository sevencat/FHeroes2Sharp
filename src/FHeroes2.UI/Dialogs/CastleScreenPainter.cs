using System.Diagnostics;

using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Castle;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The town screen's background and buildings, <c>castle_building.cpp</c>: <c>redrawCastleBuildings()</c>,
/// <c>redrawCastleBuilding()</c>, <c>redrawCastleBuildingExtended()</c>, <c>redrawBuildingConnection()</c> and
/// <c>fheroes2::drawCastleDialogBuilding()</c> (<c>ui_castle.cpp:110-148</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/castle/castle_building.cpp:44-470</c>。画一帧的顺序是固定的：整张 <c>TOWNBKG</c> 背景、
/// 野蛮人的河（<c>TWNBEXT1</c>）或法师的峡谷河（<c>TWNZEXT0</c>）的逐帧动画、海港的逐帧动画，然后按
/// <see cref="BuildingsRenderQueue"/> 的优先级逐个画建筑的主帧、动画帧、扩展件与连接件。
/// </para>
/// <para>
/// 与参考的一处结构性差异：参考把画面留在 <c>Display</c> 里，只在脏矩形上重画，所以淡入分支在「没有可画的
/// 变化」时直接 <c>return</c>，依赖上一帧的画面。端口没有帧缓存，每次都从背景重来，因此淡入分支同样要把整座
/// 城画一遍——跳过的是「哪块该用 alpha」，不是「要不要画」。
/// </para>
/// <para>
/// 未搬：<c>drawCastleDialogBuilding</c> 的玩家配色（<c>getModifiedByColorImage</c>，六张旗帜类 ICN 要按玩家
/// 颜色换调色板）与 <c>AlphaBlit</c> 的半透明混合。前者意味着城堡旗帜目前不是玩家色，后者意味着新建筑是
/// 瞬间出现而不是淡入（<see cref="FadeBuilding"/> 仍在推进，只是画的时候 alpha 未参与混合）。
/// </para>
/// </remarks>
public static class CastleScreenPainter
{
    /// <summary>How many frames a river or bay animation cycles through, <c>1 + animationIndex % 5</c>.</summary>
    public const int AnimationFrames = 5;

    /// <summary>
    /// The background of a town, <c>getTownIcnId()</c> (<c>castle_building.cpp:44-65</c>).
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <returns>The <c>TOWNBKG</c> archive of the race.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The race has no town background.</exception>
    public static IcnType GetBackground(MapRace race) => race switch
    {
        MapRace.Knight => IcnType.Townbkg0,
        MapRace.Barbarian => IcnType.Townbkg1,
        MapRace.Sorceress => IcnType.Townbkg2,
        MapRace.Warlock => IcnType.Townbkg3,
        MapRace.Wizard => IcnType.Townbkg4,
        MapRace.Necromancer => IcnType.Townbkg5,
        _ => throw new ArgumentOutOfRangeException(nameof(race), race, "The race has no town background."),
    };

    /// <summary>
    /// The animated bay of a race's shore, <c>castle_building.cpp:366-389</c>.
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <returns>The bay archive, or <see langword="null"/> for a race without one.</returns>
    public static IcnType? GetBay(MapRace race) => race switch
    {
        MapRace.Knight => IcnType.Twnkext0,
        MapRace.Barbarian => IcnType.Twnbext0,
        MapRace.Sorceress => IcnType.Twnsext0,
        MapRace.Necromancer => IcnType.Twnnext0,
        MapRace.Warlock => IcnType.Twnwext0,
        MapRace.Wizard => IcnType.WizardCastleBay,
        _ => null,
    };

    /// <summary>
    /// Draws a whole town frame: background, rivers, bay and every building of the queue.
    /// </summary>
    /// <param name="target">The screen buffer.</param>
    /// <param name="castle">The town being drawn.</param>
    /// <param name="offset">The town view's top-left corner on screen.</param>
    /// <param name="animationIndex">The town's animation counter, which drives the river, bay and flag frames.</param>
    /// <param name="fade">The fade-in in progress, whose building is drawn with its alpha.</param>
    /// <param name="world">
    /// The world, used to answer <c>HasBoatNearby()</c> for the shipyard. When <see langword="null"/> the harbour counts
    /// as empty, which only changes whether the shipyard's boat is drawn.
    /// </param>
    public static void Draw(
        PixelBuffer target,
        Castle castle,
        Point offset,
        uint animationIndex,
        FadeBuilding fade,
        World? world = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(fade);

        Sprite? background = GameAssets.GetImage(IcnCatalog.GetFileName(GetBackground(castle.Race)), 0);
        if (background is null)
        {
            return;
        }

        ImageOps.Blit(target, offset, background.Image, background.Image.Bounds);

        Rect maxArea = new(offset, background.Image.Bounds.Size);
        uint riverFrame = 1 + (animationIndex % AnimationFrames);

        if (castle.Race == MapRace.Barbarian)
        {
            // The barbarian's river is a full sprite of its own rather than an overlay on the background.
            DrawBuilding(target, IcnType.Twnbext1, (int)riverFrame, offset, maxArea);
        }
        else if (castle.Race == MapRace.Wizard)
        {
            // The wizard's "bay" is a river in a gorge: frame 0 is already on the background, only the animation is drawn.
            DrawBuilding(target, IcnType.Twnzext0, (int)riverFrame, offset, maxArea);
        }

        BuildingType fadingIn = fade.Building;

        // The barbarian keeps its bay even after the shipyard stands; every other race hides it behind the shipyard.
        if (fadingIn == BuildingType.Shipyard
            || (castle.HasSeaAccess && (castle.Race == MapRace.Barbarian || !castle.IsBuild(BuildingType.Shipyard))))
        {
            if (GetBay(castle.Race) is { } bay)
            {
                DrawBuilding(target, bay, 0, offset, maxArea);
                DrawBuilding(target, bay, (int)riverFrame, offset, maxArea);
            }
        }

        BuildingsRenderQueue buildings = new(castle, offset);

        // Even while a fade runs the whole town is redrawn, because this painter always starts from the background.
        foreach (BuildingRenderInfo info in buildings)
        {
            if (!castle.IsBuild(info.Id))
            {
                continue;
            }

            BuildingType upgrade = CastleBuildings.GetUpgradeForBuilding(castle.Race, info.Id);
            if (upgrade != info.Id && castle.IsBuild(upgrade) && upgrade != fadingIn)
            {
                continue;
            }

            byte alpha = info.Id == fadingIn && !fade.IsOnlyBoat ? fade.Alpha : byte.MaxValue;

            RedrawCastleBuilding(target, castle, offset, maxArea, info.Id, animationIndex, fadingIn, alpha);
            RedrawCastleBuildingExtended(target, castle, offset, maxArea, info.Id, animationIndex, fadingIn, alpha, world);

            if (CastleBuildingRender.IsBuildingConnectionNeeded(castle, info.Id))
            {
                RedrawBuildingConnection(target, castle, offset, maxArea, info.Id, fadingIn, alpha);
            }
        }
    }

    /// <summary>
    /// Draws one building's main sprite and its animation frame, <c>redrawCastleBuilding()</c>
    /// (<c>castle_building.cpp:150-248</c>).
    /// </summary>
    /// <param name="target">The screen buffer.</param>
    /// <param name="castle">The town.</param>
    /// <param name="offset">The town view's top-left corner.</param>
    /// <param name="maxArea">The background rectangle, which every blit is clipped to.</param>
    /// <param name="building">The building to draw.</param>
    /// <param name="animationIndex">The animation counter.</param>
    /// <param name="underConstruction">The building still fading in, or <see cref="BuildingType.Nothing"/>.</param>
    /// <param name="alpha">How opaque the building is; <see cref="byte.MaxValue"/> while no fade runs.</param>
    public static void RedrawCastleBuilding(
        PixelBuffer target,
        Castle castle,
        Point offset,
        Rect maxArea,
        BuildingType building,
        uint animationIndex,
        BuildingType underConstruction,
        byte alpha = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(castle);

        if (building == BuildingType.Tent)
        {
            // The tent is part of the background picture.
            return;
        }

        if ((building & BuildingType.MageGuild) != 0)
        {
            // Only the mage guild of the level that stands may be drawn; the previous level is allowed while the
            // upgrade fades in, because the old sprite stays visible until the new one is opaque.
            int level = castle.GetLevelMageGuild();
            Debug.Assert(level >= 1, "a mage guild is asked about only once it exists");

            bool isCurrent = building == (BuildingType)((int)BuildingType.MageGuild1 << (level - 1));
            bool isPrevious = level >= 2 && building == (BuildingType)((int)BuildingType.MageGuild1 << (level - 2));

            if (!isCurrent && !isPrevious)
            {
                return;
            }
        }

        MapRace race = castle.Race;

        // The sorceress' statue and crystal garden share one picture once both stand; it is drawn by the extended pass.
        if (race == MapRace.Sorceress
            && (building == BuildingType.Statue || building == BuildingType.Well2)
            && castle.IsBuild(BuildingType.Statue)
            && CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Well2, underConstruction))
        {
            return;
        }

        int index = building switch
        {
            BuildingType.MageGuild2 => race == MapRace.Necromancer ? 6 : 1,
            BuildingType.MageGuild3 => race == MapRace.Necromancer ? 12 : 2,
            BuildingType.MageGuild4 => race == MapRace.Necromancer ? 18 : 3,
            BuildingType.MageGuild5 => race == MapRace.Necromancer ? 24 : 4,
            _ => 0,
        };

        IcnType icn = Castle.GetICNBuilding(building, race);
        if (icn == IcnType.Unknown)
        {
            Debug.Fail($"a building ({building}) has no sprite for {race}");
            return;
        }

        DrawBuilding(target, icn, index, offset, maxArea, alpha);

        // The knight's castle flags are overlapped by the right turret, so they are drawn after the turret instead.
        bool knightCastleCase = race == MapRace.Knight
            && CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.RightTurret, underConstruction)
            && CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Castle, underConstruction);

        if (knightCastleCase && building == BuildingType.Castle)
        {
            return;
        }

        uint animated = IcnCatalog.GetAnimatedIndex(icn, (uint)index, animationIndex);
        if (animated != 0)
        {
            DrawBuilding(target, icn, (int)animated, offset, maxArea, alpha);
        }

        if (knightCastleCase && building == BuildingType.RightTurret)
        {
            IcnType castleIcn = Castle.GetICNBuilding(BuildingType.Castle, race);
            uint flags = IcnCatalog.GetAnimatedIndex(castleIcn, (uint)index, animationIndex);
            if (flags > 0)
            {
                DrawBuilding(target, castleIcn, (int)flags, offset, maxArea, alpha);
            }
        }
    }

    /// <summary>
    /// Draws the pieces that belong to more than one building, <c>redrawCastleBuildingExtended()</c>
    /// (<c>castle_building.cpp:250-328</c>).
    /// </summary>
    /// <param name="target">The screen buffer.</param>
    /// <param name="castle">The town.</param>
    /// <param name="offset">The town view's top-left corner.</param>
    /// <param name="maxArea">The background rectangle, which every blit is clipped to.</param>
    /// <param name="building">The building whose extra pieces follow.</param>
    /// <param name="animationIndex">The animation counter.</param>
    /// <param name="underConstruction">The building still fading in, or <see cref="BuildingType.Nothing"/>.</param>
    /// <param name="alpha">How opaque the pieces are.</param>
    /// <param name="world">The world, for the shipyard's boat.</param>
    public static void RedrawCastleBuildingExtended(
        PixelBuffer target,
        Castle castle,
        Point offset,
        Rect maxArea,
        BuildingType building,
        uint animationIndex,
        BuildingType underConstruction,
        byte alpha = byte.MaxValue,
        World? world = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(castle);

        if (building == BuildingType.Tent)
        {
            return;
        }

        MapRace race = castle.Race;

        if (building == BuildingType.Shipyard)
        {
            if (world is not null && castle.HasBoatNearby(world))
            {
                IcnType boat = Castle.GetICNBoat(race);

                DrawBuilding(target, boat, 0, offset, maxArea, alpha);

                uint animated = IcnCatalog.GetAnimatedIndex(boat, 0, animationIndex);
                if (animated != 0)
                {
                    DrawBuilding(target, boat, (int)animated, offset, maxArea, alpha);
                }
            }
            else
            {
                IcnType dock = Castle.GetICNBuilding(BuildingType.Shipyard, race);

                uint animated = IcnCatalog.GetAnimatedIndex(dock, 0, animationIndex);
                if (animated != 0)
                {
                    DrawBuilding(target, dock, (int)animated, offset, maxArea, alpha);
                }
            }
        }
        else if (building == BuildingType.Statue)
        {
            if (race == MapRace.Sorceress
                && underConstruction == BuildingType.Statue
                && CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Well2, underConstruction))
            {
                // While the statue fades in over a standing garden, the picture priority of the two is swapped.
                IcnType garden = Castle.GetICNBuilding(BuildingType.Well2, race);

                DrawBuilding(target, garden, 0, offset, maxArea);
                uint animated = IcnCatalog.GetAnimatedIndex(garden, 0, animationIndex);
                if (animated != 0)
                {
                    DrawBuilding(target, garden, (int)animated, offset, maxArea);
                }

                DrawBuilding(target, IcnType.Twnsext1, 0, offset, maxArea, alpha);
            }
        }
        else if (building == BuildingType.Well2)
        {
            if (race == MapRace.Sorceress)
            {
                if (underConstruction != BuildingType.Statue
                    && CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Statue, underConstruction))
                {
                    // Statue and Crystal Garden together are one picture.
                    DrawBuilding(target, IcnType.Twnsext1, 0, offset, maxArea, alpha);

                    uint animated = IcnCatalog.GetAnimatedIndex(IcnType.Twnsext1, 0, animationIndex);
                    if (animated != 0)
                    {
                        DrawBuilding(target, IcnType.Twnsext1, (int)animated, offset, maxArea, alpha);
                    }
                }
            }
            else if (race == MapRace.Knight
                     && !CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Castle, underConstruction))
            {
                // The left farm has no place of its own: it is hung off the right farm's corner while the castle fades in.
                if (GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.KnightCastleRightFarm), 0) is { } rightFarm)
                {
                    Point leftFarmOffset = new(
                        offset.X + rightFarm.OffsetX - GetFrameWidth(IcnType.KnightCastleLeftFarm),
                        offset.Y + rightFarm.OffsetY);

                    DrawBuilding(target, IcnType.KnightCastleLeftFarm, 0, leftFarmOffset, maxArea, alpha);
                }
            }
        }
        else if (building == BuildingType.Captain)
        {
            if (race == MapRace.Barbarian)
            {
                if (!CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Castle, underConstruction))
                {
                    if (GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Twnbcapt), 0) is { } rightQuarters)
                    {
                        Point leftQuartersOffset = new(
                            offset.X + rightQuarters.OffsetX - GetFrameWidth(IcnType.BarbarianCastleCaptainQuartersLeftSide),
                            offset.Y + rightQuarters.OffsetY);

                        DrawBuilding(
                            target,
                            IcnType.BarbarianCastleCaptainQuartersLeftSide,
                            0,
                            leftQuartersOffset,
                            maxArea,
                            alpha);
                    }
                }
            }
            else if (race == MapRace.Sorceress
                     && !CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Castle, underConstruction))
            {
                DrawBuilding(target, IcnType.SorceressCastleCaptainQuartersLeftSide, 0, offset, maxArea, alpha);
            }
        }
    }

    /// <summary>
    /// Draws the piece a building shares with another one, <c>redrawBuildingConnection()</c>
    /// (<c>castle_building.cpp:102-148</c>).
    /// </summary>
    /// <param name="target">The screen buffer.</param>
    /// <param name="castle">The town.</param>
    /// <param name="offset">The town view's top-left corner.</param>
    /// <param name="maxArea">The background rectangle, which every blit is clipped to.</param>
    /// <param name="building">The building that has just been drawn.</param>
    /// <param name="underConstruction">The building still fading in, or <see cref="BuildingType.Nothing"/>.</param>
    /// <param name="alpha">How opaque the piece is.</param>
    public static void RedrawBuildingConnection(
        PixelBuffer target,
        Castle castle,
        Point offset,
        Rect maxArea,
        BuildingType building,
        BuildingType underConstruction,
        byte alpha = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(castle);

        if (castle.Race == MapRace.Barbarian)
        {
            if ((building & BuildingType.MageGuild) != 0 || building == BuildingType.Spec)
            {
                bool isMageGuild = (building & BuildingType.MageGuild) != 0;

                if (isMageGuild
                    && (!castle.IsBuild(building)
                        || !CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Spec, underConstruction)))
                {
                    return;
                }

                if (!isMageGuild
                    && (!castle.IsBuild(building)
                        || !CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.MageGuild1, underConstruction)))
                {
                    return;
                }

                DrawBuilding(target, IcnType.Twnbext2, 0, offset, maxArea, alpha);
            }

            if (building == BuildingType.DwellingMonster3 || building == BuildingType.ThievesGuild)
            {
                BuildingType other = building == BuildingType.DwellingMonster3
                    ? BuildingType.ThievesGuild
                    : BuildingType.DwellingMonster3;

                if (!castle.IsBuild(building)
                    || !CastleBuildingRender.IsBuildingFullyBuilt(castle, other, underConstruction))
                {
                    return;
                }

                DrawBuilding(target, IcnType.Twnbext3, 0, offset, maxArea, alpha);
            }
        }
        else if (castle.Race == MapRace.Necromancer)
        {
            // The bridge joins the captain's quarters to the castle, so it waits for the other half to be finished.
            if (building == BuildingType.Captain
                && (!castle.IsBuild(building)
                    || !CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Castle, underConstruction)))
            {
                return;
            }

            if (building == BuildingType.Castle
                && (!castle.IsBuild(building)
                    || !CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Captain, underConstruction)))
            {
                return;
            }

            DrawBuilding(target, IcnType.NecromancerCastleCaptainQuartersBridge, 0, offset, maxArea, alpha);
        }
    }

    /// <summary>
    /// Draws one frame of an archive at a place it does not move from, <c>fheroes2::drawCastleDialogBuilding()</c>.
    /// </summary>
    /// <param name="target">The screen buffer.</param>
    /// <param name="icn">The archive.</param>
    /// <param name="frame">The frame inside it.</param>
    /// <param name="offset">Where the sprite's own offset is measured from.</param>
    /// <param name="clip">The rectangle every blit is clipped to.</param>
    /// <param name="alpha">How opaque the frame is; <see cref="byte.MaxValue"/> means "copy as is".</param>
    public static void DrawBuilding(
        PixelBuffer target,
        IcnType icn,
        int frame,
        Point offset,
        Rect clip,
        byte alpha = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (frame < 0 || alpha == 0)
        {
            return;
        }

        Sprite? sprite = GameAssets.GetImage(IcnCatalog.GetFileName(icn), frame);
        if (sprite is null)
        {
            return;
        }

        Point position = new(offset.X + sprite.OffsetX, offset.Y + sprite.OffsetY);
        Rect placed = new(position, sprite.Image.Bounds.Size);

        if (placed.Intersect(clip).IsEmpty)
        {
            return;
        }

        // The reference blends through AlphaBlit when alpha is not opaque; the port has no game palette at this layer,
        // so a fading building is drawn opaque. See the class remarks.
        ImageOps.BlitSpriteClipped(target, position, sprite, sprite.Image.Bounds, clip);
    }

    private static int GetFrameWidth(IcnType icn) =>
        GameAssets.GetImage(IcnCatalog.GetFileName(icn), 0)?.Image.Width ?? 0;
}
