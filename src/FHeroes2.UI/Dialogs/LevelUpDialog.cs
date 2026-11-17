using FHeroes2.Game.Heroes;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// "Choose a secondary skill", <c>Dialog::LevelUpSelectSkill()</c> (<c>dialog.h:115</c>,
/// <c>dialog_levelup.cpp:213</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference returns the raw <c>Skill::Secondary::Skill()</c> value and uses <c>UNKNOWN</c> for "the player
/// closed the window"; the port answers with <see cref="SecondarySkill"/> and lets <see langword="null"/> mean the
/// same thing.
/// </para>
/// <para>
/// A level up offers two skills, but only one of them can be learned when the hero reaches the level cap of a
/// primary skill - the reference then falls back to <c>DialogOneSecondary</c> and shows a single card. That choice is
/// made from the two <see cref="SecondarySkill"/> values here, so the model does not need a third flag.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class LevelUpDialog
{
    private LevelUpDialog(string heroName, PrimarySkill primarySkill, SecondarySkill first, SecondarySkill second)
    {
        HeroName = heroName;
        PrimarySkill = primarySkill;
        First = first;
        Second = second;
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::LevelUpSelectSkill()</c> (<c>dialog_levelup.cpp:213</c>).
    /// </summary>
    /// <param name="heroName">Name of the hero that levels up, <c>name</c> of the reference.</param>
    /// <param name="primarySkill">
    /// The primary skill the hero gained a level in, <c>primarySkillType</c> of the reference; it is shown on the card
    /// the level up is for.
    /// </param>
    /// <param name="first">First offered skill, <c>sec1</c>.</param>
    /// <param name="second">Second offered skill, <c>sec2</c>; <see cref="SecondarySkill.Unknown"/> when only one is offered.</param>
    /// <returns>The model.</returns>
    public static LevelUpDialog Create(string heroName, PrimarySkill primarySkill, SecondarySkill first, SecondarySkill second) =>
        new(heroName, primarySkill, first, second);

    /// <summary>Name of the hero that levels up.</summary>
    public string HeroName { get; }

    /// <summary>The primary skill the level up belongs to.</summary>
    public PrimarySkill PrimarySkill { get; }

    /// <summary>First offered skill.</summary>
    public SecondarySkill First { get; }

    /// <summary>Second offered skill, <see cref="SecondarySkill.Unknown"/> when only one is offered.</summary>
    public SecondarySkill Second { get; }

    /// <summary>Whether only one skill is offered, the <c>DialogOneSecondary</c> branch of the reference.</summary>
    public bool IsSingleChoice => Second == SecondarySkill.Unknown;

    /// <summary>
    /// The result of the dialog: the chosen skill, or <see langword="null"/> when it was closed.
    /// </summary>
    /// <remarks>
    /// The reference answers <c>Skill::Secondary::UNKNOWN</c> on close (<c>dialog_levelup.cpp:209</c>), which is what
    /// <see langword="null"/> stands for here.
    /// </remarks>
    public SecondarySkill? Result { get; private set; }

    /// <summary>
    /// Records the choice, ignoring a skill the dialog did not offer.
    /// </summary>
    /// <param name="skill">The chosen skill, or <see langword="null"/> when the dialog was closed.</param>
    public void Choose(SecondarySkill? skill)
    {
        Result = skill is { } chosen && (chosen == First || chosen == Second) ? chosen : null;
    }
}
