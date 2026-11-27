using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// What the map specifications window cannot do by itself: the text input dialogs it opens and the
/// messages it shows, the reference's <c>Dialog::inputString()</c> and <c>showStandardTextMessage()</c>.
/// </summary>
public enum EditorMapSpecsRequestKind
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>Show a modal message.</summary>
    ShowMessage,

    /// <summary>Edit the map name, <c>:2318</c>.</summary>
    EditMapName,

    /// <summary>Edit the map description, <c>:2329</c>.</summary>
    EditMapDescription,

    /// <summary>Pick a town or a hero on the map for the open condition, <c>:1252-1264</c>.</summary>
    PickConditionTarget,

    /// <summary>
    /// Type a number into one of the two number elements, the number pad the element opens on a click of its edit
    /// box (<c>ui_dialog.cpp:1007</c>); the host answers it through <see cref="EditorMapSpecsInput.ResolveValue"/>.
    /// </summary>
    EditValue,

    /// <summary>
    /// Edit the rumors of the map, <c>openRumorWindow</c> (<c>:2282-2289</c>); the host answers it through
    /// <see cref="EditorMapSpecsInput.ResolveRumors"/>.
    /// </summary>
    OpenRumors,

    /// <summary>
    /// Edit the daily events, <c>openDailyEventsWindow</c> (<c>:2290-2297</c>); the host answers it through
    /// <see cref="EditorMapSpecsInput.ResolveDailyEvents"/>.
    /// </summary>
    OpenDailyEvents,

    /// <summary>
    /// Edit the languages of the map, <c>openLanguageWindow</c> (<c>:2298-2311</c>); the host answers it through
    /// <see cref="EditorMapSpecsInput.ResolveLanguage"/>.
    /// </summary>
    OpenLanguage,

    /// <summary>
    /// Edit the notes of the map maker, <c>:2370-2377</c>; the host answers it through
    /// <see cref="EditorMapSpecsInput.Resolve"/> like the name and the description.
    /// </summary>
    EditCreatorNotes
}

/// <summary>Which of the two number elements of the map specifications window a request is about.</summary>
public enum EditorMapSpecsValueField
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>The accumulated gold of the victory condition, <c>_goldAccumulationValue</c>, <c>:1409</c>.</summary>
    VictoryGold,

    /// <summary>The days of the "lose after a time" loss condition, <c>_outOfTimeValue</c>, <c>:1854</c>.</summary>
    LossDays
}

/// <summary>
/// Which target of a condition panel the host has to pick, the candidate list windows the reference opens at
/// <c>editor_map_specs_window.cpp:1230-1264</c> (town of the victory condition), <c>:1289-1318</c> (hero to defeat) and
/// the two loss conditions (<c>:1770-1803</c>, <c>:1822-1855</c>).
/// </summary>
public enum EditorMapSpecsTargetKind
{
    /// <summary>Nothing to pick.</summary>
    None,

    /// <summary>The town of <see cref="MapVictoryCondition.CaptureTown"/>, <c>_townToCapture</c>.</summary>
    VictoryTown,

    /// <summary>The hero of <see cref="MapVictoryCondition.KillHero"/>, <c>_heroToDefeat</c>.</summary>
    VictoryHero,

    /// <summary>The town of <see cref="MapLossCondition.LoseTown"/>, <c>_townToLose</c>.</summary>
    LossTown,

    /// <summary>The hero of <see cref="MapLossCondition.LoseHero"/>, <c>_heroToLose</c>.</summary>
    LossHero
}

/// <summary>
/// Something the host has to do for the window, or <see cref="EditorMapSpecsRequestKind.None"/>.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Title">Title of the message, or the empty string for the two text inputs, as in the reference.</param>
/// <param name="Body">Body of the message, or the body of the text input.</param>
/// <param name="Dialog">
/// The text input to run, or <see langword="null"/> for a message; the host answers it through
/// <see cref="EditorMapSpecsInput.Resolve"/>.
/// </param>
/// <param name="Target">
/// What to pick on the map when <see cref="Kind"/> is <see cref="EditorMapSpecsRequestKind.PickConditionTarget"/>;
/// the host answers it through <see cref="EditorMapSpecsInput.ResolveTarget"/>.
/// </param>
/// <param name="ValueField">
/// Which number element the number pad belongs to when <see cref="Kind"/> is
/// <see cref="EditorMapSpecsRequestKind.EditValue"/>.
/// </param>
public readonly record struct EditorMapSpecsRequest(EditorMapSpecsRequestKind Kind, string Title, string Body,
    InputStringDialog? Dialog, EditorMapSpecsTargetKind Target = EditorMapSpecsTargetKind.None,
    EditorMapSpecsValueField ValueField = EditorMapSpecsValueField.None)
{
    /// <summary>Nothing to do.</summary>
    public static EditorMapSpecsRequest None => new(EditorMapSpecsRequestKind.None, string.Empty, string.Empty, null);

    /// <summary>Whether there is nothing to do.</summary>
    public bool IsNone => Kind == EditorMapSpecsRequestKind.None;
}

/// <summary>
/// How the author left the window, the <c>true</c> / <c>false</c> the reference's dialog returns.
/// </summary>
public enum EditorMapSpecsOutcome
{
    /// <summary>The window stays open.</summary>
    None,

    /// <summary>The author accepted; the caller writes <see cref="MapSpecsState.CreateInfo"/> back.</summary>
    Accepted,

    /// <summary>The author cancelled; the caller keeps whatever it had.</summary>
    Cancelled
}

