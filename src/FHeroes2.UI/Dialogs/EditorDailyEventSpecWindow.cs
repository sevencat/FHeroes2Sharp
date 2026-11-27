using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>Which number of the daily event a number pad request stands for.</summary>
public enum DailyEventValueField
{
    /// <summary>No request.</summary>
    None,

    /// <summary>The first day the event fires, <c>firstOccurrenceDay</c>.</summary>
    FirstOccurrenceDay,

    /// <summary>The repeat period in days, <c>repeatPeriodInDays</c>.</summary>
    RepeatPeriodInDays
}

/// <summary>
/// The number pad a click on an edit box asks the host to open,
/// <c>editor_daily_event_spec_window.cpp:220-239</c> through <c>ValueSelectionDialogElement::processEvents()</c>.
/// </summary>
/// <param name="Field">Which number the answer goes to.</param>
/// <param name="Minimum">Smallest accepted value.</param>
/// <param name="Maximum">Largest accepted value.</param>
/// <param name="Value">The value shown when the pad opens.</param>
public readonly record struct DailyEventNumpadRequest(DailyEventValueField Field, int Minimum, int Maximum, int Value);

/// <summary>
/// The "set the amount" dialog a click on a resource icon asks the host to open, <c>Dialog::SelectCount()</c>
/// (<c>editor_daily_event_spec_window.cpp:307-330</c>).
/// </summary>
/// <param name="ResourceType">One of the <see cref="Resource"/> flags.</param>
/// <param name="Value">The amount the event currently gives.</param>
/// <param name="Dialog">The dialog to run; its <see cref="SelectCountDialog.Result"/> is the answer.</param>
public readonly record struct DailyEventResourceRequest(int ResourceType, int Value, SelectCountDialog Dialog);

/// <summary>
/// The editor's daily event spec window, <c>editDailyEvent()</c>
/// (<c>editor_daily_event_spec_window.cpp:67-373</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口 608×430：左列是消息文本（300×210）与奖励资源面板（300×99），右列是玩家/电脑颜色复选框、首次发生日的
/// 数字控件与日期说明、重复周期的数字控件，OKAY/CANCEL 在底部两角。进来先把事件规整一遍
/// （<see cref="MapDailyEvent.Clamp"/>）。
/// </para>
/// <para>
/// 消息输入框、数字键盘、数量框都是宿主的活（参考直接调 <c>Dialog::inputString</c> / <c>openVirtualNumpad</c> /
/// <c>Dialog::SelectCount</c>）：本类只发 <see cref="MessageRequested"/>、<see cref="NumpadRequest"/>、
/// <see cref="ResourceCountRequest"/>，再吃回 <see cref="SetMessage"/>、<see cref="SetValueField"/>、
/// <see cref="SetResourceCount"/>。
/// </para>
/// <para>
/// 没有 <c>ImageRestorer</c>：<see cref="Draw"/> 每帧整窗重画，复选框的方框画在常驻缓冲里、每帧贴回，勾选标记按
/// 状态单独贴。
/// </para>
/// </remarks>
public sealed class EditorDailyEventSpecWindow
{
    /// <summary>Width of the message and reward area, <c>messageArea.width</c> (<c>:52</c>).</summary>
    public const int MessageAreaWidth = 300;

    /// <summary>Height of the message area, <c>messageArea.height</c> (<c>:53</c>).</summary>
    public const int MessageAreaHeight = 210;

    /// <summary>Gap between the parts of the window, <c>elementOffset</c> (<c>:58</c>).</summary>
    public const int ElementOffset = 9;

    /// <summary>Width of the standard window's frame, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int BorderWidth = 16;

    /// <summary>Width of the window (<c>:83</c>).</summary>
    public const int WindowWidth = 640 - (2 * BorderWidth);

    /// <summary>Height of the window (<c>:83</c>).</summary>
    public const int WindowHeight = MessageAreaHeight + 220;

    /// <summary>Width of the right column, <c>playerAreaWidth</c> (<c>:59</c>).</summary>
    public const int PlayerAreaWidth = WindowWidth - (3 * ElementOffset) - MessageAreaWidth;

