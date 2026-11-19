using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The unit information window, <c>Dialog::ArmyInfo()</c> (<c>dialog.h:127</c>, <c>dialog_armyinfo.cpp:515</c>):
/// the model, the layout and the painting of the 520 by 256 window.
/// </summary>
/// <remarks>
/// <para>
/// The <c>flags</c> of the reference carry two different things at once: <c>Dialog::BUTTONS</c>
/// (<c>YES|OK|NO|CANCEL</c>) says whether the window has buttons at all, and <c>Dialog::UPGRADE</c> /
/// <c>Dialog::DISMISS</c> say which of the two small buttons it shows. The port keeps both in one
/// <see cref="DialogButton"/> parameter, which is what the flags are anyway.
/// </para>
/// <para>
/// <b>The loop is <see cref="ProcessEvents"/></b>: one call per frame with the frame's <see cref="LocalEvent"/>
/// answers what the caller should do next — ask about an upgrade, say it is too dear, ask about a dismissal, or exit.
/// The message boxes themselves stay outside, because they are modal windows of their own; the strings they show are
/// <see cref="UpgradePrompt"/>, <see cref="UpgradeDisabledPrompt"/> and <see cref="DismissPrompt"/>. The final result of
/// a press is still <see cref="FromButton"/>.
/// </para>
/// <para>
/// <b>What is not here</b>: the painting loop of the reference — <see cref="Create"/>, <see cref="Draw"/> and
/// <see cref="CreateButtons"/> are called by the caller, so the tests can paint the window once without a display —
/// the redraw timer that animates the creature, and the right-click-only variant that has no buttons.
/// </para>
/// <para>
/// <b>Morale and luck</b> are the reference's <c>troop.GetMorale()</c>/<c>troop.GetLuck()</c>. A plain
/// <see cref="Troop"/> has neither — they live on the hero and on the battle stack — so they arrive as
/// <see cref="MoraleValue"/> and <see cref="LuckValue"/>, defaulting to neutral, which is what the reference's
/// uninitialised <c>Troop</c> reports as well.
/// </para>
/// </remarks>
public sealed class ArmyInfoDialog
{
    /// <summary>Horizontal distance from the attribute anchor to the value column, <c>offsetX</c>.</summary>
    public const int ValueColumnOffsetX = 6;

    /// <summary>Vertical distance between two attribute lines, <c>offsetY</c>.</summary>
    public const int AttributeLineHeight = 16;

    /// <summary>Width the attribute labels may use, <c>leftSideLength</c>.</summary>
    public const int AttributeLabelWidth = 123;

    /// <summary>Width the attribute values may use, <c>rightSideLength</c> = <c>120 - offsetX</c>.</summary>
    public const int AttributeValueWidth = 114;

    /// <summary>Distance from the attribute anchor up to the colon column, <c>dst.x - 3</c>.</summary>
    public const int AttributeColonOffsetX = 3;

    /// <summary>Distance from the attribute anchor down to the first line, <c>dst.y + 2</c>.</summary>
    public const int AttributeFirstLineOffsetY = 2;

    /// <summary>
    /// The question of an upgrade, <c>dialog_armyinfo.cpp:645</c>: the caller shows it with YES and NO and the total
    /// upgrade cost.
    /// </summary>
    public const string UpgradePrompt =
        "Your troops can be upgraded, but it will cost you dearly. Do you wish to upgrade them?";

    /// <summary>
    /// The answer of an unaffordable upgrade, <c>dialog_armyinfo.cpp:639</c>: shown with a single OK, and the window
    /// stays open.
    /// </summary>
    public const string UpgradeDisabledPrompt = "You can't afford to upgrade your troops!";

    /// <summary>
    /// The question of a dismissal, <c>dialog_armyinfo.cpp:660</c>; its title is <see cref="PluralName"/>.
    /// </summary>
    public const string DismissPrompt = "Are you sure you want to dismiss this army?";

