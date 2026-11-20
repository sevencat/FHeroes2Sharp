using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where everything in the recruit window goes, <c>Dialog::RecruitMonster()</c>
/// (<c>dialog_recruit.cpp:273-384</c>, <c>:96-221</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every number in the reference is an offset from <c>dialogOffset</c> - the top left of the <c>299x272</c> active area -
/// except the ones of <c>RedrawMonsterInfo</c>, whose <c>pos</c> is the same point expressed as the active area rect.
/// The two agree, so this class anchors everything on the one pair <see cref="ActiveArea"/> carries.
/// </para>
/// <para>
/// The reference also passes <c>windowOffsetY</c> into the dialog offset so the dwelling room can push the window down;
/// that is the caller's business here, it only has to hand over the area it wants the window in.
/// </para>
/// </remarks>
public sealed class RecruitWindowLayout
{
    /// <summary>Size of the area the window's content is drawn into, <c>windowSize</c>.</summary>
    public static readonly Size WindowSize = new(299, 272);

    /// <summary>Size of the count box slice of <c>ICN::RECRBKG</c>, <c>background</c>.</summary>
    public static readonly Size CountBoxSize = new(68, 19);

    /// <summary>The margin the two bottom buttons keep from the sides of the window, <c>backgroundMargin</c>.</summary>
    public const int BackgroundMargin = 18;

    private const int CountBoxX = 118;
    private const int CountBoxY = 143;
    private const int CountTextY = 147;
    private const int CountTextCentreX = 151;

    private const int CostFrameX = 132;
    private const int CostFrameY = 38;

    private const int OkayButtonY = 233;
    private const int MaxMinCentreX = 253;
    private const int MaxMinY = 140;

    private const int ArrowX = 189;
    private const int UpArrowY = 138;
    private const int DownArrowY = 153;
    private const int WheelX = 114;
    private const int WheelY = 139;
    private const int WheelWidth = 100;
    private const int WheelHeight = 30;

    private const int SwitchLeftX = 6;
    private const int SwitchRightX = 105;
    private const int SwitchY = 64;

    private const int MonsterAreaX = 24;
    private const int MonsterAreaY = 19;
    private const int MonsterAreaWidth = 75;
    private const int MonsterAreaHeight = 95;

    private const int TitleY = 11;
    private const int MonsterSpriteCentreX = 64;
    private const int MonsterSpriteBaselineY = 119;
    private const int MonsterSpriteSpace = 96;

    private const int AvailableY = 120;
    private const int AvailableCentreX = 64;
    private const int NumberToBuyY = 149;
    private const int NumberToBuyRightX = 107;

    private const int GoldLineY = 214;
    private const int GoldLineCentreX = 144;
    private const int GoldLineTwoResourcesCentreX = 117;
    private const int ExtraLineCentreX = 179;
    private const int LabelY = 166;

    private const int TotalSumShiftX = 10;
    private const int TotalSumIconShiftX = 45;
    private const int TotalSumIconShiftY = 125;

    private RecruitWindowLayout(Rect activeArea)
    {
        ActiveArea = activeArea;
    }

    /// <summary>Anchors the layout on the area the content is drawn into.</summary>
    /// <param name="activeArea">The <c>299x272</c> area of the window, <c>window.activeArea()</c>.</param>
    /// <returns>The layout.</returns>
    public static RecruitWindowLayout Create(Rect activeArea) => new(activeArea);

    /// <summary>The area the content is drawn into, <c>window.activeArea()</c>.</summary>
    public Rect ActiveArea { get; }

    /// <summary>Where the count box is blitted, <c>{ dialogOffset.x + 118, dialogOffset.y + 143 }</c>.</summary>
    public Point CountBoxPosition => new(ActiveArea.X + CountBoxX, ActiveArea.Y + CountBoxY);

    /// <summary>
    /// The count box as the reference spells it out for the numpad click,
    /// <c>Rect( pos.x + 118, pos.y + 143, 68, 19 )</c> (<c>:581</c>).
    /// </summary>
    public Rect CountBoxArea => new(ActiveArea.X + CountBoxX, ActiveArea.Y + CountBoxY, CountBoxSize.Width,
        CountBoxSize.Height);