    /// <summary>Height of the reward panel, the literal <c>99</c> of <c>:109</c>.</summary>
    public const int ResourcePanelHeight = 99;

    /// <summary>Room a colour checkbox row takes (<c>:138</c>、<c>:157</c>).</summary>
    public const int CheckboxRowHeight = 35;

    /// <summary>Distance between two colour checkboxes (<c>:131</c>).</summary>
    public const int CheckboxStepX = 32;

    /// <summary>Gap between a wrapped label and the row below it (<c>:128</c>).</summary>
    public const int LabelBottomGap = 3;

    /// <summary>Gap between a number element and the text under it (<c>:178</c>).</summary>
    public const int ValueBottomGap = 10;

    /// <summary>Room the date description takes (<c>:180</c>).</summary>
    public const int DateDescriptionHeight = 35;

    /// <summary>Longest message the author may type, <c>Maps::Map_Format::messageCharLimit</c>.</summary>
    public const int MessageCharLimit = 999;

    /// <summary>Smallest amount the resource dialog accepts (<c>:320</c>).</summary>
    public const int ResourceAmountMinimum = -99999;

    /// <summary>Largest amount the resource dialog accepts (<c>:320</c>).</summary>
    public const int ResourceAmountMaximum = 999999;

    /// <summary>Title of the window, <c>MP2::StringObject( OBJ_EVENT ) + ':'</c> (<c>:88</c>).</summary>
    public const string Title = "Event:";

    /// <summary>Label over the message area (<c>:93</c>).</summary>
    public const string MessageLabel = "Message Text:";

    /// <summary>Label over the reward panel (<c>:106</c>).</summary>
    public const string RewardLabel = "Reward:";

    /// <summary>Label over the human colours (<c>:119</c>).</summary>
    public const string HumanColorsLabel = "Player colors allowed to get event:";

    /// <summary>Label over the computer colours (<c>:140</c>).</summary>
    public const string ComputerColorsLabel = "Computer colors allowed to get event:";

    /// <summary>Label over the first day element (<c>:159</c>).</summary>
    public const string FirstDayLabel = "First day of occurrence:";

    /// <summary>Label over the repeat period element (<c>:187</c>).</summary>
    public const string RepeatPeriodLabel = "Repeat period (days):";

    /// <summary>Title the message input dialog opens with (<c>:335</c>).</summary>
    public const string MessageDialogHeader = "Message:";

    /// <summary>Title of the "set the amount" dialog, with the resource name filled in (<c>:317</c>).</summary>
    public const string ResourceCountLabelFormat = "Set {0} Count:";

    /// <summary>Hint title of a right click on a human colour checkbox (<c>:265</c>).</summary>
    public const string HumanColorTitleFormat = "Allow {0} human player to get event";

    /// <summary>Hint body of a right click on a human colour checkbox (<c>:266</c>).</summary>
    public const string HumanColorMessageFormat =
        "If this checkbox is checked, this event will trigger for the {0} player if they are controlled by a human.";

    /// <summary>Hint title of a right click on a computer colour checkbox (<c>:294</c>).</summary>
    public const string ComputerColorTitleFormat = "Allow {0} computer player to get event";

    /// <summary>Hint body of a right click on a computer colour checkbox (<c>:295</c>).</summary>
    public const string ComputerColorMessageFormat =
        "If this checkbox is checked, this event will trigger for the {0} player if they are controlled by a computer.";

    /// <summary>Title of the CANCEL button's message (<c>:346</c>).</summary>
    public const string CancelTitle = "Cancel";

    /// <summary>Body of the CANCEL button's message (<c>:346</c>).</summary>
    public const string CancelMessage = "Exit this menu without doing anything.";

    /// <summary>Title of the OKAY button's message and of the range hint (<c>:349</c>).</summary>
    public const string OkayTitle = "Okay";

    /// <summary>Body of the OKAY button's message (<c>:349</c>).</summary>
    public const string OkayMessage = "Click to save the Event properties.";

    /// <summary>Title of the reward panel's message (<c>:353</c>).</summary>
    public const string ResourcesTitle = "Resources";