    private ArmyInfoDialog(Troop troop, DialogButton flags, bool isReflected, int moraleValue, int luckValue)
    {
        Troop = troop;
        Flags = flags;
        IsReflected = isReflected;
        MoraleValue = moraleValue;
        LuckValue = luckValue;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::ArmyInfo()</c> (<c>dialog_armyinfo.cpp:515</c>).
    /// </summary>
    /// <param name="troop">The unit to show.</param>
    /// <param name="flags">
    /// Which buttons the window has: <see cref="DialogButton.None"/> for a plain information window, or
    /// <see cref="DialogButton.Ok"/> combined with <see cref="DialogButton.Upgrade"/> and/or
    /// <see cref="DialogButton.Dismiss"/> for one that can act on the unit.
    /// </param>
    /// <param name="isReflected">Whether the unit sprite is drawn mirrored, <c>isReflected</c> of the reference.</param>
    /// <param name="moraleValue">The morale to print, <c>troop.GetMorale()</c>; neutral by default.</param>
    /// <param name="luckValue">The luck to print, <c>troop.GetLuck()</c>; neutral by default.</param>
    /// <returns>The model.</returns>
    public static ArmyInfoDialog Create(
        Troop troop,
        DialogButton flags = DialogButton.None,
        bool isReflected = false,
        int moraleValue = Morale.Normal,
        int luckValue = Luck.Normal) =>
        new(troop, flags, isReflected, moraleValue, luckValue);

    /// <summary>The unit to show.</summary>
    public Troop Troop { get; }

    /// <summary>Which buttons the window has.</summary>
    public DialogButton Flags { get; }

    /// <summary>Whether the unit sprite is mirrored.</summary>
    public bool IsReflected { get; }

    /// <summary>The morale value printed in the attribute block.</summary>
    public int MoraleValue { get; }

    /// <summary>The luck value printed in the attribute block.</summary>
    public int LuckValue { get; }

    /// <summary>Whether the window has buttons at all, <c>flags &amp; Dialog::BUTTONS</c> of the reference.</summary>
    public bool HasButtons =>
        (Flags & (DialogButton.Yes | DialogButton.Ok | DialogButton.No | DialogButton.Cancel)) != 0;

    /// <summary>Whether the upgrade button is shown, <c>flags &amp; Dialog::UPGRADE</c>.</summary>
    public bool CanUpgrade => (Flags & DialogButton.Upgrade) != 0;

    /// <summary>Whether the dismiss button is shown, <c>flags &amp; Dialog::DISMISS</c>.</summary>
    public bool CanDismiss => (Flags & DialogButton.Dismiss) != 0;

    /// <summary>
    /// Whether the upgrade button answers "you can't afford it" instead of asking, <c>flags &amp;
    /// Dialog::UPGRADE_DISABLE</c> — which the reference aliases to <see cref="DialogButton.Max"/>.
    /// </summary>
    public bool IsUpgradeDisabled => (Flags & DialogButton.Max) != 0;

    /// <summary>
    /// The name of the stack as a sentence names it, <c>troop.GetPluralName( count )</c>: the title of the dismiss
    /// message box (<c>dialog_armyinfo.cpp:660</c>).
    /// </summary>
    public string PluralName => MonsterNames.GetPluralName(Troop.Monster, Troop.Count);

    /// <summary>
    /// The unit's name as the window prints it, <c>troop.GetName()</c> — singular, not the plural of
    /// <c>GetPluralName()</c>, because the window names the creature and the amount box beside it carries the count.
    /// </summary>
    public string Name => MonsterNames.GetSingular(Troop.Monster);

    /// <summary>
    /// The eight attribute lines of <c>DrawMonsterStats()</c> (<c>dialog_armyinfo.cpp:126-284</c>), label and value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the reference's: attack, defense, shots, damage, hit points, speed, morale, luck. Two of those
    /// are conditional — the shots line only appears for a shooter, and the battle-only "Shots Left"/"Hit Points
    /// Left" pair is absent because a <see cref="Troop"/> has no battle state yet.
    /// </para>
    /// <para>
    /// Damage is the <b>single creature's</b>, not the stack's: the reference reads
    /// <c>Monster( troop.GetMonster() ).GetDamageMin()</c>, not <c>troop.GetDamageMin()</c> which multiplies by
    /// the count. So is hit points. The window describes the creature; the count is in the box below.
    /// </para>
    /// </remarks>
    /// <returns>The lines, in the order the window prints them.</returns>
    public List<StatLine> GetStatLines()
    {
        MonsterBattleStats stats = MonsterStatistics.GetBattleStats(Troop.Monster);

        List<StatLine> lines =
        [
            new StatLine("Attack Skill", Troop.GetAttackString()),
            new StatLine("Defense Skill", Troop.GetDefenseString()),
        ];

        if (stats.IsArchers)
        {
            lines.Add(new StatLine("Shots", Troop.GetShotString()));
        }

        lines.Add(new StatLine(
            "Damage",
            stats.DamageMin == stats.DamageMax
                ? stats.DamageMin.ToString()
                : $"{stats.DamageMin}-{stats.DamageMax}"));
        lines.Add(new StatLine("Hit Points", stats.HitPoints.ToString()));
        lines.Add(new StatLine("Speed", Troop.GetSpeedString()));
        lines.Add(new StatLine("Morale", Morale.GetName(MoraleValue)));
        lines.Add(new StatLine("Luck", Luck.GetName(LuckValue)));

        return lines;
    }

    /// <summary>
    /// The property sentences of the description block, laid out the way <c>DrawMonsterInfo()</c> lays them out
    /// (<c>dialog_armyinfo.cpp:430-472</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two shapes, chosen by fitting: when the creature has at most three sentences and every one of them fits the
    /// 210 pixel column, each sentence takes a line of its own and the block is bottom-aligned — the reference starts
    /// at row <c>3 - count</c>. Otherwise the sentences are joined with spaces and wrapped, and the wrapped text is
    /// bottom-aligned the same way. The returned <c>StartRow</c> is the first row, which is why it is a number and not
    /// a <see langword="null"/>-padded list: a wrapped description can be longer than three rows and run upwards out
    /// of the block, and the reference lets it.
    /// </para>
    /// <para>
    /// The sentences come from <c>MonsterDescriptions.GetPropertiesDescription</c>, which owns the wording; this
    /// method only decides how to break it.
    /// </para>
    /// </remarks>
    /// <param name="font">The small font, for measuring and wrapping.</param>
    /// <param name="width">Width of the column, 210 by default.</param>
    /// <param name="maxRows">Rows the block has, 3 by default.</param>
    /// <returns>The first row relative to the block and the lines to draw, in order.</returns>
    public (int StartRow, List<string> Lines) GetDescriptionLayout(
        IGlyphSource font,
        int width = ArmyInfoLayout.DescriptionWidth,
        int maxRows = ArmyInfoLayout.DescriptionRows)
    {
        List<string> sentences = MonsterDescriptions.GetPropertiesDescription(Troop.Monster);
        if (sentences.Count == 0)
        {
            return (0, []);
        }

        bool asSolidText = true;
        if (sentences.Count <= maxRows)
        {
            asSolidText = false;
            foreach (string sentence in sentences)
            {
                if (GlyphText.Measure(font, sentence) > width)
                {
                    asSolidText = true;
                    break;
                }
            }
        }

        if (!asSolidText)
        {
            return (maxRows - sentences.Count, sentences);
        }

        List<string> wrapped = GlyphText.WrapLines(font, string.Join(' ', sentences), width);
        return (maxRows - wrapped.Count, wrapped);
    }

    /// <summary>
    /// Lays the window out from the artwork it will be painted with.
    /// </summary>
    /// <param name="screen">The whole display.</param>
    /// <param name="artwork">The sprites, which also carry the good/evil flag and the shadow anchors.</param>
    /// <param name="exitButtonSize">Size of the exit button sprite.</param>
    /// <param name="windowOffsetY">Pixels to push the centred window down, <c>windowOffsetY</c>.</param>
    /// <param name="normalFont">The normal font, for the name band's height.</param>
    /// <param name="smallFont">The small font, for the description block's height.</param>
    /// <returns>The layout.</returns>
    public ArmyInfoLayout CreateLayout(
        in Rect screen,
        ArmyInfoArtwork artwork,
        in Size exitButtonSize,
        int windowOffsetY,
        IGlyphSource normalFont,
        IGlyphSource smallFont)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(normalFont);
        ArgumentNullException.ThrowIfNull(smallFont);

        return ArmyInfoLayout.Create(
            screen,
            new Size(artwork.Background.Width, artwork.Background.Height),
            new Size(artwork.Shadow.Width, artwork.Shadow.Height),
            artwork.GetShadowShift(),
            exitButtonSize,
            CanUpgrade && artwork.UpgradeButton is { } upgrade
                ? new Size(upgrade.Released.Width, upgrade.Released.Height)
                : null,
            CanDismiss && artwork.DismissButton is { } dismiss
                ? new Size(dismiss.Released.Width, dismiss.Released.Height)
                : null,
            artwork.EvilInterface,
            windowOffsetY,
            normalFont.LineHeight,
            smallFont.LineHeight);
    }

