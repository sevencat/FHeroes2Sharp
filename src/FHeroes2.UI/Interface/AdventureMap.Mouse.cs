using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Interface;

/// <summary>
/// What the adventure screen does with a click on the map, <c>Interface::AdventureMap::mouseCursorAreaClickLeft()</c>,
/// <c>mouseCursorAreaPressRight()</c> and <c>mouseCursorAreaLongPressLeft()</c>
/// (<c>game/game_startgame.cpp:1615-1736</c>).
/// </summary>
/// <remarks>
/// <para>
/// The three handlers work on one premise: the cursor over the tile already says what the click would do
/// (<see cref="AdventureMap.Cursor.cs"/>). The left click therefore switches on
/// <c>Cursor::WithoutDistanceThemes( Cursor::Get().Themes() )</c> instead of looking at the tile again.
/// </para>
/// <para>
/// <b>Deviations.</b> <c>Dialog::QuickInfo()</c> opens a blocking window in the reference and is reported to
/// <see cref="IAdventureHost"/> here, exactly like the <c>Dialog::*</c> calls of
/// <see cref="AdventureMap.Events.cs"/>. The <c>IS_DEVEL()</c> half of the right-click fog test is dropped: the port
/// has no development build flag, so a fogged tile always shows its quick info. The <c>assert</c> on the focused hero
/// not walking is a debug check and has no counterpart.
/// </para>
/// </remarks>
public sealed partial class AdventureMap
{
    /// <inheritdoc />
    /// <remarks>
    /// The reference does not validate the index: it is called from the event loop that already mapped the mouse
    /// position to a tile. The port says so explicitly, so that a stale index from the scene cannot read a random
    /// tile.
    /// </remarks>
    public override void MouseCursorAreaClickLeft(int tileIndex)
    {
        World world = World.Current;

        if (!GetGeometry().IsValidAbsIndex(tileIndex))
        {
            return;
        }

        Hero? focusedHero = InterfaceFocus.GetFocusHeroes();
        Tile tile = world.GetTile(tileIndex);

        switch (CursorLayout.WithoutDistance(CursorState.Current?.Theme ?? CursorTheme.None))
        {
        case CursorTheme.Heroes:
        {
            // Another hero stands on the tile: select it, or open it when it is the selected one.
            Hero? otherHero = tile.GetHero(world);

            if (otherHero is null)
            {
                break;
            }

            if (focusedHero is null || !ReferenceEquals(focusedHero, otherHero))
            {
                SetFocus(otherHero, false);
                RedrawFocus();
            }
            else
            {
                _dialogs.OpenHeroesDialog(otherHero);
            }

            break;
        }

        case CursorTheme.Castle:
        {
            Mp2ObjectType objectType = tile.MainObjectType;

            if (objectType != Mp2ObjectType.Castle && objectType != Mp2ObjectType.ActionCastle)
            {
                break;
            }

            CastleTown? otherCastle = GetCastleOf(tile);

            if (otherCastle is null)
            {
                break;
            }

            CastleTown? focusedCastle = InterfaceFocus.GetFocusCastle();

            if (focusedCastle is null || !ReferenceEquals(focusedCastle, otherCastle))
            {
                SetFocus(otherCastle);
                RedrawFocus();
            }
            else
            {
                _dialogs.OpenCastleDialog(otherCastle);
            }

            break;
        }

        case CursorTheme.HeroFight:
        case CursorTheme.HeroMove:
        case CursorTheme.HeroBoat:
        case CursorTheme.HeroAnchor:
        case CursorTheme.HeroMeet:
        case CursorTheme.HeroAction:
        case CursorTheme.HeroBoatAction:
        {
            // Every other tile is a walk: the path is laid now, and a second click on the same destination makes
            // the hero start moving.
            if (focusedHero is null)
            {
                break;
            }

            ShowPathOrStartMoveHero(focusedHero, tileIndex);

            break;
        }

        default:
            break;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Shows what stands on the tile without leaving the map, the reference's <c>Dialog::QuickInfo()</c> overloads:
    /// the town window for a town, the hero window for a hero, the generic tile window otherwise.
    /// </remarks>
    public override void MouseCursorAreaPressRight(int tileIndex)
    {
        World world = World.Current;

        if (!GetGeometry().IsValidAbsIndex(tileIndex))
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);

        if (tile.IsFog(Settings.Get().Players.CurrentColor))
        {
            _dialogs.ShowQuickInfo(tile);

            return;
        }

        switch (tile.MainObjectType)
        {
        case Mp2ObjectType.Castle:
        case Mp2ObjectType.ActionCastle:
            if (GetCastleOf(tile) is { } castle)
            {
                _dialogs.ShowQuickInfo(castle);
            }
            else
            {
                // The footprint of a town whose gate is somewhere else: no town covers this tile exactly.
                _dialogs.ShowQuickInfo(tile);
            }

            break;

        case Mp2ObjectType.Hero:
        case Mp2ObjectType.ActionHero:
            if (tile.GetHero(world) is { } hero)
            {
                _dialogs.ShowQuickInfo(hero);
            }

            break;

        default:
            _dialogs.ShowQuickInfo(tile);
            break;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A long press cycles between the heroes standing on the tile, <c>EventSwitchFocusedHero()</c>
    /// (<c>gui/interface_events.cpp:133-141</c>).
    /// </remarks>
    public override void MouseCursorAreaLongPressLeft(int tileIndex) => EventSwitchFocusedHero(tileIndex);
}
