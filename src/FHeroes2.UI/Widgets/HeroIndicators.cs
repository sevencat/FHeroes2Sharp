using System.Globalization;

using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;

using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The artwork of the four indicators, the <c>Assets::getImage( ICN::HSICONS, frame )</c> calls of
/// <c>heroes_indicator.cpp</c>.
/// </summary>
/// <remarks>
/// Frames 1 (experience), 8 (spell points), 2/3/6 (luck: good, bad, neutral) and 4/5/7 (morale: good, bad, neutral) of
/// the same icon sheet. The sheet is handed in by the owner so this layer never reaches for the game data itself.
/// </remarks>
public sealed class HeroIndicatorArtwork
{
    /// <summary>The luck icon of a positive luck, frame 2.</summary>
    public required Sprite LuckGood { get; init; }

    /// <summary>The luck icon of a negative luck, frame 3.</summary>
    public required Sprite LuckBad { get; init; }

    /// <summary>The luck icon of a neutral luck, frame 6.</summary>
    public required Sprite LuckNeutral { get; init; }

    /// <summary>The morale icon of a positive morale, frame 4.</summary>
    public required Sprite MoraleGood { get; init; }

    /// <summary>The morale icon of a negative morale, frame 5.</summary>
    public required Sprite MoraleBad { get; init; }

    /// <summary>The morale icon of a neutral morale, frame 7.</summary>
    public required Sprite MoraleNeutral { get; init; }

    /// <summary>The experience icon, frame 1.</summary>
    public required Sprite Experience { get; init; }

    /// <summary>The spell points icon, frame 8.</summary>
    public required Sprite SpellPoints { get; init; }
}

/// <summary>
/// What one indicator asks its owner to show, the <c>showStandardTextMessage()</c> calls of
/// <c>heroes_indicator.cpp:119-129</c>, <c>:180-190</c>, <c>:254-265</c> and <c>:326-333</c>.
/// </summary>
/// <param name="Title">The title of the message, e.g. <c>LuckString( luck )</c>.</param>
/// <param name="Body">The full body: the description, the modifier list and its heading.</param>
public readonly record struct HeroIndicatorHelp(string Title, string Body);

/// <summary>
/// The icon family of the hero dialog, <c>HeroesIndicator</c> (<c>heroes_indicator.h:41-64</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference draws straight onto the display and keeps an <c>ImageRestorer</c> over its own rectangle, so a redraw
/// first puts the background back and then paints the icon. The port keeps the same restorer but paints into the frame
/// it is given, which is how every other widget here works; the owner passes the same frame that owns the restorer.
/// </para>
/// <para>
/// The description is prepared in the constructor (the reference builds it in <c>Redraw()</c> for luck and morale,
/// because the modifiers can change); <see cref="Redraw"/> refreshes it for those two and leaves it alone for the two
/// fixed ones.
/// </para>
/// </remarks>
public abstract class HeroIndicator
{
    private readonly ImageRestorer _back;
    private readonly IGlyphSource _smallWhite;

    /// <summary>Creates an indicator over one hero, <c>HeroesIndicator::HeroesIndicator()</c> (<c>:70-75</c>).</summary>
    /// <param name="hero">The hero to describe.</param>
    /// <param name="smallWhite">The font of the numbers inside the icons.</param>
    /// <param name="display">The frame the indicator lives on, remembered for the background restore.</param>
    /// <param name="area">The rectangle of the indicator.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    protected HeroIndicator(Hero hero, IGlyphSource smallWhite, PixelBuffer display, Rect area)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(smallWhite);
        ArgumentNullException.ThrowIfNull(display);

