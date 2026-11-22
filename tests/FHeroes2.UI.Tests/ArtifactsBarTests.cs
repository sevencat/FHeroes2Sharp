using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="ArtifactsBar"/>, the port of <c>ArtifactsBar</c> in <c>resource/artifact.cpp:1104-1183</c>.
/// The bar paints only, so every check here is a pixel or a piece of geometry.
/// </summary>
public sealed class ArtifactsBarTests
{
    private const byte PlateColor = 5;
    private const byte BorderColor = 7;
    private const byte IconColor = 3;
    private const byte EmptySlotColor = 9;

    [Fact]
    public void Mini_Ctor_TakesTheSlotSizeFromThePlate()
    {
        ArtifactsBar bar = new(CreateHero(), mini: true, readOnly: true, canChange: false,
            allowOpeningMagicBook: true, CreateSprites());

        Assert.True(bar.UseMiniSprites);
        Assert.Equal(new Size(ArtifactsBar.MiniSlotSize, ArtifactsBar.MiniSlotSize), bar.SingleItemSize);
        Assert.Equal(ArtifactBag.MaxCapacity, bar.ItemsCount);
    }

    [Fact]
    public void Mini_Ctor_WithoutThePlate_Throws()
    {
        StubArtifactsBarSprites sprites = CreateSprites();
        sprites.MiniPlate = null;

        Assert.Throws<InvalidOperationException>(() => new ArtifactsBar(CreateHero(), mini: true, readOnly: true,
            canChange: false, allowOpeningMagicBook: true, sprites));
    }

    [Fact]
    public void Full_Ctor_TakesTheSlotSizeFromTheEmptySlot()
    {
        ArtifactsBar bar = new(CreateHero(), mini: false, readOnly: true, canChange: false,
            allowOpeningMagicBook: true, CreateSprites());

        Assert.False(bar.UseMiniSprites);
        Assert.Equal(new Size(ArtifactsBar.FullSlotSize, ArtifactsBar.FullSlotSize), bar.SingleItemSize);
    }

    [Fact]
    public void Full_Ctor_WithoutTheEmptySlot_Throws()
    {
        StubArtifactsBarSprites sprites = CreateSprites();
        sprites.EmptySlot = null;

        Assert.Throws<InvalidOperationException>(() => new ArtifactsBar(CreateHero(), mini: false, readOnly: true,
            canChange: false, allowOpeningMagicBook: true, sprites));
    }

    [Fact]
    public void Redraw_WithoutATableSize_PaintsNothing()
    {
        PixelBuffer display = new(300, 300);

        ArtifactsBar bar = new(CreateHero(), mini: true, readOnly: true, canChange: false, allowOpeningMagicBook: true,
            CreateSprites());

        bar.Redraw(display);

        Assert.All(display.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public void Mini_Redraw_PaintsThePlateOfEverySlotAndTheIconOfTheArtifact()
    {
        PixelBuffer display = new(300, 300);

        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.GoldenGoose));

        ArtifactsBar bar = new(hero, mini: true, readOnly: true, canChange: false, allowOpeningMagicBook: true,
            CreateSprites());

        bar.SetRenderingOffset(new Point(10, 10));
        bar.SetTableSize(new Size(7, 2));

        bar.Redraw(display);

        // The first slot holds the artifact: its 32x32 icon sits one pixel inside the plate.
        Assert.Equal(IconColor, display[11, 11]);

        // The second slot is empty: the plate shows through.
        Assert.Equal(PlateColor, display[10 + ArtifactsBar.MiniSlotSize + 1, 11]);
        Assert.Equal(BorderColor, display[10 + ArtifactsBar.MiniSlotSize, 10]);

        // The second row is there too, which is what setTableSize( { 7, 2 } ) buys.
        Assert.Equal(BorderColor, display[10, 10 + ArtifactsBar.MiniSlotSize]);
    }

