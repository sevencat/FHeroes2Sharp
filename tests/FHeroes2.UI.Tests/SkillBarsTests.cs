using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="PrimarySkillsBar"/> and <see cref="SecondarySkillsBar"/>, the port of
/// <c>gui/skill_bar.cpp</c>. The bars only paint, so every check here is a pixel or a piece of geometry.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class SkillBarsTests
{
    private const byte PlateColor = 5;
    private const byte BorderColor = 7;
    private const byte IconColor = 3;
    private const byte FullPlateColor = 6;

    [Fact]
    public void PrimaryMini_Ctor_TakesTheSlotSizeFromThePlateAndHoldsTheFourSkills()
    {
        PrimarySkillsBar bar = new(null, useSmallSize: true, CreateSprites());

        Assert.True(bar.UseSmallSize);
        Assert.Equal(new Size(SkillBarArtwork.MiniPlateSize, SkillBarArtwork.MiniPlateSize), bar.SingleItemSize);
        Assert.Equal(4, bar.ItemCount);
        Assert.Equal(PrimarySkillsBar.DefaultSkills, bar.Skills);
    }

    [Fact]
    public void PrimaryMini_Ctor_WithoutThePlate_Throws()
    {
        StubSkillBarSprites sprites = CreateSprites();
        sprites.MiniPlate = null;

        Assert.Throws<InvalidOperationException>(() => new PrimarySkillsBar(null, useSmallSize: true, sprites));
    }

    [Fact]
    public void PrimaryFull_Ctor_TakesTheSlotSizeFromTheSkillPlate()
    {
        PrimarySkillsBar bar = new(null, useSmallSize: false, CreateSprites());

        Assert.False(bar.UseSmallSize);
        Assert.Equal(new Size(73, 70), bar.SingleItemSize);
    }

    [Fact]
    public void Primary_Redraw_WithoutATableSize_PaintsNothing()
    {
        PixelBuffer display = new(200, 200);

        PrimarySkillsBar bar = new(null, useSmallSize: true, CreateSprites());

        bar.Redraw(display);

        Assert.All(display.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public void PrimaryMini_Redraw_PaintsTheSlotThenTheIconOnTop()
    {
        PixelBuffer display = new(200, 200);

        PrimarySkillsBar bar = new(null, useSmallSize: true, CreateSprites());
        bar.SetRenderingOffset(new Point(10, 10));
        bar.SetTableSize(new Size(4, 1));

        bar.Redraw(display);

        for (int slot = 0; slot < 4; ++slot)
        {
            int slotX = 10 + (slot * SkillBarArtwork.MiniPlateSize);

            // The frame of the plate, then the icon that is centred in it.
            Assert.Equal(BorderColor, display[slotX, 10]);
            Assert.Equal(IconColor, display[slotX + 1, 11]);
        }
    }

    [Fact]
    public void PrimaryFull_Redraw_PaintsThePlateOfEverySkill()
    {
        PixelBuffer display = new(400, 200);

        PrimarySkillsBar bar = new(null, useSmallSize: false, CreateSprites());
        bar.SetRenderingOffset(new Point(5, 5));
        bar.SetTableSize(new Size(4, 1));

        bar.Redraw(display);

        for (int slot = 0; slot < 4; ++slot)
        {
            Assert.Equal(FullPlateColor, display[5 + (slot * 73) + 10, 15]);
        }
    }

    [Fact]
    public void Primary_RedrawItem_WithAnUnknownSkill_Throws()
    {
        PixelBuffer display = new(200, 200);

        PrimarySkillsBar bar = new(null, useSmallSize: true, CreateSprites());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bar.RedrawItem(PrimarySkill.Unknown, new Rect(0, 0, 34, 34), display));
    }

    [Fact]
    public void SecondaryMini_Redraw_PaintsAPlateForEverySlotOfTheHero()
    {
        PixelBuffer display = new(300, 300);

        SecondarySkillsBar bar = new(CreateHero(), CreateSprites());
        bar.SetRenderingOffset(new Point(10, 10));
        bar.SetTableSize(new Size(4, 2));

        bar.Redraw(display);

        // A fresh hero holds only empty slots, so every slot keeps its plate.
        Assert.All(bar.Skills, skill => Assert.False(skill.IsValid));

        for (int slot = 0; slot < 8; ++slot)
        {
            int slotX = 10 + ((slot % 4) * SkillBarArtwork.MiniPlateSize);
            int slotY = 10 + ((slot / 4) * SkillBarArtwork.MiniPlateSize);

            Assert.Equal(BorderColor, display[slotX, slotY]);
        }
    }

    [Fact]
    public void Secondary_RedrawItem_WithAnEmptySlot_PaintsNothing()
    {
        PixelBuffer display = new(200, 200);

        SecondarySkillsBar bar = new(CreateHero(), CreateSprites());

        bar.RedrawItem(new SecondarySkillLevel(SecondarySkill.Unknown, SkillLevel.None), new Rect(0, 0, 34, 34),
            display);

        Assert.All(display.Pixels.ToArray(), pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public void SecondaryFull_RedrawBackground_CopiesTheEmptyPlate()
    {
        PixelBuffer display = new(200, 200);

        SecondarySkillsBar bar = new(CreateHero(), CreateSprites(), mini: false);
        bar.SetRenderingOffset(new Point(20, 20));
        bar.SetTableSize(new Size(1, 1));

        bar.Redraw(display);

        Assert.Equal(FullPlateColor, display[25, 25]);
    }

    [Fact]
    public void SecondaryFull_Ctor_WithoutThePlate_Throws()
    {
        StubSkillBarSprites sprites = CreateSprites();
        sprites.SecondarySkillPlate = null;

        Assert.Throws<InvalidOperationException>(() => new SecondarySkillsBar(CreateHero(), sprites, mini: false));
    }

    /// <summary>Builds the hero the bars read their values and skills from.</summary>
    private static Hero CreateHero() => new(1, MapRace.Knight);

    /// <summary>Builds artwork that is big enough for every crop the bars make.</summary>
    private static StubSkillBarSprites CreateSprites()
    {
        Sprite plateSource = new(40, 40);
        plateSource.Image.Fill(PlateColor);

        Sprite swapWindow = new(250, 200, 0, 0);
        swapWindow.Image.Fill(IconColor);

        Sprite primaryFrame = new(73, 70);
        primaryFrame.Image.Fill(FullPlateColor);

        Sprite secondaryFrame = new(75, 75);
        secondaryFrame.Image.Fill(FullPlateColor);

        Sprite miniSkill = new(34, 34);
        miniSkill.Image.Fill(IconColor);

        return new StubSkillBarSprites
        {
            PrimaryFrame = primaryFrame,
            SwapWindow = swapWindow,
            SecondaryFrame = secondaryFrame,
            MiniSkillFrame = miniSkill,
            SecondarySkillPlate = secondaryFrame,
            MiniPlate = SkillBarArtwork.CreateMiniPlate(plateSource, BorderColor),
        };
    }

    /// <summary>The artwork lookups of the bars, backed by single frames.</summary>
    private sealed class StubSkillBarSprites : ISkillBarSprites
    {
        public Sprite? PrimaryFrame { get; set; }

        public Sprite? SwapWindow { get; set; }

        public Sprite? SecondaryFrame { get; set; }

        public Sprite? MiniSkillFrame { get; set; }

        public Sprite? SecondarySkillPlate { get; set; }

        public Sprite? MiniPlate { get; set; }

        public Sprite? GetPrimarySkillFrame(PrimarySkill skill) => PrimaryFrame;

        public Sprite? GetSwapWindowFrame() => SwapWindow;

        public Sprite? GetSecondarySkillFrame(SecondarySkill skill) => SecondaryFrame;

        public Sprite? GetMiniSecondarySkillFrame(SecondarySkill skill) => MiniSkillFrame;
    }
}
