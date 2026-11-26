using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Campaign;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>What the campaign difficulty window asks the caller to do.</summary>
/// <remarks>
/// 参考里选择难度只移动那个框、不关窗口，所以 <see cref="SelectDecreased"/> 与两位兄弟不代表「结束」，
/// 调用方照样继续跑循环；只有 <see cref="Okay"/> 才是退出。
/// </remarks>
public enum CampaignDifficultyAction
{
    /// <summary>Nothing happened this frame.</summary>
    None,

    /// <summary>The okay button was clicked; the window closes.</summary>
    Okay,

    /// <summary>The first icon was picked, <see cref="CampaignDifficulty.Decreased"/>.</summary>
    SelectDecreased,

    /// <summary>The second icon was picked, <see cref="CampaignDifficulty.Default"/>.</summary>
    SelectDefault,

    /// <summary>The third icon was picked, <see cref="CampaignDifficulty.Increased"/>.</summary>
    SelectIncreased,
}

/// <summary>Answer of the campaign difficulty window to one frame of input.</summary>
/// <param name="Action">What the caller should do next.</param>
/// <param name="Difficulty">The difficulty the window now shows, one of <see cref="CampaignDifficulty"/>.</param>
public readonly record struct CampaignDifficultyChoice(CampaignDifficultyAction Action, int Difficulty = 0);

/// <summary>The part of the campaign difficulty window the right mouse button asks about.</summary>
public enum CampaignDifficultyHelpTarget
{
    /// <summary>Nothing was hit.</summary>
    None,

    /// <summary>The okay button.</summary>
    Okay,

    /// <summary>The decreased difficulty icon.</summary>
    Decreased,

    /// <summary>The default difficulty icon.</summary>
    Default,

    /// <summary>The increased difficulty icon.</summary>
    Increased,
}

/// <summary>
/// The campaign difficulty window, <c>setCampaignDifficulty()</c>
/// (<c>game_campaign.cpp:912-1082</c>).
/// </summary>
/// <remarks>
/// <para>
/// Three 65×65 icons - pawn, horse and rook of <c>DIFFICULTY_ICON_EASY</c>, <c>_NORMAL</c> and <c>_HARD</c> - in
/// a stone window, with the name of each under its icon, the description of the picked one 140 pixels down and a
/// <c>NGEXTRA</c> frame 62 selection box around it. The window is not a fixed plate: the reference widens it by
/// <c>3 × ( widest name - 85 )</c> so that a long translation of "Decreased" still fits between two icons, and
/// makes it as tall as the longest description needs.
/// </para>
/// <para>
/// The three icon positions are spread by a third of the window width from the centre, which is why they do not
/// land on an even grid: with a 300 pixel window they sit 100 pixels apart, and the widened ones spread further.
/// The reference computes the names' width with <c>Text</c> and the descriptions' height with <c>Text::height(
/// width )</c>, both of which are the port's <see cref="GlyphText"/> on an <see cref="IGlyphSource"/>.
/// </para>
/// <para>
/// The strings are the English source text of the reference and live here as constants rather than being passed
/// in: the width and the height of the window are derived from them, so a caller cannot swap them afterwards
/// without redoing the layout. A localisation layer will have to feed them to <see cref="TryCreate"/> someday;
/// until it exists, this is the single place they live.
/// </para>
/// </remarks>
public sealed class CampaignDifficultyView
{
    /// <summary>Title of the window, <c>_( "Campaign Difficulty" )</c>.</summary>
    public const string Title = "Campaign Difficulty";

    /// <summary>Name of the easier difficulty, <c>_( "campaignDifficulty|Decreased" )</c>.</summary>
    public const string DecreasedName = "Decreased";

    /// <summary>Name of the original difficulty, <c>_( "campaignDifficulty|Default" )</c>.</summary>
    public const string DefaultName = "Default";

    /// <summary>Name of the harder difficulty, <c>_( "campaignDifficulty|Increased" )</c>.</summary>
    public const string IncreasedName = "Increased";

    /// <summary>Description of the easier difficulty, drawn under the icons.</summary>
    public const string DecreasedDescription =
        "Choose this difficulty to experience the game's story with decreased challenge. The AI will be weaker than at the default difficulty.";

    /// <summary>Description of the original difficulty.</summary>
    public const string DefaultDescription =
        "Choose this difficulty to experience the campaign as close to the original design as possible with the fheroes2 AI.";

    /// <summary>Description of the harder difficulty.</summary>
    public const string IncreasedDescription =
        "Choose this difficulty if you want more of a challenge. The AI will be stronger than at the default difficulty.";

    /// <summary>Side of an icon, <c>iconSize</c> of the reference.</summary>
    public const int IconSize = 65;

    /// <summary>Margin on each side of an icon used to widen the window for long names, <c>iconLateralMarginWidth</c>.</summary>
    public const int IconLateralMargin = 10;

