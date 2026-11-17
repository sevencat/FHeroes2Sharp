using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The recruitment window of a dwelling, <c>Dialog::RecruitMonster()</c> (<c>dialog.h:125</c>,
/// <c>dialog_recruit.cpp:253</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference answers with a <c>Troop</c> and lets the default-constructed one mean "nothing was recruited". The
/// port returns <see langword="null"/> for that instead of an empty stack, so a caller cannot read the empty troop as
/// "a stack of zero units was chosen".
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class RecruitDialog
{
    private RecruitDialog(MonsterType monster, uint available, bool allowDowngradedMonster)
    {
        Monster = monster;
        Available = available;
        AllowDowngradedMonster = allowDowngradedMonster;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::RecruitMonster()</c> (<c>dialog_recruit.cpp:253</c>).
    /// </summary>
    /// <param name="monster">The unit the dwelling offers, <c>monster0</c> of the reference.</param>
    /// <param name="available">How many units are available, <c>available</c>.</param>
    /// <param name="allowDowngradedMonster">
    /// Whether the player may also hire the previous tier of the dwelling, <c>allowDowngradedMonster</c>.
    /// </param>
    /// <returns>The model.</returns>
    public static RecruitDialog Create(MonsterType monster, uint available, bool allowDowngradedMonster) =>
        new(monster, available, allowDowngradedMonster);

    /// <summary>The unit the dwelling offers.</summary>
    public MonsterType Monster { get; }

    /// <summary>How many units are available.</summary>
    public uint Available { get; }

    /// <summary>Whether the previous tier can be hired as well.</summary>
    public bool AllowDowngradedMonster { get; }

    /// <summary>
    /// The result of the dialog: the hired stack, or <see langword="null"/> when nothing was recruited.
    /// </summary>
    public Troop? Result { get; private set; }

    /// <summary>
    /// Records the stack that was recruited.
    /// </summary>
    /// <param name="troop">The hired stack, or <see langword="null"/> when the dialog was cancelled.</param>
    public void Choose(Troop? troop)
    {
        Result = troop is { Count: > 0 } ? troop : null;
    }
}
