namespace FHeroes2.UI.Dialogs;

/// <summary>
/// One row of a settings dialog: a label plus the values it can take, of which one is selected.
/// </summary>
/// <remarks>
/// This is the port stand-in for an <c>OptionButtonGroup</c> row of the reference settings dialog:
/// instead of laying out a group of arrow buttons, the dialog draws the value with a chevron on
/// each side and lets the caller move the selection, so the row needs no extra artwork and the
/// selection logic is testable on its own. Moving past the last value wraps around to the first
/// one, which is what a repeated press on the right hand arrow of the original does for short
/// lists.
/// </remarks>
public sealed class SettingsOption
{
    private readonly string[] _values;

    /// <summary>Creates a row.</summary>
    /// <param name="label">Translated label shown on the left, e.g. <c>"Language"</c>.</param>
    /// <param name="values">Values the row can show, at least one.</param>
    /// <param name="selectedIndex">Index of the initially selected value.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="label"/> or <paramref name="values"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> is empty.</exception>
    public SettingsOption(string label, IReadOnlyList<string> values, int selectedIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            throw new ArgumentException("A settings option needs at least one value.", nameof(values));
        }

        _values = [.. values];
        Label = label;
        SelectedIndex = Math.Clamp(selectedIndex, 0, _values.Length - 1);
    }

    /// <summary>Translated label of the row.</summary>
    public string Label { get; }

    /// <summary>All values of the row, in display order.</summary>
    public IReadOnlyList<string> Values => _values;

    /// <summary>Index of the currently selected value.</summary>
    public int SelectedIndex { get; private set; }

    /// <summary>The currently selected value.</summary>
    public string Value => _values[SelectedIndex];

    /// <summary>Selects the value at <paramref name="index"/>, wrapping around.</summary>
    /// <param name="index">Index to select; it is taken modulo the number of values.</param>
    public void Select(int index) => SelectedIndex = Wrap(index);

    /// <summary>Moves the selection by <paramref name="delta"/> values, wrapping around.</summary>
    /// <param name="delta">Number of values to move; negative moves towards the first one.</param>
    public void Move(int delta) => SelectedIndex = Wrap(SelectedIndex + delta);

    private int Wrap(int index)
    {
        int count = _values.Length;
        return ((index % count) + count) % count;
    }
}
