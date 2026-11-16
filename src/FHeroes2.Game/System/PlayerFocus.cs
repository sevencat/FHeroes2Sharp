namespace FHeroes2.Game.System;

/// <summary>
/// What the adventure map of a player is looking at, <c>FOCUS_UNSEL</c>/<c>FOCUS_HEROES</c>/<c>FOCUS_CASTLE</c>
/// (<c>players.h:50-55</c>).
/// </summary>
public enum FocusKind
{
    /// <summary>Nothing is selected.</summary>
    None = 0,

    /// <summary>A hero is selected.</summary>
    Heroes = 1,

    /// <summary>A castle is selected.</summary>
    Castle = 2,
}

/// <summary>
/// The subject of the adventure map camera: a hero or a castle, <c>struct Focus</c> (<c>players.h:57-88</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference derives from <c>std::pair&lt;int32_t, void *&gt;</c> and casts the pointer to whichever type the
/// kind says. The port keeps two typed references instead: a <c>void *</c> exists in C++ because the pair has to
/// be serialisable without knowing the types, while the port has no serialiser and gains nothing from the cast.
/// </para>
/// <para>
/// Exactly one of <see cref="Hero"/> and <see cref="Castle"/> is non-null while the kind says so; the getters
/// re-check the kind rather than trusting the fields, as the reference does, so a stale reference left behind by
/// a reset can never be read back as a focus.
/// </para>
/// </remarks>
public sealed class PlayerFocus
{
    /// <summary>What is selected, the <c>first</c> half of the reference pair.</summary>
    public FocusKind Kind { get; private set; }

    /// <summary>The selected hero, when <see cref="Kind"/> is <see cref="FocusKind.Heroes"/>.</summary>
    public Heroes.Hero? Hero { get; private set; }

    /// <summary>The selected castle, when <see cref="Kind"/> is <see cref="FocusKind.Castle"/>.</summary>
    public Castle.Castle? Castle { get; private set; }

    /// <summary>Forgets the selection, <c>Focus::Reset()</c>.</summary>
    public void Reset()
    {
        Kind = FocusKind.None;
        Hero = null;
        Castle = null;
    }

    /// <summary>Selects a hero, <c>Focus::Set( Heroes * )</c>.</summary>
    /// <param name="hero">The hero to select.</param>
    public void Set(Heroes.Hero hero)
    {
        Kind = FocusKind.Heroes;
        Hero = hero;
        Castle = null;
    }

    /// <summary>Selects a castle, <c>Focus::Set( Castle * )</c>.</summary>
    /// <param name="castle">The castle to select.</param>
    public void Set(Castle.Castle castle)
    {
        Kind = FocusKind.Castle;
        Castle = castle;
        Hero = null;
    }

    /// <summary>
    /// The selected castle, or null, <c>Focus::GetCastle()</c>.
    /// </summary>
    /// <returns>The castle when the kind says castle.</returns>
    public Castle.Castle? GetCastle() => Kind == FocusKind.Castle ? Castle : null;

    /// <summary>
    /// The selected hero, or null, <c>Focus::GetHeroes()</c>.
    /// </summary>
    /// <returns>The hero when the kind says hero.</returns>
    public Heroes.Hero? GetHeroes() => Kind == FocusKind.Heroes ? Hero : null;
}
