using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The control of the castle details window the cursor is over, in the order the reference tests them
/// (<c>editor_castle_details_window.cpp:878-1049</c>).
/// </summary>
public enum CastleDetailsControl
{
    /// <summary>Nothing, or a spot of the window no control covers.</summary>
    None,

    /// <summary>The castle name bar.</summary>
    Name,

    /// <summary>The reset button next to the name bar.</summary>
    ResetName,

    /// <summary>The Allow Castle checkbox, only a town has one.</summary>
    AllowCastle,

    /// <summary>The Default Buildings checkbox.</summary>
    DefaultBuildings,

    /// <summary>The RESTRICT button.</summary>
    Restrict,

    /// <summary>The Default Army checkbox, only a neutral castle has one.</summary>
    DefaultArmy,

    /// <summary>The reset button of the army strip.</summary>
    ResetArmy,

    /// <summary>The army strip itself.</summary>
    Army,

    /// <summary>The SET SPELLS button.</summary>
    Spells,

    /// <summary>The OKAY button.</summary>
    Okay,

    /// <summary>The EXIT button.</summary>
    Exit,

    /// <summary>One of the nineteen building slots.</summary>
    Building
}

/// <summary>
/// What the window cannot do by itself: the things the reference opens a dialog for, or hands to the army bar.
/// </summary>
public enum CastleDetailsRequestKind
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>Ask the author for a new name, <c>Dialog::inputString()</c> (<c>:882-890</c>).</summary>
    EditName,

    /// <summary>Show a modal message, <c>showStandardTextMessage()</c>.</summary>
    ShowMessage,

    /// <summary>Open the mage guild window, <c>mageGuildSpellsDialog()</c> (<c>:872-874</c>).</summary>
    SetSpells,

    /// <summary>Let the army bar consume the event, <c>armyBar.QueueEventProcessing()</c> (<c>:992-1003</c>).</summary>
    Army
}

/// <summary>
/// Something the host has to do for the window, or <see cref="CastleDetailsRequestKind.None"/>.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Title">Title of the modal message, or the text the author is editing.</param>
/// <param name="Body">Body of the modal message; empty for a name-only message.</param>
public readonly record struct CastleDetailsRequest(CastleDetailsRequestKind Kind, string Title, string Body)
{
    /// <summary>Nothing to do.</summary>
    public static CastleDetailsRequest None => new(CastleDetailsRequestKind.None, string.Empty, string.Empty);

    /// <summary>Whether there is nothing to do.</summary>
    public bool IsNone => Kind == CastleDetailsRequestKind.None;
}

/// <summary>
/// How the author left the window, <c>castleDetailsDialog()</c>'s return value (<c>:851-866</c>, <c>:1061-1063</c>).
/// </summary>
public enum CastleDetailsOutcome
{
    /// <summary>The window stays open.</summary>
    None,

    /// <summary>The author accepted the changes; the caller writes them back.</summary>
    Accepted,

    /// <summary>The author left, discarding the changes of this session.</summary>
    Discarded
}

/// <summary>
/// The event handling of the castle details window, <c>:844-1059</c> minus the parts that only touch the display.
/// </summary>
/// <remarks>
/// <para>
/// 参考把「命中谁、说什么、点了做什么」全写在一个 <c>while</c> 里，中间夹着 <c>display.render()</c>。端口把纯状态的部分
/// 抽到这里，宿主每帧先 <see cref="CastleDetailsView.Paint"/> 再调 <see cref="Process"/>——命中判定读的是绘制时算出的区域
/// （<see cref="CastleDetailsView.AllowCastleArea"/> 那些），没画过就没有区域可测。
/// </para>
/// <para>
/// <b>要弹窗的四件事交回宿主</b>：改名字、显示提示、开法师公会、让军队条吃事件。端口没有模态对话框这一层
/// （<c>Dialog::inputString</c> 与 <c>showStandardTextMessage</c> 在 <c>FHeroes2.App</c> 里），所以这里只发
/// <see cref="CastleDetailsRequest"/>，由宿主去做，做完再回来问 <see cref="Outcome"/>。
/// </para>
/// <para>
/// <b>EXIT 的确认也是宿主的</b>：有改动时这里不直接收摊，而是把 <see cref="CastleDetailsRequestKind.ShowMessage"/>
/// 留给宿主，宿主弹完框再调 <see cref="ResolveUnsavedChanges"/> 告诉窗口作者选了哪边。
/// </para>
/// </remarks>
public sealed class CastleDetailsInput
{
    /// <summary>The line the status bar shows when nothing is under the cursor, <c>:1052-1053</c>.</summary>
    public const string IdleMessage = "Castle Settings";