/// <summary>
/// The event handling of the map specifications window, the loop body of
/// <c>mapSpecificationsDialog()</c> (<c>editor_map_specs_window.cpp:2256-2516</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把「按住的样子」和判断写在一起（<c>:2257-2264</c> 的八次 <c>drawOnState</c> 紧跟 <c>:2266-2509</c> 的判断）。
/// 端口沿用 <see cref="EditorSpellSelectionInput"/> 的做法：按住的样子交给
/// <see cref="EditorMapSpecsWindow.Paint"/>（它收本帧事件就是为了这个），这里只改状态与产出请求。
/// </para>
/// <para>
/// <b>本类覆盖哪几段。</b><c>:2266-2272</c>（取消 / 确定）、<c>:2312-2335</c>（名称与描述输入）、
/// <c>:2336-2368</c>（两个条件下拉框）、<c>:2378-2415</c>（取消、确定、名称、描述与两个下拉按钮的右键提示）、
/// <c>:2417-2509</c>（玩家图标三态与难度四格）、<c>:2512-2516</c>（退出时写回）。条件面板事件
/// （<c>:2274-2281</c>）留给 <c>a7l.3.5.3.2.3</c>，四个子窗按钮（<c>:2282-2311</c>、<c>:2370-2377</c>）
/// 连同它们的右键提示留给 <c>a7l.3.5.3.3</c>。
/// </para>
/// <para>
/// <b>下拉列表。</b>参考在循环里再套一层列表循环（<c>showWinLoseList()</c>），端口不阻塞：点开时由
/// <see cref="EditorMapSpecsWindow.OpenConditionList"/> 建好 <see cref="EditorMapSpecsConditionPopup"/>，
/// 之后每帧的事件先在 <see cref="Process"/> 开头全部交给它，关掉时按它给的条件写回（右键说明也由它出）。
/// </para>
/// <para>
/// <b>热键。</b>参考写 <c>Game::HotKeyPressEvent( DEFAULT_CANCEL / DEFAULT_OKAY )</c>，端口只认 Escape 与 Enter：
/// 键位表（<c>Key::Escape</c>、<c>Key::Enter</c>）还没接进来，而这两键正是参考默认绑定的那两个。
/// </para>
/// <para>
/// <b>文本输入。</b>参考在循环里阻塞地跑 <c>Dialog::inputString()</c>，端口把输入框作为
/// <see cref="EditorMapSpecsRequest"/> 交回宿主（<see cref="InputStringDialog"/> 目前只有契约），宿主跑完
/// 用 <see cref="Resolve"/> 把文本送回来。名称上限取 MP2 头里的 16 字节字段，描述取 200 字节字段；
/// 参考的名字上限来自编辑器参数，描述用 <c>Map_Format::messageCharLimit</c>，MP2 的字段比它们短，所以按字段来。
/// </para>
/// </remarks>
public sealed class EditorMapSpecsInput
{
    /// <summary>Title of the CANCEL button's message, <c>:2379</c>.</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message.</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message, <c>:2382</c>.</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message.</summary>
    public const string OkayMessage = "Click to accept the changes made.";

    /// <summary>Title of the map name's message, <c>:2405</c>.</summary>
    public const string MapNameTitle = "Map Name";

    /// <summary>
    /// Body of the map name's message, <c>:2399-2403</c>: the name between quotes, an empty line, then the hint.
    /// <c>%{name}</c> is replaced with the current name.
    /// </summary>
    public const string MapNameMessage = "\"%{name}\"\n\nClick to change your map name.";

    /// <summary>Title of the map description's message, <c>:2408</c>.</summary>
    public const string MapDescriptionTitle = "Map Description";

    /// <summary>Body of the map description's message.</summary>
    public const string MapDescriptionMessage = "Click to change the description of the current map.";

    /// <summary>Title of the player icons' message, <c>:2479</c>.</summary>
    public const string PlayerTypeTitle = "Player Type";

    /// <summary>Body of the player icons' message.</summary>
    public const string PlayerTypeMessage = "Indicates the player types in the scenario. Click to change.";

    /// <summary>Title of the difficulty icons' message, <c>:2505</c>.</summary>
    public const string MapDifficultyTitle = "Map Difficulty";

    /// <summary>Body of the difficulty icons' message.</summary>
    public const string MapDifficultyMessage =
        "Click to set map difficulty. More difficult maps might include more or stronger enemies, fewer resources, " +
        "or other special conditions making things tougher for the human player.";

    /// <summary>Body of the victory condition button's hint, <c>:2411</c>.</summary>
    public const string VictoryConditionMessage = "Click to change the victory condition of the current map.";

    /// <summary>Body of the loss condition button's hint, <c>:2414</c>.</summary>
    public const string LossConditionMessage = "Click to change the loss condition of the current map.";

    /// <summary>Title of the hints inside the victory panel, <c>:1326</c>.</summary>
    public const string SpecialVictoryTitle = "Special Victory Condition";

    /// <summary>Body of the capture-town row hint, <c>:1258</c>.</summary>
    public const string CaptureTownVictoryMessage =
        "Click here to change the town needed to capture to achieve victory.";

    /// <summary>Body of the defeat-hero row hint, <c>:1326</c>.</summary>
    public const string KillHeroVictoryMessage = "Click here to change the hero needed to defeat to achieve victory.";

    /// <summary>Title of the hints inside the loss panel, <c>:1793</c>.</summary>
    public const string SpecialLossTitle = "Special Loss Condition";

    /// <summary>Body of the lose-town row hint, <c>:1793</c>.</summary>
    public const string LoseTownLossMessage = "Click here to change the town whose loss would mean defeat.";

    /// <summary>Body of the lose-hero row hint, <c>:1847</c>.</summary>
    public const string LoseHeroLossMessage = "Click here to change the hero whose loss would mean defeat.";

    /// <summary>
    /// Header of an alliance checkbox hint, <c>:1390</c>; <c>%{color}</c> is the colour name and <c>%{alliance}</c> the
    /// order of the alliance.
    /// </summary>
    public const string AllianceCheckboxTitle = "Put %{color} player in the %{alliance} alliance";