    /// <summary>Centre of the count digits, the <c>pos.x + 151</c> of <c>RedrawCurrentInfo</c>.</summary>
    public Point CountTextCentre => new(ActiveArea.X + CountTextCentreX, ActiveArea.Y + CountTextY);

    /// <summary>Where the cost per troop frame goes, <c>{ dialogOffset.x + 132, dialogOffset.y + 38 }</c>.</summary>
    public Point CostFramePosition => new(ActiveArea.X + CostFrameX, ActiveArea.Y + CostFrameY);

    /// <summary>Where the OKAY button goes, <c>{ dialogOffset.x + 18, dialogOffset.y + 233 }</c>.</summary>
    public Point OkayButtonPosition => new(ActiveArea.X + BackgroundMargin, ActiveArea.Y + OkayButtonY);

    /// <summary>Where the CANCEL button goes, right aligned with the same margin, <c>:313</c>.</summary>
    /// <param name="width">Width of the cancel button's released frame.</param>
    /// <returns>The top left corner.</returns>
    public Point CancelButtonPosition(int width) =>
        new(ActiveArea.X + WindowSize.Width - BackgroundMargin - width, ActiveArea.Y + OkayButtonY);

    /// <summary>Where the MAX and MIN buttons go, both centred on <c>x + 253</c>, <c>:319</c>.</summary>
    /// <param name="width">Width of the button's released frame.</param>
    /// <returns>The top left corner.</returns>
    public Point MaxMinButtonPosition(int width) =>
        new(ActiveArea.X + MaxMinCentreX - (width / 2), ActiveArea.Y + MaxMinY);

    /// <summary>Where the up arrow goes, <c>{ dialogOffset.x + 189, dialogOffset.y + 138 }</c>.</summary>
    public Point UpButtonPosition => new(ActiveArea.X + ArrowX, ActiveArea.Y + UpArrowY);

    /// <summary>Where the down arrow goes, <c>{ dialogOffset.x + 189, dialogOffset.y + 153 }</c>.</summary>
    public Point DownButtonPosition => new(ActiveArea.X + ArrowX, ActiveArea.Y + DownArrowY);

    /// <summary>Area the mouse wheel changes the count in, <c>rtWheel</c>.</summary>
    public Rect WheelArea => new(ActiveArea.X + WheelX, ActiveArea.Y + WheelY, WheelWidth, WheelHeight);

    /// <summary>Where the left switch arrow goes, <c>{ dialogOffset.x + 6, dialogOffset.y + 64 }</c>.</summary>
    public Point SwitchLeftPosition => new(ActiveArea.X + SwitchLeftX, ActiveArea.Y + SwitchY);

    /// <summary>Where the right switch arrow goes, <c>{ dialogOffset.x + 105, dialogOffset.y + 64 }</c>.</summary>
    public Point SwitchRightPosition => new(ActiveArea.X + SwitchRightX, ActiveArea.Y + SwitchY);

    /// <summary>The unit the dialog offers is drawn into this area, <c>monsterArea</c>.</summary>
    public Rect MonsterArea => new(ActiveArea.X + MonsterAreaX, ActiveArea.Y + MonsterAreaY, MonsterAreaWidth,
        MonsterAreaHeight);

    /// <summary>Where the title is drawn so that it is centred over the window, <c>:165</c>.</summary>
    /// <param name="textWidth">Width of the title.</param>
    /// <returns>The top left corner of the text.</returns>
    public Point TitlePosition(int textWidth) => new(ActiveArea.X + ((WindowSize.Width - textWidth) / 2),
        ActiveArea.Y + TitleY);

    /// <summary>
    /// The extra pixels a sprite taller than 96 pixels hangs above the usual line, <c>monsterExtraOffsetY</c>.
    /// </summary>
    /// <param name="spriteHeight">Height of the unit sprite.</param>
    /// <returns>The offset, zero for the usual sized sprites.</returns>
    public static int MonsterExtraOffsetY(int spriteHeight) => Math.Max(0, spriteHeight - MonsterSpriteSpace);

