using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Interface;

/// <summary>
/// Which cursor the adventure screen shows over a tile, <c>Interface::AdventureMap::GetCursorFocus*()</c>,
/// <c>_getCursorNoFocus()</c>, <c>GetCursorTileIndex()</c> and <c>updateCursor()</c>
/// (<c>game/game_startgame.cpp:477-730</c>).
/// </summary>
/// <remarks>
/// <para>
/// The cursor answers one question: what happens when the player clicks this tile with this hero selected. It is
/// therefore the click handler's twin — <see cref="AdventureMap.Events.cs"/> does the same classification through
/// <c>Mp2ObjectTypes.IsRevisitAllowedForObject()</c> and the action dispatcher.
/// </para>
/// <para>
/// <b>Object types.</b> The reference switches on <c>tile.getMainObjectType()</c>, which keeps the action bit, and
/// lists the two variants of a castle explicitly because the gate and the footprint of a town answer differently.
/// The port does the same through the cached <see cref="Tile.MainObjectType"/>: <see cref="Mp2ObjectType.Castle"/>
/// is the footprint, <see cref="Mp2ObjectType.ActionCastle"/> is the gate. Objects that exist in only one flavour in
/// the port (heroes, boats, monsters) are listed with both labels, since a tile may carry either.
/// </para>
/// <para>
/// <b>Hot keys.</b> <c>Game::HotKeyHoldEvent( WORLD_QUICK_SELECT_HERO )</c> is not ported; the screen says whether
/// the key is held, the same way <see cref="Widgets.ArmyBar"/> takes its hot-key answer.
/// </para>
/// <para>
/// <b>Theme distance.</b> <c>Cursor::DistanceThemes()</c> is <see cref="CursorLayout.WithDistance"/>, and it answers
/// <see cref="CursorTheme.Pointer"/> for a zero distance, which is why several branches fall back to the tame cursor
/// when the hero cannot reach the tile today.
/// </para>
/// </remarks>
public sealed partial class AdventureMap
{
    /// <summary>
    /// The cursor over a tile while a town is focused, <c>GetCursorFocusCastle()</c>
    /// (<c>game_startgame.cpp:477-506</c>).
    /// </summary>
    /// <param name="castle">The focused town.</param>
    /// <param name="tile">The tile under the cursor.</param>
    /// <returns>The cursor theme.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="castle"/> or <paramref name="tile"/> is <see langword="null"/>.
    /// </exception>
    public CursorTheme GetCursorFocusCastle(CastleTown castle, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(tile);

        switch (tile.MainObjectType)
        {
        case Mp2ObjectType.Castle:
        case Mp2ObjectType.ActionCastle:
        {
            CastleTown? otherCastle = GetCastleOf(tile);

            if (otherCastle is not null)
            {
                return otherCastle.Color == castle.Color ? CursorTheme.Castle : CursorTheme.Pointer;
            }

            break;
        }

        case Mp2ObjectType.Hero:
        case Mp2ObjectType.ActionHero:
        {
            Hero? hero = tile.GetHero(World.Current);

            if (hero is not null)
            {
                return hero.Color == castle.Color ? CursorTheme.Heroes : CursorTheme.Pointer;
            }

            break;
        }

        default:
            break;
        }

        return CursorTheme.Pointer;
    }

