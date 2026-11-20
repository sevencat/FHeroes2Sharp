using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The sprites of the recruit window: the two composites the reference builds in memory and the frames of
/// <c>ICN::RECRBKG</c>, <c>BLDGXTRA</c>, <c>RECRUIT</c>, <c>MONSTER_SWITCH_LEFT_ARROW</c>,
/// <c>MONSTER_SWITCH_RIGHT_ARROW</c>, <c>RESOURCE</c> and the four small buttons
/// (<c>dialog_recruit.cpp:69-94</c>, <c>:282-345</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two pieces of the window are not in any archive: the count box is a slice of the recruit background and the
/// "Cost per troop:" frame is assembled from the title background of a building window plus the borders of the recruit
/// background. Both are composed here once, the way <c>drawCostPerTroopFrame()</c> does it, rather than on every frame.
/// </para>
/// <para>
/// The evil interface recolours exactly these two composites - the rest of the window comes from
/// <c>StandardWindow</c>'s own artwork and from the evil button frames the caller passes.
/// </para>
/// </remarks>
public sealed class RecruitWindowArtwork
{
    /// <summary>Frame of the up arrow's released look, <c>ICN::RECRUIT</c> frame <c>0</c>.</summary>
    public const int UpReleasedFrame = 0;

    /// <summary>Frame of the up arrow's pressed look, frame <c>1</c>.</summary>
    public const int UpPressedFrame = 1;

    /// <summary>Frame of the down arrow's released look, frame <c>2</c>.</summary>
    public const int DownReleasedFrame = 2;

    /// <summary>Frame of the down arrow's pressed look, frame <c>3</c>.</summary>
    public const int DownPressedFrame = 3;

    /// <summary>Number of <c>ICN::RECRUIT</c> frames the window reads, <c>4</c>.</summary>
    public const int RequiredArrowFrames = DownPressedFrame + 1;

    /// <summary>Number of frames of each monster switch arrow, <c>2</c>.</summary>
    public const int RequiredSwitchFrames = 2;

    /// <summary>Number of frames of <c>ICN::RESOURCE</c>, <c>7</c>.</summary>
    public const int RequiredResourceFrames = 7;

    /// <summary>Width of the composite cost frame, <c>132</c>.</summary>
    public const int CostFrameWidth = 132;

    /// <summary>Height of the composite cost frame, <c>67</c>.</summary>
    public const int CostFrameHeight = 67;

    /// <summary>Left edge of the count box in <c>ICN::RECRBKG</c>'s first frame, <c>134</c>.</summary>
    public const int CountBoxSourceX = 134;

    /// <summary>Top edge of the count box in <c>ICN::RECRBKG</c>'s first frame, <c>159</c>.</summary>
    public const int CountBoxSourceY = 159;

    private RecruitWindowArtwork(Sprite countBox, Sprite costFrame, IReadOnlyList<Sprite> switchLeft,
        IReadOnlyList<Sprite> switchRight, IReadOnlyList<Sprite> arrows, DialogButtonArtwork okayButton,
        DialogButtonArtwork cancelButton, DialogButtonArtwork maxButton, DialogButtonArtwork minButton,
        IReadOnlyList<Sprite> resourceIcons, Sprite monster)
    {
        CountBox = countBox;
        CostFrame = costFrame;
        SwitchLeft = switchLeft;
        SwitchRight = switchRight;
        UpReleased = arrows[UpReleasedFrame];
        UpPressed = arrows[UpPressedFrame];
        DownReleased = arrows[DownReleasedFrame];
        DownPressed = arrows[DownPressedFrame];
        OkayButton = okayButton;
        CancelButton = cancelButton;
        MaxButton = maxButton;
        MinButton = minButton;
        ResourceIcons = resourceIcons;
        Monster = monster;
    }

    /// <summary>The <c>68x19</c> count box sliced out of <c>ICN::RECRBKG</c>.</summary>
    public Sprite CountBox { get; }

    /// <summary>The composite "Cost per troop:" frame, <c>132x67</c>.</summary>
    public Sprite CostFrame { get; }

    /// <summary>The two frames of the left switch arrow.</summary>
    public IReadOnlyList<Sprite> SwitchLeft { get; }

    /// <summary>The two frames of the right switch arrow.</summary>
    public IReadOnlyList<Sprite> SwitchRight { get; }

    /// <summary>The up arrow, released.</summary>
    public Sprite UpReleased { get; }

    /// <summary>The up arrow, pressed.</summary>
    public Sprite UpPressed { get; }

    /// <summary>The down arrow, released.</summary>
    public Sprite DownReleased { get; }

    /// <summary>The down arrow, pressed.</summary>
    public Sprite DownPressed { get; }

    /// <summary>The three small buttons and the MAX/MIN pair, in their good or evil look.</summary>
    public DialogButtonArtwork OkayButton { get; }

    /// <inheritdoc cref="OkayButton"/>
    public DialogButtonArtwork CancelButton { get; }

    /// <inheritdoc cref="OkayButton"/>
    public DialogButtonArtwork MaxButton { get; }

    /// <inheritdoc cref="OkayButton"/>
    public DialogButtonArtwork MinButton { get; }

    /// <summary>The seven resource icons, indexed by <see cref="Game.Resources.ResourceIconIndex"/>.</summary>
    public IReadOnlyList<Sprite> ResourceIcons { get; }

    /// <summary>The static frame of the unit on offer, from <c>ICN::MONSTER</c>.</summary>
    public Sprite Monster { get; }

