using FHeroes2.Core.Geometry;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the spell selection window cannot do by itself: the popups the reference opens through
/// <c>showStandardTextMessage()</c> and <c>SpellDialogElement</c>.
/// </summary>
public enum EditorSpellSelectionRequestKind
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>Show a modal message.</summary>
    ShowMessage,

    /// <summary>Show the description of a spell, <c>SpellDialogElement::showPopup()</c> (<c>:155</c>).</summary>
    ShowSpell
}

/// <summary>
/// Something the host has to do for the window, or <see cref="EditorSpellSelectionRequestKind.None"/>.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Title">Title of the message, or the empty string for a spell.</param>
/// <param name="Body">Body of the message, or the empty string for a spell.</param>
/// <param name="Spell">The spell to describe; only meaningful for <see cref="EditorSpellSelectionRequestKind.ShowSpell"/>.</param>
public readonly record struct EditorSpellSelectionRequest(EditorSpellSelectionRequestKind Kind, string Title, string Body,
    SpellType Spell)
{
    /// <summary>Nothing to do.</summary>
    public static EditorSpellSelectionRequest None => new(EditorSpellSelectionRequestKind.None, string.Empty, string.Empty,
        default);

    /// <summary>Whether there is nothing to do.</summary>
    public bool IsNone => Kind == EditorSpellSelectionRequestKind.None;
}

/// <summary>
/// How the author left the window, the <c>bool</c> of <c>openEditorSpellSelectionWindow()</c> plus the level it hands back.
/// </summary>
public enum EditorSpellSelectionOutcome
{
    /// <summary>The window stays open.</summary>
    None,

    /// <summary>The author accepted; the caller reads <see cref="EditorSpellSelectionWindow.GetSelectedSpells"/>.</summary>
    Accepted,

    /// <summary>The author cancelled; the caller keeps whatever it had.</summary>
    Cancelled,

    /// <summary>
    /// The author asked for another level: the caller opens a new window for <see cref="EditorSpellSelectionWindow.Level"/>,
    /// which is what the reference's <c>return true</c> followed by a reopen amounts to (<c>:324-326</c>).
    /// </summary>
    LevelChanged
}

/// <summary>
/// The event handling of the spell selection window, <c>editor_spell_selection.cpp:278-416</c>.
/// </summary>
/// <remarks>
/// 参考的循环里 <c>drawOnState()</c> 与判断交叉在一起；端口把「按住的样子」交给
/// <see cref="EditorSpellSelectionWindow.Paint"/>（它需要 events 就是为了这个），这里只管状态变化。
/// 要弹窗的三处（等级说明、四个按钮的说明、法术说明）按 <see cref="EditorSpellSelectionRequest"/> 交回宿主。
/// </remarks>
public sealed class EditorSpellSelectionInput
{
    /// <summary>Title of the level buttons' message, <c>:307</c>.</summary>
    public const string SpellsLevelTitle = "Spells level";

    /// <summary>Body of the level buttons' message, <c>:305</c>; <c>%{level}</c> is the number of the button.</summary>
    public const string SpellsLevelMessage = "Click to show only level %{level} spells.";

    /// <summary>Title of the CANCEL button's message, <c>:333</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:336</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message.</summary>
    public const string OkayMessage = "Click to accept the changes made.";

    /// <summary>Title of the Enable All Spells button's message, <c>:339</c>.</summary>
    public const string EnableAllTitle = "Enable All Spells";

    /// <summary>Body of the Enable All Spells button's message.</summary>
    public const string EnableAllMessage = "Click to enable all spells.";

    /// <summary>Title of the Disable All Spells button's message, <c>:342</c>.</summary>
    public const string DisableAllTitle = "Disable All Spells";

    /// <summary>Body of the Disable All Spells button's message.</summary>
    public const string DisableAllMessage = "Click to disable all spells.";

    private readonly EditorSpellSelectionWindow _window;

