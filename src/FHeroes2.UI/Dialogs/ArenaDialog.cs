using FHeroes2.Game.Heroes;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The choice of an arena, <c>Dialog::SelectSkillFromArena()</c> (<c>dialog.h:117</c>,
/// <c>dialog_arena.cpp:102-214</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two of the four primary skills are drawn and the player picks one; the reference returns the
/// <c>Skill::Primary::Type</c> of the pick and <c>UNKNOWN</c> when the window is closed, so the port answers with
/// <see cref="PrimarySkill"/> and <see langword="null"/>.
/// </para>
/// <para>
/// Which two skills are offered is not part of the model: the reference draws them from the global RNG inside the
/// dialog (<c>dialog_arena.cpp:60-100</c>), and once the scene is in charge of that it will pass them in rather than
/// let the dialog roll dice. That is why the model has no <see cref="Create"/> arguments yet.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): no layout yet.
/// </para>
/// </remarks>
public sealed class ArenaDialog
{
    private ArenaDialog()
    {
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::SelectSkillFromArena()</c> (<c>dialog_arena.cpp:102</c>).
    /// </summary>
    /// <returns>The model.</returns>
    public static ArenaDialog Create() => new();

    /// <summary>
    /// The result of the dialog: the chosen primary skill, or <see langword="null"/> when it was closed.
    /// </summary>
    public PrimarySkill? Result { get; private set; }

    /// <summary>
    /// Records the choice.
    /// </summary>
    /// <param name="skill">The chosen primary skill, or <see langword="null"/> when the dialog was closed.</param>
    public void Choose(PrimarySkill? skill)
    {
        Result = skill;
    }
}
