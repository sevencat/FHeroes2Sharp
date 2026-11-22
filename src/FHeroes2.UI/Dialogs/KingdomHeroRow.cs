using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Interface;
using FHeroes2.UI.Widgets;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>One hero of the hero list, <c>HeroRow</c> (<c>kingdom_overview.cpp:122-167</c>).</summary>
/// <remarks>
/// The row owns its four bars and they read the hero live, so a changed army needs no new row while a hired or
/// dismissed hero does — which is what <see cref="KingdomHeroesList.Refresh"/> checks.
/// </remarks>
public sealed class KingdomHeroRow
{
    private readonly KingdomOverviewArtwork _artwork;
    private readonly IGlyphSource _smallWhite;
    private readonly World _world;

    /// <summary>Creates the row of one hero, <c>HeroRow::Init()</c> (<c>:142-166</c>).</summary>
    /// <param name="hero">The hero to show.</param>
    /// <param name="world">The world the hero stands in, needed by the small portrait.</param>
    /// <param name="artwork">The artwork of the bars.</param>
    /// <param name="smallWhite">The font of the four primary skill values.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public KingdomHeroRow(Hero hero, World world, KingdomOverviewArtwork artwork, IGlyphSource smallWhite)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(smallWhite);

        _artwork = artwork;
        _smallWhite = smallWhite;
        _world = world;
        Hero = hero;

        ArmyBar = new ArmyBar(hero.Army, true, false);
        ArmyBar.SetBackground(
            new Size(41, 53), EmptyButtons.GetColorId(72, 28, 0), EmptyButtons.GetColorId(0xD0, 0xC0, 0x48));
        ArmyBar.SetTableSize(new Size(5, 1));
        ArmyBar.SetInBetweenItemsOffset(new Size(-1, 0));
        ArmyBar.TroopWindowOffsetY = -60;

        // The reference passes access = false: the artifacts are looked at, not rearranged, in this window.
        ArtifactsBar = new ArtifactsBar(hero, true, true, false, true, artwork.Artifacts);
        ArtifactsBar.SetTableSize(new Size(7, 2));
        ArtifactsBar.SetInBetweenItemsOffset(new Size(1, 8));

        SecondarySkillsBar = new SecondarySkillsBar(hero, artwork.Skills);
        SecondarySkillsBar.SetTableSize(new Size(4, 2));
        SecondarySkillsBar.SetInBetweenItemsOffset(new Size(-1, 8));

        PrimarySkillsBar = new PrimarySkillsBar(hero, true, artwork.Skills);
        PrimarySkillsBar.SetTableSize(new Size(4, 1));
        PrimarySkillsBar.SetInBetweenItemsOffset(new Size(2, 0));
        PrimarySkillsBar.SetTextOff(20, -13);
    }

    /// <summary>The hero the row shows, <c>hero</c>.</summary>
    public Hero Hero { get; }

    /// <summary>The army, <c>armyBar</c>.</summary>
    public ArmyBar ArmyBar { get; }

    /// <summary>The artifacts, <c>artifactsBar</c>.</summary>
    public ArtifactsBar ArtifactsBar { get; }

    /// <summary>The secondary skills, <c>secSkillsBar</c>.</summary>
    public SecondarySkillsBar SecondarySkillsBar { get; }

    /// <summary>The four primary skills, <c>primSkillsBar</c>.</summary>
    public PrimarySkillsBar PrimarySkillsBar { get; }

    /// <summary>Paints the row, <c>StatsHeroesList::RedrawItem()</c> (<c>:325-373</c>).</summary>
    /// <param name="dstx">Left edge of the row.</param>
    /// <param name="dsty">Top edge of the row.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Redraw(int dstx, int dsty, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, new Point(dstx, dsty), _artwork.HeroRowBackground);
        HeroPortraitPainter.DrawSmall(Hero, _artwork.Interface, _world, new Point(dstx + 5, dsty + 4), output);

        int offsetX = dstx + 90;
        const int offsetY = 22;

        DrawRightAligned(Hero.GetAttack().ToString(CultureInfo.InvariantCulture), offsetX, dsty + offsetY, output);
        offsetX += 35;
        DrawRightAligned(Hero.GetDefense().ToString(CultureInfo.InvariantCulture), offsetX, dsty + offsetY, output);
        offsetX += 35;
        DrawRightAligned(Hero.GetPower().ToString(CultureInfo.InvariantCulture), offsetX, dsty + offsetY, output);
        offsetX += 35;
        DrawRightAligned(Hero.GetKnowledge().ToString(CultureInfo.InvariantCulture), offsetX, dsty + offsetY, output);

        PrimarySkillsBar.SetRenderingOffset(new Point(dstx + 56, dsty - 3));
        PrimarySkillsBar.Redraw(output);

        SecondarySkillsBar.SetRenderingOffset(new Point(dstx + 206, dsty + 3));
        SecondarySkillsBar.Redraw(output);

        ArtifactsBar.SetRenderingOffset(new Point(dstx + 348, dsty + 3));
        ArtifactsBar.Redraw(output);

        ArmyBar.SetRenderingOffset(new Point(dstx - 1, dsty + 30));
        ArmyBar.Redraw(output, _artwork.Army);
    }

    /// <summary>
    /// Prints a primary skill value with its right edge on <paramref name="right"/>, the
    /// <c>text.draw( offsetX - text.width(), offsetY, display )</c> of <c>:343-356</c>.
    /// </summary>
    private void DrawRightAligned(string text, int right, int y, PixelBuffer output) =>
        GlyphText.DrawLine(_smallWhite, output, new Point(right - GlyphText.Measure(_smallWhite, text), y), text);
}