    /// <summary>
    /// Body of an alliance checkbox hint, <c>:1391</c>; same placeholders as
    /// <see cref="AllianceCheckboxTitle"/>.
    /// </summary>
    public const string AllianceCheckboxMessage =
        "If this checkbox is checked, the %{color} player will be in the %{alliance} alliance.";

    /// <summary>Order of the first alliance in <see cref="AllianceCheckboxTitle"/>, <c>getAllianceOrderText( 0 )</c>.</summary>
    public const string FirstAllianceOrder = "1st";

    /// <summary>Order of the second alliance in <see cref="AllianceCheckboxTitle"/>, <c>getAllianceOrderText( 1 )</c>.</summary>
    public const string SecondAllianceOrder = "2nd";

    /// <summary>Body of the map name input, <c>:2317</c>.</summary>
    public const string ChangeMapNameBody = "Change Map Name:";

    /// <summary>Body of the map description input, <c>:2328</c>.</summary>
    public const string ChangeMapDescriptionBody = "Change Map Description:";

    /// <summary>Title of the RUMORS button's message, <c>:2385</c>.</summary>
    public const string RumorsTitle = "Rumors";

    /// <summary>Body of the RUMORS button's message, <c>:2385</c>.</summary>
    public const string RumorsMessage = "Click to edit custom rumors.";

    /// <summary>Title of the EVENTS button's message, <c>:2388</c>.</summary>
    public const string EventsTitle = "Events";

    /// <summary>Body of the EVENTS button's message, <c>:2388</c>.</summary>
    public const string EventsMessage = "Click to edit daily events.";

    /// <summary>Title of the LANGUAGE button's message, <c>:2391</c>.</summary>
    public const string LanguageTitle = "Language";

    /// <summary>Body of the LANGUAGE button's message, <c>:2391</c>.</summary>
    public const string LanguageMessage = "Click to change the language of the map.";

    /// <summary>Title of the ABOUT button's message, <c>:2394</c> (<c>_( "map|About" )</c>).</summary>
    public const string AboutTitle = "About";

    /// <summary>Body of the ABOUT button's message, <c>:2394-2396</c>.</summary>
    public const string AboutMessage =
        "Click to edit notes from the map creator. These notes are optional and do not appear during gameplay.";

    /// <summary>Body of the creator notes input, <c>_( "map|About" ) + ':'</c> (<c>:2373</c>).</summary>
    public const string ChangeCreatorNotesBody = "About:";

    /// <summary>
    /// Longest text the multi-line inputs accept, <c>Map_Format::messageCharLimit</c>
    /// (<c>map_format_info.h:59</c>).
    /// </summary>
    public const int MessageCharLimit = 999;

    private readonly EditorMapSpecsWindow _window;

    private EditorMapSpecsRequestKind _pendingEdit = EditorMapSpecsRequestKind.None;
    private EditorMapSpecsTargetKind _pendingTarget = EditorMapSpecsTargetKind.None;
    private EditorMapSpecsValueField _pendingValue = EditorMapSpecsValueField.None;