    /// <summary>Body of the reward panel's message when it is empty (<c>:353</c>).</summary>
    public const string NoResourcesMessage = "No resources will be given as a reward.";

    /// <summary>Body of the reward panel's message (<c>:357</c>).</summary>
    public const string ResourcesMessage = "Resources will be given as a reward.";

    /// <summary>Hint title of the message area (<c>:362</c>).</summary>
    public const string MessageAreaTitle = "Event Message Text";

    /// <summary>Hint body of the message area (<c>:362</c>).</summary>
    public const string MessageAreaMessage = "Click here to change the event message.";

    /// <summary>Body of the range message a right click on an edit box shows.</summary>
    public const string RangeMessageFormat = "The available values range from {0} to {1}.";

    private static readonly int[] ResourceTypes =
    [
        Resource.Wood, Resource.Sulfur, Resource.Crystal, Resource.Mercury, Resource.Ore, Resource.Gems, Resource.Gold
    ];

    private readonly StandardWindow _window;
    private readonly MapDailyEvent _event;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _bodyFont;

    private readonly Point _titlePosition;
    private readonly Point _messageLabelPosition;
    private readonly Rect _messageRoi;
    private readonly Point _rewardLabelPosition;
    private readonly Rect _resourceRoi;
    private readonly Rect[] _individualResourceRoi = new Rect[7];
    private readonly int _playerAreaX;
    private readonly Point _humanLabelPosition;
    private readonly int _humanLabelWidth;
    private readonly Point _computerLabelPosition;
    private readonly int _computerLabelWidth;
    private readonly Point _firstDayLabelPosition;
    private readonly int _firstDayLabelWidth;
    private readonly Point _repeatLabelPosition;
    private readonly int _repeatLabelWidth;
    private readonly Rect _dateDescriptionRoi;

    private readonly List<Checkbox> _humanCheckboxes = [];
    private readonly List<Checkbox> _computerCheckboxes = [];
    private readonly PixelBuffer _checkboxLayer;
    private readonly Point _checkboxLayerOrigin;

    private readonly ValueSelectionElement _firstDaySelection;
    private readonly ValueSelectionElement _repeatPeriodSelection;

    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;

