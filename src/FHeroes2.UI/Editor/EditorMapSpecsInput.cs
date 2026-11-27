using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
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
    PickConditionTarget
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
public readonly record struct EditorMapSpecsRequest(EditorMapSpecsRequestKind Kind, string Title, string Body,
    InputStringDialog? Dialog, EditorMapSpecsTargetKind Target = EditorMapSpecsTargetKind.None)
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

    private readonly EditorMapSpecsWindow _window;

    private EditorMapSpecsRequestKind _pendingEdit = EditorMapSpecsRequestKind.None;
    private EditorMapSpecsTargetKind _pendingTarget = EditorMapSpecsTargetKind.None;

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

        // :2378-2409 —— 右键提示，参考也是 else-if 链；四个子窗按钮的两条在 .3 补上。
        if (events.IsMouseRightButtonPressedInArea(_window.Cancel.Area))
        {
            Request = ShowMessage(CancelTitle, CancelMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_window.Okay.Area))
        {
            Request = ShowMessage(OkayTitle, OkayMessage);
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
                if (ProcessValue(events, _window.GoldValue, value => state.VictoryGoldInThousands = value))
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
                return ProcessValue(events, _window.DaysValue, value => state.LossDays = value);

            default:
                // Everything 没有界面（:1674-1677）。
                return false;
        }
    }

    /// <summary>
    /// Feeds a click of the two arrows of a number element and writes the new value back, the
    /// <c>_goldAccumulationValue.processEvents()</c> / <c>_outOfTimeValue.processEvents()</c> calls of the reference.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <param name="element">The element of the panel, <see langword="null"/> when the window has no font.</param>
    /// <param name="apply">Writes the new value into the state.</param>
    /// <returns><see langword="true"/> when the element took the event.</returns>
    /// <remarks>
    /// 参考的元素自己在 <c>processEvents()</c> 里改值并返回是否变了（<c>ui_dialog.cpp:986-1019</c>）。端口的
    /// <see cref="ValueSelectionElement"/> 只认按下与松开两步（<c>SetLeftButton</c>），于是这里按住时把按下交进去、
    /// 松开时取回结果。
    /// </remarks>
    private static bool ProcessValue(LocalEvent events, ValueSelectionElement? element, Action<int> apply)
    {
        if (element is null)
        {
            return false;
        }

        Rect up = element.UpButton.Area;
        Rect down = element.DownButton.Area;

        if (events.MouseClickLeft(up))
        {
            return Step(element, up.Position, apply);
        }

        if (events.MouseClickLeft(down))
        {
            return Step(element, down.Position, apply);
        }

        if (events.IsMouseLeftButtonPressedInArea(up) || events.IsMouseLeftButtonPressedInArea(down))
        {
            element.SetLeftButton(true, events.CursorPosition);

            return true;
        }

        if (events.IsMouseLeftButtonReleasedInArea(up) || events.IsMouseLeftButtonReleasedInArea(down))
        {
            if (element.SetLeftButton(false, events.CursorPosition))
            {
                apply(element.Value);
            }

            return true;
        }

        return false;
    }

    /// <summary>One full press and release of an arrow, so that a click and a held button behave the same.</summary>
    private static bool Step(ValueSelectionElement element, Point point, Action<int> apply)
    {
        element.SetLeftButton(true, point);

        if (element.SetLeftButton(false, point))
        {
            apply(element.Value);
        }

        return true;
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
}