    /// <summary>
    /// Wires the handling to a window.
    /// </summary>
    /// <param name="window">The window to drive.</param>
    /// <param name="maxMapNameLength">
    /// Longest name the name input accepts, the <c>maxMapNameLength</c> the reference is given; the MP2 header
    /// keeps <see cref="Mp2FileInfo.NameFieldLength"/> characters, so that is the default.
    /// </param>
    /// <param name="maxDescriptionLength">Longest description the description input accepts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is negative.</exception>
    public EditorMapSpecsInput(EditorMapSpecsWindow window, int maxMapNameLength = Mp2FileInfo.NameFieldLength,
        int maxDescriptionLength = Mp2FileInfo.DescriptionFieldLength)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentOutOfRangeException.ThrowIfNegative(maxMapNameLength);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDescriptionLength);

        _window = window;
        MaxMapNameLength = maxMapNameLength;
        MaxDescriptionLength = maxDescriptionLength;
    }

    /// <summary>The window being driven.</summary>
    public EditorMapSpecsWindow Window => _window;

    /// <summary>Longest name the name input accepts.</summary>
    public int MaxMapNameLength { get; }

    /// <summary>Longest description the description input accepts.</summary>
    public int MaxDescriptionLength { get; }

    /// <summary>What the host must do for this frame, <see cref="EditorMapSpecsRequest.None"/> when nothing.</summary>
    public EditorMapSpecsRequest Request { get; private set; } = EditorMapSpecsRequest.None;

    /// <summary>
    /// Handles one frame of events, <c>:2256-2509</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>Whether the author cancelled or accepted; <see cref="EditorMapSpecsOutcome.None"/> while the window stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorMapSpecsOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Request = EditorMapSpecsRequest.None;

        // :2382-2389 —— 列表打开时事件全归它（参考是嵌套循环），点中才写回条件。
        if (_window.ConditionPopup is { } popup)
        {
            popup.Process(events);

            if (popup.Message is { } message)
            {
                Request = ShowMessage(popup.MessageTitle, message);
                popup.ClearMessage();
            }

            if (popup.IsClosed)
            {
                ApplyConditionChoice(popup);
            }

            return EditorMapSpecsOutcome.None;
        }

        // :2266-2268 —— 取消优先，参考用 else-if 链保证同帧只认一个。
        if (events.IsKeyPressed(Key.Escape) || events.MouseClickLeft(_window.Cancel.Area))
        {
            return EditorMapSpecsOutcome.Cancelled;
        }

        // :2270-2272。
        if (events.IsKeyPressed(Key.Enter) || events.MouseClickLeft(_window.Okay.Area))
        {
            return EditorMapSpecsOutcome.Accepted;
        }

        // :2274-2281 —— 两个条件面板：参考先胜利后面失败，谁处理了这帧就 break 出去。
        if (ProcessVictoryPanel(events) || ProcessLossPanel(events))
        {
            return EditorMapSpecsOutcome.None;
        }

        // :2282-2289 —— 传闻窗：参考把 mapFormat.rumors 拷一份进去、确定后搬回来，端口的宿主照
        // :2284-2286 的 if 做同一个判断（取消就丢掉窗口里的改动）。
        if (events.MouseClickLeft(_window.Rumors.Area))
        {
            Request = OpenSubWindow(EditorMapSpecsRequestKind.OpenRumors);

            return EditorMapSpecsOutcome.None;
        }

        // :2290-2297。
        if (events.MouseClickLeft(_window.Events.Area))
        {
            Request = OpenSubWindow(EditorMapSpecsRequestKind.OpenDailyEvents);

            return EditorMapSpecsOutcome.None;
        }

        // :2298-2311 —— 语言窗返回后参考重画名称、描述与两个条件面板；端口每帧都从状态重画，宿主只要
        // 在 Accepted 时把列表写回状态（ResolveLanguage 代劳），下一帧就是新主语言的文本。
        if (events.MouseClickLeft(_window.Language.Area))
        {
            Request = OpenSubWindow(EditorMapSpecsRequestKind.OpenLanguage);

            return EditorMapSpecsOutcome.None;
        }

        Rect mapNameRoi = _window.PageLayout.MapNameRoi;

        // :2312-2324。
        if (events.MouseClickLeft(mapNameRoi))
        {
            Request = CreateTextInput(EditorMapSpecsRequestKind.EditMapName, ChangeMapNameBody, _window.State.Name,
                MaxMapNameLength, isMultiLine: false);

            return EditorMapSpecsOutcome.None;
        }

        Rect descriptionRoi = _window.PageLayout.DescriptionTextRoi;

        // :2325-2335。
        if (events.MouseClickLeft(descriptionRoi))
        {
            Request = CreateTextInput(EditorMapSpecsRequestKind.EditMapDescription, ChangeMapDescriptionBody,
                _window.State.Description, MaxDescriptionLength, isMultiLine: true);

            return EditorMapSpecsOutcome.None;
        }

        // :2336-2368 —— 两个下拉框：点文字条或下拉按钮都把列表开在文字框下方。
        if (events.MouseClickLeft(_window.PageLayout.VictoryDropRoi))
        {
            _window.OpenConditionList(isLoss: false);

            return EditorMapSpecsOutcome.None;
        }

        if (events.MouseClickLeft(_window.PageLayout.LossDropRoi))
        {
            _window.OpenConditionList(isLoss: true);

            return EditorMapSpecsOutcome.None;
        }

        // :2370-2377 —— 作者注记：多行输入，上限是 messageCharLimit。
        if (events.MouseClickLeft(_window.About.Area))
        {
            Request = CreateTextInput(EditorMapSpecsRequestKind.EditCreatorNotes, ChangeCreatorNotesBody,
                _window.State.CreatorNotes, MessageCharLimit, isMultiLine: true);

            return EditorMapSpecsOutcome.None;
        }

        // :2378-2409 —— 右键提示，参考也是 else-if 链。
        if (events.IsMouseRightButtonPressedInArea(_window.Cancel.Area))
        {
            Request = ShowMessage(CancelTitle, CancelMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.Okay.Area))
        {
            Request = ShowMessage(OkayTitle, OkayMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.Rumors.Area))
        {
            Request = ShowMessage(RumorsTitle, RumorsMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.Events.Area))
        {
            Request = ShowMessage(EventsTitle, EventsMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.Language.Area))
        {
            Request = ShowMessage(LanguageTitle, LanguageMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.About.Area))
        {
            Request = ShowMessage(AboutTitle, AboutMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(mapNameRoi))
        {
            Request = ShowMessage(MapNameTitle,
                MapNameMessage.Replace("%{name}", _window.State.Name, StringComparison.Ordinal));
        }
        else if (events.IsMouseRightButtonPressedInArea(descriptionRoi))
        {
            Request = ShowMessage(MapDescriptionTitle, MapDescriptionMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.PageLayout.VictoryButtonRoi))
        {
            Request = ShowMessage(EditorMapSpecsConditionPopup.VictoryTitle, VictoryConditionMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.PageLayout.LossButtonRoi))
        {
            Request = ShowMessage(EditorMapSpecsConditionPopup.LossTitle, LossConditionMessage);
        }

        // :2417-2481 —— 玩家图标：点一下切换三态，右键出提示；参考点中即 break。
        IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(_window.State.AvailablePlayers);

        for (int i = 0; i < _window.PageLayout.PlayerRois.Count; ++i)
        {
            Rect area = _window.PageLayout.PlayerRois[i];

            if (events.MouseClickLeft(area))
            {
                if (i < colors.Count)
                {
                    _window.State.TogglePlayerColor(colors[i]);
                }

                break;
            }

            if (events.IsMouseRightButtonPressedInArea(area))
            {
                Request = ShowMessage(PlayerTypeTitle, PlayerTypeMessage);

                break;
            }
        }

        // :2483-2508 —— 难度四格：点中即换光标，右键出提示。
        for (int i = 0; i < _window.PageLayout.DifficultyRois.Count; ++i)
        {
            Rect area = _window.PageLayout.DifficultyRois[i];

            if (events.MouseClickLeft(area))
            {
                _window.State.Difficulty = MapSpecsPageIcons.GetDifficultyFromIndex(i);

                break;
            }

            if (events.IsMouseRightButtonPressedInArea(area))
            {
                Request = ShowMessage(MapDifficultyTitle, MapDifficultyMessage);

                break;
            }
        }

        return EditorMapSpecsOutcome.None;
    }

    /// <summary>
    /// Answers the text input the host was asked to run, the write-back half of <c>:2318-2323</c> and
    /// <c>:2329-2334</c>.
    /// </summary>
    /// <param name="text">The typed text, or <see langword="null"/> when the input was cancelled.</param>
    /// <returns>Whether the state changed and the page has to be repainted.</returns>
    public bool Resolve(string? text)
    {
        EditorMapSpecsRequestKind kind = _pendingEdit;
        _pendingEdit = EditorMapSpecsRequestKind.None;
        Request = EditorMapSpecsRequest.None;

        if (text is null)
        {
            return false;
        }

        switch (kind)
        {
            case EditorMapSpecsRequestKind.EditMapName:
                _window.State.Name = text;

                return true;

            case EditorMapSpecsRequestKind.EditMapDescription:
                _window.State.Description = text;

                return true;

            case EditorMapSpecsRequestKind.EditCreatorNotes:
                _window.State.CreatorNotes = text;

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Answers the rumors window the host was asked to run, the write-back of <c>:2284-2286</c>.
    /// </summary>
    /// <param name="rumors">The rumors the window ended up with, or <see langword="null"/> when it was cancelled.</param>
    /// <returns>Whether the state changed and the page has to be repainted.</returns>
    /// <remarks>取消时参考不动 <c>mapFormat.rumors</c>，端口同样不写。</remarks>
    public bool ResolveRumors(IReadOnlyList<string>? rumors)
    {
        Request = EditorMapSpecsRequest.None;

        if (rumors is null)
        {
            return false;
        }

        _window.State.SetRumors(rumors);

        return true;
    }

    /// <summary>
    /// Answers the daily events window the host was asked to run, the write-back of <c>:2292-2294</c>.
    /// </summary>
    /// <param name="events">The events the window ended up with, or <see langword="null"/> when it was cancelled.</param>
    /// <returns>Whether the state changed and the page has to be repainted.</returns>
    public bool ResolveDailyEvents(IReadOnlyList<MapDailyEvent>? events)
    {
        Request = EditorMapSpecsRequest.None;

        if (events is null)
        {
            return false;
        }

        _window.State.SetDailyEvents(events);

        return true;
    }

    /// <summary>
    /// Answers the language window the host was asked to run, the <c>:2299-2310</c> block: the languages go back into
    /// the state when the author accepted, and the name, the description and the two condition panels follow from it
    /// on the next repaint.
    /// </summary>
    /// <param name="window">The window the host ran.</param>
    /// <returns>Whether the state changed and the page has to be repainted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考在语言窗返回后无条件跑 <c>renderMapName()</c> / <c>renderMapDescription()</c> 与两次
    /// <c>updateCondition()</c>，因为它的 <c>mapFormat</c> 可能已经被改了主语言。端口的窗口只在 Accepted 时把列表
    /// 写回状态（<see cref="EditorLanguageWindow.ApplyTo(MapSpecsState)"/>），名字与描述本来就读自当前主语言的
    /// 文本槽，条件面板每帧从状态重画，所以「重画」这一步在这里退化成一次重读。
    /// </remarks>
    public bool ResolveLanguage(EditorLanguageWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        Request = EditorMapSpecsRequest.None;

        return window.Result == EditorListOutcome.Accepted && window.ApplyTo(_window.State);
    }

    /// <summary>
    /// Answers the number pad the host was asked to run, the write-back half of
    /// <c>openVirtualNumpad( _value, _minimum, _maximum )</c>.
    /// </summary>
    /// <param name="value">The number typed, or <see langword="null"/> when the pad was cancelled.</param>
    /// <returns>Whether the state changed and the page has to be repainted.</returns>
    /// <remarks>元素自己会把值夹在范围内，这里再夹一次，免得宿主的输入框给了个越界的数。</remarks>
    public bool ResolveValue(int? value)
    {
        EditorMapSpecsValueField field = _pendingValue;
        _pendingValue = EditorMapSpecsValueField.None;
        Request = EditorMapSpecsRequest.None;

        if (value is not int typed)
        {
            return false;
        }

        switch (field)
        {
            case EditorMapSpecsValueField.VictoryGold:
                if (_window.GoldValue is { } gold)
                {
                    gold.Value = typed;
                    _window.State.VictoryGoldInThousands = gold.Value;
                }

                return true;

            case EditorMapSpecsValueField.LossDays:
                if (_window.DaysValue is { } days)
                {
                    days.Value = typed;
                    _window.State.LossDays = days.Value;
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Answers the map pick the host was asked to run, the write-back half of the
    /// <c>SelectMapObject</c> calls (<c>editor_map_specs_window.cpp:1252-1264</c> and the three siblings).
    /// </summary>
    /// <param name="tile">The tile the author picked, or <see langword="null"/> when the pick was cancelled.</param>
    /// <returns>Whether the state changed and the page has to be repainted.</returns>
    /// <remarks>取消时参考保留原来的目标，端口同样不动状态。</remarks>
    public bool ResolveTarget(Point? tile)
    {
        EditorMapSpecsTargetKind target = _pendingTarget;
        _pendingTarget = EditorMapSpecsTargetKind.None;
        Request = EditorMapSpecsRequest.None;

        if (tile is not Point point)
        {
            return false;
        }

        switch (target)
        {
            case EditorMapSpecsTargetKind.VictoryTown:
            case EditorMapSpecsTargetKind.VictoryHero:
                _window.State.VictoryTarget = point;

                return true;

            case EditorMapSpecsTargetKind.LossTown:
            case EditorMapSpecsTargetKind.LossHero:
                _window.State.LossTarget = point;

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Builds the candidate list window the last <see cref="EditorMapSpecsRequestKind.PickConditionTarget"/> asked for,
    /// the <c>SelectMapCastle</c> / <c>SelectMapHero</c> window the reference opens inside its event loop.
    /// </summary>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, the real one when omitted.</param>
    /// <returns>
    /// The window with the current target selected, or <see langword="null"/> when the request is not
    /// <see cref="EditorMapSpecsRequestKind.PickConditionTarget"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artwork"/>, <paramref name="titleFont"/> or <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public EditorMapSpecsTargetPicker? CreateTargetPicker(ItemSelectionWindowArtwork artwork,
        IGlyphSource titleFont, IGlyphSource font, Size? screen = null) =>
        EditorMapSpecsTargetPicker.Create(
            _pendingTarget, _window.State, artwork, titleFont, font, _window.IsEvilInterface, screen);

    /// <summary>
    /// Builds the rumors window the last <see cref="EditorMapSpecsRequestKind.OpenRumors"/> asked for, the
    /// <c>openRumorWindow( temp, mapFormat.mainLanguage )</c> of <c>:2282-2289</c>.
    /// </summary>
    /// <param name="artwork">Frames of the scroll bar and the four buttons.</param>
    /// <param name="headerFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, the real one when omitted.</param>
    /// <returns>
    /// The window holding a copy of the current rumors, or <see langword="null"/> when the request is not
    /// <see cref="EditorMapSpecsRequestKind.OpenRumors"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artwork"/>, <paramref name="headerFont"/> or <paramref name="bodyFont"/> is <see langword="null"/>.
    /// </exception>
    public EditorRumorWindow? CreateRumorWindow(EditorListArtwork artwork, IGlyphSource headerFont,
        IGlyphSource bodyFont, Size? screen = null) =>
        Request.Kind == EditorMapSpecsRequestKind.OpenRumors
            ? EditorRumorWindow.Create(screen ?? ScreenBuffer.DefaultSize, _window.State.Rumors, artwork, headerFont,
                bodyFont)
            : null;

    /// <summary>
    /// Builds the daily events window the last <see cref="EditorMapSpecsRequestKind.OpenDailyEvents"/> asked for, the
    /// <c>openDailyEventsWindow( temp, ... )</c> of <c>:2290-2297</c>.
    /// </summary>
    /// <param name="artwork">Frames of the scroll bar and the four buttons.</param>
    /// <param name="headerFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, the real one when omitted.</param>
    /// <returns>
    /// The window holding a copy of the current events, or <see langword="null"/> when the request is not
    /// <see cref="EditorMapSpecsRequestKind.OpenDailyEvents"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artwork"/>, <paramref name="headerFont"/> or <paramref name="bodyFont"/> is <see langword="null"/>.
    /// </exception>
    public EditorDailyEventsWindow? CreateDailyEventsWindow(EditorListArtwork artwork, IGlyphSource headerFont,
        IGlyphSource bodyFont, Size? screen = null) =>
        Request.Kind == EditorMapSpecsRequestKind.OpenDailyEvents
            ? EditorDailyEventsWindow.Create(screen ?? ScreenBuffer.DefaultSize, _window.State.DailyEvents, artwork,
                headerFont, bodyFont)
            : null;

    /// <summary>
    /// Builds the language window the last <see cref="EditorMapSpecsRequestKind.OpenLanguage"/> asked for, the
    /// <c>openLanguageWindow( mapFormat )</c> of <c>:2298-2311</c>.
    /// </summary>
    /// <param name="artwork">Frames of the scroll bar and the two small buttons.</param>
    /// <param name="headerFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, the real one when omitted.</param>
    /// <returns>
    /// The window editing the languages of the state, or <see langword="null"/> when the request is not
    /// <see cref="EditorMapSpecsRequestKind.OpenLanguage"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artwork"/>, <paramref name="headerFont"/> or <paramref name="bodyFont"/> is <see langword="null"/>.
    /// </exception>
    public EditorLanguageWindow? CreateLanguageWindow(EditorListArtwork artwork, IGlyphSource headerFont,
        IGlyphSource bodyFont, Size? screen = null) =>
        Request.Kind == EditorMapSpecsRequestKind.OpenLanguage
            ? EditorLanguageWindow.Create(_window.State, artwork, screen ?? ScreenBuffer.DefaultSize, headerFont,
                bodyFont)
            : null;

    /// <summary>
    /// Writes the edited specifications back to the map, the <c>getConditionMetadata()</c> calls of
    /// <c>:2512-2515</c> plus the caller's own write.
    /// </summary>
    /// <param name="session">The editing session to write into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public void Commit(MapEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.ApplyMapInfo(_window.State.CreateInfo());
    }

    /// <summary>
    /// Handles the victory panel, <c>VictoryConditionUI::processEvents()</c>
    /// (<c>editor_map_specs_window.cpp:1219-1443</c>).
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns><see langword="true"/> when the panel took the event.</returns>
    /// <remarks>
    /// <para>
    /// 参考每个条件只认自己的那几个区域，处理完就 <c>break</c> 出事件循环；端口用返回值表达同一个意思。
    /// </para>
    /// <para>
    /// 点城镇/英雄那一行，参考当场开一个候选列表窗（<c>SelectMapCastle</c> / <c>SelectMapHero</c>，<c>:1230-1264</c>、
    /// <c>:1289-1318</c>）。端口一帧一步，于是这一行把 <see cref="EditorMapSpecsRequestKind.PickConditionTarget"/>
    /// 交给宿主，宿主用 <see cref="CreateTargetPicker"/> 造窗、自己跑循环，选完由 <see cref="ResolveTarget"/> 送回
    /// 目标——与地图名、描述那几处「请求宿主跑一个窗」同一个套路。
    /// </para>
    /// <para>
    /// 联盟勾选框受 MP2 头限制（只存第一方的人数），点击被翻译成「把该颜色所在的分界点挪过来」，见
    /// <see cref="MoveAllianceBoundary"/>。
    /// </para>
    /// </remarks>
    private bool ProcessVictoryPanel(LocalEvent events)
    {
        if (_window.VictoryPanel is not { } panel)
        {
            return false;
        }

        MapSpecsConditionPanelLayout layout = panel.Layout;
        MapSpecsState state = _window.State;

        switch (state.VictoryCondition)
        {
            case MapVictoryCondition.CaptureTown:
                if (events.IsMouseRightButtonPressedInArea(layout.SelectRowRoi))
                {
                    Request = ShowMessage(SpecialVictoryTitle, CaptureTownVictoryMessage);

                    return true;
                }

                if (events.MouseClickLeft(layout.SelectRowRoi))
                {
                    Request = CreateTargetRequest(EditorMapSpecsTargetKind.VictoryTown);

                    return true;
                }

                if (events.MouseClickLeft(layout.AllowNormalVictoryRoi))
                {
                    state.AllowNormalVictory = !state.AllowNormalVictory;

                    return true;
                }

                return ProcessAllowForAi(events, layout, state);

            case MapVictoryCondition.KillHero:
                if (events.IsMouseRightButtonPressedInArea(layout.SelectRowRoi))
                {
                    Request = ShowMessage(SpecialVictoryTitle, KillHeroVictoryMessage);

                    return true;
                }

                if (events.MouseClickLeft(layout.SelectRowRoi))
                {
                    Request = CreateTargetRequest(EditorMapSpecsTargetKind.VictoryHero);

                    return true;
                }

                return false;

            case MapVictoryCondition.ObtainArtifact:
                // 参考右键弹神器资料页、点框开神器选择窗（:1266-1304），两者端口都还没有。
                if (events.MouseClickLeft(layout.AllowNormalVictoryRoi))
                {
                    state.AllowNormalVictory = !state.AllowNormalVictory;

                    return true;
                }

                return events.MouseClickLeft(layout.SelectRowRoi);

            case MapVictoryCondition.DefeatOtherSide:
                return ProcessAlliances(events, panel, state);

            case MapVictoryCondition.CollectGold:
                if (ProcessValue(events, _window.GoldValue, value => state.VictoryGoldInThousands = value,
                        EditorMapSpecsValueField.VictoryGold))
                {
                    return true;
                }

                if (events.MouseClickLeft(layout.AllowNormalVictoryRoi))
                {
                    state.AllowNormalVictory = !state.AllowNormalVictory;

                    return true;
                }

                return ProcessAllowForAi(events, layout, state);

            default:
                // DefeatEveryone 没有界面（:1029-1032）。
                return false;
        }
    }

    /// <summary>
    /// Handles the loss panel, <c>LossConditionUI::processEvents()</c> (<c>editor_map_specs_window.cpp:1745-1870</c>).
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns><see langword="true"/> when the panel took the event.</returns>
    /// <remarks>失败条件只有一行目标或一个数值控件，没有勾选项，也没有联盟行。</remarks>
    private bool ProcessLossPanel(LocalEvent events)
    {
        if (_window.LossPanel is not { } panel)
        {
            return false;
        }

        MapSpecsConditionPanelLayout layout = panel.Layout;
        MapSpecsState state = _window.State;

        switch (state.LossCondition)
        {
            case MapLossCondition.LoseTown:
                if (events.IsMouseRightButtonPressedInArea(layout.SelectRowRoi))
                {
                    Request = ShowMessage(SpecialLossTitle, LoseTownLossMessage);

                    return true;
                }

                if (events.MouseClickLeft(layout.SelectRowRoi))
                {
                    Request = CreateTargetRequest(EditorMapSpecsTargetKind.LossTown);

                    return true;
                }

                return false;

            case MapLossCondition.LoseHero:
                if (events.IsMouseRightButtonPressedInArea(layout.SelectRowRoi))
                {
                    Request = ShowMessage(SpecialLossTitle, LoseHeroLossMessage);

                    return true;
                }

                if (events.MouseClickLeft(layout.SelectRowRoi))
                {
                    Request = CreateTargetRequest(EditorMapSpecsTargetKind.LossHero);

                    return true;
                }

                return false;

            case MapLossCondition.OutOfTime:
                return ProcessValue(events, _window.DaysValue, value => state.LossDays = value,
                    EditorMapSpecsValueField.LossDays);

            default:
                // Everything 没有界面（:1674-1677）。
                return false;
        }
    }

    /// <summary>
    /// Feeds one frame to a number element and writes the new value back, the
    /// <c>_goldAccumulationValue.processEvents()</c> / <c>_outOfTimeValue.processEvents()</c> calls of the reference.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <param name="element">The element of the panel, <see langword="null"/> when the window has no font.</param>
    /// <param name="apply">Writes the new value into the state.</param>
    /// <param name="field">Which element this is, so that the number pad's answer finds it again.</param>
    /// <returns><see langword="true"/> when the element took the event.</returns>
    /// <remarks>
    /// 参考的元素自己在 <c>processEvents()</c> 里改值、开数字键盘、出范围提示（<c>ui_dialog.cpp:986-1019</c>）；
    /// 端口的 <see cref="ValueSelectionElement.ProcessEvents"/> 只回「变没变」与「要开哪个窗」，窗由宿主跑，
    /// 结果走 <see cref="ResolveValue"/>。按住时也算「吃了事件」，因为箭头要重画成按下的样子。
    /// </remarks>
    private bool ProcessValue(LocalEvent events, ValueSelectionElement? element, Action<int> apply,
        EditorMapSpecsValueField field)
    {
        if (element is null)
        {
            return false;
        }

        ValueSelectionFrame frame = element.ProcessEvents(events);

        if (frame.Changed)
        {
            apply(element.Value);

            return true;
        }

        switch (frame.Request)
        {
            case ValueSelectionRequest.Range:
                Request = ShowMessage(string.Empty, element.RangeMessage);

                return true;

            case ValueSelectionRequest.Numpad:
                _pendingValue = field;
                Request = new EditorMapSpecsRequest(EditorMapSpecsRequestKind.EditValue, string.Empty, string.Empty,
                    null, EditorMapSpecsTargetKind.None, field);

                return true;

            default:
                return element.IsUpPressed || element.IsDownPressed;
        }
    }

    /// <summary>The "allow this condition also for AI" checkbox, <c>:1072</c> and <c>:1409-1420</c>.</summary>
    private static bool ProcessAllowForAi(LocalEvent events, MapSpecsConditionPanelLayout layout, MapSpecsState state)
    {
        if (layout.AllowForAiRoi == Rect.Empty || !events.MouseClickLeft(layout.AllowForAiRoi))
        {
            return false;
        }

        state.ComputerAlsoWins = !state.ComputerAlsoWins;

        return true;
    }

    /// <summary>
    /// Handles the alliance checkboxes of <see cref="MapVictoryCondition.DefeatOtherSide"/>,
    /// <c>editor_map_specs_window.cpp:1355-1440</c>.
    /// </summary>
    /// <remarks>
    /// 参考给每个颜色一个勾选框，两方各存一套颜色集合；MP2 头只存第一方的人数（<see cref="MapSpecsState.VictorySideCount"/>），
    /// 端口因此把「把该颜色放进某方」翻译成移动分界点，详见 <see cref="MoveAllianceBoundary"/>。
    /// </remarks>
    private bool ProcessAlliances(LocalEvent events, MapSpecsConditionPanel panel, MapSpecsState state)
    {
        for (int alliance = 0; alliance < panel.AllianceCheckboxes.Count; ++alliance)
        {
            foreach (Checkbox checkbox in panel.AllianceCheckboxes[alliance])
            {
                if (events.MouseClickLeft(checkbox.Area))
                {
                    MoveAllianceBoundary(state, checkbox.Color, alliance);

                    return true;
                }

                if (events.IsMouseRightButtonPressedInArea(checkbox.Area))
                {
                    string order = alliance == 0 ? FirstAllianceOrder : SecondAllianceOrder;
                    string color = PlayerColors.GetName(checkbox.Color);

                    Request = ShowMessage(
                        AllianceCheckboxTitle
                            .Replace("%{color}", color, StringComparison.Ordinal)
                            .Replace("%{alliance}", order, StringComparison.Ordinal),
                        AllianceCheckboxMessage
                            .Replace("%{color}", color, StringComparison.Ordinal)
                            .Replace("%{alliance}", order, StringComparison.Ordinal));

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Moves the only boundary MP2 can store so that <paramref name="color"/> ends up in
    /// <paramref name="alliance"/>.
    /// </summary>
    /// <param name="state">The specifications being edited.</param>
    /// <param name="color">The colour whose checkbox was clicked.</param>
    /// <param name="alliance">Row of the box, 0 for the first alliance and 1 for the second.</param>
    /// <remarks>
    /// <para>
    /// 参考的两套联盟是可以任意挑颜色的集合（<c>_alliances</c>），MP2 头只有「第一方几个人」，端口的状态因此是一条
    /// 分界点：<see cref="MapSpecsState.VictorySide1"/> 是按颜色顺序取前 N 个（<see cref="MapSpecsState.VictorySideCount"/>）。
    /// </para>
    /// <para>
    /// 点击只有两种结果能表达：让分界点停在该颜色之后（颜色进第一方）或之前（颜色进第二方）。点中已经在目标方的
    /// 颜色什么都不动；分界点被夹到 <c>[1, MaxSideCount]</c> 两端时同样不会变，这与参考「一方至少留一个颜色」的
    /// 约束一致。
    /// </para>
    /// </remarks>
    private static void MoveAllianceBoundary(MapSpecsState state, PlayerColor color, int alliance)
    {
        IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(state.AvailablePlayers);
        int position = -1;

        for (int index = 0; index < colors.Count; ++index)
        {
            if (colors[index] == color)
            {
                position = index;

                break;
            }
        }

        if (position < 0 || (position < state.VictorySideCount) == (alliance == 0))
        {
            return;
        }

        state.VictorySideCount = alliance == 0 ? position + 1 : position;
    }

    /// <summary>Asks the host to pick the target of <paramref name="target"/> out of its candidates.</summary>
    private EditorMapSpecsRequest CreateTargetRequest(EditorMapSpecsTargetKind target)
    {
        _pendingTarget = target;

        return new EditorMapSpecsRequest(EditorMapSpecsRequestKind.PickConditionTarget, string.Empty, string.Empty, null, target);
    }

    private EditorMapSpecsRequest CreateTextInput(EditorMapSpecsRequestKind kind, string body, string initialText,
        int characterLimit, bool isMultiLine)
    {
        _pendingEdit = kind;

        return new EditorMapSpecsRequest(kind, string.Empty, body,
            InputStringDialog.Create(string.Empty, body, characterLimit, isMultiLine, initialText));
    }

    /// <summary>
    /// Closes the open list and writes the picked condition into the state,
    /// <c>editor_map_specs_window.cpp:2362-2369</c>.
    /// </summary>
    /// <remarks>
    /// 参考只在选中的条件真的变了才赋值并 <c>updateCondition()</c>；端口同样先比再写，否则会把条件的「目标」
    /// （城堡、英雄、天数）重置一遍。
    /// </remarks>
    private void ApplyConditionChoice(EditorMapSpecsConditionPopup popup)
    {
        int? chosen = popup.ChosenCondition;
        bool isLoss = popup.IsLoss;

        _window.CloseConditionList();

        if (chosen is not int condition)
        {
            return;
        }

        if (isLoss)
        {
            if ((MapLossCondition)condition != _window.State.LossCondition)
            {
                _window.State.LossCondition = (MapLossCondition)condition;
                _window.State.UpdateLossCondition();
            }

            return;
        }

        if ((MapVictoryCondition)condition != _window.State.VictoryCondition)
        {
            _window.State.VictoryCondition = (MapVictoryCondition)condition;
            _window.State.UpdateVictoryCondition();
        }
    }

    private static EditorMapSpecsRequest ShowMessage(string title, string body) =>
        new(EditorMapSpecsRequestKind.ShowMessage, title, body, null);

    private static EditorMapSpecsRequest OpenSubWindow(EditorMapSpecsRequestKind kind) =>
        new(kind, string.Empty, string.Empty, null);
}