    /// <summary>
    /// Paints the window: shadow, window, unit, attribute block, name, description and amount
    /// (<c>dialog_armyinfo.cpp:526-572</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order matters and is the reference's: the unit goes down before the text, so that a tall sprite can run
    /// up into the name band without covering it. The buttons are not painted here — they are widgets that repaint
    /// themselves on hover, see <see cref="CreateButtons"/>.
    /// </para>
    /// <para>
    /// The description is bottom-aligned inside its three row block by starting at a row that can be negative; the
    /// reference does the same and lets a long description run up into the name band rather than truncating it.
    /// </para>
    /// </remarks>
    /// <param name="target">The display buffer.</param>
    /// <param name="layout">Where everything goes.</param>
    /// <param name="artwork">The sprites of the window and the shadow.</param>
    /// <param name="normalFont">The normal font, white.</param>
    /// <param name="smallFont">The small font, white.</param>
    /// <param name="nameFont">The font of the name, yellow in the reference; <paramref name="normalFont"/> by default.</param>
    /// <param name="unitSprite">The idle frame of the unit, or <see langword="null"/> for a window without one.</param>
    public void Draw(
        PixelBuffer target,
        ArmyInfoLayout layout,
        ArmyInfoArtwork artwork,
        IGlyphSource normalFont,
        IGlyphSource smallFont,
        IGlyphSource? nameFont = null,
        Sprite? unitSprite = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(normalFont);
        ArgumentNullException.ThrowIfNull(smallFont);

        ImageOps.BlitSprite(target, layout.Shadow.Position, artwork.Shadow);
        ImageOps.BlitSprite(target, layout.Window.Position, artwork.Background);

        if (unitSprite is { } sprite)
        {
            // The unit is anchored at its feet, the way the reference's pos_mon is: BlitSprite takes a top left
            // corner, so the sprite's own anchors have to come off first.
            Sprite drawn = IsReflected ? sprite.Flip(horizontal: true, vertical: false) : sprite;
            ImageOps.BlitSprite(
                target,
                new Point(layout.Monster.X - drawn.OffsetX, layout.Monster.Y - drawn.OffsetY),
                drawn);
        }

        DrawStats(target, layout, normalFont);
        DrawName(target, layout, nameFont ?? normalFont);
        DrawDescription(target, layout, smallFont);
        DrawAmount(target, layout, normalFont);
    }