        Hero = hero;
        Area = area;
        _smallWhite = smallWhite;
        _back = new ImageRestorer(display);
    }

    /// <summary>The hero the indicator describes, <c>_hero</c>.</summary>
    public Hero Hero { get; }

    /// <summary>The rectangle of the indicator, <c>GetArea()</c>.</summary>
    public Rect Area { get; private set; }

    /// <summary>The body of the help message, <c>_description</c>.</summary>
    public string Description { get; protected set; } = string.Empty;

    /// <summary>The font of the numbers, <c>fheroes2::FontType::smallWhite()</c>.</summary>
    protected IGlyphSource SmallWhite => _smallWhite;

    /// <summary>Moves the indicator, <c>SetPos()</c> (<c>:77-83</c>).</summary>
    /// <param name="position">The new top left corner.</param>
    public void SetPos(Point position)
    {
        Area = new Rect(position.X, position.Y, Area.Width, Area.Height);

        _back.Update(Area.X, Area.Y, Area.Width, Area.Height);
    }

    /// <summary>Puts the background back without drawing, <c>redrawOnlyBackground()</c>.</summary>
    public void RedrawOnlyBackground() => _back.Restore();

    /// <summary>
    /// Puts the background back, the <c>_back.restore()</c> at the top of every <c>Redraw()</c>.
    /// </summary>
    protected void RestoreBackground() => _back.Restore();

    /// <summary>Paints the indicator, the <c>Redraw()</c> of the four classes.</summary>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="artwork">The icons.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public abstract void Redraw(PixelBuffer output, HeroIndicatorArtwork artwork);

    /// <summary>
    /// The help message a click asks for, the <c>QueueEventProcessing()</c> of the four classes.
    /// </summary>
    /// <param name="events">The frame's input.</param>
    /// <returns>The message to show, or <see langword="null"/> when the click missed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public abstract HeroIndicatorHelp? QueueEventProcessing(LocalEvent events);

    /// <summary>Whether the frame landed on this indicator.</summary>
    protected bool IsHit(LocalEvent events) =>
        events.MouseClickLeft(Area) || events.IsMouseRightButtonPressedInArea(Area);
}

/// <summary>The luck icon of one hero, <c>LuckIndicator</c> (<c>heroes_indicator.h:66-82</c>).</summary>
public sealed class LuckIndicator : HeroIndicator
{
    /// <summary>Creates the indicator of one hero, <c>:66-74</c>.</summary>
    /// <param name="hero">The hero to describe.</param>
    /// <param name="smallWhite">The font of the numbers, kept for the base class.</param>
    /// <param name="display">The frame the indicator lives on.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public LuckIndicator(Hero hero, IGlyphSource smallWhite, PixelBuffer display)
        : base(hero, smallWhite, display, new Rect(0, 0, 35, 26))
    {
    }

    /// <summary>The luck the last redraw saw, <c>_luck</c>.</summary>
    public int Luck { get; private set; } = FHeroes2.Game.Kingdom.Luck.Normal;

    /// <summary>Paints the icon once per point of luck, <c>LuckIndicator::Redraw()</c> (<c>:85-117</c>).</summary>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="artwork">The icons.</param>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public override void Redraw(PixelBuffer output, HeroIndicatorArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        System.Text.StringBuilder modifiers = new();
        Luck = Hero.GetLuckWithModifiers(modifiers);

        Description = FHeroes2.Game.Kingdom.Luck.Description(Luck) + "\n\n" + "Current Luck Modifiers:" + "\n\n" +
                      (modifiers.Length == 0 ? "None" : modifiers.ToString());

        Sprite sprite = Luck < 0 ? artwork.LuckBad : (Luck > 0 ? artwork.LuckGood : artwork.LuckNeutral);
        const int inter = 6;
        int count = Luck == 0 ? 1 : Math.Abs(Luck);
        int cx = Area.X + (Area.Width - (sprite.Width + inter * (count - 1))) / 2;
        int cy = Area.Y + (Area.Height - sprite.Height) / 2;

        RestoreBackground();