/// <summary>One town of the town list, <c>CstlRow</c> (<c>kingdom_overview.cpp:396-446</c>).</summary>
/// <remarks>
/// The hero army bar exists only while a hero is at home, so a row cannot be built once and forgotten: the window calls
/// <see cref="UpdateHeroArmyBar"/> whenever the hero list changed (<c>:483-497</c>, <c>:918</c>).
/// </remarks>
public sealed class KingdomCastleRow
{
    private readonly KingdomOverviewArtwork _artwork;
    private readonly IGlyphSource _smallWhite;
    private readonly IGlyphSource _smallYellow;
    private readonly World _world;
    private readonly Sprite _dwellingPlate;

    /// <summary>Creates the row of one town, <c>CstlRow::Init()</c> (<c>:415-430</c>).</summary>
    /// <param name="castle">The town to show.</param>
    /// <param name="world">The world, asked who is home.</param>
    /// <param name="artwork">The artwork of the bars.</param>
    /// <param name="smallWhite">The font of the name and of the dwelling stock.</param>
    /// <param name="smallYellow">The font of the weekly growth of a dwelling.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public KingdomCastleRow(CastleTown castle, World world, KingdomOverviewArtwork artwork, IGlyphSource smallWhite,
        IGlyphSource smallYellow)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(smallWhite);
        ArgumentNullException.ThrowIfNull(smallYellow);

        _artwork = artwork;
        _smallWhite = smallWhite;
        _smallYellow = smallYellow;
        _world = world;
        Castle = castle;

        GarrisonArmyBar = CreateArmyBar(castle.Army, true);

        DwellingsBar = new DwellingsBar(castle, new Size(39, 52));
        DwellingsBar.TableSize = new Size(6, 1);
        DwellingsBar.InBetweenItemsSize = new Size(2, 0);
        _dwellingPlate = DwellingsBarPainter.CreatePlate(new Size(39, 52), EmptyButtons.GetColorId(0xD0, 0xC0, 0x48));

        UpdateHeroArmyBar();
    }

    /// <summary>The town the row shows, <c>castle</c>.</summary>
    public CastleTown Castle { get; }

    /// <summary>The garrison, <c>garrisonArmyBar</c>; it exists even for an empty garrison.</summary>
    public ArmyBar GarrisonArmyBar { get; }

    /// <summary>The army of the hero at home, <c>heroArmyBar</c>, or <see langword="null"/> when nobody is home.</summary>
    public ArmyBar? HeroArmyBar { get; private set; }

    /// <summary>The six dwellings, <c>dwellingsBar</c>.</summary>
    public DwellingsBar DwellingsBar { get; }

    /// <summary>
    /// Rebuilds the hero army bar after the heroes changed, <c>CstlRow::updateHeroArmyBar()</c> (<c>:432-446</c>).
    /// </summary>
    /// <returns><see langword="true"/> when a hero is at home now.</returns>
    public bool UpdateHeroArmyBar()
    {
        if (_world.GetHero(Castle) is { } hero)
        {
            HeroArmyBar = CreateArmyBar(hero.Army, false);

            return true;
        }

        HeroArmyBar = null;

        return false;
    }

    /// <summary>Paints the row, <c>StatsCastlesList::RedrawItem()</c> (<c>:610-664</c>).</summary>
    /// <param name="dstx">Left edge of the row.</param>
    /// <param name="dsty">Top edge of the row.</param>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Redraw(int dstx, int dsty, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        ImageOps.BlitSprite(output, new Point(dstx, dsty), _artwork.CastleRowBackground);
        CastleIconPainter.Draw(Castle, _artwork.Interface, new Point(dstx + 17, dsty + 19), output);

        string? stats = null;

        if (_world.GetHero(Castle) is { } hero)
        {
            HeroPortraitPainter.DrawSmall(hero, _artwork.Interface, _world, new Point(dstx + 82, dsty + 19), output);
            stats = string.Join(
                '-',
                hero.GetAttack().ToString(CultureInfo.InvariantCulture),
                hero.GetDefense().ToString(CultureInfo.InvariantCulture),
                hero.GetPower().ToString(CultureInfo.InvariantCulture),
                hero.GetKnowledge().ToString(CultureInfo.InvariantCulture));
        }
        else if (Castle.Captain.IsValid)
        {
            if (_artwork.CaptainPortraits is { } portraits && portraits.TryGetValue(Castle.Race, out Sprite? portrait))
            {
                ImageOps.BlitSprite(output, new Point(dstx + 82, dsty + 19), portrait);
            }

            stats = string.Join(
                '-',
                Castle.Captain.GetAttack().ToString(CultureInfo.InvariantCulture),
                Castle.Captain.GetDefense().ToString(CultureInfo.InvariantCulture),
                Castle.Captain.GetPower().ToString(CultureInfo.InvariantCulture),
                Castle.Captain.GetKnowledge().ToString(CultureInfo.InvariantCulture));
        }

        if (stats is not null)
        {
            GlyphText.DrawLine(
                _smallWhite,
                output,
                new Point(dstx + 104 - (GlyphText.Measure(_smallWhite, stats) / 2), dsty + 45),
                stats);
        }

        string name = Castle.Name;
        GlyphText.DrawLine(
            _smallWhite,
            output,
            new Point(dstx + 72 - (GlyphText.Measure(_smallWhite, name) / 2), dsty + 63),
            name);

        // Without a hero at home the garrison sits 20 pixels lower and takes the room of the second bar.
        GarrisonArmyBar.SetRenderingOffset(new Point(dstx + 146, HeroArmyBar is null ? dsty + 20 : dsty));
        GarrisonArmyBar.Redraw(output, _artwork.Army);

        if (HeroArmyBar is { } heroArmyBar)
        {
            heroArmyBar.SetRenderingOffset(new Point(dstx + 146, dsty + 41));
            heroArmyBar.Redraw(output, _artwork.Army);
        }

        DwellingsBar.RenderingOffset = new Point(dstx + 349, dsty + 15);

        foreach (Rect slot in DwellingsBar.ItemAreas)
        {
            DwellingsBarPainter.RedrawBackground(slot, _dwellingPlate, output);
        }

        for (int i = 0; i < DwellingsBar.Items.Count && i < DwellingsBar.ItemAreas.Count; ++i)
        {
            DwellingsBarPainter.RedrawItem(
                DwellingsBar,
                DwellingsBar.Items[i],
                DwellingsBar.ItemAreas[i],
                _artwork.Monsters,
                _smallWhite,
                _smallYellow,
                _artwork.NotBuiltMarker,
                output);
        }
    }

    private static ArmyBar CreateArmyBar(Army army, bool troopWindowAbove)
    {
        ArmyBar bar = new(army, true, false);
        bar.SetBackground(
            new Size(41, 41), EmptyButtons.GetColorId(40, 12, 0), EmptyButtons.GetColorId(0xD0, 0xC0, 0x48));
        bar.SetTableSize(new Size(5, 1));
        bar.SetInBetweenItemsOffset(new Size(-1, 0));

        if (troopWindowAbove)
        {
            // Only the garrison lifts its troop window, <c>:423</c>.
            bar.TroopWindowOffsetY = -60;
        }

        return bar;
    }
}
