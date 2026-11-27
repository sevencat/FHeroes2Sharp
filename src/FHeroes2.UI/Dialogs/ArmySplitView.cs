using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The split window, <c>Dialog::ArmySplitTroop()</c> (<c>dialog_selectcount.cpp:462-651</c>): a number, a row of
/// slot icons and OK/CANCEL.
/// </summary>
/// <remarks>
/// <para>
/// 参考把两件事放在一扇窗里问：「移动几个兵」是那个数字框，「拆成几格」是数字框下面那排 <c>REQUESTS</c> 图标
/// （各格的兵数示意）。点一格就是选定拆成 N 格，被选的那格套一圈 214 号色的框；不点任何格就按「只拆两格、按数字框
/// 里的数量移动」处理（<c>:629-648</c>）。
/// </para>
/// <para>
/// <b>与参考的出入</b>：MAX / MIN 两颗快捷键没转（<c>UNIFORM_GOOD_MIN_BUTTON</c> 是运行期合成的，归档里没有，
/// 与 <see cref="SelectCountView"/> 同一个理由），数字框的键盘直输、滚轮与长按重复也没转——这一层只吃
/// <see cref="SetLeftButton"/> 的一次点击。
/// </para>
/// </remarks>
public sealed class ArmySplitView
{
    /// <summary>Gap under the unit name, <c>headerHeight = header.height() + 6</c>.</summary>
    public const int HeaderGap = 6;

    /// <summary>Gap between the window top and the question, <c>textTopOffset = 13</c>.</summary>
    public const int TextTopOffset = 13;

    /// <summary>Gap between the question and the slot row, <c>63 + spacer</c> minus the two text heights.</summary>
    public const int SlotRowGap = 45;

    /// <summary>Distance from the question to the slot caption, <c>37</c> of the reference.</summary>
    public const int SlotCaptionOffsetY = 37;

    /// <summary>Left inset of the number, <c>pos.x + 70</c>.</summary>
    public const int ValueInsetX = 70;

    /// <summary>Horizontal step between two slot icons, <c>deltaX = 10</c>.</summary>
    public const int SlotStepX = 10;

    /// <summary>Horizontal shift of the slot row per extra slot, <c>deltaXStart = ( freeSlots - 2 ) * -5</c>.</summary>
    public const int SlotStartShiftX = -5;

    /// <summary>Palette index of the frame around the chosen slot, <c>fheroes2::DrawBorder( ssp, 214 )</c>.</summary>
    public const byte SelectionBorderColor = 214;

    /// <summary>Height the layout reserves for the number, <c>selectionAreaHeight = 30</c>.</summary>
    public const int SelectionAreaHeight = 30;

    /// <summary>The question above the number, <c>_( "How many creatures do you wish to move?" )</c>.</summary>
    public const string MoveQuestion = "How many creatures do you wish to move?";

    /// <summary>The caption above the slot row, <c>_( "Select how many units to separate into:" )</c>.</summary>
    public const string SlotCaption = "Select how many units to separate into:";

    private readonly ArmySplitDialog _dialog;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _textFont;
    private readonly FrameBox _box;
    private readonly ValueSelectionElement _value;
    private readonly ButtonGroup _buttons;
    private readonly List<Sprite> _slotIcons;
    private readonly List<Rect> _slotAreas = [];
    private readonly Rect _captionPosition;

    private int? _selectedSlot;