        while (count-- > 0)
        {
            ImageOps.BlitSprite(output, new Point(cx, cy), sprite);
            cx += inter;
        }
    }

    /// <summary>The help message of a click, <c>:119-129</c>.</summary>
    /// <param name="events">The frame's input.</param>
    /// <returns>The message, or <see langword="null"/> when the click missed.</returns>
    public override HeroIndicatorHelp? QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return IsHit(events)
            ? new HeroIndicatorHelp(FHeroes2.Game.Kingdom.Luck.GetLabel(Luck), Description)
            : null;
    }
}

/// <summary>The morale icon of one hero, <c>MoraleIndicator</c> (<c>heroes_indicator.h:84-100</c>).</summary>
public sealed class MoraleIndicator : HeroIndicator
{
    /// <summary>Creates the indicator of one hero, <c>:84-92</c>.</summary>
    /// <param name="hero">The hero to describe.</param>
    /// <param name="smallWhite">The font of the numbers, kept for the base class.</param>
    /// <param name="display">The frame the indicator lives on.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MoraleIndicator(Hero hero, IGlyphSource smallWhite, PixelBuffer display)
        : base(hero, smallWhite, display, new Rect(0, 0, 35, 26))
    {
    }

    /// <summary>The morale the last redraw saw, <c>_morale</c>.</summary>
    public int Morale { get; private set; } = FHeroes2.Game.Kingdom.Morale.Normal;

    /// <summary>Paints the icon once per point of morale, <c>MoraleIndicator::Redraw()</c> (<c>:131-178</c>).</summary>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="artwork">The icons.</param>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public override void Redraw(PixelBuffer output, HeroIndicatorArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        System.Text.StringBuilder modifiers = new();
        Morale = Hero.GetMoraleWithModifiers(modifiers);

        Description = FHeroes2.Game.Kingdom.Morale.Description(Morale) + "\n\n" + "Current Morale Modifiers:" + "\n\n" +
                      (modifiers.Length == 0 ? "None" : modifiers.ToString());

        uint spriteIndex = 7;

        if (Morale < FHeroes2.Game.Kingdom.Morale.Normal)
        {
            spriteIndex = 5;
        }
        else if (Morale > FHeroes2.Game.Kingdom.Morale.Normal)
        {
            spriteIndex = 4;
        }

        Sprite sprite = (spriteIndex switch
        {
            4 => artwork.MoraleGood,
            5 => artwork.MoraleBad,
            _ => artwork.MoraleNeutral,
        }).Clone();

        if (Hero.Army.AllTroopsAreUndead())
        {
            Description += "\n\n" + "Entire army is undead, so morale does not apply.";

            ImageOps.ApplyPalette(sprite.Image, PaletteId.Gray);
            ImageOps.ApplyPalette(sprite.Image, PaletteId.Darkening);
        }

        const int inter = 6;
        int count = Morale == 0 ? 1 : Math.Abs(Morale);
        int cx = Area.X + (Area.Width - (sprite.Width + inter * (count - 1))) / 2;
        int cy = Area.Y + (Area.Height - sprite.Height) / 2;

        RestoreBackground();

        while (count-- > 0)
        {
            ImageOps.BlitSprite(output, new Point(cx, cy), sprite);
            cx += inter;
        }
    }

    /// <summary>The help message of a click, <c>:180-190</c>.</summary>
    /// <param name="events">The frame's input.</param>
    /// <returns>The message, or <see langword="null"/> when the click missed.</returns>
    public override HeroIndicatorHelp? QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return IsHit(events)
            ? new HeroIndicatorHelp(FHeroes2.Game.Kingdom.Morale.GetLabel(Morale), Description)
            : null;
    }
}

