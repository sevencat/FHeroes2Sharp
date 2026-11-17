namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The thieves guild report, <c>Dialog::ThievesGuild()</c> (<c>dialog.h:134</c>, <c>dialog_thievesguild.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window lists the kingdoms of the map on one page and one of them in detail on the next, and it has no answer:
/// the reference returns <see langword="void"/>. Its only input is whether it is the oracle version, which has four
/// guards instead of one and can be opened anywhere.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs only; layout comes with the widget layer.
/// </para>
/// </remarks>
public sealed class ThievesGuildDialog
{
    private ThievesGuildDialog(bool isOracle)
    {
        IsOracle = isOracle;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::ThievesGuild( bool oracle )</c>.
    /// </summary>
    /// <param name="isOracle">
    /// Whether the window is the oracle one, <c>oracle</c> of the reference: it shows more and does not need a
    /// thieves guild in a castle.
    /// </param>
    /// <returns>The model.</returns>
    public static ThievesGuildDialog Create(bool isOracle) => new(isOracle);

    /// <summary>Whether the window is the oracle version.</summary>
    public bool IsOracle { get; }
}