    /// <summary>
    /// The three buttons of the window, positioned and ready to be drawn by the caller's loop
    /// (<c>dialog_armyinfo.cpp:592-619</c>).
    /// </summary>
    /// <remarks>
    /// The order is the one <see cref="FromButton"/> expects: upgrade, dismiss, exit. A button the flags do not ask
    /// for — or whose artwork was not supplied — is left out, so the list is shorter rather than padded with
    /// <see langword="null"/>.
    /// </remarks>
    /// <param name="layout">Where the buttons go.</param>
    /// <param name="artwork">The button sprites.</param>
    /// <returns>The buttons, in <see cref="FromButton"/> order.</returns>
    public List<ButtonSprite> CreateButtons(ArmyInfoLayout layout, ArmyInfoArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artwork);

        List<ButtonSprite> buttons = [];

        if (CanUpgrade && layout.UpgradeButton is { } upgrade && artwork.UpgradeButton is { } upgradeArtwork)
        {
            buttons.Add(new ButtonSprite(
                upgradeArtwork.Released,
                upgradeArtwork.Pressed,
                offsetX: upgrade.X,
                offsetY: upgrade.Y));
        }

        if (CanDismiss && layout.DismissButton is { } dismiss && artwork.DismissButton is { } dismissArtwork)
        {
            buttons.Add(new ButtonSprite(
                dismissArtwork.Released,
                dismissArtwork.Pressed,
                offsetX: dismiss.X,
                offsetY: dismiss.Y));
        }

        buttons.Add(new ButtonSprite(
            artwork.ExitButton is { } exitArtwork ? exitArtwork.Released : artwork.Background,
            artwork.ExitButton is { } pressedArtwork ? pressedArtwork.Pressed : artwork.Background,
            offsetX: layout.ExitButton.X,
            offsetY: layout.ExitButton.Y));