    /// <summary>Hint of the name bar, <c>:879</c>.</summary>
    public const string NameHint = "Click to change the Castle name.";

    /// <summary>Hint of the reset name button, <c>:903</c>.</summary>
    public const string ResetNameHint = "Reset the Castle name.";

    /// <summary>Hint of the Allow Castle checkbox, <c>:917</c>.</summary>
    public const string AllowCastleHint = "Allow to build a castle in this town.";

    /// <summary>Hint of the Default Buildings checkbox, <c>:928</c>.</summary>
    public const string DefaultBuildingsHint =
        "Toggle the use of default buildings. Custom buildings will be reset!";

    /// <summary>Hint of the RESTRICT button, <c>:956</c>.</summary>
    public const string RestrictHint = "Toggle building construction restriction mode.";

    /// <summary>Hint of the Default Army checkbox, <c>:963</c>.</summary>
    public const string DefaultArmyHint = "Use default defenders army.";

    /// <summary>Hint of the reset army button, <c>:982</c>.</summary>
    public const string ResetArmyHint = "Reset the army.";

    /// <summary>Hint of the army strip, <c>:1000-1002</c>.</summary>
    public const string ArmyHint = "Set custom Castle Army.";

    /// <summary>Title of the reset buttons' message, <c>:913</c> and <c>:989</c>.</summary>
    public const string ResetTitle = "Reset";

    /// <summary>Title of the Allow Castle message, <c>:924</c>.</summary>
    public const string AllowCastleTitle = "Allow Castle build";

    /// <summary>Title of the Default Buildings message, <c>:952</c>.</summary>
    public const string DefaultBuildingsTitle = "Default Buildings";

    /// <summary>Title of the RESTRICT message, <c>:959</c>.</summary>
    public const string RestrictTitle = "Restrict Building Construction";

    /// <summary>Title of the Default Army message, <c>:978</c>.</summary>
    public const string DefaultArmyTitle = "Default Army";

    /// <summary>Hint of the SET SPELLS button, <c>:1005</c>.</summary>
    public const string SpellsHint = "Set spells to appear in the Mage Guild.";

    /// <summary>Title of the SET SPELLS message, <c>:1008</c>.</summary>
    public const string SpellsTitle = "Set Spells";

    /// <summary>Hint of the OKAY button, <c>:1012</c>.</summary>
    public const string OkayHint = "Click to accept the changes made.";

    /// <summary>Title of the OKAY message, <c>:1015</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Hint of the EXIT button, <c>:1019</c>.</summary>
    public const string ExitHint = "Exit this dialog, discarding the changes made.";

    /// <summary>Title of the EXIT message, <c>:1022</c>.</summary>
    public const string ExitTitle = "Exit";

    /// <summary>What the exit confirmation asks, <c>:862-863</c>.</summary>
    public const string UnsavedChangesMessage = "You have unsaved changes. Do you wish to proceed?";

    /// <summary>Header of the "what must be built first" block of a slot's description, <c>:186</c>.</summary>
    public const string RequiresTitle = "Requires:";

    private readonly CastleDetailsView _view;

    /// <summary>
    /// Wires the handling to a painted window.
    /// </summary>
    /// <param name="view">The window, painted at least once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
    public CastleDetailsInput(CastleDetailsView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
    }

    /// <summary>The window being driven.</summary>
    public CastleDetailsView View => _view;

    /// <summary>The state the window edits.</summary>
    public CastleDetailsState State => _view.State;

