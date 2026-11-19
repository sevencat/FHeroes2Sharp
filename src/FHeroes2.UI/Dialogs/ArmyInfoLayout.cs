using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where everything of the army info dialog goes, the offsets of the anonymous namespace of
/// <c>dialog_armyinfo.cpp</c> turned into rectangles.
/// </summary>
/// <remarks>
/// <para>
/// The reference computes the window once (<c>dialog_armyinfo.cpp:522-556</c>) and then hands the pieces to the
/// drawing functions as raw offsets: the attribute block at <c>pos_rt + ( 400, 37 )</c>, the unit at
/// <c>pos_rt + ( 520 / 4 + 16, 175 )</c>, the amount box as a 125 by 23 rectangle 80 pixels in. This class holds
/// the arithmetic in one place so that a test can check the numbers without a display, and so that the drawing
/// methods do not each repeat <c>pos_rt + ...</c>.
/// </para>
/// <para>
/// <b>Three rectangles, not one.</b> The reference keeps three: the window sprite's own rectangle, the shadow's
/// rectangle (the same artwork shifted by the shadow frame's anchors), and <c>pos_rt</c> — the second one plus
/// <c>( +9, -1 )</c> on the evil interface, because the evil window is drawn nine pixels to the right. Everything
/// inside the window is measured from <c>pos_rt</c>, so <see cref="Dialog"/> is what the text methods use and
/// <see cref="Window"/> is only there to blit the sprite.
/// </para>
/// <para>
/// The button rectangles are computed here as well, but only when the dialog has the matching flag: the reference
/// checks <c>flags &amp; Dialog::UPGRADE</c> before it even loads the upgrade sprite, so a missing one is
/// <see langword="null"/> rather than an empty rectangle.
/// </para>
/// </remarks>
public sealed class ArmyInfoLayout
{
    /// <summary>Width of the drop shadow the window sprite carries, <c>shadowWidthPx</c> (<c>image.h</c>).</summary>
    public const int ShadowWidth = 12;

    /// <summary>Offset of the attribute block from <see cref="Dialog"/>, <c>monsterStatOffset</c>.</summary>
    public const int StatsOffsetX = 400;

    /// <summary>Offset of the attribute block from <see cref="Dialog"/>, <c>monsterStatOffset</c>.</summary>
    public const int StatsOffsetY = 37;

    /// <summary>Offset of the battle attribute block from <see cref="Dialog"/>, <c>battleStatOffset</c>.</summary>
    public const int BattleStatsOffsetX = 395;

    /// <summary>Offset of the battle attribute block from <see cref="Dialog"/>, <c>battleStatOffset</c>.</summary>
    public const int BattleStatsOffsetY = 184;

    /// <summary>Offset of the unit sprite from <see cref="Dialog"/>, <c>pos_mon</c>: <c>520 / 4 + 16</c>.</summary>
    public const int MonsterOffsetX = 146;

    /// <summary>Offset of the unit sprite from <see cref="Dialog"/>, <c>pos_mon</c>.</summary>
    public const int MonsterOffsetY = 175;

    /// <summary>Offset of the name area from <see cref="Dialog"/>.</summary>
    public const int NameOffsetX = 29;

    /// <summary>Offset of the name area from <see cref="Dialog"/>.</summary>
    public const int NameOffsetY = 37;

    /// <summary>Width the name is centred in, the <c>227</c> of <c>drawUnitName()</c>.</summary>
    public const int NameWidth = 227;

    /// <summary>Offset of the description area from <see cref="Dialog"/>.</summary>
    public const int DescriptionOffsetX = 37;

    /// <summary>Offset of the description area from <see cref="Dialog"/>.</summary>
    public const int DescriptionOffsetY = 185;

    /// <summary>Width the description is wrapped to, <c>descriptionWidth</c>.</summary>
    public const int DescriptionWidth = 210;

    /// <summary>Rows the description is allowed to use, <c>maxRows</c>.</summary>
    public const int DescriptionRows = 3;

    /// <summary>Offset of the amount box from <see cref="Dialog"/>.</summary>
    public const int AmountOffsetX = 80;