    /// <summary>
    /// The cursor over a tile while a hero at sea is focused, <c>GetCursorFocusShipmaster()</c>
    /// (<c>game_startgame.cpp:508-576</c>).
    /// </summary>
    /// <param name="hero">The focused hero, a ship master.</param>
    /// <param name="tile">The tile under the cursor.</param>
    /// <returns>The cursor theme.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hero"/> or <paramref name="tile"/> is <see langword="null"/>.
    /// </exception>
    public CursorTheme GetCursorFocusShipmaster(Hero hero, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(tile);

        World world = World.Current;
        bool isWater = tile.IsWater;

        switch (tile.MainObjectType)
        {
        case Mp2ObjectType.Castle:
        case Mp2ObjectType.ActionCastle:
        {
            CastleTown? castle = GetCastleOf(tile);

            if (castle is not null)
            {
                // A ship can enter the footprint of a town when the water reaches it: the harbour is there.
                if (tile.MainObjectType == Mp2ObjectType.Castle
                    && isWater
                    && tile.IsPassableFrom(world, Direction.Center, true, false, hero.Color))
                {
                    return GetDistanceTheme(CursorTheme.HeroBoat, hero, tile.Index);
                }

                return hero.Color == castle.Color ? CursorTheme.Castle : CursorTheme.Pointer;
            }

            break;
        }

        case Mp2ObjectType.Hero:
        case Mp2ObjectType.ActionHero:
        {
            Hero? otherHero = tile.GetHero(world);

            if (otherHero is not null)
            {
                if (!otherHero.IsShipMaster)
                {
                    return hero.Color == otherHero.Color ? CursorTheme.Heroes : CursorTheme.Pointer;
                }

                if (otherHero.MapIndex == hero.MapIndex)
                {
                    return CursorTheme.Heroes;
                }

                if (hero.Color == otherHero.Color)
                {
                    return GetDistanceTheme(CursorTheme.HeroMeet, hero, tile.Index);
                }

                if (hero.IsFriends(otherHero.Color))
                {
                    return CursorTheme.Pointer;
                }

                return GetDistanceTheme(CursorTheme.HeroFight, hero, tile.Index);
            }

            break;
        }

        // Some map editors allow to place monsters on water tiles.
        case Mp2ObjectType.Monster:
        case Mp2ObjectType.ActionMonster:
            return isWater ? GetDistanceTheme(CursorTheme.HeroFight, hero, tile.Index) : CursorTheme.Pointer;

        default:
            if (isWater)
            {
                if (Mp2ObjectTypes.IsWaterActionObject(tile.MainObjectType))
                {
                    return GetDistanceTheme(CursorTheme.HeroBoatAction, hero, tile.Index);
                }

                if (tile.IsPassableFrom(world, Direction.Center, true, false, hero.Color))
                {
                    return GetDistanceTheme(CursorTheme.HeroBoat, hero, tile.Index);
                }
            }
            else
            {
                // A ship master on the water points at the shore: that is where the ship can drop the anchor.
                return GetDistanceTheme(CursorTheme.HeroAnchor, hero, tile.Index);
            }

            break;
        }

        return CursorTheme.Pointer;
    }

    /// <summary>
    /// The cursor over a tile while a hero on foot is focused, <c>GetCursorFocusHeroes()</c>
    /// (<c>game_startgame.cpp:603-696</c>).
    /// </summary>
    /// <param name="hero">The focused hero.</param>
    /// <param name="tile">The tile under the cursor.</param>
    /// <param name="isQuickSelectHeld">
    /// Whether the quick-select hot key is held, <c>HotKeyHoldEvent( WORLD_QUICK_SELECT_HERO )</c>: it turns the
    /// meeting cursor into the plain hero cursor, so that two heroes of the same player can be swapped without
    /// moving.
    /// </param>
    /// <returns>The cursor theme.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hero"/> or <paramref name="tile"/> is <see langword="null"/>.
    /// </exception>
    public CursorTheme GetCursorFocusHeroes(Hero hero, Tile tile, bool isQuickSelectHeld = false)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(tile);

        if (hero.Modes.HasFlag(HeroModes.MoveEnabled))
        {
            // The hero is walking: its own cursor stays on screen.
            return CursorState.Current?.Theme ?? CursorTheme.Pointer;
        }

        if (hero.IsShipMaster)
        {
            return GetCursorFocusShipmaster(hero, tile);
        }

        World world = World.Current;
        int tileIndex = tile.Index;