    /// <summary>Where the unit sprite is drawn, <c>:174-182</c>.</summary>
    /// <param name="sprite">The unit's static frame.</param>
    /// <param name="isWide">Whether the unit is a wide one, drawn 22 pixels to the left.</param>
    /// <param name="isChampion">Whether the unit is the champion, which the reference nudges one pixel right.</param>
    /// <returns>The top left corner of the sprite.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sprite"/> is <see langword="null"/>.</exception>
    public Point MonsterSpritePosition(Sprite sprite, bool isWide, bool isChampion)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        int extraOffsetY = MonsterExtraOffsetY(sprite.Height);

        return new Point(
            ActiveArea.X + MonsterSpriteCentreX + sprite.OffsetX - (isWide ? 22 : 0) + (isChampion ? 1 : 0),
            ActiveArea.Y + MonsterSpriteBaselineY - sprite.Height + extraOffsetY);
    }

    /// <summary>Where the "Available: N" line is centred, <c>:215</c>.</summary>
    /// <param name="spriteHeight">Height of the unit sprite, which pushes the line down for tall units.</param>
    /// <returns>The centre of the text.</returns>
    public Point AvailableCentre(int spriteHeight) => new(ActiveArea.X + AvailableCentreX,
        ActiveArea.Y + AvailableY + Math.Max(MonsterExtraOffsetY(spriteHeight), 2));

    /// <summary>Where "Number to buy:" is right aligned above the count box, <c>:219</c>.</summary>
    /// <param name="textWidth">Width of the caption.</param>
    /// <returns>The top left corner of the text.</returns>
    public Point NumberToBuyPosition(int textWidth) =>
        new(ActiveArea.X + NumberToBuyRightX - textWidth, ActiveArea.Y + NumberToBuyY);

    /// <summary>Where the gold line under the count is centred, <c>:117-125</c>.</summary>
    /// <param name="withSecondResource">Whether a second resource line is drawn next to it.</param>
    /// <returns>The centre of the text.</returns>
    public Point GoldLineCentre(bool withSecondResource) =>
        new(ActiveArea.X + (withSecondResource ? GoldLineTwoResourcesCentreX : GoldLineCentreX), ActiveArea.Y + GoldLineY);

    /// <summary>Where the line of the second resource is centred, <c>:121</c>.</summary>
    public Point ExtraLineCentre => new(ActiveArea.X + ExtraLineCentreX, ActiveArea.Y + GoldLineY);

    /// <summary>Where the MAX/MIN label is centred, <c>:132</c>.</summary>
    public Point LabelCentre => new(ActiveArea.X + CountTextCentreX, ActiveArea.Y + LabelY);

    /// <summary>Where a resource icon of the price is drawn, <c>RedrawResourceInfo</c>.</summary>
    /// <param name="x">The <c>px1</c> of the reference.</param>
    /// <param name="y">The <c>py1</c> of the reference.</param>
    /// <param name="showTotalSum">Whether the shift for the total sum icon applies.</param>
    /// <returns>The top left corner of the icon.</returns>
    public Point ResourceIconPosition(int x, int y, bool showTotalSum) =>
        new(ActiveArea.X + x + (showTotalSum ? TotalSumShiftX : 0), ActiveArea.Y + y);

    /// <summary>Where the amount of a resource is centred, <c>px2</c>/<c>py2</c> of the reference.</summary>
    /// <param name="x">The <c>px2</c> of the reference.</param>
    /// <param name="y">The <c>py2</c> of the reference.</param>
    /// <param name="showTotalSum">Whether the shift for the total sum icon applies.</param>
    /// <returns>The centre of the text.</returns>
    public Point ResourceTextCentre(int x, int y, bool showTotalSum) =>
        new(ActiveArea.X + x + (showTotalSum ? TotalSumShiftX : 0), ActiveArea.Y + y);

    /// <summary>Where the icon of the total sum is drawn for the alternate resource, <c>:151</c>.</summary>
    /// <param name="x">The <c>px1</c> of the icon it repeats.</param>
    /// <param name="y">The <c>py1</c> of the icon it repeats.</param>
    /// <returns>The top left corner of the icon.</returns>
    public Point TotalSumIconPosition(int x, int y) =>
        new(ActiveArea.X + x - TotalSumIconShiftX, ActiveArea.Y + y + TotalSumIconShiftY);
}
