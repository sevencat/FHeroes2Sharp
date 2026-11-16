using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// One hero on offer in a kingdom's tavern, <c>Recruit</c> (<c>heroes_recruits.h:39-70</c>).
/// </summary>
/// <remarks>
/// <para>
/// A recruit is only an id and a day — the hero itself is looked up in <see cref="AllHeroes"/>, exactly as the
/// reference uses <c>world.GetHeroes()</c>. Keeping the class free of the world is what lets the tavern and the
/// new-week settlement share it.
/// </para>
/// <para>
/// <c>default</c> is the reference's default constructor: <c>UNKNOWN</c> on day 0. That is what
/// <see cref="None"/> returns and what a reset slot holds.
/// </para>
/// </remarks>
/// <param name="Id">The hero's id, <c>_id</c>; <see cref="Hero.UnknownId"/> when the slot is empty.</param>
/// <param name="SurrenderDay">The day the hero surrendered on, <c>_surrenderDay</c>; 0 for a hero never hired.</param>
public readonly record struct Recruit(int Id, uint SurrenderDay)
{
    /// <summary>A hero offered by the tavern, <c>Recruit( const Heroes &amp; )</c> (<c>heroes_recruits.cpp:37</c>).</summary>
    /// <param name="hero">The hero.</param>
    /// <remarks>The day stays 0: a hero that was never hired has never surrendered either.</remarks>
    public Recruit(Hero hero)
        : this(hero.Id, 0)
    {
    }

    /// <summary>An empty slot, <c>Recruit()</c> (<c>heroes_recruits.cpp:33</c>).</summary>
    public static Recruit None => default;

    /// <summary>Whether the slot holds a hero, the inverse of <c>GetID() == UNKNOWN</c>.</summary>
    public bool IsValid => Id != Hero.UnknownId;
}

/// <summary>
/// The two heroes a kingdom offers in its tavern, <c>Recruits</c> (<c>heroes_recruits.h:72-92</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference derives from <c>std::pair&lt;Recruit, Recruit&gt;</c>, so <c>first</c> and <c>second</c> are public
/// members. The port keeps the names as read-only properties and mutates them only through
/// <see cref="SetHero1"/>/<see cref="SetHero2"/>/<see cref="AppendSurrenderedHero"/> — the reference has no other
/// writers either, except the save-file loader, which is not translated yet.
/// </para>
/// <para>
/// <b>These are not the heroes of the kingdom.</b> They are the two the tavern is willing to sell: one of them may
/// belong to nobody (a native hero), and after a battle they can be the enemy heroes that surrendered or retreated.
/// That second mechanic is fheroes2's extension of the original game, which offers only the last one.
/// </para>
/// </remarks>
public sealed class Recruits
{
    /// <summary>The first slot, <c>first</c>.</summary>
    public Recruit First { get; private set; } = Recruit.None;

    /// <summary>The second slot, <c>second</c>.</summary>
    public Recruit Second { get; private set; } = Recruit.None;

    /// <summary>
    /// The id of the first hero, <c>GetID1()</c> (<c>heroes_recruits.h:76</c>).
    /// </summary>
    public int Id1 => First.Id;

    /// <summary>
    /// The id of the second hero, <c>GetID2()</c> (<c>heroes_recruits.h:81</c>).
    /// </summary>
    public int Id2 => Second.Id;

    /// <summary>
    /// Empties both slots, <c>Reset()</c> (<c>heroes_recruits.cpp:44</c>).
    /// </summary>
    /// <remarks>
    /// Called by <c>Kingdom::clear()</c> (<c>kingdom.cpp:132</c>) and by the new-week settlement before it looks for
    /// fresh heroes. Note that it does <b>not</b> clear the heroes' <see cref="HeroModes.Recruit"/> bit: the mode is
    /// cleared by whoever drops the hero out of the offer, not by the kingdom that stops offering it.
    /// </remarks>
    public void Reset()
    {
        First = Recruit.None;
        Second = Recruit.None;
    }

