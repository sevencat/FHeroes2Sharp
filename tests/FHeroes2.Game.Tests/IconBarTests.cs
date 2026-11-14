using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="IconBar"/>: the layout of <c>Interface::IconsPanel::SetPos</c> for a 640x480
/// display, the frame tables of <c>LOCATORS.ICN</c> (<c>fheroes2::getCastleIcnIndex</c> and the empty
/// slot loop of <c>Interface::IconsBar::redrawBackground</c>) and where the icons land inside a slot.
/// </summary>
public sealed class IconBarTests
{
    private static readonly Size Screen = new(640, 480);

    private static IconBar Bar => new(new MapFrame(Screen, null).Panel);

    [Fact]
    public void TheHeroColumnSitsOneBorderBelowTheRadar()
    {
        // The bay of the panel starts at the bottom of the radar (160); the icons start one border
        // below it, which is where the brick of the panel background starts too.
        Assert.Equal(new Rect(480, 176, 56, 128), Bar.Heroes);
    }

    [Fact]
    public void TheCastleColumnIsSeventyTwoPixelsToTheRight()
    {
        Assert.Equal(new Rect(552, 176, 56, 128), Bar.Castles);
    }

    [Fact]
    public void SlotsStackThirtyTwoPixelsApart()
    {
        IconBar bar = Bar;

        Assert.Equal(new Rect(480, 176, 56, 32), IconBar.SlotAt(bar.Heroes, 0));
        Assert.Equal(new Rect(480, 208, 56, 32), IconBar.SlotAt(bar.Heroes, 1));
        Assert.Equal(new Rect(552, 272, 56, 32), IconBar.SlotAt(bar.Castles, 3));
    }

    [Fact]
    public void TheSixRacesTakeSixFramesOfCastlesAndTheNextSixOfTowns()
    {
        Assert.Equal(9, IconBar.CastleFrame(MapRace.Knight, hasFort: true));
        Assert.Equal(14, IconBar.CastleFrame(MapRace.Necromancer, hasFort: true));
        Assert.Equal(15, IconBar.CastleFrame(MapRace.Knight, hasFort: false));
        Assert.Equal(20, IconBar.CastleFrame(MapRace.Necromancer, hasFort: false));
    }

    [Fact]
    public void ARandomCastleHasNoIcon()
    {
        // The reference answers 25 and 26 here, but those are the editor's "random castle" icons and
        // LOCATORS.ICN only has 25 frames: a map always stores the race a castle resolved to.
        Assert.Null(IconBar.CastleFrame(MapRace.Random, hasFort: true));
        Assert.Null(IconBar.CastleFrame(MapRace.Random, hasFort: false));
        Assert.Null(IconBar.CastleFrame(MapRace.None, hasFort: true));
    }

    [Fact]
    public void EmptySlotsCycleThroughTheEightPlayerColors()
    {
        Assert.Equal(1, IconBar.EmptySlotFrame(0));
        Assert.Equal(8, IconBar.EmptySlotFrame(7));
        Assert.Equal(1, IconBar.EmptySlotFrame(8));
    }

    [Fact]
    public void TheIconsLandAtTheInsetOfTheirSlot()
    {
        IconBar bar = Bar;
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(3);

        bar.Draw(pixels, new FakeIcons(), [new Mp2CastlePosition(0, 0, 0x80, MapRace.Knight)]);

        // A castle in the first slot of the right column, empty slots everywhere else.
        Assert.Equal(0x22, pixels[bar.Castles.X + IconBar.IconInset, bar.Castles.Y + IconBar.IconInset]);
        Assert.Equal(0x11, pixels[bar.Castles.X + IconBar.IconInset, bar.Castles.Y + IconBar.SlotHeight + IconBar.IconInset]);
        Assert.Equal(0x11, pixels[bar.Heroes.X + IconBar.IconInset, bar.Heroes.Y + IconBar.IconInset]);

        // Nothing is painted on the slot border, the brick of the panel shows there.
        Assert.Equal(3, pixels[bar.Heroes.X, bar.Heroes.Y]);
        Assert.Equal(3, pixels[bar.Castles.X, bar.Castles.Y]);
    }

    [Fact]
    public void ASourceWithoutFramesLeavesThePanelAlone()
    {
        IconBar bar = Bar;
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(3);

        bar.Draw(pixels, new FakeIcons(nothing: true), []);

        Assert.Equal(3, pixels[bar.Heroes.X + IconBar.IconInset, bar.Heroes.Y + IconBar.IconInset]);
        Assert.Equal(3, pixels[bar.Castles.X + IconBar.IconInset, bar.Castles.Y + IconBar.IconInset]);
    }

    [Fact]
    public void ASourceOfNothingPaintsNothing()
    {
        IconBar bar = Bar;
        PixelBuffer pixels = new(Screen.Width, Screen.Height);
        pixels.Fill(3);

        bar.Draw(pixels, null, []);

        Assert.Equal(3, pixels[bar.Heroes.X + IconBar.IconInset, bar.Heroes.Y + IconBar.IconInset]);
    }

    /// <summary>Icons of constant colour: <c>0x11</c> for empty slots, <c>0x22</c> for castles.</summary>
    private sealed class FakeIcons(bool nothing = false) : IIconSpriteSource
    {
        public Sprite? GetCastle(MapRace race, bool hasFort)
        {
            if (nothing)
            {
                return null;
            }

            Sprite sprite = new(IconBar.IconWidth, IconBar.IconHeight);
            sprite.Image.Fill(0x22);
            return sprite;
        }

        public Sprite? GetEmptySlot(int row)
        {
            if (nothing)
            {
                return null;
            }

            Sprite sprite = new(IconBar.IconWidth, IconBar.IconHeight);
            sprite.Image.Fill(0x11);
            return sprite;
        }
    }
}