        return buttons;
    }

    /// <summary>
    /// The result of pressing a button, <c>result = Dialog::UPGRADE</c> and its neighbours
    /// (<c>dialog_armyinfo.cpp:636-670</c>).
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 upgrade, 1 dismiss, 2 exit.</param>
    /// <returns>
    /// <see cref="DialogButton.Upgrade"/>, <see cref="DialogButton.Dismiss"/> or <see cref="DialogButton.Cancel"/>.
    /// </returns>
    public static DialogButton FromButton(int index) => index switch
    {
        0 => DialogButton.Upgrade,
        1 => DialogButton.Dismiss,
        _ => DialogButton.Cancel,
    };

    /// <summary>What a right click on one of the three buttons says, <c>dialog_armyinfo.cpp:668-678</c>.</summary>
    /// <param name="button">The button under the cursor.</param>
    /// <returns>The heading and the body of the message.</returns>
    public static ArmyInfoTooltip GetTooltip(ArmyInfoButton button) => button switch
    {
        ArmyInfoButton.Upgrade => new ArmyInfoTooltip("Upgrade", "Upgrade your troops.", ShowsUpgradeCost: true),
        ArmyInfoButton.Dismiss => new ArmyInfoTooltip("Dismiss", "Dismiss this army."),
        _ => new ArmyInfoTooltip("Exit", "Exit this menu."),
    };

    /// <summary>
    /// One frame of the reference's message loop, <c>dialog_armyinfo.cpp:623-712</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the reference's: the three pressed looks, then the upgrade button, then dismiss, then exit, then
    /// the three right-click tooltips as an <c>else if</c> chain. The branches consume the click
    /// (<see cref="LocalEvent.MouseClickLeft(Rect)"/> eats the release), so a frame answers at most one button.
    /// </para>
    /// <para>
    /// <b>The two buttons are nullable</b>, like <c>std::unique_ptr&lt;Button&gt; buttonUpgrade</c> and its neighbour:
    /// pass <see langword="null"/> for the buttons the flags or the artwork left out, which is what
    /// <see cref="CreateButtons"/> decided when it built the list. The exit button always exists.
    /// </para>
    /// <para>
    /// <b>The three message boxes stay outside</b>. The reference asks the upgrade question and, on YES, breaks with
    /// <c>Dialog::UPGRADE</c>; it shows "you can't afford" with a single OK and keeps looping; the dismiss box asks and
    /// keeps looping on NO. The port answers <see cref="ArmyInfoAction.Upgrade"/> (ask with <see cref="UpgradePrompt"/>,
    /// answer <see cref="DialogButton.Upgrade"/> on YES), <see cref="ArmyInfoAction.UpgradeDisabled"/> (show
    /// <see cref="UpgradeDisabledPrompt"/> with OK and keep looping), <see cref="ArmyInfoAction.Dismiss"/> (ask with
    /// <see cref="DismissPrompt"/> and <see cref="PluralName"/> as the title, answer <see cref="DialogButton.Dismiss"/>
    /// on YES) and <see cref="ArmyInfoAction.Exit"/> (<see cref="DialogButton.Cancel"/>). The first two show
    /// <c>Troop.GetTotalUpgradeCost()</c> with the resource icon, which is the caller's box to draw.
    /// </para>
    /// <para>
    /// <b>Not here</b>: the redraw timer (the reference repaints on <c>CASTLE_UNIT_DELAY</c> to animate the creature),
    /// the right-click-only variant of the window — no buttons, which the reference closes when the right button comes
    /// up, so the caller keeps it up while <see cref="LocalEvent.IsMouseRightButtonPressed"/> — and the spell areas of a
    /// battle stack, whose tooltips need <c>DrawBattleStats()</c>.
    /// </para>
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="exitButton">The exit button, always present.</param>
    /// <param name="upgradeButton">The upgrade button, or <see langword="null"/> when the window has none.</param>
    /// <param name="dismissButton">The dismiss button, or <see langword="null"/> when the window has none.</param>
    /// <param name="hotKeys">The hot keys pressed this frame.</param>
    /// <returns>What the caller should do, plus the tooltip of the frame.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> or <paramref name="exitButton"/> is
    /// <see langword="null"/>.</exception>
    public ArmyInfoFrame ProcessEvents(
        LocalEvent events,
        ButtonSprite exitButton,
        ButtonSprite? upgradeButton = null,
        ButtonSprite? dismissButton = null,
        ArmyInfoHotKeys hotKeys = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(exitButton);

        // drawOnState( isMouseLeftButtonPressedAndHeldInArea( area() ) ), dialog_armyinfo.cpp:625-635.
        upgradeButton?.UpdatePressState(events);
        dismissButton?.UpdatePressState(events);
        exitButton.UpdatePressState(events);

        if (upgradeButton is not null && (events.MouseClickLeft(upgradeButton.Area) || hotKeys.Upgrade))
        {
            return new ArmyInfoFrame(
                IsUpgradeDisabled ? ArmyInfoAction.UpgradeDisabled : ArmyInfoAction.Upgrade);
        }

        if (dismissButton is not null && (events.MouseClickLeft(dismissButton.Area) || hotKeys.Dismiss))
        {
            return new ArmyInfoFrame(ArmyInfoAction.Dismiss);
        }

        if (events.MouseClickLeft(exitButton.Area) || hotKeys.CloseWindow)
        {
            return new ArmyInfoFrame(ArmyInfoAction.Exit);
        }

        // An else-if chain in the reference, exit first; the three areas never overlap.
        if (events.IsMouseRightButtonPressedInArea(exitButton.Area))
        {
            return new ArmyInfoFrame(ArmyInfoAction.None, ArmyInfoButton.Exit);
        }

        if (upgradeButton is not null && events.IsMouseRightButtonPressedInArea(upgradeButton.Area))
        {
            return new ArmyInfoFrame(ArmyInfoAction.None, ArmyInfoButton.Upgrade);
        }

        if (dismissButton is not null && events.IsMouseRightButtonPressedInArea(dismissButton.Area))
        {
            return new ArmyInfoFrame(ArmyInfoAction.None, ArmyInfoButton.Dismiss);
        }

        return default;
    }

    private void DrawStats(PixelBuffer target, ArmyInfoLayout layout, IGlyphSource font)
    {
        Point anchor = layout.Stats;
        int labelRight = anchor.X - AttributeColonOffsetX;
        int labelLeftLimit = anchor.X - AttributeLabelWidth;
        int valueX = anchor.X + ValueColumnOffsetX;

        List<StatLine> lines = GetStatLines();

        for (int index = 0; index < lines.Count; ++index)
        {
            int y = anchor.Y + AttributeFirstLineOffsetY + (index * AttributeLineHeight);

            GlyphText.DrawLine(font, target, new Point(labelRight, y), ":");

            string label = lines[index].Label;
            int labelX = Math.Max(labelRight - GlyphText.Measure(font, label), labelLeftLimit);
            GlyphText.DrawLine(font, target, new Point(labelX, y), label);

            GlyphText.DrawLine(font, target, new Point(valueX, y), lines[index].Value);
        }
    }

    private void DrawName(PixelBuffer target, ArmyInfoLayout layout, IGlyphSource font)
    {
        string name = Name;
        int x = layout.Name.X + ((layout.Name.Width - GlyphText.Measure(font, name)) / 2);

        GlyphText.DrawLine(font, target, new Point(x, layout.Name.Y + AttributeFirstLineOffsetY), name);
    }

    private void DrawDescription(PixelBuffer target, ArmyInfoLayout layout, IGlyphSource font)
    {
        (int startRow, List<string> lines) = GetDescriptionLayout(font);
        if (lines.Count == 0)
        {
            return;
        }

        for (int index = 0; index < lines.Count; ++index)
        {
            int y = layout.Description.Y + ((startRow + index) * font.LineHeight);
            GlyphText.DrawLine(font, target, new Point(layout.Description.X, y), lines[index]);
        }
    }

    private void DrawAmount(PixelBuffer target, ArmyInfoLayout layout, IGlyphSource font)
    {
        if (Troop.Count == 0)
        {
            return;
        }

        string amount = Troop.Count.ToString();
        int width = GlyphText.Measure(font, amount);
        int x = layout.Amount.X + (layout.Amount.Width / 2) - (width / 2);
        int y = layout.Amount.Y + (layout.Amount.Height / 2) - (font.LineHeight / 2);

        GlyphText.DrawLine(font, target, new Point(x, y + AttributeFirstLineOffsetY), amount);
    }

    /// <summary>One line of the attribute block: what it is called and what it says.</summary>
    /// <param name="Label">The left hand word, e.g. <c>"Attack Skill"</c>.</param>
    /// <param name="Value">The right hand text, e.g. <c>"12 (14)"</c>.</param>
    public readonly record struct StatLine(string Label, string Value);
}

