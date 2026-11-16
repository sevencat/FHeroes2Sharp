namespace FHeroes2.Game.System;

/// <summary>
/// Who drives a player, <c>CONTROL_NONE</c>/<c>CONTROL_HUMAN</c>/<c>CONTROL_AI</c> (<c>players.h:42-48</c>).
/// </summary>
/// <remarks>
/// <para>
/// The two bits can be set at once, and the game leans on that: a hot-seat slot whose army may be handed to the
/// computer is stored as <c>Human | Ai</c> and collapses to <see cref="Ai"/> when the game starts
/// (<c>fixMultiControl</c>). That is why this is a flag set rather than three exclusive values.
/// </para>
/// <para>
/// The reference keeps the constants as plain <c>int</c>s and adds a virtual <c>Control</c> base class with
/// <c>isControlAI</c>/<c>isControlHuman</c>. The port keeps the values and turns the two predicates into
/// extension methods, because nothing else inherits from that base class.
/// </para>
/// </remarks>
[Flags]
public enum PlayerControl
{
    /// <summary>Nobody — a player that has not been given a side yet, <c>CONTROL_NONE</c>.</summary>
    None = 0,

    /// <summary>A person plays this colour, <c>CONTROL_HUMAN</c>.</summary>
    Human = 1,

    /// <summary>The computer plays this colour, <c>CONTROL_AI</c>.</summary>
    Ai = 4,
}

/// <summary>
/// The two questions everything asks of <see cref="PlayerControl"/>, <c>Control::isControlAI()</c> and
/// <c>Control::isControlHuman()</c> (<c>players.h:89-97</c>).
/// </summary>
public static class PlayerControlExtensions
{
    /// <summary>Whether the computer has any say in this slot, <c>isControlAI()</c>.</summary>
    /// <param name="control">The control flags.</param>
    /// <returns>True when the AI bit is set.</returns>
    public static bool IsAi(this PlayerControl control) => (control & PlayerControl.Ai) != 0;

    /// <summary>Whether a person has any say in this slot, <c>isControlHuman()</c>.</summary>
    /// <param name="control">The control flags.</param>
    /// <returns>True when the human bit is set.</returns>
    public static bool IsHuman(this PlayerControl control) => (control & PlayerControl.Human) != 0;
}
