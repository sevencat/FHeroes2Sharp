namespace FHeroes2.UI.Dialogs;

/// <summary>
/// A number with plus and minus buttons, <c>Dialog::SelectCount()</c> (<c>dialog.h:118</c>,
/// <c>dialog_selectcount.cpp:79</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference answers with a <c>bool</c> and writes the number into an out parameter, and it can show two
/// caller-supplied widgets above and below the number. Neither of them is modelled: the widgets do not exist in the
/// port, and the number is what every caller of <c>SelectCount</c> actually uses.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class SelectCountDialog
{
    private SelectCountDialog(string header, int minimum, int maximum, int step)
    {
        Header = header;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::SelectCount()</c> (<c>dialog_selectcount.cpp:79</c>).
    /// </summary>
    /// <param name="header">Translated title of the dialog.</param>
    /// <param name="minimum">Smallest selectable number, <c>min</c> of the reference.</param>
    /// <param name="maximum">Largest selectable number, <c>max</c> of the reference.</param>
    /// <param name="step">Amount one press changes the number by, <c>step</c> of the reference.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimum"/> is greater than <paramref name="maximum"/>, or <paramref name="step"/> is not
    /// positive. The reference asserts the first case only; a zero or negative step would loop forever there, so the
    /// port refuses it up front.
    /// </exception>
    public static SelectCountDialog Create(string header, int minimum, int maximum, int step = 1)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);

        return new SelectCountDialog(header, minimum, maximum, step);
    }

    /// <summary>Translated title of the dialog.</summary>
    public string Header { get; }

    /// <summary>Smallest selectable number.</summary>
    public int Minimum { get; }

    /// <summary>Largest selectable number.</summary>
    public int Maximum { get; }

    /// <summary>Amount one press changes the number by.</summary>
    public int Step { get; }

    /// <summary>
    /// The result of the dialog: the chosen number, or <see langword="null"/> when it was cancelled.
    /// </summary>
    public int? Result { get; private set; }

    /// <summary>
    /// Records the chosen number, clamped into <see cref="Minimum"/>..<see cref="Maximum"/>.
    /// </summary>
    /// <remarks>
    /// The reference clamps while the buttons are pressed and returns the value as it stands when OK is pressed, so
    /// an out of range result cannot happen there either; clamping here keeps that promise for the callers.
    /// </remarks>
    /// <param name="value">The number the player chose, or <see langword="null"/> when cancelled.</param>
    public void Choose(int? value)
    {
        Result = value is { } chosen ? Math.Clamp(chosen, Minimum, Maximum) : null;
    }
}
