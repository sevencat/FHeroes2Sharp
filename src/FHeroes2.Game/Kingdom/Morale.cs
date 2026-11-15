namespace FHeroes2.Game.Kingdom;

/// <summary>
/// <c>Morale::</c> of <c>src/fheroes2/kingdom/morale.h</c>: the morale of a hero's army, a seven step scale
/// plus one "unknown" value. Same shape as <see cref="Luck"/>.
/// </summary>
/// <remarks>
/// The two scales share their numbers but not their meaning: a negative morale makes units freeze, a
/// negative luck makes their attacks do half damage, and the positive messages differ too. They are
/// separate types here for the same reason they are separate namespaces in the reference — a
/// <c>Morale</c> value must never be passed where a <c>Luck</c> value is expected.
/// <c>Morale::String()</c> and <c>Morale::Description()</c> are translated text and are not ported.
/// </remarks>
public static class Morale
{
    /// <summary><c>Morale::UNKNOWN</c>, the value of a hero who is not in the game yet.</summary>
    public const int Unknown = -4;

    /// <summary><c>Morale::TREASON</c>: units may freeze in combat.</summary>
    public const int Treason = -3;

    /// <summary><c>Morale::AWFUL</c>.</summary>
    public const int Awful = -2;

    /// <summary><c>Morale::POOR</c>.</summary>
    public const int Poor = -1;

    /// <summary><c>Morale::NORMAL</c>, the value of an army without any morale modifier.</summary>
    public const int Normal = 0;

    /// <summary><c>Morale::GOOD</c>.</summary>
    public const int Good = 1;

    /// <summary><c>Morale::GREAT</c>.</summary>
    public const int Great = 2;

    /// <summary><c>Morale::BLOOD</c>: units may get extra attacks.</summary>
    public const int Blood = 3;

    /// <summary>
    /// Clamps a morale sum into the scale, <c>Morale::Normalize()</c> (<c>morale.cpp:87</c>).
    /// </summary>
    /// <param name="morale">The raw sum of the modifiers, which can be outside the scale.</param>
    /// <returns>The value clamped into <see cref="Treason"/>…<see cref="Blood"/>.</returns>
    public static int Normalize(int morale) => Math.Clamp(morale, Treason, Blood);
}