        switch (tile.MainObjectType)
        {
        case Mp2ObjectType.Castle:
        case Mp2ObjectType.ActionCastle:
        {
            CastleTown? castle = GetCastleOf(tile);

            if (castle is not null)
            {
                if (tile.MainObjectType == Mp2ObjectType.Castle)
                {
                    // The footprint of the town: only its own player can walk in, everybody else has to reach the
                    // gate.
                    if (tile.PassableDirections == Direction.Unknown)
                    {
                        return hero.Color == castle.Color ? CursorTheme.Castle : CursorTheme.Pointer;
                    }

                    return GetDistanceTheme(
                        world.IsTileUnderProtection(tileIndex) ? CursorTheme.HeroFight : CursorTheme.HeroMove,
                        hero,
                        tileIndex);
                }

                if (hero.MapIndex == castle.TileIndex)
                {
                    return hero.Color == castle.Color ? CursorTheme.Castle : CursorTheme.Pointer;
                }

                if (hero.Color == castle.Color)
                {
                    return GetDistanceTheme(CursorTheme.HeroAction, hero, castle.TileIndex);
                }

                if (hero.IsFriends(castle.Color))
                {
                    return CursorTheme.Pointer;
                }

                if (castle.GetActualArmy(null).IsValid)
                {
                    return GetDistanceTheme(CursorTheme.HeroFight, hero, castle.TileIndex);
                }

                return GetDistanceTheme(CursorTheme.HeroAction, hero, castle.TileIndex);
            }

            break;
        }

        case Mp2ObjectType.Hero:
        case Mp2ObjectType.ActionHero:
        {
            Hero? otherHero = tile.GetHero(world);

            if (otherHero is not null)
            {
                if (otherHero.MapIndex == hero.MapIndex)
                {
                    return CursorTheme.Heroes;
                }

                if (hero.Color == otherHero.Color)
                {
                    if (isQuickSelectHeld)
                    {
                        return CursorTheme.Heroes;
                    }

                    CursorTheme meetCursor = GetDistanceTheme(CursorTheme.HeroMeet, hero, tileIndex);

                    return meetCursor != CursorTheme.Pointer ? meetCursor : CursorTheme.Heroes;
                }

                if (hero.IsFriends(otherHero.Color))
                {
                    return CursorTheme.Pointer;
                }

                return GetDistanceTheme(CursorTheme.HeroFight, hero, tileIndex);
            }

            break;
        }

        case Mp2ObjectType.Boat:
        case Mp2ObjectType.ActionBoat:
            return GetDistanceTheme(CursorTheme.HeroBoat, hero, tileIndex);

        default:
            if (Mp2ObjectTypes.IsInGameActionObject(tile.MainObjectType))
            {
                // A guarded object, or one whose guards have been beaten, is still a capture object: the fight
                // cursor only shows while somebody else's guards stand on it.
                bool isProtected = world.IsTileUnderProtection(tileIndex)
                                   || (!hero.IsFriends(tile.GetColor(world)) && world.IsCaptureObjectProtected(tileIndex));

                return GetDistanceTheme(isProtected ? CursorTheme.HeroFight : CursorTheme.HeroAction, hero, tileIndex);
            }

            if (tile.IsPassableFrom(world, Direction.Center, hero.IsShipMaster, false, hero.Color))
            {
                return GetDistanceTheme(
                    world.IsTileUnderProtection(tileIndex) ? CursorTheme.HeroFight : CursorTheme.HeroMove,
                    hero,
                    tileIndex);
            }

            break;
        }