/// <summary>The three buttons of the window, in the order of <see cref="ArmyInfoDialog.FromButton"/>.</summary>
public enum ArmyInfoButton
{
    /// <summary>The upgrade button, <c>Dialog::UPGRADE</c> (<c>dialog_armyinfo.cpp:596-604</c>).</summary>
    Upgrade,

    /// <summary>The dismiss button, <c>Dialog::DISMISS</c> (<c>dialog_armyinfo.cpp:606-614</c>).</summary>
    Dismiss,

    /// <summary>The exit button, always drawn (<c>dialog_armyinfo.cpp:586-588</c>).</summary>
    Exit,
}

/// <summary>
/// What a right click on one of the three buttons says, <c>dialog_armyinfo.cpp:668-678</c>.
/// </summary>
/// <param name="Title">The heading of the message box.</param>
/// <param name="Text">The body of the message box.</param>
/// <param name="ShowsUpgradeCost">
/// Whether the box also shows what the upgrade costs: the reference calls <c>showResourceMessage()</c> for the upgrade
/// button and <c>showStandardTextMessage()</c> for the other two.
/// </param>
public readonly record struct ArmyInfoTooltip(string Title, string Text, bool ShowsUpgradeCost = false);

/// <summary>
/// What a frame of the unit information window asks the caller to do, the body of the <c>while</c> loop in
/// <c>Dialog::ArmyInfo()</c> (<c>dialog_armyinfo.cpp:623-712</c>).
/// </summary>
/// <remarks>
/// The reference answers a message box inside the loop, because a box is modal there: only YES leaves with
/// <c>Dialog::UPGRADE</c> or <c>Dialog::DISMISS</c>, a NO keeps the window open. The port hands the question out and
/// lets the caller open it, so a NO is simply a frame that is not acted upon.
/// </remarks>
public enum ArmyInfoAction
{
    /// <summary>Nothing happened: holds the zero value, so a default frame is a quiet frame.</summary>
    None,