    /// <summary>Width of the window before the compensation for long names, <c>300</c> of the reference.</summary>
    public const int BaseWidth = 300;

    /// <summary>Distance from the top of the content to the title.</summary>
    public const int TitleOffsetY = 10;

    /// <summary>Distance from the top of the content to the icons.</summary>
    public const int IconOffsetY = 40;

    /// <summary>Distance from the left of the content to the description.</summary>
    public const int DescriptionOffsetX = 8;

    /// <summary>Distance from the top of the content to the description.</summary>
    public const int DescriptionOffsetY = 140;

    /// <summary>Room the reference keeps under the description, <c>40</c> plus the description offset.</summary>
    public const int BottomMargin = 40;

    /// <summary>Distance from the bottom of an icon to the top of its name.</summary>
    public const int NameOffsetY = 5;

    /// <summary>How far the selection box reaches past its icon on each side, <c>-3</c> of the reference.</summary>
    public const int SelectionOverhang = 3;

    /// <summary>Vertical offset of the okay button from the bottom of the content.</summary>
    public const int OkayOffsetY = 5;

    /// <summary>Direction of the icon drop shadows, <c>{ -5, 5 }</c> of the reference.</summary>
    private static readonly Point IconShadowOffset = new(-5, 5);

    private readonly StandardWindow _window;
    private readonly IGlyphSource _font;
    private readonly IGlyphSource _captionFont;
    private readonly Sprite[] _icons;
    private readonly Sprite _selection;
    private readonly ButtonSprite _okay;
    private readonly Rect _okayArea;
    private readonly Rect[] _difficultyAreas;
    private readonly Point _descriptionPosition;
    private readonly int _descriptionWidth;
    private readonly int _dialogWidth;
    private int _currentDifficulty;
    private Point? _cursor;
    private bool _okayPressed;
    private int _pressedDifficulty = -1;

    private CampaignDifficultyView(StandardWindow window, IGlyphSource font, IGlyphSource captionFont,
        Sprite[] icons, Sprite selection, ButtonSprite okay, Rect okayArea, Rect[] difficultyAreas,
        Point descriptionPosition, int descriptionWidth, int dialogWidth, int currentDifficulty)
    {
        _window = window;
        _font = font;
        _captionFont = captionFont;
        _icons = icons;
        _selection = selection;
        _okay = okay;
        _okayArea = okayArea;
        _difficultyAreas = difficultyAreas;
        _descriptionPosition = descriptionPosition;
        _descriptionWidth = descriptionWidth;
        _dialogWidth = dialogWidth;
        _currentDifficulty = currentDifficulty;
    }

    /// <summary>Area the window content is drawn into, <c>activeArea()</c> of the reference.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The stone frame around <see cref="ActiveArea"/>.</summary>
    public StandardWindow Window => _window;

    /// <summary>The difficulty the window shows, one of <see cref="CampaignDifficulty"/>.</summary>
    public int CurrentDifficulty => _currentDifficulty;

    /// <summary>Area of the okay button.</summary>
    public Rect OkayArea => _okayArea;

    /// <summary>The three icons in drawing order: decreased, default, increased.</summary>
    public IReadOnlyList<Sprite> Icons => _icons;

    /// <summary>The three press areas, one per icon, at the size of the selection box.</summary>
    public IReadOnlyList<Rect> DifficultyAreas => _difficultyAreas;

    /// <summary>Where the description of the picked difficulty is drawn.</summary>
    public Point DescriptionPosition => _descriptionPosition;

    /// <summary>Width the description is wrapped to.</summary>
    public int DescriptionWidth => _descriptionWidth;

    /// <summary>Width of the window content, <c>dialogWidth</c> of the reference.</summary>
    public int DialogWidth => _dialogWidth;

    /// <summary>The name shown under an icon, <c>getCampaignDifficultyText()</c>.</summary>
    /// <param name="difficulty">One of <see cref="CampaignDifficulty"/>.</param>
    /// <returns>The English name, or an empty string for an unknown difficulty.</returns>
    public static string GetName(int difficulty) => difficulty switch
    {
        CampaignDifficulty.Decreased => DecreasedName,
        CampaignDifficulty.Default => DefaultName,
        CampaignDifficulty.Increased => IncreasedName,
        _ => string.Empty,
    };

    /// <summary>The description of a difficulty, <c>descriptionDecreased</c> and friends of the reference.</summary>
    /// <param name="difficulty">One of <see cref="CampaignDifficulty"/>.</param>
    /// <returns>The English description, or an empty string for an unknown difficulty.</returns>
    public static string GetDescription(int difficulty) => difficulty switch
    {
        CampaignDifficulty.Decreased => DecreasedDescription,
        CampaignDifficulty.Default => DefaultDescription,
        CampaignDifficulty.Increased => IncreasedDescription,
        _ => string.Empty,
    };