    /// <summary>
    /// The first hero, <c>GetHero1()</c> (<c>heroes_recruits.cpp:50</c>).
    /// </summary>
    /// <param name="heroes">The hero table to look the id up in; the reference reaches <c>world.GetHeroes()</c>.</param>
    /// <returns>The hero, or <see langword="null"/> when the slot is empty or the id is not a hero's.</returns>
    public Hero? GetHero1(AllHeroes heroes) => heroes.Get(First.Id);

    /// <summary>
    /// The second hero, <c>GetHero2()</c> (<c>heroes_recruits.cpp:55</c>).
    /// </summary>
    /// <param name="heroes">The hero table to look the id up in.</param>
    /// <returns>The hero, or <see langword="null"/> when the slot is empty or the id is not a hero's.</returns>
    public Hero? GetHero2(AllHeroes heroes) => heroes.Get(Second.Id);

    /// <summary>
    /// The day the first hero surrendered on, <c>getSurrenderDayOfHero1()</c> (<c>heroes_recruits.cpp:60</c>).
    /// </summary>
    public uint GetSurrenderDayOfHero1() => First.SurrenderDay;

    /// <summary>
    /// The day the second hero surrendered on, <c>getSurrenderDayOfHero2()</c> (<c>heroes_recruits.cpp:65</c>).
    /// </summary>
    public uint GetSurrenderDayOfHero2() => Second.SurrenderDay;

    /// <summary>
    /// Offers <paramref name="hero"/> in the first slot, <c>SetHero1()</c> (<c>heroes_recruits.cpp:70</c>).
    /// </summary>
    /// <param name="hero">The hero, or <see langword="null"/> to empty the slot.</param>
    /// <remarks>
    /// Setting a hero also sets its <see cref="HeroModes.Recruit"/> bit; emptying the slot leaves the bit alone,
    /// because the hero that leaves the offer is usually the one that has just been hired and must stop being marked.
    /// </remarks>
    public void SetHero1(Hero? hero)
    {
        if (hero != null)
        {
            hero.SetModes(HeroModes.Recruit);
            First = new Recruit(hero);
        }
        else
        {
            First = Recruit.None;
        }
    }

    /// <summary>
    /// Offers <paramref name="hero"/> in the second slot, <c>SetHero2()</c> (<c>heroes_recruits.cpp:82</c>).
    /// </summary>
    /// <param name="hero">The hero, or <see langword="null"/> to empty the slot.</param>
    public void SetHero2(Hero? hero)
    {
        if (hero != null)
        {
            hero.SetModes(HeroModes.Recruit);
            Second = new Recruit(hero);
        }
        else
        {
            Second = Recruit.None;
        }
    }

    /// <summary>
    /// Offers a hero that surrendered or retreated, in place of the older of the two slots,
    /// <c>appendSurrenderedHero()</c> (<c>heroes_recruits.cpp:94</c>).
    /// </summary>
    /// <param name="hero">The hero that surrendered or retreated.</param>
    /// <param name="surrenderDay">The day it happened; must be a real day, so 1 or later.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="surrenderDay"/> is 0.</exception>
    /// <remarks>
    /// <para>
    /// The original game offers only the hero who surrendered last; fheroes2 offers the last two. The replacement
    /// therefore goes into the slot with the <b>older</b> surrender day — and when the days are equal (both slots
    /// fresh, or both filled on the same day) the reference's <c>first.getSurrenderDay() &gt; second.getSurrenderDay()</c>
    /// is false, so the <b>first</b> slot is replaced. Keeping that order matters: a kingdom that keeps two
    /// surrendered heroes must not keep swapping which slot the newest one lands in.
    /// </para>
    /// <para>
    /// The reference asserts on day 0. The port throws, because a day of 0 would make the slot look like an empty
    /// one to <see cref="First"/>'s default value and the hero would be offered forever.
    /// </para>
    /// </remarks>
    public void AppendSurrenderedHero(Hero hero, uint surrenderDay)
    {
        ArgumentOutOfRangeException.ThrowIfZero(surrenderDay);

        hero.SetModes(HeroModes.Recruit);

        if (First.SurrenderDay > Second.SurrenderDay)
        {
            Second = new Recruit(hero.Id, surrenderDay);
        }
        else
        {
            First = new Recruit(hero.Id, surrenderDay);
        }
    }
}
