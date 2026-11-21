using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where the parts of the hero quick info box go, <c>showQuickInfo()</c>
/// (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:693-911</c>).
/// </summary>
/// <remarks>
/// <para>
/// 整块底板是 <c>QWIKHERO.ICN</c> 帧 0，内部所有部件都相对一个固定的「内容框」摆放：底板左上角 <c>+28,+10</c>
/// 起、146x144 的一块（参考里 <c>cur_rt</c> 被重写成这个矩形）。名字居中写在内容框顶上 <c>+2</c>，小肖像相框在
/// <c>+12</c>、肖像本体在 <c>+16</c>，两列图标一左（士气 <c>+10</c>）一右（幸运 <c>+120</c>），
/// 数量为 1 时下移 7 像素（<c>+20</c>）让单枚图标垂直居中。旗子在 <c>+13</c>，左旗从 <c>(146-40)/2</c> 往左伸、
/// 右旗从 <c>(146+40)/2</c> 往右伸。四维从相框下沿（<c>+13 + 相框高</c>）开始，每行 11 像素，数值列右移 89。
/// 军队线画在内容框左下角 <c>-7,+118</c>，可用宽度 160。
/// </para>
/// <para>
/// 底板的高度（160）与宽度（236）不参与算术，只有内容框的位置是常数，所以这里把底板矩形直接当输入。
/// </para>
/// </remarks>
public sealed class HeroQuickInfoLayout
{
    /// <summary>How far the content box sits from the box's left edge, <c>restorer.x() + 28</c>.</summary>
    public const int ContentOffsetX = 28;

    /// <summary>How far the content box sits from the box's top edge, <c>restorer.y() + 10</c>.</summary>
    public const int ContentOffsetY = 10;

    /// <summary>Width of the content box, <c>146</c>.</summary>
    public const int ContentWidth = 146;

    /// <summary>Height of the content box, <c>144</c>.</summary>
    public const int ContentHeight = 144;

    /// <summary>Distance from the content box's top to the name, <c>cur_rt.y + 2</c>.</summary>
    public const int NameOffsetY = 2;

    /// <summary>Distance from the content box's top to the small portrait's frame, <c>cur_rt.y + 12</c>.</summary>
    public const int PortraitFrameOffsetY = 12;

    /// <summary>Distance from the content box's top to the small portrait itself, <c>cur_rt.y + 16</c>.</summary>
    public const int PortraitOffsetY = 16;

    /// <summary>Left edge of the luck column, <c>cur_rt.x + 120</c>.</summary>
    public const int LuckOffsetX = 120;

    /// <summary>Left edge of the morale column, <c>cur_rt.x + 10</c>.</summary>
    public const int MoraleOffsetX = 10;

    /// <summary>Top edge of a column of several icons, <c>cur_rt.y + 13</c>.</summary>
    public const int IconOffsetY = 13;

    /// <summary>Top edge of a single icon, <c>cur_rt.y + 20</c>, so that it sits in the middle of the column.</summary>
    public const int SingleIconOffsetY = 20;

    /// <summary>Top edge of the two colour flags, <c>cur_rt.y + 13</c>.</summary>
    public const int FlagOffsetY = 13;

    /// <summary>Gap the two flags leave in the middle, <c>( cur_rt.width ± 40 ) / 2</c>.</summary>
    public const int FlagGap = 40;

    /// <summary>Left edge of the stat labels, <c>cur_rt.x + 10</c>.</summary>
    public const int StatOffsetX = 10;

    /// <summary>Distance between two stat rows, <c>statRow</c> (11).</summary>
    public const int StatRowHeight = 11;

    /// <summary>Distance from a stat label to its value, <c>statNumberColumn</c> (89).</summary>
    public const int StatNumberColumn = 89;

    /// <summary>How far the army line starts left of the content box, <c>cur_rt.x - 7</c>.</summary>
    public const int ArmyOffsetX = -7;

    /// <summary>Top edge of the army line, <c>cur_rt.y + 118</c>.</summary>
    public const int ArmyOffsetY = 118;

    /// <summary>Room the army line may use, <c>160</c>.</summary>
    public const int ArmyLineWidth = 160;

    /// <summary>Number of stat rows the box shows: the four skills, spell points and move points.</summary>
    public const int StatRowCount = 6;