    /// <summary>The control under the cursor after the last <see cref="Process"/>.</summary>
    public CastleDetailsControl Cursor { get; private set; }

    /// <summary>The building slot under the cursor when <see cref="Cursor"/> is <see cref="CastleDetailsControl.Building"/>.</summary>
    public int CursorSlot { get; private set; } = -1;

    /// <summary>
    /// The line the status bar should show, already resolved: the hint of the control under the cursor, or
    /// <see cref="IdleMessage"/>.
    /// </summary>
    public string Message { get; private set; } = IdleMessage;

    /// <summary>What the host must do for this frame, <see cref="CastleDetailsRequest.None"/> when nothing.</summary>
    public CastleDetailsRequest Request { get; private set; } = CastleDetailsRequest.None;

    /// <summary>How the author left the window, if they did.</summary>
    public CastleDetailsOutcome Outcome { get; private set; }

    /// <summary>
    /// The six buttons are built by the first <see cref="CastleDetailsView.Paint"/>, so before that they have no
    /// area at all; an empty area is never under the cursor and is never clicked, which is what the reference's
    /// already-drawn buttons amount to before the first frame.
    /// </summary>
    private Rect OkayArea => _view.Frame.Okay?.Area ?? Rect.Empty;

    private Rect ExitArea => _view.Frame.Exit?.Area ?? Rect.Empty;

    private Rect RestrictArea => _view.Frame.Restrict?.Area ?? Rect.Empty;

    private Rect SpellsArea => _view.Frame.Spells?.Area ?? Rect.Empty;

    private Rect ResetNameArea => _view.Frame.ResetName?.Area ?? Rect.Empty;

    private Rect ResetArmyArea => _view.Frame.ResetArmy?.Area ?? Rect.Empty;

    /// <summary>
    /// Handles one frame of events, <c>:844-1059</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>How the author left the window; <see cref="CastleDetailsOutcome.None"/> while it stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public CastleDetailsOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Request = CastleDetailsRequest.None;
        Message = string.Empty;
        Cursor = CastleDetailsControl.None;
        CursorSlot = -1;

        // OKAY 和 EXIT 先判：参考的循环里它们排在所有控件之前，点下去这一帧的其余判断都不再走。
        if (events.MouseClickLeft(OkayArea))
        {
            return Accept();
        }

        if (events.MouseClickLeft(ExitArea))
        {
            return RequestDiscard();
        }

        if (events.MouseClickLeft(RestrictArea))
        {
            State.BuildingRestriction = !State.BuildingRestriction;
        }
        else if (events.MouseClickLeft(SpellsArea))
        {
            Request = new CastleDetailsRequest(CastleDetailsRequestKind.SetSpells, SpellsTitle, SpellsHint);
        }

