using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Tests the ported cursor rules: which image set a theme belongs to, which sprite inside that set
/// it uses, where the cursor is drawn relative to the mouse and how the adventure map distance
/// variants are selected. The expected values are copied from <c>Cursor::SetThemes()</c>,
/// <c>Cursor::SetOffset()</c>, <c>Cursor::DistanceThemes()</c> and
/// <c>Cursor::WithoutDistanceThemes()</c>.
/// </summary>
public sealed class CursorLayoutTests
{
    [Fact]
    public void AnchorOffset_CentersTheSpriteOnItsAnchor()
    {
        Assert.Equal(new Point(16, 16), CursorLayout.AnchorOffset(new Sprite(32, 32)));
        Assert.Equal(new Point(14, 14), CursorLayout.AnchorOffset(new Sprite(32, 32, 4, 4)));
        Assert.Equal(new Point(18, 15), CursorLayout.AnchorOffset(new Sprite(33, 31, -3, 1)));
    }

    [Fact]
    public void Offset_KeepsThePointersAtTheMousePosition()
    {
        Point centered = new(16, 16);

        Assert.Equal(new Point(0, 0), CursorLayout.Offset(CursorTheme.Pointer, centered));
        Assert.Equal(new Point(0, 0), CursorLayout.Offset(CursorTheme.PointerVideo, centered));
        Assert.Equal(new Point(0, 0), CursorLayout.Offset(CursorTheme.CombatPointer, centered));
    }

    [Fact]
    public void Offset_PinsTheSwordCursorsToTheirTip()
    {
        Point centered = new(16, 16);

        Assert.Equal(new Point(-20, -20), CursorLayout.Offset(CursorTheme.SwordBottomRight, centered));
        Assert.Equal(new Point(-5, -20), CursorLayout.Offset(CursorTheme.SwordBottomLeft, centered));
        Assert.Equal(new Point(-5, -5), CursorLayout.Offset(CursorTheme.SwordTopLeft, centered));
        Assert.Equal(new Point(-20, -5), CursorLayout.Offset(CursorTheme.SwordTopRight, centered));
        Assert.Equal(new Point(-16, -5), CursorLayout.Offset(CursorTheme.SwordTop, centered));
        Assert.Equal(new Point(-16, -25), CursorLayout.Offset(CursorTheme.SwordBottom, centered));
        Assert.Equal(new Point(-5, -7), CursorLayout.Offset(CursorTheme.SwordLeft, centered));
        Assert.Equal(new Point(-25, -7), CursorLayout.Offset(CursorTheme.SwordRight, centered));
    }

    [Fact]
    public void Offset_PinsTheScrollCursorsSoTheyStayOnScreen()
    {
        Point centered = new(16, 16);

        Assert.Equal(new Point(-15, 0), CursorLayout.Offset(CursorTheme.ScrollTopRight, centered));
        Assert.Equal(new Point(-15, 0), CursorLayout.Offset(CursorTheme.ScrollRight, centered));
        Assert.Equal(new Point(0, -15), CursorLayout.Offset(CursorTheme.ScrollBottom, centered));
        Assert.Equal(new Point(0, -15), CursorLayout.Offset(CursorTheme.ScrollBottomLeft, centered));
        Assert.Equal(new Point(-20, -20), CursorLayout.Offset(CursorTheme.ScrollBottomRight, centered));

        // The remaining scroll cursors keep the anchor based offset.
        Assert.Equal(new Point(-16, -16), CursorLayout.Offset(CursorTheme.ScrollTop, centered));
        Assert.Equal(new Point(-16, -16), CursorLayout.Offset(CursorTheme.ScrollLeft, centered));
    }

    [Fact]
    public void Offset_UsesTheSameHotspotForEveryHeroFightVariant()
    {
        Point centered = new(16, 16);

        for (CursorTheme theme = CursorTheme.HeroFight; theme <= CursorTheme.HeroFight8; theme = (CursorTheme)((int)theme + 1))
        {
            Assert.Equal(new Point(-10, -11), CursorLayout.Offset(theme, centered));
        }
    }

    [Fact]
    public void Offset_DefaultsToTheAnchorForOrdinaryThemes()
    {
        Assert.Equal(new Point(-16, -16), CursorLayout.Offset(CursorTheme.Castle, new Point(16, 16)));
    }

