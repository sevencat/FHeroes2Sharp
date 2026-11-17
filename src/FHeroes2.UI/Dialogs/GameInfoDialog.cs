namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The scenario information, <c>Dialog::GameInfo()</c> (<c>dialog.h:135</c>, <c>dialog_gameinfo.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference takes no argument at all: it reads the kingdoms, the victory conditions and the map name from the
/// global <c>world</c>. The port has no global world, so the class is an empty model for now and the world will be
/// passed to whatever renders it - <see cref="Create"/> does not need to change for that, because the reference call
/// site has nothing to pass either.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): no inputs, no answer, no layout yet.
/// </para>
/// </remarks>
public sealed class GameInfoDialog
{
    private GameInfoDialog()
    {
    }

    /// <summary>
    /// Builds the window, <c>Dialog::GameInfo()</c> <c>dialog.h:135</c>.
    /// </summary>
    /// <returns>The model.</returns>
    public static GameInfoDialog Create() => new();
}