    [Fact]
    public void Mini_RedrawItem_FramesTheSelectedSlotWithoutHidingTheIcon()
    {
        PixelBuffer display = new(300, 300);

        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.GoldenGoose));

        ArtifactsBar bar = new(hero, mini: true, readOnly: true, canChange: false, allowOpeningMagicBook: true,
            CreateSprites());

        bar.RedrawItem(hero.BagArtifacts.Slots[0], new Rect(10, 10, ArtifactsBar.MiniSlotSize,
            ArtifactsBar.MiniSlotSize), selected: true, display);

        // The frame replaces the plate's gold border, and the interior stays the artifact's icon.
        Assert.Equal(ArtifactsBar.MiniCursorColor, display[10, 10]);
        Assert.Equal(IconColor, display[11, 11]);
    }

    [Fact]
    public void Full_Redraw_PaintsTheEmptySlotAndTheArtifactOverIt()
    {
        PixelBuffer display = new(300, 300);

        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.GoldenGoose));

        ArtifactsBar bar = new(hero, mini: false, readOnly: false, canChange: false, allowOpeningMagicBook: true,
            CreateSprites());

        bar.SetRenderingOffset(new Point(5, 5));
        bar.SetTableSize(new Size(7, 2));

        bar.Redraw(display);

        Assert.Equal(IconColor, display[5 + 10, 5 + 10]);
        Assert.Equal(EmptySlotColor, display[5 + ArtifactsBar.FullSlotSize + 10, 5 + 10]);
    }

    [Fact]
    public void Full_RedrawItem_FramesTheSelectedSlotOutsideIt()
    {
        PixelBuffer display = new(300, 300);

        Hero hero = CreateHero();
        Assert.True(hero.BagArtifacts.Push(ArtifactType.GoldenGoose));

        ArtifactsBar bar = new(hero, mini: false, readOnly: false, canChange: false, allowOpeningMagicBook: true,
            CreateSprites());

        bar.RedrawItem(hero.BagArtifacts.Slots[0], new Rect(10, 10, ArtifactsBar.FullSlotSize,
            ArtifactsBar.FullSlotSize), selected: true, display);

        // The 70x70 cursor hangs three pixels outside the 64x64 slot: outermost line, then the darker one, then the
        // icon inside the slot.
        Assert.Equal(ArtifactsBar.FullCursorColor, display[7, 7]);
        Assert.Equal(ArtifactsBar.FullCursorMiddleColor, display[8, 8]);
        Assert.Equal(IconColor, display[10 + 10, 10 + 10]);
    }

    [Fact]
    public void RedrawItem_WithAnEmptySlot_PaintsNothing()
    {
        PixelBuffer display = new(300, 300);

        ArtifactsBar bar = new(CreateHero(), mini: true, readOnly: true, canChange: false, allowOpeningMagicBook: true,
            CreateSprites());

        bar.RedrawItem(ArtifactType.Unknown, new Rect(0, 0, ArtifactsBar.MiniSlotSize, ArtifactsBar.MiniSlotSize),
            selected: false, display);

        Assert.All(display.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public void Select_MovesAndClearsTheCursor()
    {
        ArtifactsBar bar = new(CreateHero(), mini: true, readOnly: true, canChange: false, allowOpeningMagicBook: true,
            CreateSprites());

        Assert.False(bar.IsSelected);

        bar.Select(3);

        Assert.True(bar.IsSelected);
        Assert.Equal(3, bar.SelectedIndex);

        bar.Select(-1);

        Assert.False(bar.IsSelected);
        Assert.Equal(-1, bar.SelectedIndex);

        bar.Select(ArtifactBag.MaxCapacity - 1);
        Assert.Equal(ArtifactBag.MaxCapacity - 1, bar.SelectedIndex);

        bar.ResetSelected();
        Assert.Equal(-1, bar.SelectedIndex);

        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Select(ArtifactBag.MaxCapacity));
    }

    /// <summary>Builds the hero whose bag the bar shows; a fresh hero carries no artifacts.</summary>
    private static Hero CreateHero() => new(1, MapRace.Knight);

    /// <summary>Builds artwork that is big enough for every crop the bar makes.</summary>
    private static StubArtifactsBarSprites CreateSprites()
    {
        Sprite plateSource = new(40, 40);
        plateSource.Image.Fill(PlateColor);

        Sprite emptySlot = new(ArtifactsBar.FullSlotSize, ArtifactsBar.FullSlotSize);
        emptySlot.Image.Fill(EmptySlotColor);

        Sprite fullIcon = new(ArtifactsBar.FullSlotSize, ArtifactsBar.FullSlotSize);
        fullIcon.Image.Fill(IconColor);

        Sprite miniIcon = new(32, 32);
        miniIcon.Image.Fill(IconColor);

        return new StubArtifactsBarSprites
        {
            MiniPlate = SkillBarArtwork.CreateMiniPlate(plateSource, BorderColor),
            EmptySlot = emptySlot,
            FullIcon = fullIcon,
            MiniIcon = miniIcon,
        };
    }

    /// <summary>The artwork lookups of the bar, backed by single frames.</summary>
    private sealed class StubArtifactsBarSprites : IArtifactsBarSprites
    {
        public Sprite? MiniPlate { get; set; }

        public Sprite? EmptySlot { get; set; }

        public Sprite? FullIcon { get; set; }

        public Sprite? MiniIcon { get; set; }

        public Sprite? GetMiniArtifact(uint frame) => MiniIcon;

        public Sprite? GetArtifact(uint frame) => FullIcon;
    }
}