/// <summary>The experience icon of one hero, <c>ExperienceIndicator</c> (<c>heroes_indicator.h:102-119</c>).</summary>
public sealed class ExperienceIndicator : HeroIndicator
{
    /// <summary>Creates the indicator of one hero, <c>:192-205</c>.</summary>
    /// <param name="hero">The hero to describe.</param>
    /// <param name="smallWhite">The font of the experience number.</param>
    /// <param name="display">The frame the indicator lives on.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ExperienceIndicator(Hero hero, IGlyphSource smallWhite, PixelBuffer display)
        : base(hero, smallWhite, display, new Rect(0, 0, 35, 36))
    {
        string description = "Current experience %{exp1}.\n Next level %{exp2}.";
        description = description.Replace(
            "%{exp1}", Hero.Experience.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        description = description.Replace(
            "%{exp2}",
            HeroExperience.FromLevel(HeroExperience.LevelFrom(Hero.Experience))
                .ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

        Description = description;
    }

    /// <summary>
    /// Whether the editor default is shown instead of the hero's own number, <c>_isDefault</c>.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>Paints the icon and the experience inside it, <c>ExperienceIndicator::Redraw()</c> (<c>:207-252</c>).</summary>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="artwork">The icons.</param>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public override void Redraw(PixelBuffer output, HeroIndicatorArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        Sprite experienceImage = artwork.Experience;
        ImageOps.BlitSprite(output, new Point(Area.X, Area.Y), experienceImage);

        Rect renderRoi = new(Area.X + 1, Area.Y + 24, 33, 9);
        int widthReduction = experienceImage.Width - renderRoi.Width - 1;

        if (IsDefault)
        {
            // For the default range of experience see Hero.GetStartingExperience().
            const string text = "40-90";
            int x = renderRoi.X + (experienceImage.Width - GlyphText.Measure(SmallWhite, text)) / 2 - widthReduction;
            GlyphText.DrawLine(SmallWhite, output, new Point(x, Area.Y + 25), text, renderRoi);

            return;
        }

        // Experience can be longer than the width of the rendering area, so a long value is shortened to millions and
        // the shortened text can still be drawn twice - the reference draws it inside the narrowing branch and again
        // after it.
        uint experienceValue = Hero.Experience;
        string experienceString = experienceValue.ToString(CultureInfo.InvariantCulture);

        if (GlyphText.Measure(SmallWhite, experienceString) > renderRoi.Width + 1)
        {
            const uint millionValue = 1_000_000;
            uint millions = experienceValue / millionValue;

            experienceString = experienceValue < 10 * millionValue
                ? millions.ToString(CultureInfo.InvariantCulture) + "." +
                  ((experienceValue - millions * millionValue) / (millionValue / 100)).ToString(CultureInfo.InvariantCulture) + "M"
                : millions.ToString(CultureInfo.InvariantCulture) + "." +
                  ((experienceValue - millions * millionValue) / (millionValue / 10)).ToString(CultureInfo.InvariantCulture) + "M";

            int x = renderRoi.X + (experienceImage.Width - GlyphText.Measure(SmallWhite, experienceString)) / 2 -
                    widthReduction;

            GlyphText.DrawLine(SmallWhite, output, new Point(x, Area.Y + 25), experienceString, renderRoi);
        }

        int textX = renderRoi.X + (experienceImage.Width - GlyphText.Measure(SmallWhite, experienceString)) / 2 -
                    widthReduction;

        GlyphText.DrawLine(SmallWhite, output, new Point(textX, Area.Y + 25), experienceString, renderRoi);
    }

    /// <summary>The help message of a click, <c>:254-265</c>.</summary>
    /// <param name="events">The frame's input.</param>
    /// <returns>The message, or <see langword="null"/> when the click missed.</returns>
    public override HeroIndicatorHelp? QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!IsHit(events))
        {
            return null;
        }

        string title = "Level " + Hero.Level.ToString(CultureInfo.InvariantCulture);

        return new HeroIndicatorHelp(title, Description);
    }
}

/// <summary>The spell points icon of one hero, <c>SpellPointsIndicator</c> (<c>heroes_indicator.h:121-139</c>).</summary>
public sealed class SpellPointsIndicator : HeroIndicator
{
    private bool _needBackgroundRestore;