        // 下面是「光标停在哪」，一串 else if，顺序就是参考的顺序。
        if (events.IsMouseCursorPosInArea(_view.NameArea))
        {
            Cursor = CastleDetailsControl.Name;
            Message = NameHint;

            if (events.MouseClickLeft(_view.NameArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.EditName, State.Metadata.CustomName,
                    NameHint);
            }
            else if (events.IsMouseRightButtonPressedInArea(_view.NameArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage,
                    CastleDetailsLabelsName(), string.Empty);
            }
        }
        else if (events.IsMouseCursorPosInArea(ResetNameArea))
        {
            Cursor = CastleDetailsControl.ResetName;
            Message = ResetNameHint;

            if (events.MouseClickLeft(ResetNameArea))
            {
                State.ResetName();
            }
            else if (events.IsMouseRightButtonPressedInArea(ResetNameArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, ResetTitle, Message);
            }
        }
        else if (State.IsTown && events.IsMouseCursorPosInArea(_view.AllowCastleArea))
        {
            Cursor = CastleDetailsControl.AllowCastle;
            Message = AllowCastleHint;

            if (events.MouseClickLeft(_view.AllowCastleArea))
            {
                State.AllowCastle = !State.AllowCastle;
            }
            else if (events.IsMouseRightButtonPressedInArea(_view.AllowCastleArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, AllowCastleTitle, Message);
            }
        }
        else if (events.IsMouseCursorPosInArea(_view.DefaultBuildingsArea))
        {
            Cursor = CastleDetailsControl.DefaultBuildings;
            Message = DefaultBuildingsHint;

            if (events.MouseClickLeft(_view.DefaultBuildingsArea))
            {
                State.ToggleDefaultBuildings();
            }
            else if (events.IsMouseRightButtonPressedInArea(_view.DefaultBuildingsArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, DefaultBuildingsTitle,
                    Message);
            }
        }
        else if (events.IsMouseCursorPosInArea(RestrictArea))
        {
            Cursor = CastleDetailsControl.Restrict;
            Message = RestrictHint;

            if (events.IsMouseRightButtonPressedInArea(RestrictArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, RestrictTitle, Message);
            }
        }
        else if (State.IsNeutral && events.IsMouseCursorPosInArea(_view.DefaultArmyArea))
        {
            Cursor = CastleDetailsControl.DefaultArmy;
            Message = DefaultArmyHint;

            if (events.MouseClickLeft(_view.DefaultArmyArea))
            {
                State.ToggleDefaultArmy();
            }
            else if (events.IsMouseRightButtonPressedInArea(_view.DefaultArmyArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, DefaultArmyTitle, Message);
            }
        }
        else if (events.IsMouseCursorPosInArea(ResetArmyArea))
        {
            Cursor = CastleDetailsControl.ResetArmy;
            Message = ResetArmyHint;

            if (events.MouseClickLeft(ResetArmyArea))
            {
                State.ResetArmy();
            }
            else if (events.IsMouseRightButtonPressedInArea(ResetArmyArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, ResetTitle, Message);
            }
        }
        else if (events.IsMouseCursorPosInArea(_view.ArmyBarArea))
        {
            Cursor = CastleDetailsControl.Army;
            Message = ArmyHint;
            Request = new CastleDetailsRequest(CastleDetailsRequestKind.Army, string.Empty, ArmyHint);
        }
        else if (events.IsMouseCursorPosInArea(SpellsArea))
        {
            Cursor = CastleDetailsControl.Spells;
            Message = SpellsHint;

            if (events.IsMouseRightButtonPressedInArea(SpellsArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, SpellsTitle, Message);
            }
        }
        else if (events.IsMouseCursorPosInArea(OkayArea))
        {
            Cursor = CastleDetailsControl.Okay;
            Message = OkayHint;

            if (events.IsMouseRightButtonPressedInArea(OkayArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, OkayTitle, Message);
            }
        }
        else if (events.IsMouseCursorPosInArea(ExitArea))
        {
            Cursor = CastleDetailsControl.Exit;
            Message = ExitHint;

            if (events.IsMouseRightButtonPressedInArea(ExitArea))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, ExitTitle, Message);
            }
        }
        else
        {
            ProcessBuilding(events);
        }

        if (Message.Length == 0)
        {
            Message = IdleMessage;
        }

        return Outcome;
    }

    /// <summary>
    /// The OKAY button pressed from outside the event loop, the <c>HotKeyEvent::DEFAULT_OKAY</c> half of <c>:851</c>.
    /// </summary>
    /// <remarks>
    /// 参考的 <c>le.MouseClickLeft( buttonOkay.area() ) || Game::HotKeyPressEvent( DEFAULT_OKAY )</c> 是同一个分支的开头，
    /// 热键由宿主送进来时没有鼠标坐标可判，所以把这一半单拎出来，语义与点 OKAY 完全相同。
    /// </remarks>
    /// <returns><see cref="CastleDetailsOutcome.Accepted"/>.</returns>
    public CastleDetailsOutcome Accept() => Outcome = CastleDetailsOutcome.Accepted;

    /// <summary>
    /// The EXIT path from outside the event loop, the <c>HotKeyEvent::DEFAULT_CANCEL</c> half of <c>:855</c>.
    /// </summary>
    /// <remarks>
    /// 参考这一支先 <c>updateMetadata()</c> 再比对新旧，端口把「有没有改动」交给 <see cref="CastleDetailsState.HasUnsavedChanges"/>
    /// （见 <c>TryDiscard</c>）；所以热键与点 EXIT 走的是同一条路，包括那句确认请求。
    /// </remarks>
    /// <returns><see cref="CastleDetailsOutcome.Discarded"/> when nothing changed, otherwise
    /// <see cref="CastleDetailsOutcome.None"/> with the confirmation left in <see cref="Request"/>.</returns>
    public CastleDetailsOutcome RequestDiscard() => TryDiscard();

    /// <summary>
    /// Answers the exit confirmation the window asked for, <c>:855-867</c>.
    /// </summary>
    /// <param name="discard">Whether the author chose to throw the changes away.</param>
    /// <returns><see cref="CastleDetailsOutcome.Discarded"/> when the window closes.</returns>
    public CastleDetailsOutcome ResolveUnsavedChanges(bool discard)
    {
        if (!discard)
        {
            return Outcome = CastleDetailsOutcome.None;
        }

        return Outcome = CastleDetailsOutcome.Discarded;
    }

    /// <summary>
    /// The EXIT button: closes right away when nothing changed, otherwise leaves the confirmation to the host.
    /// </summary>
    /// <returns><see cref="CastleDetailsOutcome.Discarded"/>, or <see cref="CastleDetailsOutcome.None"/> when the
    /// host still has to ask.</returns>
    private CastleDetailsOutcome TryDiscard()
    {
        if (!State.HasUnsavedChanges())
        {
            return Outcome = CastleDetailsOutcome.Discarded;
        }

        // 参考在这里同步弹框；端口把这步交回宿主，弹完再调 ResolveUnsavedChanges。
        Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage,
            CastleDetailsLabelsName(), UnsavedChangesMessage);

        return Outcome = CastleDetailsOutcome.None;
    }

    /// <summary>
    /// Handles the nineteen building slots, <c>:1025-1050</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    private void ProcessBuilding(LocalEvent events)
    {
        for (int i = 0; i < State.Cells.Count; ++i)
        {
            CastleBuildingCell cell = State.Cells[i];

            if (!events.IsMouseCursorPosInArea(cell.Area))
            {
                continue;
            }

            Cursor = CastleDetailsControl.Building;
            CursorSlot = i;
            Message = cell.RenderBuildingName;

            if (events.MouseClickLeft(cell.Area))
            {
                cell.Cycle(State.BuildingRestriction);

                if (!State.CustomBuildings)
                {
                    // 改了某一格，就不再是「默认建筑」了：参考把标记藏起来并整块重画。
                    State.CustomBuildings = true;
                }
            }
            else if (events.IsMouseRightButtonPressedInArea(cell.Area))
            {
                Request = new CastleDetailsRequest(CastleDetailsRequestKind.ShowMessage, cell.RenderBuildingName,
                    DescribeBuilding(cell));
            }

            return;
        }
    }

    /// <summary>
    /// The body of a slot's right button message: the description plus what must be built first,
    /// <c>:178-191</c>.
    /// </summary>
    /// <param name="cell">The slot whose drawn building is described.</param>
    /// <returns>The description, possibly with a "Requires:" block.</returns>
    private string DescribeBuilding(CastleBuildingCell cell)
    {
        string description = BuildingInfo.GetBuildingDescription(State.Race, cell.RenderBuilding);
        string requirement = CastleBuildingInfo.GetRequirementString(State.Race, cell.RenderBuilding);

        if (requirement.Length == 0)
        {
            return description;
        }

        return string.Concat(description, "\n\n", RequiresTitle, "\n", requirement);
    }

    /// <summary>The title the name messages use: the name the bar shows right now.</summary>
    /// <returns>The name, or the placeholder of a ruin.</returns>
    private string CastleDetailsLabelsName() =>
        CastleDetailsLabels.GetCastleName(State.Metadata.CustomName, State.IsTown);
}
