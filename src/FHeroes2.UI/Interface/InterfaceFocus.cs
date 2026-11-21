using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.System;

namespace FHeroes2.UI.Interface;

/// <summary>
/// What the adventure map of the current player is looking at, <c>namespace GameFocus</c>
/// (<c>game_interface.h:47-53</c>).
/// </summary>
public enum GameFocus
{
    /// <summary>Nothing is selected, <c>UNSEL</c>.</summary>
    None = 0,

    /// <summary>A hero is selected, <c>HEROES</c>.</summary>
    Heroes = 1,

    /// <summary>A castle is selected, <c>CASTLE</c>.</summary>
    Castle = 2,

    /// <summary>The first hero of the turn is selected, <c>FIRSTHERO</c>.</summary>
    FirstHero = 3,
}

/// <summary>
/// The three focus queries the interface widgets ask, <c>Interface::GetFocusHeroes()</c>,
/// <c>GetFocusCastle()</c> and <c>GetFocusType()</c> (<c>interface_focus.cpp:38-64</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference also owns the focus <b>setters</b> (<c>Interface::AdventureMap::SetFocus()</c> and the
/// <c>ResetFocus()</c> overloads). They belong to the adventure map orchestrator, which is not ported yet, so only
/// the getters live here; the mutating half arrives with <see cref="AdventureMapInterface"/>.
/// </para>
/// <para>
/// The focus itself is already modelled by <see cref="PlayerFocus"/> on the current <see cref="Player"/>, so these
/// queries need no interface object: like the reference, they read the current player from the settings.
/// </para>
/// </remarks>
public static class InterfaceFocus
{
    /// <summary>The hero the adventure map is centred on, <c>GetFocusHeroes()</c>.</summary>
    /// <returns>The hero, or <see langword="null"/> when a castle or nothing is selected.</returns>
    public static Hero? GetFocusHeroes()
    {
        PlayerFocus? focus = Settings.Get().Players.Current?.Focus;

        return focus is { Kind: FocusKind.Heroes } ? focus.Hero : null;
    }

    /// <summary>The castle the adventure map is centred on, <c>GetFocusCastle()</c>.</summary>
    /// <returns>The castle, or <see langword="null"/> when a hero or nothing is selected.</returns>
    public static Castle? GetFocusCastle()
    {
        PlayerFocus? focus = Settings.Get().Players.Current?.Focus;

        return focus is { Kind: FocusKind.Castle } ? focus.Castle : null;
    }

    /// <summary>What the adventure map is centred on, <c>GetFocusType()</c>.</summary>
    /// <returns><see cref="GameFocus.None"/> when there is no current player or nothing is selected.</returns>
    /// <remarks>
    /// The reference cannot return <c>FIRSTHERO</c> from here: the value only exists as an argument of
    /// <c>AdventureMap::SetFocus()</c>, which turns it into a hero focus before storing it.
    /// </remarks>
    public static GameFocus GetFocusType()
    {
        return Settings.Get().Players.Current?.Focus.Kind switch
        {
            FocusKind.Heroes => GameFocus.Heroes,
            FocusKind.Castle => GameFocus.Castle,
            _ => GameFocus.None,
        };
    }
}