    /// <summary>
    /// Composes the window from the frames of the archive and sizes it to the texts.
    /// </summary>
    /// <param name="screen">Size of the frame the window is centred in, <c>Display::instance()</c>.</param>
    /// <param name="font">Normal white font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="captionFont">Yellow font of the title, <c>FontType::normalYellow()</c>.</param>
    /// <param name="icons">The three <see cref="IconSize"/> icons.</param>
    /// <param name="selection">Frame 62 of <c>NGEXTRA</c>, the box around the picked icon.</param>
    /// <param name="okayReleased">Released okay button, <c>BUTTON_SMALL_OKAY_GOOD</c> or <c>_EVIL</c>.</param>
    /// <param name="okayPressed">Pressed frame of the same button.</param>
    /// <param name="currentDifficulty">Difficulty the window opens on.</param>
    /// <returns><see langword="null"/> when an input is missing or has the wrong frame count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> or <paramref name="captionFont"/> is
    /// <see langword="null"/>.</exception>
    public static CampaignDifficultyView? TryCreate(
        Size screen,
        IGlyphSource font,
        IGlyphSource captionFont,
        IReadOnlyList<Sprite>? icons,
        Sprite? selection,
        Sprite? okayReleased,
        Sprite? okayPressed,
        int currentDifficulty)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(captionFont);

        if (icons is not { Count: 3 } || selection is null || okayReleased is null || okayPressed is null)
        {
            return null;
        }

        // 宽：三个名字里最宽的那个若比「图标 + 两侧 10 像素」还宽，就按三份补偿把窗口撑开。
        int widestName = Math.Max(
            GlyphText.Measure(font, DecreasedName),
            Math.Max(GlyphText.Measure(font, DefaultName), GlyphText.Measure(font, IncreasedName)));
        int compensation = Math.Max(0, widestName - ((IconSize + (IconLateralMargin * 2))));
        int dialogWidth = BaseWidth + (compensation * 3);

        // 高：三段描述在可用宽度下最长的那一段，加上固定的上下留白。
        int descriptionWidth = dialogWidth - (DescriptionOffsetX * 2);
        int tallest = Math.Max(
            MeasureDescriptionHeight(font, DecreasedDescription, descriptionWidth),
            Math.Max(
                MeasureDescriptionHeight(font, DefaultDescription, descriptionWidth),
                MeasureDescriptionHeight(font, IncreasedDescription, descriptionWidth)));

        StandardWindow window = StandardWindow.CreateCentered(
            new Size(dialogWidth, tallest + DescriptionOffsetY + BottomMargin), screen: screen);
        Rect roi = window.ActiveArea;

        Point okayPosition = window.RenderPosition(
            new Point(0, OkayOffsetY), new Size(okayReleased.Width, okayReleased.Height),
            WindowPadding.BottomCenter);
        ButtonSprite okay = new(okayReleased, okayPressed, offsetX: okayPosition.X, offsetY: okayPosition.Y);
        Rect okayArea = new(okayPosition.X, okayPosition.Y, okayReleased.Width, okayReleased.Height);

        // 三个图标以窗口宽度三等分向两侧铺开，不是等间距的网格。
        int iconCenterX = roi.X + ((roi.Width - IconSize) / 2);
        int iconY = roi.Y + IconOffsetY;
        int third = roi.Width / 3;

        Rect[] areas = new Rect[3];
        for (int index = 0; index < 3; ++index)
        {
            int iconX = iconCenterX + ((index - 1) * third);
            areas[index] = new Rect(
                iconX - SelectionOverhang, iconY - SelectionOverhang, selection.Width, selection.Height);
        }