    private EditorDailyEventSpecWindow(MapDailyEvent dailyEvent, PlayerColor availableHumanColors,
        PlayerColor availableComputerColors, EditorDailyEventSpecArtwork artwork, Size screen, IGlyphSource headerFont,
        IGlyphSource bodyFont, Func<long>? clockMs)
    {
        _event = dailyEvent.Clone();
        _event.Clamp(availableHumanColors, availableComputerColors);

        _headerFont = headerFont;
        _bodyFont = bodyFont;

        _window = StandardWindow.CreateCentered(new Size(WindowWidth, WindowHeight), renderBackground: true, screen);

        Rect active = _window.ActiveArea;

        int y = active.Y + ElementOffset;
        int titleWidth = GlyphText.Measure(headerFont, Title);

        _titlePosition = new Point(active.X + ((active.Width - titleWidth) / 2), y);

        y += headerFont.LineHeight + ElementOffset;

        _messageRoi = new Rect(active.X + ElementOffset, y + bodyFont.LineHeight, MessageAreaWidth, MessageAreaHeight);
        _messageLabelPosition = new Point(
            _messageRoi.X + ((MessageAreaWidth - GlyphText.Measure(bodyFont, MessageLabel)) / 2), y);

        _rewardLabelPosition = new Point(
            _messageRoi.X + ((MessageAreaWidth - GlyphText.Measure(bodyFont, RewardLabel)) / 2),
            y + MessageAreaHeight + (2 * ElementOffset));
        _resourceRoi = new Rect(_messageRoi.X, bodyFont.LineHeight + y + MessageAreaHeight + (2 * ElementOffset),
            MessageAreaWidth, ResourcePanelHeight);

        _playerAreaX = active.X + ElementOffset + MessageAreaWidth + ElementOffset;

        _humanLabelPosition = CreateLabel(bodyFont, HumanColorsLabel, _playerAreaX, ref y, out _humanLabelWidth);

        int availablePlayers = PlayerColors.Count(availableHumanColors | availableComputerColors);
        int checkOffsetX = (PlayerAreaWidth - (availablePlayers * CheckboxStepX)) / 2;

        _checkboxLayerOrigin = _window.TotalArea.Position;
        _checkboxLayer = new PixelBuffer(_window.TotalArea.Width, _window.TotalArea.Height);

        Point humanCheckboxOrigin = ToLayer(new Point(_playerAreaX + checkOffsetX, y));

        EditorUiHelper.CreateColorCheckboxes(_humanCheckboxes, availableHumanColors, _event.HumanPlayerColors,
            humanCheckboxOrigin.X, humanCheckboxOrigin.Y, _checkboxLayer);

        y += CheckboxRowHeight;

        _computerLabelPosition = CreateLabel(bodyFont, ComputerColorsLabel, _playerAreaX, ref y, out _computerLabelWidth);

        Point computerCheckboxOrigin = ToLayer(new Point(_playerAreaX + checkOffsetX, y));

        EditorUiHelper.CreateColorCheckboxes(_computerCheckboxes, availableComputerColors, _event.ComputerPlayerColors,
            computerCheckboxOrigin.X, computerCheckboxOrigin.Y, _checkboxLayer);

        y += CheckboxRowHeight;

        _firstDayLabelPosition = CreateLabel(bodyFont, FirstDayLabel, _playerAreaX, ref y, out _firstDayLabelWidth);

        Size valueArea = ValueSelectionElement.GetArea(artwork.ValueFrames[ValueSelectionElement.EditBoxFrame],
            artwork.ValueFrames[ValueSelectionElement.UpReleasedFrame]);

        Point firstDayPosition = new(_playerAreaX + ((PlayerAreaWidth - valueArea.Width) / 2), y);

        _firstDaySelection = new ValueSelectionElement(1, (int)MapDailyEvent.LastDayForEvents,
            (int)_event.FirstOccurrenceDay, 1, firstDayPosition, bodyFont, artwork.ValueFrames, clockMs);

        y += ValueBottomGap + valueArea.Height;

        _dateDescriptionRoi = new Rect(_playerAreaX, y, PlayerAreaWidth, DateDescriptionHeight);

        y += DateDescriptionHeight;

        _repeatLabelPosition = CreateLabel(bodyFont, RepeatPeriodLabel, _playerAreaX, ref y, out _repeatLabelWidth);

        Point repeatPeriodPosition = new(_playerAreaX + ((PlayerAreaWidth - valueArea.Width) / 2), y);

        _repeatPeriodSelection = new ValueSelectionElement(0, (int)MapDailyEvent.DaysInYear,
            (int)_event.RepeatPeriodInDays, 1, repeatPeriodPosition, bodyFont, artwork.ValueFrames, clockMs);

        // 资源图标的位置在布局里，先把它们量出来，点击才有地方可落。
        PixelBuffer measurement = new(WindowWidth, WindowHeight);
        EditorUiHelper.RenderResources(_event.Resources, _resourceRoi, measurement, _individualResourceRoi);

        Point okayPosition = _window.RenderPosition(new Point(20, 7),
            new Size(artwork.Okay.Released.Width, artwork.Okay.Released.Height), WindowPadding.BottomLeft);
        Point cancelPosition = _window.RenderPosition(new Point(20, 7),
            new Size(artwork.Cancel.Released.Width, artwork.Cancel.Released.Height), WindowPadding.BottomRight);

        _okay = new ButtonSprite(artwork.Okay.Released, artwork.Okay.Pressed);
        _okay.SetPosition(okayPosition.X, okayPosition.Y);

        _cancel = new ButtonSprite(artwork.Cancel.Released, artwork.Cancel.Pressed);
        _cancel.SetPosition(cancelPosition.X, cancelPosition.Y);
    }