    /// <summary>
    /// The upgrade button or its hot key: ask <see cref="ArmyInfoDialog.UpgradePrompt"/> with YES and NO, and answer
    /// <see cref="DialogButton.Upgrade"/> on YES. The box shows <c>Troop.GetTotalUpgradeCost()</c>.
    /// </summary>
    Upgrade,

    /// <summary>
    /// The upgrade button of a window built with <see cref="DialogButton.Max"/>: show
    /// <see cref="ArmyInfoDialog.UpgradeDisabledPrompt"/> with a single OK and keep the window open.
    /// </summary>
    UpgradeDisabled,

    /// <summary>
    /// The dismiss button or its hot key: ask <see cref="ArmyInfoDialog.DismissPrompt"/> with
    /// <see cref="ArmyInfoDialog.PluralName"/> as the title, and answer <see cref="DialogButton.Dismiss"/> on YES.
    /// </summary>
    Dismiss,

    /// <summary>The exit button or the close-window hot key: <see cref="DialogButton.Cancel"/>.</summary>
    Exit,
}

/// <summary>
/// The hot keys of the unit information window besides the pointer, <c>dialog_armyinfo.cpp:636-666</c>.
/// </summary>
/// <remarks>
/// The port has no hot key table yet, so the scene decodes the frame's key press into these three answers and hands
/// them over.
/// </remarks>
/// <param name="Upgrade"><c>Game::HotKeyPressEvent( ARMY_UPGRADE_TROOP )</c>.</param>
/// <param name="Dismiss"><c>Game::HotKeyPressEvent( ARMY_DISMISS )</c>.</param>
/// <param name="CloseWindow"><c>Game::HotKeyCloseWindow()</c>: Escape or Enter.</param>
public readonly record struct ArmyInfoHotKeys(
    bool Upgrade = false,
    bool Dismiss = false,
    bool CloseWindow = false);

/// <summary>One frame of the unit information window.</summary>
/// <param name="Action">What the caller should do next.</param>
/// <param name="Tooltip">The button a right click landed on, or <see langword="null"/>.</param>
public readonly record struct ArmyInfoFrame(ArmyInfoAction Action, ArmyInfoButton? Tooltip = null);