    [Theory]
    [InlineData(CursorTheme.Castle, false, CursorImageSet.AdventureMap)]
    [InlineData(CursorTheme.Castle, true, CursorImageSet.AdventureMapMonochrome)]
    [InlineData(CursorTheme.PointerVideo, true, CursorImageSet.AdventureMap)]
    [InlineData(CursorTheme.CombatMove, false, CursorImageSet.Combat)]
    [InlineData(CursorTheme.CombatMove, true, CursorImageSet.CombatMonochrome)]
    [InlineData(CursorTheme.SpellArrow, false, CursorImageSet.Spell)]
    [InlineData(CursorTheme.SpellArrow, true, CursorImageSet.SpellMonochrome)]
    [InlineData(CursorTheme.HeroMove, false, CursorImageSet.AdventureMapDistanceColor)]
    [InlineData(CursorTheme.HeroMove, true, CursorImageSet.AdventureMapDistanceMonochrome)]
    [InlineData(CursorTheme.None, false, CursorImageSet.AdventureMap)]
    public void ImageSet_SelectsTheSetFromTheTheme(
        CursorTheme theme,
        bool monochrome,
        CursorImageSet expected)
    {
        Assert.Equal(expected, CursorLayout.ImageSet(theme, monochrome));
    }

    [Theory]
    [InlineData(CursorTheme.Pointer, 0)]
    [InlineData(CursorTheme.Castle, 3)]
    [InlineData(CursorTheme.ScrollTopLeft, 0x27)]
    [InlineData(CursorTheme.SpellArrow, 0x26)]
    [InlineData(CursorTheme.HeroBoatAction8, 0x37)]
    public void ImageIndex_UsesTheLowByteOfTheTheme(CursorTheme theme, int expected)
    {
        Assert.Equal(expected, CursorLayout.ImageIndex(theme));
    }

    [Theory]
    [InlineData(CursorTheme.HeroMove, 0, CursorTheme.Pointer)]
    [InlineData(CursorTheme.HeroMove, 1, CursorTheme.HeroMove)]
    [InlineData(CursorTheme.HeroMove, 3, CursorTheme.HeroMove3)]
    [InlineData(CursorTheme.HeroFight, 8, CursorTheme.HeroFight8)]
    [InlineData(CursorTheme.HeroBoatAction, 12, CursorTheme.HeroBoatAction8)]
    [InlineData(CursorTheme.CombatMove, 5, CursorTheme.CombatMove)]
    public void WithDistance_SelectsTheDistanceVariant(
        CursorTheme theme,
        uint distance,
        CursorTheme expected)
    {
        Assert.Equal(expected, CursorLayout.WithDistance(theme, distance));
    }

    [Theory]
    [InlineData(CursorTheme.HeroBoat4, CursorTheme.HeroBoat)]
    [InlineData(CursorTheme.HeroMove8, CursorTheme.HeroMove)]
    [InlineData(CursorTheme.HeroAnchor2, CursorTheme.HeroAnchor)]
    [InlineData(CursorTheme.HeroAction7, CursorTheme.HeroAction)]
    [InlineData(CursorTheme.HeroMove, CursorTheme.HeroMove)]
    [InlineData(CursorTheme.CombatMove, CursorTheme.CombatMove)]
    public void WithoutDistance_DropsTheDistanceVariant(CursorTheme theme, CursorTheme expected)
    {
        Assert.Equal(expected, CursorLayout.WithoutDistance(theme));
    }

    [Fact]
    public void WithoutDistance_RoundTripsWithDistance()
    {
        CursorTheme[] themes =
        [
            CursorTheme.HeroMove,
            CursorTheme.HeroFight,
            CursorTheme.HeroBoat,
            CursorTheme.HeroAnchor,
            CursorTheme.HeroMeet,
            CursorTheme.HeroAction,
            CursorTheme.HeroBoatAction,
        ];

        foreach (CursorTheme theme in themes)
        {
            for (uint distance = 1; distance <= CursorLayout.MaximumDistance; ++distance)
            {
                Assert.Equal(
                    theme,
                    CursorLayout.WithoutDistance(CursorLayout.WithDistance(theme, distance)));
            }
        }
    }
}
