using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// <c>Speed::</c> of <c>src/fheroes2/kingdom/speed.h</c>: the ten step scale of a monster stack and the
/// two adjustments the slow and the haste spells make to it.
/// </summary>
/// <remarks>
/// <para>
/// The scale is a plain <see cref="uint"/> in the reference as well (<c>namespace Speed { enum : uint32_t }</c>
/// is an unnamed enum), because monster speeds are stored as numbers in the monster table and only compared
/// with each other.
/// </para>
/// <para>
/// <c>Speed::String()</c> returns the reference's English (<see cref="GetName"/>), and the two spell
/// adjustments are here, including their fallback to a single step when the spell has no <c>extraValue</c> —
/// the reference checks that value first, so a modified spell changes the step size.
/// </para>
/// </remarks>
public static class Speed
{
    /// <summary><c>Speed::STANDING</c>: a stack that cannot move.</summary>
    public const uint Standing = 0;

    /// <summary><c>Speed::CRAWLING</c>.</summary>
    public const uint Crawling = 1;

    /// <summary><c>Speed::VERYSLOW</c>.</summary>
    public const uint VerySlow = 2;

    /// <summary><c>Speed::SLOW</c>.</summary>
    public const uint Slow = 3;

    /// <summary><c>Speed::AVERAGE</c>.</summary>
    public const uint Average = 4;

    /// <summary><c>Speed::FAST</c>.</summary>
    public const uint Fast = 5;

    /// <summary><c>Speed::VERYFAST</c>.</summary>
    public const uint VeryFast = 6;

    /// <summary><c>Speed::ULTRAFAST</c>.</summary>
    public const uint UltraFast = 7;

    /// <summary><c>Speed::BLAZING</c>.</summary>
    public const uint Blazing = 8;

    /// <summary><c>Speed::INSTANT</c>.</summary>
    public const uint Instant = 9;

    /// <summary>
    /// The name of a speed, <c>Speed::String()</c> (<c>speed.cpp:84-112</c>).
    /// </summary>
    /// <remarks>
    /// The reference's English with the <c>speed|</c> gettext context prefix dropped, and <c>"Unknown"</c> for a
    /// value off the scale — the reference's <c>default</c> branch, which a bad save can reach.
    /// </remarks>
    /// <param name="speed">The speed.</param>
    /// <returns>The word the info screen prints.</returns>
    public static string GetName(uint speed) => speed switch
    {
        Standing => "Standing",
        Crawling => "Crawling",
        VerySlow => "Very Slow",
        Slow => "Slow",
        Average => "Average",
        Fast => "Fast",
        VeryFast => "Very Fast",
        UltraFast => "Ultra Fast",
        Blazing => "Blazing",
        Instant => "Instant",
        _ => "Unknown",
    };

    /// <summary>
    /// The speed after the slow spell, <c>Speed::getSlowSpeedFromSpell()</c> (<c>speed.cpp:114</c>).
    /// </summary>
    /// <param name="currentSpeed">The speed of the stack before the spell.</param>
    /// <returns>The speed reduced by the <c>extraValue</c> of the spell, or by one step without it.</returns>
    public static uint GetSlowSpeedFromSpell(uint currentSpeed)
    {
        uint spellExtraValue = new Spell(SpellType.Slow).ExtraValue();

        return spellExtraValue != 0 ? currentSpeed - spellExtraValue : GetSlowerSpeed(currentSpeed);
    }

    /// <summary>
    /// The speed after the haste spell, <c>Speed::getHasteSpeedFromSpell()</c> (<c>speed.cpp:120</c>).
    /// </summary>
    /// <param name="currentSpeed">The speed of the stack before the spell.</param>
    /// <returns>The speed raised by the <c>extraValue</c> of the spell, or by one step without it.</returns>
    public static uint GetHasteSpeedFromSpell(uint currentSpeed)
    {
        uint spellExtraValue = new Spell(SpellType.Haste).ExtraValue();

        return spellExtraValue != 0 ? currentSpeed + spellExtraValue : GetFasterSpeed(currentSpeed);
    }

    /// <summary>
    /// One step down the scale, <c>getSlowerSpeed()</c> (<c>speed.cpp:31-53</c>) — a private helper there.
    /// </summary>
    /// <remarks>
    /// The pairs share a step: crawling and very slow both stay at crawling, slow and average both drop to
    /// very slow. <see cref="Standing"/> and anything unknown stay where they are, which is what the
    /// reference's <c>default</c> branch does with <c>SPEED::STANDING</c>.
    /// </remarks>
    /// <param name="speed">The speed to lower.</param>
    /// <returns>The speed one step slower.</returns>
    public static uint GetSlowerSpeed(uint speed) => speed switch
    {
        Crawling or VerySlow => Crawling,
        Slow or Average => VerySlow,
        Fast or VeryFast => Slow,
        UltraFast or Blazing => Average,
        Instant => Fast,
        _ => Standing
    };

    /// <summary>
    /// One step up the scale, <c>getFasterSpeed()</c> (<c>speed.cpp:55-79</c>) — a private helper there.
    /// </summary>
    /// <remarks>
    /// Crawling is the only value that skips a step: the reference raises it to slow, then climbs by one
    /// each time. <see cref="UltraFast"/> and above all end at <see cref="Instant"/>.
    /// </remarks>
    /// <param name="speed">The speed to raise.</param>
    /// <returns>The speed one step faster.</returns>
    public static uint GetFasterSpeed(uint speed) => speed switch
    {
        Crawling => Slow,
        VerySlow => Average,
        Slow => Fast,
        Average => VeryFast,
        Fast => UltraFast,
        VeryFast => Blazing,
        UltraFast or Blazing or Instant => Instant,
        _ => Standing
    };
}
