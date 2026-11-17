namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The load and save file pickers, <c>Dialog::SelectFileLoad()</c> and <c>Dialog::SelectFileSave()</c>
/// (<c>dialog.h:89-90</c>, <c>dialog_selectfile.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The two functions share their whole body in the reference and differ only in what they list and in whether a name
/// has to be confirmed before overwriting, so the port models them as one class with a mode flag.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): the model carries the mode and the answer. The file list itself belongs to the
/// save-game directory, which has no port yet; when it lands it becomes part of <see cref="Create"/> without
/// changing the shape of the class.
/// </para>
/// </remarks>
public sealed class SelectFileDialog
{
    private SelectFileDialog(bool isSaveMode)
    {
        IsSaveMode = isSaveMode;
    }

    /// <summary>
    /// Builds the load picker, <c>Dialog::SelectFileLoad()</c>.
    /// </summary>
    /// <returns>The model.</returns>
    public static SelectFileDialog CreateLoad() => new(false);

    /// <summary>
    /// Builds the save picker, <c>Dialog::SelectFileSave()</c>.
    /// </summary>
    /// <returns>The model.</returns>
    public static SelectFileDialog CreateSave() => new(true);

    /// <summary>Whether the dialog saves instead of loading.</summary>
    public bool IsSaveMode { get; }

    /// <summary>
    /// The result of the dialog: the chosen file name, or <see langword="null"/> when it was cancelled.
    /// </summary>
    /// <remarks>
    /// The reference returns a <c>std::string</c> and lets the empty string mean "cancelled"; the port keeps
    /// <see langword="null"/> for that so that an empty file name cannot be mistaken for a choice.
    /// </remarks>
    public string? Result { get; private set; }

    /// <summary>
    /// Records the choice the caller made in the file list.
    /// </summary>
    /// <param name="fileName">The file name, or <see langword="null"/> when the dialog was cancelled.</param>
    public void Choose(string? fileName)
    {
        Result = string.IsNullOrEmpty(fileName) ? null : fileName;
    }
}