    /// <summary>Offset of the amount box from <see cref="Dialog"/>.</summary>
    public const int AmountOffsetY = 223;

    /// <summary>Width of the amount box drawn into the window sprite.</summary>
    public const int AmountWidth = 125;

    /// <summary>Height of the amount box drawn into the window sprite.</summary>
    public const int AmountHeight = 23;

    /// <summary>Offset of the upgrade button from <see cref="Dialog"/>.</summary>
    public const int UpgradeButtonOffsetX = 280;

    /// <summary>Offset of the upgrade button from <see cref="Dialog"/>.</summary>
    public const int UpgradeButtonOffsetY = 192;

    /// <summary>Offset of the dismiss button from <see cref="Dialog"/>.</summary>
    public const int DismissButtonOffsetX = 280;

    /// <summary>Offset of the dismiss button from <see cref="Dialog"/>.</summary>
    public const int DismissButtonOffsetY = 221;

    /// <summary>Distance the exit button keeps from the right edge, <c>58</c>.</summary>
    public const int ExitButtonRightGap = 58;

    /// <summary>Offset of the exit button from <see cref="Dialog"/>.</summary>
    public const int ExitButtonOffsetY = 221;

    /// <summary>Offset of the exit button from the right edge on the good interface.</summary>
    public const int GoodExitButtonOffsetX = 18;

    /// <summary>Pixels the evil window is pushed down, <c>isEvilInterface ? 3 : 0</c>.</summary>
    public const int EvilWindowOffsetY = 3;

    /// <summary>Pixels <c>pos_rt</c> moves right on the evil interface.</summary>
    public const int EvilDialogOffsetX = 9;

    /// <summary>Pixels <c>pos_rt</c> moves up on the evil interface.</summary>
    public const int EvilDialogOffsetY = -1;

    private ArmyInfoLayout(
        Rect window,
        Rect shadow,
        Rect dialog,
        Point stats,
        Point battleStats,
        Point monster,
        Rect name,
        Rect description,
        Rect amount,
        Rect exitButton,
        Rect? upgradeButton,
        Rect? dismissButton)
    {
        Window = window;
        Shadow = shadow;
        Dialog = dialog;
        Stats = stats;
        BattleStats = battleStats;
        Monster = monster;
        Name = name;
        Description = description;
        Amount = amount;
        ExitButton = exitButton;
        UpgradeButton = upgradeButton;
        DismissButton = dismissButton;
    }

    /// <summary>Where the window sprite lands, <c>dialogOffset</c>.</summary>
    public Rect Window { get; }

    /// <summary>Where the shadow sprite lands, <c>shadowOffset</c>.</summary>
    public Rect Shadow { get; }

    /// <summary>
    /// The window rectangle the content is measured from, <c>pos_rt</c> — <see cref="Window"/> with the evil
    /// interface's <c>( +9, -1 )</c>.
    /// </summary>
    public Rect Dialog { get; }

    /// <summary>Anchor of the attribute block, <c>monsterStatOffset</c>.</summary>
    public Point Stats { get; }

    /// <summary>Anchor of the battle attribute block, <c>battleStatOffset</c>.</summary>
    public Point BattleStats { get; }

    /// <summary>Where the unit sprite is anchored, <c>pos_mon</c> — its feet, not its top left.</summary>
    public Point Monster { get; }

    /// <summary>The 227 pixel band the unit's name is centred in.</summary>
    public Rect Name { get; }

    /// <summary>The 210 pixel, three row band the properties are written into.</summary>
    public Rect Description { get; }

    /// <summary>The amount box drawn into the window sprite; only the number is painted into it.</summary>
    public Rect Amount { get; }

    /// <summary>The button that closes the window.</summary>
    public Rect ExitButton { get; }

    /// <summary>The upgrade button, or <see langword="null"/> for a window without one.</summary>
    public Rect? UpgradeButton { get; }

    /// <summary>The dismiss button, or <see langword="null"/> for a window without one.</summary>
    public Rect? DismissButton { get; }

