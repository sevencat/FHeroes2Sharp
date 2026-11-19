using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
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
/// <b>What is not here</b>: the input loop. The reference opens the window, blocks in
/// <c>while ( le.HandleEvents( ... ) )</c> until a button is pressed and only then returns a
/// <c>Dialog::</c> result. The port splits that into <see cref="Create"/>, <see cref="Draw"/> and
/// <see cref="CreateButtons"/>, so the caller owns the loop and the tests can paint the window once without a
/// display. The result of a press is still <see cref="FromButton"/>.
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