    private readonly Size _portraitFrameSize;
    private readonly Size _flagSize;

    private HeroQuickInfoLayout(Rect box, Size portraitFrameSize, Size flagSize)
    {
        _portraitFrameSize = portraitFrameSize;
        _flagSize = flagSize;
        Box = box;
        Content = new Rect(box.X + ContentOffsetX, box.Y + ContentOffsetY, ContentWidth, ContentHeight);
    }

    /// <summary>Bounding box of the whole box, <c>QWIKHERO.ICN</c> frame 0.</summary>
    public Rect Box { get; }

    /// <summary>The content box the parts are placed in, <c>cur_rt</c> of the reference.</summary>
    public Rect Content { get; }

    /// <summary>Top-left corner of the name; the text itself is centred over the content box.</summary>
    public Point Name => new(Content.X, Content.Y + NameOffsetY);

    /// <summary>Top-left corner of the small portrait's frame.</summary>
    public Point PortraitFrame =>
        new(Content.X + ((Content.Width - _portraitFrameSize.Width) / 2), Content.Y + PortraitFrameOffsetY);

    /// <summary>Top-left corner of the small portrait, inside its frame.</summary>
    /// <param name="size">Size of the portrait sprite.</param>
    /// <returns>Top-left corner of the portrait.</returns>
    public Point GetPortrait(Size size) =>
        new(Content.X + ((Content.Width - size.Width) / 2), Content.Y + PortraitOffsetY);

    /// <summary>Top-left corner of the first luck icon.</summary>
    /// <param name="count">How many icons the luck value asks for.</param>
    /// <returns>Top-left corner of the column.</returns>
    public Point GetLuck(int count) => new(Content.X + LuckOffsetX, GetIconTop(count));

    /// <summary>Top-left corner of the first morale icon.</summary>
    /// <param name="count">How many icons the morale value asks for.</param>
    /// <returns>Top-left corner of the column.</returns>
    public Point GetMorale(int count) => new(Content.X + MoraleOffsetX, GetIconTop(count));

    /// <summary>Top-left corner of the left flag.</summary>
    public Point LeftFlag =>
        new(Content.X + ((Content.Width - FlagGap) / 2) - _flagSize.Width, Content.Y + FlagOffsetY);

    /// <summary>Top-left corner of the right flag.</summary>
    public Point RightFlag => new(Content.X + ((Content.Width + FlagGap) / 2), Content.Y + FlagOffsetY);

    /// <summary>Top-left corner of the first stat label.</summary>
    public Point Stats => new(Content.X + StatOffsetX, Content.Y + FlagOffsetY + _portraitFrameSize.Height);

    /// <summary>Top-left corner of a stat's label.</summary>
    /// <param name="row">Row number, <c>0</c> being the attack.</param>
    /// <returns>Top-left corner of the label.</returns>
    public Point GetStatLabel(int row) => new(Stats.X, Stats.Y + (row * StatRowHeight));

    /// <summary>Top-left corner of a stat's value.</summary>
    /// <param name="row">Row number, <c>0</c> being the attack.</param>
    /// <returns>Top-left corner of the value.</returns>
    public Point GetStatValue(int row) => new(Stats.X + StatNumberColumn, Stats.Y + (row * StatRowHeight));

    /// <summary>Top-left corner of the army line.</summary>
    public Point Army => new(Content.X + ArmyOffsetX, Content.Y + ArmyOffsetY);

    /// <summary>
    /// Places the parts in a box.
    /// </summary>
    /// <param name="box">Bounding box of the whole box, <c>QWIKHERO.ICN</c> frame 0.</param>
    /// <param name="portraitFrameSize">Size of the small portrait's frame, <c>LOCATORS.ICN</c> frame 22.</param>
    /// <param name="flagSize">Size of a colour flag, <c>FLAG32.ICN</c>.</param>
    /// <returns>The layout.</returns>
    public static HeroQuickInfoLayout Create(Rect box, Size portraitFrameSize, Size flagSize) =>
        new(box, portraitFrameSize, flagSize);

    private int GetIconTop(int count) =>
        Content.Y + (count == 1 ? SingleIconOffsetY : IconOffsetY);
}