    /// <summary>Creates the indicator of one hero, <c>:267-279</c>.</summary>
    /// <param name="hero">The hero to describe.</param>
    /// <param name="smallWhite">The font of the points.</param>
    /// <param name="display">The frame the indicator lives on.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public SpellPointsIndicator(Hero hero, IGlyphSource smallWhite, PixelBuffer display)
        : base(hero, smallWhite, display, new Rect(0, 0, 35, 36))
    {
        string description =
            "%{name} currently has %{point} spell points out of a maximum of %{max}. The maximum number of spell points " +
            "is 10 times the hero's knowledge. It is occasionally possible for the hero to have more than their " +
            "maximum spell points via special events.";
        description = description.Replace("%{name}", Hero.Name, StringComparison.Ordinal);
        description = description.Replace(
            "%{point}", Hero.SpellPoints.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        description = description.Replace(
            "%{max}", Hero.GetMaxSpellPoints().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

        Description = description;
    }

    /// <summary>
    /// Whether the editor default is shown instead of the hero's own numbers, <c>_isDefault</c>.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Paints the icon and the points inside it, <c>SpellPointsIndicator::Redraw()</c> (<c>:281-324</c>).
    /// </summary>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="artwork">The icons.</param>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public override void Redraw(PixelBuffer output, HeroIndicatorArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        Sprite sprite = artwork.SpellPoints;
        Rect renderRoi = new(Area.X + 1, Area.Y + 22, 33, 9);

        string points = Hero.SpellPoints.ToString(CultureInfo.InvariantCulture);
        string maximum = Hero.GetMaxSpellPoints().ToString(CultureInfo.InvariantCulture);
        string text = IsDefault ? maximum : points + "/" + maximum;

        bool drawOneLinedText = IsDefault || GlyphText.Measure(SmallWhite, text) <= renderRoi.Width;

        if (drawOneLinedText && _needBackgroundRestore)
        {
            _needBackgroundRestore = false;
            RedrawOnlyBackground();
        }

        ImageOps.BlitSprite(output, new Point(Area.X, Area.Y), sprite);

        if (drawOneLinedText)
        {
            int x = Area.X + sprite.Width / 2 - GlyphText.Measure(SmallWhite, text) / 2;
            GlyphText.DrawLine(SmallWhite, output, new Point(x, Area.Y + 23), text, renderRoi);

            return;
        }

        // The two numbers do not fit on one line: the icon is extended upwards and the numbers are stacked.
        ImageOps.Blit(
            output,
            new Point(renderRoi.X - 1, renderRoi.Y - renderRoi.Height - 1),
            sprite.Image,
            new Rect(0, 21, renderRoi.Width + 2, renderRoi.Height + 1));

        string topText = points + "/";
        Rect topRoi = new(renderRoi.X, renderRoi.Y - renderRoi.Height, renderRoi.Width, renderRoi.Height);
        GlyphText.DrawLine(SmallWhite, output, new Point(renderRoi.X, renderRoi.Y - renderRoi.Height + 1), topText, topRoi);

        int bottomX = renderRoi.X + renderRoi.Width - GlyphText.Measure(SmallWhite, maximum) - 1;
        GlyphText.DrawLine(SmallWhite, output, new Point(bottomX, renderRoi.Y + 1), maximum, renderRoi);

        // The background of the two lined text has to be restored once the one lined variant is drawn again.
        _needBackgroundRestore = true;
    }

    /// <summary>The help message of a click, <c>:326-333</c>.</summary>
    /// <param name="events">The frame's input.</param>
    /// <returns>The message, or <see langword="null"/> when the click missed.</returns>
    public override HeroIndicatorHelp? QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return IsHit(events) ? new HeroIndicatorHelp("Spell Points", Description) : null;
    }
}