    /// <summary>
    /// Wires the handling to a window.
    /// </summary>
    /// <param name="window">The window to drive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public EditorSpellSelectionInput(EditorSpellSelectionWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
    }

    /// <summary>The window being driven.</summary>
    public EditorSpellSelectionWindow Window => _window;

    /// <summary>What the host must do for this frame, <see cref="EditorSpellSelectionRequest.None"/> when nothing.</summary>
    public EditorSpellSelectionRequest Request { get; private set; } = EditorSpellSelectionRequest.None;

    /// <summary>The scroll under the cursor after the last <see cref="Process"/>.</summary>
    public int CursorSpell { get; private set; } = -1;

    /// <summary>
    /// Handles one frame of events, <c>:279-416</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>How the author left the window; <see cref="EditorSpellSelectionOutcome.None"/> while it stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorSpellSelectionOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Request = EditorSpellSelectionRequest.None;
        CursorSpell = _window.Grid.IndexAt(events.CursorPosition);

        if (events.MouseClickLeft(_window.Cancel.Area))
        {
            return EditorSpellSelectionOutcome.Cancelled;
        }

        if (_window.Okay.IsEnabled && events.MouseClickLeft(_window.Okay.Area))
        {
            return EditorSpellSelectionOutcome.Accepted;
        }

        if (_window.IsMultiLevelSelectionEnabled)
        {
            for (int i = 0; i < _window.LevelButtons.Count; ++i)
            {
                ButtonSprite button = _window.LevelButtons[i];

                if (events.IsMouseRightButtonPressedInArea(button.Area))
                {
                    Request = new EditorSpellSelectionRequest(
                        EditorSpellSelectionRequestKind.ShowMessage,
                        SpellsLevelTitle,
                        SpellsLevelMessage.Replace("%{level}", (i + 1).ToString(
                            System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
                        default);

                    return EditorSpellSelectionOutcome.None;
                }

                if (i + 1 == _window.Level || !button.IsEnabled)
                {
                    continue;
                }

                if (events.MouseClickLeft(button.Area))
                {
                    _window.ChangeLevel(i + 1);

                    return EditorSpellSelectionOutcome.LevelChanged;
                }
            }
        }

        if (events.IsMouseRightButtonPressedInArea(_window.Cancel.Area))
        {
            Request = new EditorSpellSelectionRequest(EditorSpellSelectionRequestKind.ShowMessage, CancelTitle, CancelMessage,
                default);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.Okay.Area))
        {
            Request = new EditorSpellSelectionRequest(EditorSpellSelectionRequestKind.ShowMessage, OkayTitle, OkayMessage,
                default);
        }
        else if (_window.ToggleAllOn.IsEnabled && events.IsMouseRightButtonPressedInArea(_window.ToggleAllOn.Area))
        {
            Request = new EditorSpellSelectionRequest(EditorSpellSelectionRequestKind.ShowMessage, EnableAllTitle,
                EnableAllMessage, default);
        }
        else if (_window.ToggleAllOff.IsEnabled && events.IsMouseRightButtonPressedInArea(_window.ToggleAllOff.Area))
        {
            Request = new EditorSpellSelectionRequest(EditorSpellSelectionRequestKind.ShowMessage, DisableAllTitle,
                DisableAllMessage, default);
        }
        else if (_window.ToggleAllOn.IsEnabled && events.MouseClickLeft(_window.ToggleAllOn.Area))
        {
            _window.EnableAllSpells();
        }
        else if (_window.ToggleAllOff.IsEnabled && events.MouseClickLeft(_window.ToggleAllOff.Area))
        {
            _window.DisableAllSpells();
        }
        else if (CursorSpell >= 0)
        {
            Rect area = _window.Grid.Areas[CursorSpell];

            if (events.MouseClickLeft(area))
            {
                _window.ToggleSpell(CursorSpell);
            }
            else if (events.IsMouseRightButtonPressedInArea(area))
            {
                Request = new EditorSpellSelectionRequest(
                    EditorSpellSelectionRequestKind.ShowSpell,
                    string.Empty,
                    string.Empty,
                    _window.Grid.Spells[CursorSpell]);
            }
        }

        return EditorSpellSelectionOutcome.None;
    }
}