        return CursorTheme.Pointer;
    }

    /// <summary>
    /// The cursor over a tile while nothing is focused, <c>_getCursorNoFocus()</c>
    /// (<c>game_startgame.cpp:578-601</c>).
    /// </summary>
    /// <param name="tile">The tile under the cursor.</param>
    /// <returns>The cursor theme: only the player's own towns and heroes can be entered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public CursorTheme GetCursorNoFocus(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        PlayerColor currentColor = Settings.Get().Players.CurrentColor;

        switch (tile.MainObjectType)
        {
        case Mp2ObjectType.Castle:
        case Mp2ObjectType.ActionCastle:
        {
            CastleTown? castle = GetCastleOf(tile);

            if (castle is not null && castle.Color == currentColor)
            {
                return CursorTheme.Castle;
            }

            break;
        }

        case Mp2ObjectType.Hero:
        case Mp2ObjectType.ActionHero:
        {
            Hero? hero = tile.GetHero(World.Current);

            if (hero is not null && hero.Color == currentColor)
            {
                return CursorTheme.Heroes;
            }

            break;
        }

        default:
            break;
        }

        return CursorTheme.Pointer;
    }

    /// <summary>
    /// The cursor a tile asks for, <c>GetCursorTileIndex()</c> (<c>game_startgame.cpp:703-730</c>).
    /// </summary>
    /// <param name="destinationIndex">The tile under the cursor.</param>
    /// <param name="isQuickSelectHeld">Whether the quick-select hot key is held.</param>
    /// <returns>The cursor theme; the plain pointer for fog and for tiles outside the map.</returns>
    public CursorTheme GetCursorTileIndex(int destinationIndex, bool isQuickSelectHeld = false)
    {
        World world = World.Current;

        if (!GetGeometry().IsValidAbsIndex(destinationIndex))
        {
            return CursorTheme.Pointer;
        }

        Tile tile = world.GetTile(destinationIndex);

        if (tile.IsFog(Settings.Get().Players.CurrentColor))
        {
            return CursorTheme.Pointer;
        }

        return InterfaceFocus.GetFocusType() switch
        {
            GameFocus.Heroes when InterfaceFocus.GetFocusHeroes() is { } hero =>
                GetCursorFocusHeroes(hero, tile, isQuickSelectHeld),
            GameFocus.Castle when InterfaceFocus.GetFocusCastle() is { } castle => GetCursorFocusCastle(castle, tile),
            GameFocus.None => GetCursorNoFocus(tile),
            _ => CursorTheme.Pointer
        };
    }

    /// <summary>
    /// Shows the cursor a tile asks for, <c>updateCursor()</c> (<c>game_startgame.cpp:698-701</c>).
    /// </summary>
    /// <param name="tileIndex">The tile under the cursor.</param>
    /// <param name="isQuickSelectHeld">Whether the quick-select hot key is held.</param>
    /// <remarks>
    /// The reference's <c>Cursor::Get()</c> always exists; the port's <see cref="CursorState.Current"/> is
    /// <see langword="null"/> when nothing draws a cursor, and then there is nothing to set.
    /// </remarks>
    public void UpdateCursor(int tileIndex, bool isQuickSelectHeld)
    {
        CursorState.Current?.SetThemes(GetCursorTileIndex(tileIndex, isQuickSelectHeld));
    }

    /// <summary>The town that owns a tile, <c>world.getCastle( tile.GetCenter() )</c>.</summary>
    /// <param name="tile">The tile.</param>
    /// <returns>The town, or <see langword="null"/> when no town covers the tile.</returns>
    private static CastleTown? GetCastleOf(Tile tile)
    {
        Point center = GetCenterOf(tile.Index);

        return World.Current.GetCastle(center.X, center.Y);
    }

    /// <summary>
    /// The cursor a hero walk to a tile would show, <c>Cursor::DistanceThemes()</c>, with the number of days the
    /// walk takes.
    /// </summary>
    /// <param name="theme">The theme to decorate.</param>
    /// <param name="hero">The hero that would walk.</param>
    /// <param name="destinationIndex">The tile to walk to.</param>
    /// <returns>The decorated theme, or the plain pointer when the hero cannot be there today.</returns>
    private static CursorTheme GetDistanceTheme(CursorTheme theme, Hero hero, int destinationIndex)
    {
        int days = hero.GetNumOfTravelDays(World.Current, destinationIndex);

        return CursorLayout.WithDistance(theme, days > 0 ? (uint)days : 0);
    }
}
