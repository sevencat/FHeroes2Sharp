using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The window that only describes a dwelling, <c>Dialog::DwellingInfo()</c> (<c>dialog.h:126</c>,
/// <c>dialog_recruit.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// It is the information half of <see cref="RecruitDialog"/>: the unit, its statistics and how many are available,
/// with no button that changes anything. A right click on a dwelling opens it.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs only, since the window has no answer; layout comes with the widget layer.
/// </para>
/// </remarks>
public sealed class DwellingInfoDialog
{
    private DwellingInfoDialog(MonsterType monster, uint available)
    {
        Monster = monster;
        Available = available;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::DwellingInfo( const Monster &amp;, const uint32_t available )</c>.
    /// </summary>
    /// <param name="monster">The unit the dwelling offers.</param>
    /// <param name="available">How many units are available.</param>
    /// <returns>The model.</returns>
    public static DwellingInfoDialog Create(MonsterType monster, uint available) => new(monster, available);

    /// <summary>The unit the dwelling offers.</summary>
    public MonsterType Monster { get; }

    /// <summary>How many units are available.</summary>
    public uint Available { get; }
}