    /// <summary>
    /// Lays the dialog out, <c>dialog_armyinfo.cpp:522-556</c>.
    /// </summary>
    /// <remarks>
    /// The window is centred and then pushed down by <paramref name="windowOffsetY"/>, the reference's last
    /// parameter, which the battle and the castle call with a few pixels so that the window does not cover what it
    /// was opened from. The shadow follows the window by the anchors stored in its own sprite, which is why
    /// <paramref name="shadowShift"/> is passed in rather than guessed: the archive's shadow is not at
    /// <c>( 0, 0 )</c>.
    /// </remarks>
    /// <param name="screen">The whole display.</param>
    /// <param name="dialogSize">Size of the window sprite.</param>
    /// <param name="shadowSize">Size of the shadow sprite.</param>
    /// <param name="shadowShift">Position of the shadow's anchor relative to the window's.</param>
    /// <param name="exitButtonSize">Size of the exit button sprite.</param>
    /// <param name="upgradeButtonSize">Size of the upgrade button, or <see langword="null"/> without the flag.</param>
    /// <param name="dismissButtonSize">Size of the dismiss button, or <see langword="null"/> without the flag.</param>
    /// <param name="evilInterface">Whether the evil interface is in use.</param>
    /// <param name="windowOffsetY">Pixels to push the centred window down.</param>
    /// <param name="normalLineHeight">Height of a line of the normal font.</param>
    /// <param name="smallLineHeight">Height of a line of the small font.</param>
    /// <returns>The layout.</returns>
    public static ArmyInfoLayout Create(
        in Rect screen,
        in Size dialogSize,
        in Size shadowSize,
        in Point shadowShift,
        in Size exitButtonSize,
        Size? upgradeButtonSize,
        Size? dismissButtonSize,
        bool evilInterface,
        int windowOffsetY,
        int normalLineHeight,
        int smallLineHeight)
    {
        Point windowPosition = new(
            (screen.Width - dialogSize.Width) / 2,
            ((screen.Height - dialogSize.Height) / 2) + (evilInterface ? EvilWindowOffsetY : 0) + windowOffsetY);

        Rect window = new(windowPosition, dialogSize);
        Rect shadow = new(windowPosition + shadowShift, shadowSize);

        Point dialogPosition = evilInterface
            ? windowPosition.Offset(EvilDialogOffsetX, EvilDialogOffsetY)
            : windowPosition;

        Rect dialog = new(dialogPosition, dialogSize);

        Point stats = dialogPosition.Offset(StatsOffsetX, StatsOffsetY);
        Rect name = new(dialogPosition.Offset(NameOffsetX, NameOffsetY), new Size(NameWidth, normalLineHeight));
        Rect description = new(
            dialogPosition.Offset(DescriptionOffsetX, DescriptionOffsetY),
            new Size(DescriptionWidth, DescriptionRows * smallLineHeight));
        Rect amount = new(dialogPosition.Offset(AmountOffsetX, AmountOffsetY), new Size(AmountWidth, AmountHeight));

        int exitButtonX = dialogPosition.X + dialogSize.Width - ExitButtonRightGap - exitButtonSize.Width
            + (evilInterface ? 0 : GoodExitButtonOffsetX);
        Rect exitButton = new(new Point(exitButtonX, dialogPosition.Y + ExitButtonOffsetY), exitButtonSize);

        Rect? upgradeButton = upgradeButtonSize is { } upgradeSize
            ? new Rect(
                dialogPosition.Offset(UpgradeButtonOffsetX, UpgradeButtonOffsetY),
                upgradeSize)
            : null;

        Rect? dismissButton = dismissButtonSize is { } dismissSize
            ? new Rect(
                dialogPosition.Offset(DismissButtonOffsetX, DismissButtonOffsetY),
                dismissSize)
            : null;

        return new ArmyInfoLayout(
            window,
            shadow,
            dialog,
            stats,
            dialogPosition.Offset(BattleStatsOffsetX, BattleStatsOffsetY),
            dialogPosition.Offset(MonsterOffsetX, MonsterOffsetY),
            name,
            description,
            amount,
            exitButton,
            upgradeButton,
            dismissButton);
    }
}