    private ArmySplitView(ArmySplitDialog dialog, IGlyphSource headerFont, IGlyphSource textFont, FrameBox box,
        ValueSelectionElement value, ButtonGroup buttons, List<Sprite> slotIcons, Rect captionPosition)
    {
        _dialog = dialog;
        _headerFont = headerFont;
        _textFont = textFont;
        _box = box;
        _value = value;
        _buttons = buttons;
        _slotIcons = slotIcons;
        _captionPosition = captionPosition;
        _selectedSlot = dialog.UseFastSplit && slotIcons.Count > 0 ? 0 : null;
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="dialog">The question: how many slots, how many units, the unit's name and the two start values.</param>
    /// <param name="buttonArtwork">OK and CANCEL artwork.</param>
    /// <param name="headerFont">Font of the unit name, <c>FontType::normalYellow()</c>.</param>
    /// <param name="textFont">Font of the two captions, <c>FontType::normalWhite()</c>.</param>
    /// <param name="townWindowFrames">
    /// Frames of <c>TOWNWIND.ICN</c>, at least <see cref="ValueSelectionElement.RequiredFrameCount"/>.
    /// </param>
    /// <param name="slotIcons">
    /// The <c>REQUESTS</c> icons of the slot row, one per way of splitting, i.e.
    /// <c>dialog.FreeSlots - 1</c> of them starting at frame 21.
    /// </param>
    /// <param name="screen">Screen the window is centred on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The window, or <see langword="null"/> when an asset is missing — the frame artwork included.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ArmySplitView? TryCreate(ArmySplitDialog dialog, IDialogButtonArtworkSource buttonArtwork,
        IGlyphSource headerFont, IGlyphSource textFont, IReadOnlyList<Sprite> townWindowFrames,
        IReadOnlyList<Sprite> slotIcons, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(buttonArtwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(textFont);
        ArgumentNullException.ThrowIfNull(townWindowFrames);
        ArgumentNullException.ThrowIfNull(slotIcons);

        if (townWindowFrames.Count < ValueSelectionElement.RequiredFrameCount || !FrameBox.IsArtworkAvailable)
        {
            return null;
        }

        // 槽位那排是 freeSlots - 1 张图；只有一个槽时参考连那排都不画（dialog_selectcount.cpp:487-490）。
        if (slotIcons.Count < Math.Max(0, dialog.FreeSlots - 1))
        {
            return null;
        }

        int headerHeight = headerFont.LineHeight + HeaderGap;
        int titleHeight = headerHeight + GlyphText.WrapLines(textFont, MoveQuestion, FrameBox.AreaWidth).Count *
            textFont.LineHeight;
        int captionHeight = GlyphText.WrapLines(textFont, SlotCaption, FrameBox.AreaWidth).Count * textFont.LineHeight;

        // 参考：freeSlots > 1 时 63 + spacer + 标题 + 说明，否则 45（连槽位那排都不画）。
        int boxHeight = dialog.FreeSlots > 1 ? 63 + TextTopOffset + titleHeight + captionHeight : 45;

        Size surface = screen ?? ScreenBuffer.DefaultSize;
        int startY = 160 + ((surface.Height - ScreenBuffer.DefaultSize.Height) / 2) - boxHeight;

        FrameBox box = FrameBox.Create(boxHeight, showButtons: true, startY: startY, screen: surface);
        Rect area = box.Area;

        ValueSelectionElement value = new(
            dialog.Minimum, dialog.RedistributeMaximum, dialog.InitialCount, step: 1,
            new Point(area.X + ValueInsetX, area.Y + TextTopOffset + titleHeight), textFont, townWindowFrames);

        Rect captionPosition = new(area.X, area.Y + TextTopOffset + titleHeight + SlotCaptionOffsetY,
            FrameBox.AreaWidth, captionHeight);

        ButtonGroup buttons = ButtonGroup.CreateStandard(box.Area, DialogButton.Ok | DialogButton.Cancel,
            buttonArtwork);

        List<Sprite> icons = new(slotIcons.Count);

        for (int index = 0; index < slotIcons.Count; ++index)
        {
            icons.Add(slotIcons[index]);
        }

        ArmySplitView view = new(dialog, headerFont, textFont, box, value, buttons, icons, captionPosition);

        view.SetSlotAreas(area, titleHeight, captionHeight);

        return view;
    }

    /// <summary>The question being asked.</summary>
    public ArmySplitDialog Dialog => _dialog;

    /// <summary>Text area of the frame, <c>GetArea()</c> of the reference.</summary>
    public Rect Area => _box.Area;

    /// <summary>Bounding box of the frame artwork.</summary>
    public Rect FrameArea => _box.FrameArea;

    /// <summary>The number element.</summary>
    public ValueSelectionElement Value => _value;

    /// <summary>The OK and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>
    /// The slot icons, one per way of splitting, i.e. the index <c>i</c> stands for <c>i + 2</c> slots.
    /// </summary>
    public IReadOnlyList<Rect> SlotAreas => _slotAreas;

    /// <summary>The number of the slot row that is selected, or <see langword="null"/> when none is.</summary>
    public int? SelectedSlot => _selectedSlot;

    /// <summary>
    /// How many slots the split goes into, the <c>slots</c> the reference returns: the chosen row plus two, or two
    /// when no row was chosen (<c>dialog_selectcount.cpp:629-648</c>).
    /// </summary>
    public int SlotCount => _selectedSlot is { } slot ? slot + 2 : 2;

    /// <summary>
    /// Whether the equal split was chosen, the <c>useFastSplit</c> out parameter of the reference: true only when a
    /// slot row was picked.
    /// </summary>
    public bool UseFastSplit => _selectedSlot is not null;

    /// <summary>The number of units the split moves, <c>valueSelectionElement.getValue()</c>.</summary>
    public int Count => _value.Value;

    /// <summary>
    /// A left button transition over the window: the number's arrows first, then the slot row, then the buttons.
    /// </summary>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>The pressed button on release, or <see cref="DialogButton.None"/> when nothing was pressed.</returns>
    /// <remarks>
    /// 参考点槽位是在按下时就挪框（<c>le.MouseClickLeft( rt )</c> 在循环里每次都对每个槽位判一遍），这里同样在按下
    /// 时选定，抬起不再重复触发。
    /// </remarks>
    public DialogButton SetLeftButton(bool down, Point point)
    {
        _value.SetLeftButton(down, point);
        _buttons.SetCursor(point);

        if (down)
        {
            for (int index = 0; index < _slotAreas.Count; ++index)
            {
                if (_slotAreas[index].Contains(point))
                {
                    _selectedSlot = index;

                    break;
                }
            }
        }

        return _buttons.SetLeftButton(down);
    }

    /// <summary>Answers a standard button hot key.</summary>
    /// <param name="hotKey">Key that was pressed.</param>
    /// <returns>The button the key stands for, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton ProcessHotKey(ButtonHotKey hotKey) => _buttons.ProcessHotKey(hotKey);

    /// <summary>Draws the frame, the name, the question, the number, the slot row and the buttons.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _box.Draw(target);

        Rect area = _box.Area;

        GlyphText.DrawMultiline(_headerFont, target, new Point(area.X, area.Y + 2), _dialog.TroopName,
            FrameBox.AreaWidth);
        GlyphText.DrawMultiline(_textFont, target,
            new Point(area.X, area.Y + 2 + _headerFont.LineHeight + HeaderGap), MoveQuestion, FrameBox.AreaWidth);

        _value.Draw(target);

        if (_slotIcons.Count > 0)
        {
            GlyphText.DrawMultiline(_textFont, target, _captionPosition.Position, SlotCaption, FrameBox.AreaWidth);

            for (int index = 0; index < _slotIcons.Count; ++index)
            {
                ImageOps.BlitSprite(target, _slotAreas[index].Position, _slotIcons[index]);
            }

            if (_selectedSlot is { } selected)
            {
                ImageOps.DrawRect(target, _slotAreas[selected], SelectionBorderColor);
            }
        }

        _buttons.Draw(target);
        _buttons.DrawShadows(target);
    }

    /// <summary>
    /// Lays the slot row out, the <c>vrts[i]</c> loop of <c>dialog_selectcount.cpp:509-529</c>.
    /// </summary>
    /// <param name="area">Text area of the frame.</param>
    /// <param name="titleHeight">Height of the name plus the question.</param>
    /// <param name="captionHeight">Height of the caption above the row.</param>
    private void SetSlotAreas(Rect area, int titleHeight, int captionHeight)
    {
        int count = _slotIcons.Count;

        if (count == 0)
        {
            return;
        }

        int center = area.X + (area.Width / 2);
        int startShift = (count - 1) * SlotStartShiftX;
        int y = area.Y + TextTopOffset + titleHeight + captionHeight + SlotRowGap;

        for (int index = 0; index < count; ++index)
        {
            Sprite icon = _slotIcons[index];
            int offset = icon.Width * ((2 * index) + 1 - (count + 1)) / 2;

            _slotAreas.Add(new Rect(center + offset + startShift + (index * SlotStepX), y, icon.Width,
                icon.Height));
        }
    }
}
