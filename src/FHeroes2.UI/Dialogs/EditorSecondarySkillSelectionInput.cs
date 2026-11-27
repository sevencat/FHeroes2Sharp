using FHeroes2.Core.Geometry;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the secondary skill selection window cannot do by itself: the popups the reference opens through
/// <c>showStandardTextMessage()</c> and <c>SecondarySkillDialogElement</c>.
/// </summary>
public enum EditorSecondarySkillSelectionRequestKind
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>Show a modal message.</summary>
    ShowMessage,

    /// <summary>
    /// Show the description of a skill, <c>SecondarySkillDialogElement::showPopup()</c> (<c>:181</c>).
    /// </summary>
    ShowSkill
}

/// <summary>
/// Something the host has to do for the window, or <see cref="EditorSecondarySkillSelectionRequestKind.None"/>.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Title">Title of the message, or the empty string for a skill.</param>
/// <param name="Body">Body of the message, or the empty string for a skill.</param>
/// <param name="Skill">
/// The skill to describe; only meaningful for <see cref="EditorSecondarySkillSelectionRequestKind.ShowSkill"/>.
/// </param>
public readonly record struct EditorSecondarySkillSelectionRequest(EditorSecondarySkillSelectionRequestKind Kind,
    string Title, string Body, SecondarySkillLevel Skill)
{
    /// <summary>Nothing to do.</summary>
    public static EditorSecondarySkillSelectionRequest None => new(EditorSecondarySkillSelectionRequestKind.None,
        string.Empty, string.Empty, default);

    /// <summary>Whether there is nothing to do.</summary>
    public bool IsNone => Kind == EditorSecondarySkillSelectionRequestKind.None;
}

/// <summary>
/// How the author left the window, the <c>bool</c> of <c>openSecondarySkillSelectionWindow()</c>.
/// </summary>
public enum EditorSecondarySkillSelectionOutcome
{
    /// <summary>The window stays open.</summary>
    None,

    /// <summary>
    /// The author accepted; the caller reads <see cref="EditorSecondarySkillSelectionWindow.GetSelectedSkills"/>.
    /// </summary>
    Accepted,

    /// <summary>The author cancelled; the caller keeps whatever it had.</summary>
    Cancelled
}

/// <summary>
/// The event handling of the secondary skill selection window,
/// <c>editor_secondary_skill_selection.cpp:273-377</c>.
/// </summary>
/// <remarks>
/// 参考的循环里 <c>drawOnState()</c> 与判断交叉在一起；端口把「按住的样子」交给
/// <see cref="EditorSecondarySkillSelectionWindow.Paint"/>（它需要 events 就是为了这个），这里只管状态变化。
/// 要弹窗的五处（四个按钮的说明、技能说明）按 <see cref="EditorSecondarySkillSelectionRequest"/> 交回宿主。
/// 参考 :288/:292 自己处理 CANCEL/OKAY 热键，端口和法术窗一样把热键留给宿主。
/// </remarks>
public sealed class EditorSecondarySkillSelectionInput
{
    /// <summary>Title of the CANCEL button's message, <c>:300</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:303</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message.</summary>
    public const string OkayMessage = "Click to accept the changes made.";

    /// <summary>Title of the Enable All Secondary Skills button's message, <c>:306</c>.</summary>
    public const string EnableAllTitle = "Enable All Secondary Skills";

    /// <summary>Body of the Enable All Secondary Skills button's message.</summary>
    public const string EnableAllMessage = "Click to enable all secondary skills.";

    /// <summary>Title of the Disable All Secondary Skills button's message, <c>:309</c>.</summary>
    public const string DisableAllTitle = "Disable All Secondary Skills";

    /// <summary>Body of the Disable All Secondary Skills button's message.</summary>
    public const string DisableAllMessage = "Click to disable all secondary skills.";

    private readonly EditorSecondarySkillSelectionWindow _window;

    /// <summary>
    /// Wires the handling to a window.
    /// </summary>
    /// <param name="window">The window to drive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public EditorSecondarySkillSelectionInput(EditorSecondarySkillSelectionWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
    }

    /// <summary>The window being driven.</summary>
    public EditorSecondarySkillSelectionWindow Window => _window;

    /// <summary>What the host must do for this frame, <see cref="EditorSecondarySkillSelectionRequest.None"/> when nothing.</summary>
    public EditorSecondarySkillSelectionRequest Request { get; private set; } =
        EditorSecondarySkillSelectionRequest.None;

    /// <summary>The cell under the cursor after the last <see cref="Process"/>.</summary>
    public int CursorSkill { get; private set; } = -1;

    /// <summary>
    /// Handles one frame of events, <c>:273-377</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>How the author left the window; <see cref="EditorSecondarySkillSelectionOutcome.None"/> while it stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorSecondarySkillSelectionOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Request = EditorSecondarySkillSelectionRequest.None;
        CursorSkill = _window.Grid.IndexAt(events.CursorPosition);

        if (events.MouseClickLeft(_window.Cancel.Area))
        {
            return EditorSecondarySkillSelectionOutcome.Cancelled;
        }

        if (_window.Okay.IsEnabled && events.MouseClickLeft(_window.Okay.Area))
        {
            return EditorSecondarySkillSelectionOutcome.Accepted;
        }

        if (events.IsMouseRightButtonPressedInArea(_window.Cancel.Area))
        {
            Request = new EditorSecondarySkillSelectionRequest(EditorSecondarySkillSelectionRequestKind.ShowMessage,
                CancelTitle, CancelMessage, default);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.Okay.Area))
        {
            Request = new EditorSecondarySkillSelectionRequest(EditorSecondarySkillSelectionRequestKind.ShowMessage,
                OkayTitle, OkayMessage, default);
        }
        else if (_window.ToggleAllOn.IsEnabled && events.IsMouseRightButtonPressedInArea(_window.ToggleAllOn.Area))
        {
            Request = new EditorSecondarySkillSelectionRequest(EditorSecondarySkillSelectionRequestKind.ShowMessage,
                EnableAllTitle, EnableAllMessage, default);
        }
        else if (_window.ToggleAllOff.IsEnabled && events.IsMouseRightButtonPressedInArea(_window.ToggleAllOff.Area))
        {
            Request = new EditorSecondarySkillSelectionRequest(EditorSecondarySkillSelectionRequestKind.ShowMessage,
                DisableAllTitle, DisableAllMessage, default);
        }
        else if (_window.ToggleAllOn.IsEnabled && events.MouseClickLeft(_window.ToggleAllOn.Area))
        {
            _window.EnableAllSkills();
        }
        else if (_window.ToggleAllOff.IsEnabled && events.MouseClickLeft(_window.ToggleAllOff.Area))
        {
            _window.DisableAllSkills();
        }
        else if (CursorSkill >= 0)
        {
            Rect area = _window.Grid.Areas[CursorSkill];

            if (events.MouseClickLeft(area))
            {
                _window.ToggleSkill(CursorSkill);
            }
            else if (events.IsMouseRightButtonPressedInArea(area))
            {
                Request = new EditorSecondarySkillSelectionRequest(
                    EditorSecondarySkillSelectionRequestKind.ShowSkill,
                    string.Empty,
                    string.Empty,
                    _window.Grid.Skills[CursorSkill]);
            }
        }

        return EditorSecondarySkillSelectionOutcome.None;
    }
}
