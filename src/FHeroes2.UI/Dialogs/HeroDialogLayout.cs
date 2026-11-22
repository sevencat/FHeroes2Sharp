using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The geometry of the hero dialog, the coordinates <c>Heroes::OpenDialog()</c> hard-codes relative to the dialog's
/// top-left corner (<c>heroes_dialog.cpp:100-420</c>).
/// </summary>
/// <remarks>
/// The reference draws straight into the display with <c>dialogRoi</c> as the origin and keeps the numbers in locals.
/// The port keeps them here so the view, the input handling and the tests agree on one set of coordinates; the dialog
/// origin is <see cref="Offset"/>, which is the top-left of the 640x480 interface the reference centres.
/// </remarks>
public sealed class HeroDialogLayout
{
    /// <summary>Creates the geometry of a dialog that starts at the frame's top-left corner.</summary>
    public HeroDialogLayout()
    {
    }

    /// <summary>Creates the geometry of a dialog whose top-left corner is <paramref name="offset"/>.</summary>
    /// <param name="offset">Where the dialog's top-left corner goes in the frame.</param>
    public HeroDialogLayout(Point offset) => Offset = offset;

    /// <summary>The width of the dialog, <c>Display::DEFAULT_WIDTH</c>.</summary>
    public const int DialogWidth = 640;

    /// <summary>The height of the dialog, <c>Display::DEFAULT_HEIGHT</c>.</summary>
    public const int DialogHeight = 480;

    /// <summary>Where the dialog's top-left corner goes in the frame.</summary>
    public Point Offset { get; set; }

    /// <summary>The whole dialog, <c>dialogRoi</c>.</summary>
    public Rect Area => new(Offset.X, Offset.Y, DialogWidth, DialogHeight);

    /// <summary>The hero's portrait, <c>portPos</c> (<c>heroes_dialog.cpp:104</c>).</summary>
    public Rect PortraitArea => new(Offset.X + 49, Offset.Y + 31, 101, 93);

    /// <summary>The plate the hero's description goes in, <c>titleRoi</c> (<c>heroes_dialog.cpp:105</c>).</summary>
    public Rect TitleArea => new(Offset.X + 60, Offset.Y + 1, 519, 17);

    /// <summary>The top-left of the primary skill bar, <c>heroes_dialog.cpp:113</c>.</summary>
    public Point PrimarySkillsOffset => new(Offset.X + 156, Offset.Y + 31);

    /// <summary>The primary skill bar's table: four skills in one row.</summary>
    public static Size PrimarySkillsTableSize => new(4, 1);

    /// <summary>The gap between two primary skill slots.</summary>
    public static Size PrimarySkillsBetweenOffset => new(6, 0);

    /// <summary>The morale indicator, <c>heroes_dialog.cpp:207</c>; the editor moves it up ten pixels.</summary>
    public Point MoraleOffset(bool isEditor) => new(Offset.X + 514, Offset.Y + (isEditor ? 44 : 34));

    /// <summary>The luck indicator, one slot right of morale (<c>heroes_dialog.cpp:211</c>).</summary>
    public Point LuckOffset(bool isEditor) => new(Offset.X + 550, Offset.Y + (isEditor ? 44 : 34));

    /// <summary>The spread formation icon, <c>heroes_dialog.cpp:215-219</c>.</summary>
    public Point SpreadFormationOffset => new(Offset.X + 516, Offset.Y + 63);

    /// <summary>The grouped formation icon, right of the spread one (<c>heroes_dialog.cpp:225</c>).</summary>
    public Point GroupedFormationOffset => new(Offset.X + 552, Offset.Y + 63);

    /// <summary>The experience indicator, <c>heroes_dialog.cpp:243-244</c>.</summary>
    public Point ExperienceOffset(bool isEditor) => new(Offset.X + 512, Offset.Y + (isEditor ? 76 : 86));

    /// <summary>The spell point indicator, two pixels below and 38 right of experience.</summary>
    public Point SpellPointsOffset(bool isEditor) => new(Offset.X + 550, Offset.Y + (isEditor ? 78 : 88));

    /// <summary>The colour crest or, in the editor, the race panel (<c>heroes_dialog.cpp:264</c>).</summary>
    public Rect CrestArea => new(Offset.X + 49, Offset.Y + 130, 101, 93);

    /// <summary>The army bar, <c>heroes_dialog.cpp:333</c>.</summary>
    public Point ArmyOffset => new(Offset.X + 156, Offset.Y + 130);

    /// <summary>The army bar's table: five slots in one row.</summary>
    public static Size ArmyTableSize => new(5, 1);

    /// <summary>The gap between two army slots.</summary>
    public static Size ArmyBetweenOffset => new(6, 0);

    /// <summary>The secondary skill bar, <c>heroes_dialog.cpp:342</c>.</summary>
    public Point SecondarySkillsOffset => new(Offset.X + 3, Offset.Y + 233);

    /// <summary>The secondary skill bar's table: eight slots in one row.</summary>
    public static Size SecondarySkillsTableSize => new(8, 1);

    /// <summary>The gap between two secondary skill slots.</summary>
    public static Size SecondarySkillsBetweenOffset => new(5, 0);

    /// <summary>The status bar plate, <c>heroes_dialog.cpp:348</c>.</summary>
    public Point StatusBarOffset => new(Offset.X + 22, Offset.Y + 460);

    /// <summary>How far the status bar's text is inset from its plate, <c>16</c> on both sides (<c>heroes_dialog.cpp:354</c>).</summary>
    public const int StatusBarInset = 16;

    /// <summary>The artifact bar, <c>heroes_dialog.cpp:358</c>.</summary>
    public Point ArtifactsOffset => new(Offset.X + 51, Offset.Y + 308);

    /// <summary>The artifact bar's table: seven columns and two rows.</summary>
    public static Size ArtifactsTableSize => new(7, 2);

    /// <summary>The gap between two artifact slots.</summary>
    public static Size ArtifactsBetweenOffset => new(15, 15);

    /// <summary>The previous hero button, bottom left (<c>heroes_dialog.cpp:371</c>).</summary>
    public Point PreviousHeroOffset => new(Offset.X, Offset.Y + DialogHeight - 20);

    /// <summary>The next hero button, bottom right (<c>heroes_dialog.cpp:381</c>).</summary>
    public Point NextHeroOffset => new(Offset.X + DialogWidth - 22, Offset.Y + DialogHeight - 20);

    /// <summary>The dismiss (or, in the editor, patrol) button, <c>heroes_dialog.cpp:394</c>.</summary>
    public Point DismissOffset => new(Offset.X + 9, Offset.Y + 378);

    /// <summary>The exit button, <c>heroes_dialog.cpp:401</c>.</summary>
    public Point ExitOffset => new(Offset.X + 602, Offset.Y + 378);
}
