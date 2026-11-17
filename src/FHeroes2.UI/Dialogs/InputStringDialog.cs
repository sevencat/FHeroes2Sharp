namespace FHeroes2.UI.Dialogs;

/// <summary>
/// A one or multi line text input, <c>Dialog::inputString()</c> (<c>dialog.h:122</c>,
/// <c>dialog_selectcount.cpp:227</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference takes its title and its body as <c>fheroes2::TextBase</c>, i.e. already formatted text with a font
/// and, for multi line input, a language for the caret; the port has no <c>TextBase</c> yet and takes the two
/// strings instead. That is the one placeholder of this class - when the text widget lands, the two parameters become
/// that type and nothing else about the class changes.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class InputStringDialog
{
    private InputStringDialog(string title, string body, int characterLimit, bool isMultiLine)
    {
        Title = title;
        Body = body;
        CharacterLimit = characterLimit;
        IsMultiLine = isMultiLine;
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::inputString()</c> (<c>dialog_selectcount.cpp:227</c>).
    /// </summary>
    /// <param name="title">Title above the input, the <c>title</c> of the reference.</param>
    /// <param name="body">Text above the input, the <c>body</c> of the reference.</param>
    /// <param name="characterLimit">
    /// Maximum number of characters, <c>charLimit</c> of the reference; <c>0</c> means no limit, as it does there.
    /// </param>
    /// <param name="isMultiLine">Whether the input scrolls over several lines, <c>isMultiLine</c>.</param>
    /// <returns>The model.</returns>
    public static InputStringDialog Create(string title, string body, int characterLimit = 0, bool isMultiLine = false) =>
        new(title, body, Math.Max(0, characterLimit), isMultiLine);

    /// <summary>Translated title above the input.</summary>
    public string Title { get; }

    /// <summary>Translated text above the input.</summary>
    public string Body { get; }

    /// <summary>Maximum number of characters, <c>0</c> for no limit.</summary>
    public int CharacterLimit { get; }

    /// <summary>Whether the input is multi line.</summary>
    public bool IsMultiLine { get; }

    /// <summary>
    /// The result of the dialog: the text the player typed, or <see langword="null"/> when it was cancelled.
    /// </summary>
    /// <remarks>
    /// The reference answers with a <c>bool</c> and writes into a <c>std::string&amp;</c>; the port folds the two into
    /// one <see cref="string"/> that is <see langword="null"/> exactly when the reference would have returned
    /// <see langword="false"/>.
    /// </remarks>
    public string? Result { get; private set; }

    /// <summary>
    /// Records the text the dialog produced.
    /// </summary>
    /// <param name="text">The typed text, or <see langword="null"/> when the dialog was cancelled.</param>
    public void Choose(string? text)
    {
        Result = text;
    }
}