        return new CampaignDifficultyView(window, font, captionFont, [.. icons], selection, okay, okayArea,
            areas, new Point(roi.X + DescriptionOffsetX, roi.Y + DescriptionOffsetY), descriptionWidth,
            dialogWidth, currentDifficulty);
    }

    /// <summary>Whether the left button is being held on the okay button, for the pressed frame.</summary>
    /// <param name="point">Cursor in logical pixels, <see langword="null"/> when it left the window.</param>
    public void SetCursor(Point? point) => _cursor = point;

    /// <summary>
    /// A left button transition over the window.
    /// </summary>
    /// <remarks>
    /// 三个图标与确定键都按「按下与抬起落在同一区域」算一次点击，参考用的是 <c>MouseClickLeft()</c>。
    /// 选难度只改 <see cref="CurrentDifficulty"/> 并回到调用方，窗口不关。
    /// </remarks>
    /// <param name="down">Whether the left button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>What the caller should do next.</returns>
    public CampaignDifficultyChoice SetLeftButton(bool down, Point point)
    {
        if (down)
        {
            _okayPressed = _okayArea.Contains(point);
            _pressedDifficulty = _okayPressed ? -1 : IndexAt(point);

            return default;
        }

        if (_okayPressed)
        {
            _okayPressed = false;

            return _okayArea.Contains(point)
                ? new CampaignDifficultyChoice(CampaignDifficultyAction.Okay, _currentDifficulty)
                : default;
        }

        int index = _pressedDifficulty;
        _pressedDifficulty = -1;

        if (index < 0 || !_difficultyAreas[index].Contains(point))
        {
            return default;
        }

        _currentDifficulty = DifficultyAt(index);

        return new CampaignDifficultyChoice(ActionAt(index), _currentDifficulty);
    }

    /// <summary>
    /// The block the right mouse button asks about, the <c>isMouseRightButtonPressedInArea()</c> cascade of the
    /// reference loop.
    /// </summary>
    /// <param name="point">Cursor position in logical pixels.</param>
    /// <returns>The block that was hit, <see cref="CampaignDifficultyHelpTarget.None"/> when nothing was.</returns>
    public CampaignDifficultyHelpTarget ProcessRightClick(Point point)
    {
        if (_okayArea.Contains(point))
        {
            return CampaignDifficultyHelpTarget.Okay;
        }

        for (int index = 0; index < _difficultyAreas.Length; ++index)
        {
            if (_difficultyAreas[index].Contains(point))
            {
                return index switch
                {
                    0 => CampaignDifficultyHelpTarget.Decreased,
                    1 => CampaignDifficultyHelpTarget.Default,
                    _ => CampaignDifficultyHelpTarget.Increased,
                };
            }
        }

        return CampaignDifficultyHelpTarget.None;
    }

    /// <summary>
    /// Draws the window, the title, the icons with their shadows, the selection box, the description, the three
    /// names and the okay button, <c>game_campaign.cpp:955-1027</c>.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        string title = Title;
        int titleWidth = GlyphText.Measure(_captionFont, title);
        GlyphText.DrawLine(
            _captionFont, target,
            new Point(ActiveArea.X + ((ActiveArea.Width - titleWidth) / 2), ActiveArea.Y + TitleOffsetY), title);

        for (int index = 0; index < _icons.Length; ++index)
        {
            Point iconPosition = new(_difficultyAreas[index].X + SelectionOverhang,
                _difficultyAreas[index].Y + SelectionOverhang);

            ImageOps.BlitSprite(target, iconPosition, _icons[index]);
            ImageOps.AddGradientShadow(_icons[index], target, iconPosition, IconShadowOffset);
        }

        Rect selectionArea = _difficultyAreas[SelectedIndex(_currentDifficulty)];
        ImageOps.BlitSprite(target, selectionArea.Position, _selection);

        // 参考给描述套了 setUniformVerticalAlignment( false )，即逐行左对齐、不垂直居中。
        GlyphText.DrawMultiline(_font, target, _descriptionPosition, GetDescription(_currentDifficulty),
            _descriptionWidth);

        for (int index = 0; index < 3; ++index)
        {
            string name = GetName(DifficultyAt(index));
            int nameWidth = GlyphText.Measure(_font, name);
            Rect area = _difficultyAreas[index];

            GlyphText.DrawLine(
                _font, target,
                new Point(area.X + ((area.Width - nameWidth) / 2), area.Y + area.Height + NameOffsetY), name);
        }

        bool isOkayDown = _okayPressed && _cursor is { } cursor && _okayArea.Contains(cursor);

        if (isOkayDown)
        {
            _okay.Press();
        }
        else
        {
            _okay.Release();
        }

        _okay.DrawShadow(target);
        _okay.Draw(target);
    }

    private static int MeasureDescriptionHeight(IGlyphSource font, string text, int width) =>
        GlyphText.WrapLines(font, text, width).Count * font.LineHeight;

    private static int DifficultyAt(int index) => index switch
    {
        0 => CampaignDifficulty.Decreased,
        1 => CampaignDifficulty.Default,
        _ => CampaignDifficulty.Increased,
    };

    private static int SelectedIndex(int difficulty) => difficulty switch
    {
        CampaignDifficulty.Decreased => 0,
        CampaignDifficulty.Increased => 2,
        _ => 1,
    };

    private static CampaignDifficultyAction ActionAt(int index) => index switch
    {
        0 => CampaignDifficultyAction.SelectDecreased,
        1 => CampaignDifficultyAction.SelectDefault,
        _ => CampaignDifficultyAction.SelectIncreased,
    };

    private int IndexAt(Point point)
    {
        for (int index = 0; index < _difficultyAreas.Length; ++index)
        {
            if (_difficultyAreas[index].Contains(point))
            {
                return index;
            }
        }

        return -1;
    }
}