    /// <summary>
    /// Builds the window; the event is copied and clamped to what the map allows.
    /// </summary>
    /// <param name="dailyEvent">The event to edit.</param>
    /// <param name="availableHumanColors">Colours the map lets humans play.</param>
    /// <param name="availableComputerColors">Colours the map lets computers play.</param>
    /// <param name="artwork">Its sprites.</param>
    /// <param name="screen">Screen size.</param>
    /// <param name="headerFont">The normal yellow font; only the title is in it.</param>
    /// <param name="bodyFont">The normal white font, also the font of the numbers.</param>
    /// <param name="clockMs">Clock the number elements' repeat timers read, or <see langword="null"/> for the system one.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static EditorDailyEventSpecWindow Create(MapDailyEvent dailyEvent, PlayerColor availableHumanColors,
        PlayerColor availableComputerColors, EditorDailyEventSpecArtwork artwork, Size screen, IGlyphSource headerFont,
        IGlyphSource bodyFont, Func<long>? clockMs = null)
    {
        ArgumentNullException.ThrowIfNull(dailyEvent);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        return new EditorDailyEventSpecWindow(dailyEvent, availableHumanColors, availableComputerColors, artwork, screen,
            headerFont, bodyFont, clockMs);
    }

    /// <summary>The window the parts sit in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The event being edited, a copy of the one passed to <see cref="Create"/>.</summary>
    public MapDailyEvent Event => _event;

    /// <summary>The message area, <c>messageRoi</c> (<c>:95</c>).</summary>
    public Rect MessageArea => _messageRoi;

    /// <summary>The reward panel, <c>resourceRoi</c> (<c>:109</c>).</summary>
    public Rect ResourceArea => _resourceRoi;

    /// <summary>The area of every resource icon, in the order of <see cref="ResourceTypes"/>.</summary>
    public IReadOnlyList<Rect> ResourceIconAreas => _individualResourceRoi;

    /// <summary>The area of the date description under the first day element (<c>:180</c>).</summary>
    public Rect DateDescriptionArea => _dateDescriptionRoi;

    /// <summary>Left edge of the right column, <c>playerAreaOffsetX</c> (<c>:117</c>).</summary>
    public int PlayerAreaX => _playerAreaX;

    /// <summary>The colour checkboxes of the human players, in colour order.</summary>
    public IReadOnlyList<Checkbox> HumanCheckboxes => _humanCheckboxes;

    /// <summary>The colour checkboxes of the computer players, in colour order.</summary>
    public IReadOnlyList<Checkbox> ComputerCheckboxes => _computerCheckboxes;

    /// <summary>The number element of the first day.</summary>
    public ValueSelectionElement FirstDaySelection => _firstDaySelection;

    /// <summary>The number element of the repeat period.</summary>
    public ValueSelectionElement RepeatPeriodSelection => _repeatPeriodSelection;

    /// <summary>The OKAY button.</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>How the author left the window; reset by <see cref="Process"/>.</summary>
    public EditorListOutcome Result { get; private set; }

    /// <summary>A message the host has to show, or <see langword="null"/>.</summary>
    public DialogPopup? Popup { get; private set; }

    /// <summary>Whether the author clicked the message area (<c>:332-344</c>).</summary>
    public bool MessageRequested { get; private set; }

    /// <summary>The number pad to open, or <see langword="null"/>.</summary>
    public DailyEventNumpadRequest? NumpadRequest { get; private set; }

    /// <summary>The "set the amount" dialog to open, or <see langword="null"/>.</summary>
    public DailyEventResourceRequest? ResourceCountRequest { get; private set; }

    /// <summary>Replaces the message, <c>eventMetadata.message = std::move( temp )</c> (<c>:337</c>).</summary>
    /// <param name="message">The text the author typed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void SetMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _event.Message = message.Length > MessageCharLimit ? message[..MessageCharLimit] : message;
    }

    /// <summary>Writes the amount of one resource, <c>*resourcePtr = temp</c> (<c>:321</c>).</summary>
    /// <param name="resourceType">One of the <see cref="Resource"/> flags.</param>
    /// <param name="value">The amount the author chose.</param>
    public void SetResourceCount(int resourceType, int value)
    {
        Funds resources = _event.Resources;
        Funds.GetPtr(ref resources, resourceType) = Math.Clamp(value, ResourceAmountMinimum, ResourceAmountMaximum);
        _event.Resources = resources;
    }

    /// <summary>
    /// Writes one of the two numbers after the host's number pad returned (<c>:223</c>、<c>:236</c>).
    /// </summary>
    /// <param name="field">Which number to write.</param>
    /// <param name="value">The number the author typed.</param>
    public void SetValueField(DailyEventValueField field, int value)
    {
        switch (field)
        {
            case DailyEventValueField.FirstOccurrenceDay:
                _event.FirstOccurrenceDay = (uint)Math.Clamp(value, 1, (int)MapDailyEvent.LastDayForEvents);
                _firstDaySelection.Value = (int)_event.FirstOccurrenceDay;

                break;

            case DailyEventValueField.RepeatPeriodInDays:
                _event.RepeatPeriodInDays = (uint)Math.Clamp(value, 0, (int)MapDailyEvent.DaysInYear);
                _repeatPeriodSelection.Value = (int)_event.RepeatPeriodInDays;

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Draws the whole window, the <c>display.render()</c> of <c>:211</c> and the element draws around it.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <param name="events">The events of this frame, for the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(events);

        _window.Draw(target);

        GlyphText.DrawLine(_headerFont, target, _titlePosition, Title);

        StandardWindow.ApplyTextBackgroundShading(target, _messageRoi);
        StandardWindow.ApplyTextBackgroundShading(target, _resourceRoi);

        GlyphText.DrawLine(_bodyFont, target, _messageLabelPosition, MessageLabel);
        GlyphText.DrawLine(_bodyFont, target, _rewardLabelPosition, RewardLabel);

        DrawMessageText(target);

        EditorUiHelper.RenderResources(_event.Resources, _resourceRoi, target, _individualResourceRoi);

        // 复选框：先贴方框，再贴勾（勾与方框同在缓冲坐标里）。
        ImageOps.Blit(target, _checkboxLayerOrigin, _checkboxLayer,
            new Rect(0, 0, _checkboxLayer.Width, _checkboxLayer.Height));

        foreach (Checkbox checkbox in _humanCheckboxes)
        {
            PaintCheckmark(target, checkbox);
        }

        foreach (Checkbox checkbox in _computerCheckboxes)
        {
            PaintCheckmark(target, checkbox);
        }

        DrawLabel(target, _humanLabelPosition, HumanColorsLabel, _humanLabelWidth);
        DrawLabel(target, _computerLabelPosition, ComputerColorsLabel, _computerLabelWidth);
        DrawLabel(target, _firstDayLabelPosition, FirstDayLabel, _firstDayLabelWidth);
        DrawLabel(target, _repeatLabelPosition, RepeatPeriodLabel, _repeatLabelWidth);

        _firstDaySelection.Draw(target);
        _repeatPeriodSelection.Draw(target);

        string description = GameDate.Describe((int)_event.FirstOccurrenceDay);
        int descriptionWidth = GlyphText.Measure(_bodyFont, description);

        GlyphText.DrawLine(_bodyFont, target,
            new Point(_dateDescriptionRoi.X + ((_dateDescriptionRoi.Width - descriptionWidth) / 2),
                _dateDescriptionRoi.Y), description, _dateDescriptionRoi);

        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
    }

    /// <summary>
    /// Handles one frame of events, the body of the <c>while ( le.HandleEvents() )</c> loop of <c>:216-370</c>.
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The result of the frame; <see cref="EditorListOutcome.None"/> while the window stays open.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public EditorListOutcome Process(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        Result = EditorListOutcome.None;
        Popup = null;
        MessageRequested = false;
        NumpadRequest = null;
        ResourceCountRequest = null;

        // 参考 :220-239：先让两个数字控件吃事件，改了就写回事件。
        ProcessValueSelection(_firstDaySelection, DailyEventValueField.FirstOccurrenceDay, events);
        ProcessValueSelection(_repeatPeriodSelection, DailyEventValueField.RepeatPeriodInDays, events);

        // 参考 :242-255：然后才是 OKAY/CANCEL。
        if (events.MouseClickLeft(_cancel.Area))
        {
            Result = EditorListOutcome.Cancelled;

            return Result;
        }

        if (events.MouseClickLeft(_okay.Area))
        {
            Result = EditorListOutcome.Accepted;

            return Result;
        }

        // 参考 :258-283：人类的颜色框。
        if (ProcessColorCheckboxes(_humanCheckboxes, isHuman: true, events))
        {
            return Result;
        }

        // 参考 :286-312：电脑的颜色框。
        if (ProcessColorCheckboxes(_computerCheckboxes, isHuman: false, events))
        {
            return Result;
        }

        // 参考 :314-330：资源图标，点一下改数量、右键看清单。
        for (int i = 0; i < ResourceTypes.Length; ++i)
        {
            if (!events.MouseClickLeft(_individualResourceRoi[i]))
            {
                continue;
            }

            int resourceType = ResourceTypes[i];

            ResourceCountRequest = new DailyEventResourceRequest(resourceType, _event.Resources.Get(resourceType),
                SelectCountDialog.Create(string.Format(ResourceCountLabelFormat, ResourceText.Of(resourceType)),
                    ResourceAmountMinimum, ResourceAmountMaximum, 1));

            return Result;
        }

        // 参考 :332-344：消息区，点一下改文本。
        if (events.MouseClickLeft(_messageRoi))
        {
            MessageRequested = true;

            return Result;
        }

        // 参考 :346-368：其余右键说明。
        if (events.IsMouseRightButtonPressedInArea(_cancel.Area))
        {
            Popup = new DialogPopup(CancelTitle, CancelMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_okay.Area))
        {
            Popup = new DialogPopup(OkayTitle, OkayMessage);
        }
        else if (events.IsMouseRightButtonPressedInArea(_resourceRoi))
        {
            Popup = CreateResourcesPopup();
        }
        else if (events.IsMouseRightButtonPressedInArea(_messageRoi))
        {
            Popup = new DialogPopup(MessageAreaTitle, MessageAreaMessage);
        }

        return Result;
    }

    /// <summary>
    /// Measures a two-line label of the right column and moves <paramref name="y"/> past it
    /// (<c>while ( text.rows( textWidth ) &lt; 2 )</c> of <c>:122-127</c>).
    /// </summary>
    /// <remarks>
    /// 参考靠不断缩窄宽度逼出两行，遇到一个词就永远出不来第二行，所以这里多加 <c>width &gt; 1</c> 的护栏。
    /// </remarks>
    private static Point CreateLabel(IGlyphSource font, string text, int playerAreaX, ref int y, out int width)
    {
        int labelWidth = PlayerAreaWidth;

        while (labelWidth > 1 && GlyphText.WrapLines(font, text, labelWidth).Count < 2)
        {
            labelWidth = (labelWidth * 2) / 3;
        }

        Point position = new(playerAreaX + ((PlayerAreaWidth - labelWidth) / 2), y);

        width = labelWidth;
        y += LabelBottomGap + (GlyphText.WrapLines(font, text, labelWidth).Count * font.LineHeight);

        return position;
    }

    private void DrawLabel(PixelBuffer target, Point position, string text, int width) =>
        GlyphText.DrawMultiline(_bodyFont, target, position, text, width);

    /// <summary>Draws the message inside its area, the <c>text.drawInRoi()</c> of <c>:97-104</c>.</summary>
    private void DrawMessageText(PixelBuffer target)
    {
        if (string.IsNullOrEmpty(_event.Message))
        {
            return;
        }

        Rect roi = new(_messageRoi.X + 5, _messageRoi.Y + 5, _messageRoi.Width - 10, _messageRoi.Height - 10);
        int y = roi.Y;

        foreach (string line in GlyphText.WrapLines(_bodyFont, _event.Message, roi.Width))
        {
            if (y + _bodyFont.LineHeight > roi.Bottom)
            {
                break;
            }

            GlyphText.DrawLine(_bodyFont, target, new Point(roi.X, y), line, roi);
            y += _bodyFont.LineHeight;
        }
    }

    private void PaintCheckmark(PixelBuffer target, Checkbox checkbox)
    {
        MovableSprite checkmark = checkbox.Checkmark;

        if (checkmark.IsHidden || checkmark.Sprite is null)
        {
            return;
        }

        ImageOps.BlitSprite(target, new Point(_checkboxLayerOrigin.X + checkmark.X, _checkboxLayerOrigin.Y + checkmark.Y),
            checkmark.Sprite);
    }

    private Point ToLayer(Point position) => new(position.X - _checkboxLayerOrigin.X, position.Y - _checkboxLayerOrigin.Y);

    private void ProcessValueSelection(ValueSelectionElement element, DailyEventValueField field, LocalEvent events)
    {
        ValueSelectionFrame frame = element.ProcessEvents(events);

        if (frame.Changed)
        {
            SetValueField(field, element.Value);
        }

        switch (frame.Request)
        {
            case ValueSelectionRequest.Numpad:
                NumpadRequest = new DailyEventNumpadRequest(field, element.Minimum, element.Maximum, element.Value);

                break;

            case ValueSelectionRequest.Range:
                Popup = new DialogPopup(OkayTitle,
                    string.Format(RangeMessageFormat, element.Minimum, element.Maximum));

                break;

            default:
                break;
        }
    }

    /// <summary>Handles one group of colour checkboxes, the loops of <c>:258-312</c>.</summary>
    /// <returns><see langword="true"/> when a right click opened a hint.</returns>
    private bool ProcessColorCheckboxes(List<Checkbox> checkboxes, bool isHuman, LocalEvent events)
    {
        for (int i = 0; i < checkboxes.Count; ++i)
        {
            Checkbox checkbox = checkboxes[i];

            if (events.MouseClickLeft(checkbox.Area))
            {
                // 参考 :269-273：勾上的补进颜色集，取消勾的从颜色集里去掉。
                bool isChecked = checkbox.Toggle();

                if (isHuman)
                {
                    _event.HumanPlayerColors = isChecked
                        ? _event.HumanPlayerColors | checkbox.Color
                        : _event.HumanPlayerColors ^ checkbox.Color;
                }
                else
                {
                    _event.ComputerPlayerColors = isChecked
                        ? _event.ComputerPlayerColors | checkbox.Color
                        : _event.ComputerPlayerColors ^ checkbox.Color;
                }

                break;
            }

            if (events.IsMouseRightButtonPressedInArea(checkbox.Area))
            {
                string color = PlayerColors.GetName(checkbox.Color);
                string title = string.Format(isHuman ? HumanColorTitleFormat : ComputerColorTitleFormat, color);
                string message = string.Format(isHuman ? HumanColorMessageFormat : ComputerColorMessageFormat, color);

                Popup = new DialogPopup(title, message);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The hint of the reward panel, <c>:353-360</c>.
    /// </summary>
    /// <remarks>
    /// 参考有资源时弹的是一个画着 7 个资源图标的窗口（<c>showResourceMessage()</c>），端口还没有那个窗口，
    /// 因此把「给了什么」按文本列在同一个提示里。
    /// </remarks>
    private DialogPopup CreateResourcesPopup()
    {
        if (_event.Resources.GetValidItemsCount() == 0)
        {
            return new DialogPopup(ResourcesTitle, NoResourcesMessage);
        }

        List<string> parts = [];

        foreach (int resourceType in ResourceTypes)
        {
            int amount = _event.Resources.Get(resourceType);

            if (amount != 0)
            {
                parts.Add($"{ResourceText.Of(resourceType)}: {amount}");
            }
        }

        return new DialogPopup(ResourcesTitle, ResourcesMessage + "\n" + string.Join(", ", parts));
    }

    private static void PaintButton(ButtonSprite button, PixelBuffer target, LocalEvent events)
    {
        ImageOps.AddGradientShadow(button.Released, target, button.Area.Position, new Point(-5, 5));

        if (button.IsEnabled)
        {
            button.DrawOnState(events.IsMouseLeftButtonPressedAndHeldInArea(button.Area), target);
        }
        else
        {
            button.Draw(target);
        }
    }
}