    /// <summary>
    /// Builds the artwork, or returns <see langword="null"/> when the tables are too short.
    /// </summary>
    /// <param name="recruitBackground">Frames of <c>ICN::RECRBKG</c>; frame <c>0</c> is read.</param>
    /// <param name="titleBackground">Frames of <c>ICN::BLDGXTRA</c>; frame <c>0</c> is read.</param>
    /// <param name="switchLeft">Frames of <c>ICN::MONSTER_SWITCH_LEFT_ARROW</c>.</param>
    /// <param name="switchRight">Frames of <c>ICN::MONSTER_SWITCH_RIGHT_ARROW</c>.</param>
    /// <param name="arrows">Frames <c>0</c> to <c>3</c> of <c>ICN::RECRUIT</c>.</param>
    /// <param name="okayButton">The small OKAY button, <c>BUTTON_SMALL_OKAY_GOOD</c> or its evil twin.</param>
    /// <param name="cancelButton">The small CANCEL button.</param>
    /// <param name="maxButton">The small MAX button.</param>
    /// <param name="minButton">The small MIN button.</param>
    /// <param name="resourceIcons">Frames of <c>ICN::RESOURCE</c>.</param>
    /// <param name="monster">The static frame of the unit on offer.</param>
    /// <param name="font">Font the "Cost per troop:" caption is drawn with, <c>smallWhite</c>.</param>
    /// <param name="evilInterface">Whether the composites take the evil palette, <c>isEvilInterface</c>.</param>
    /// <returns>The artwork, or <see langword="null"/> when a table is missing frames.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RecruitWindowArtwork? TryCreate(IReadOnlyList<Sprite> recruitBackground,
        IReadOnlyList<Sprite> titleBackground, IReadOnlyList<Sprite> switchLeft, IReadOnlyList<Sprite> switchRight,
        IReadOnlyList<Sprite> arrows, DialogButtonArtwork okayButton, DialogButtonArtwork cancelButton,
        DialogButtonArtwork maxButton, DialogButtonArtwork minButton, IReadOnlyList<Sprite> resourceIcons,
        Sprite monster, IGlyphSource font, bool evilInterface = false)
    {
        ArgumentNullException.ThrowIfNull(recruitBackground);
        ArgumentNullException.ThrowIfNull(titleBackground);
        ArgumentNullException.ThrowIfNull(switchLeft);
        ArgumentNullException.ThrowIfNull(switchRight);
        ArgumentNullException.ThrowIfNull(arrows);
        ArgumentNullException.ThrowIfNull(resourceIcons);
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(font);

        if (recruitBackground.Count < 1 || titleBackground.Count < 1 || switchLeft.Count < RequiredSwitchFrames
            || switchRight.Count < RequiredSwitchFrames || arrows.Count < RequiredArrowFrames
            || resourceIcons.Count < RequiredResourceFrames)
        {
            return null;
        }

        if (okayButton.Released is null || okayButton.Pressed is null || cancelButton.Released is null
            || cancelButton.Pressed is null || maxButton.Released is null || maxButton.Pressed is null
            || minButton.Released is null || minButton.Pressed is null)
        {
            return null;
        }

        Sprite countBox = ComposeCountBox(recruitBackground[0], evilInterface);
        Sprite costFrame = ComposeCostFrame(recruitBackground[0], titleBackground[0], font, evilInterface);

        return new RecruitWindowArtwork(countBox, costFrame, switchLeft, switchRight, arrows, okayButton, cancelButton,
            maxButton, minButton, resourceIcons, monster);
    }

    /// <summary>The <c>68x19</c> slice of the recruit background, <c>:285-292</c>.</summary>
    private static Sprite ComposeCountBox(Sprite background, bool evilInterface)
    {
        Sprite countBox = new(RecruitWindowLayout.CountBoxSize.Width, RecruitWindowLayout.CountBoxSize.Height);

        ImageOps.Copy(background,
            new Rect(CountBoxSourceX, CountBoxSourceY, countBox.Width, countBox.Height), countBox, Point.Zero);

        if (evilInterface)
        {
            ImageOps.ApplyPalette(countBox.Image, PaletteId.GoodToEvilInterface);
        }

        return countBox;
    }

    /// <summary>The composite title frame of the cost box, <c>drawCostPerTroopFrame()</c> (<c>:69-94</c>).</summary>
    private static Sprite ComposeCostFrame(Sprite background, Sprite title, IGlyphSource font, bool evilInterface)
    {
        Sprite frame = new(CostFrameWidth, CostFrameHeight);

        // The title background of a building window supplies the two halves of the header, the recruit background the
        // side borders and the strip under the text.
        ImageOps.Copy(title, new Rect(3, 58, 63, 14), frame, new Point(3, 0));
        ImageOps.Copy(title, new Rect(title.Width - 66, 58, 63, 14), frame, new Point(66, 0));
        ImageOps.Copy(background, new Rect(138, 54, 3, 63), frame, Point.Zero);
        ImageOps.Copy(background, new Rect(267, 54, 3, 63), frame, new Point(129, 0));
        ImageOps.Copy(background, new Rect(138, 117, 132, 4), frame, new Point(0, 63));

        int textWidth = GlyphText.Measure(font, RecruitDialog.CostPerTroopTitle);
        GlyphText.DrawLine(font, frame.Image, new Point((frame.Width - textWidth) / 2, 3),
            RecruitDialog.CostPerTroopTitle);

        if (evilInterface)
        {
            ImageOps.ApplyPalette(frame.Image, PaletteId.GoodToEvilInterface);
        }

        return frame;
    }
}
