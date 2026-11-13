using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

public sealed class DoorHighlightTests
{
    private const byte GlowColor = 12;
    private const byte Background = 9;

    [Fact]
    public void Constants_MatchTheReferenceEngine()
    {
        // Game::MainMenu(): highlightDoor = SHNGANIM 18, settingsArea = (63,202,108,160) and
        // doorOffsetY = 55 * scale + offsetY at the logical resolution.
        Assert.Equal(18, DoorHighlight.HighlightFrame);
        Assert.Equal(55, DoorHighlight.SourceOffsetY);
        Assert.Equal(new Rect(63, 202, 108, 160), DoorHighlight.HoverArea);
    }

    [Fact]
    public void IsHovered_UsesTheSettingsAreaOfTheReference()
    {
        DoorHighlight door = new(CreateSprite(GlowColor, 63, 202, 20, 60));

        Assert.True(door.IsHovered(new Point(63, 202)));
        Assert.True(door.IsHovered(new Point(170, 361)));

        // The area is exclusive on the right and bottom edges, and a cursor that left the window is
        // never over the door.
        Assert.False(door.IsHovered(new Point(171, 361)));
        Assert.False(door.IsHovered(new Point(170, 362)));
        Assert.False(door.IsHovered(new Point(62, 202)));
        Assert.False(door.IsHovered(cursor: null));
    }

    [Fact]
    public void SourceArea_SkipsTheLanternRows()
    {
        // Sprite 18 of SHNGANIM.ICN is 136x180 at 48,183, the same anchor as every glow frame.
        DoorHighlight door = new(CreateSprite(GlowColor, 48, 183, 136, 180));

        Assert.Equal(new Rect(0, 55, 136, 125), door.SourceArea);
        Assert.Equal(new Rect(48, 238, 136, 125), door.Area);
    }

    [Fact]
    public void Constructor_RejectsASpriteWithoutRowsBelowTheLantern()
    {
        Sprite flat = CreateSprite(GlowColor, 0, 0, 4, DoorHighlight.SourceOffsetY);

        ArgumentException error = Assert.Throws<ArgumentException>(() => new DoorHighlight(flat));

        Assert.Contains("55", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Draw_WithoutHover_LeavesTheFrameUntouched()
    {
        DoorHighlight door = new(CreateSprite(GlowColor, 63, 202, 20, 60));
        PixelBuffer target = new(120, 320);
        target.Fill(Background);

        Assert.False(door.Draw(target, new Point(10, 10)));

        Assert.Equal(Background, target[63, 257]);
        Assert.Equal(Background, target[82, 261]);
    }

    [Fact]
    public void Draw_WithHover_LightsOnlyTheRowsBelowTheLantern()
    {
        DoorHighlight door = new(CreateSprite(GlowColor, 63, 202, 20, 60));
        PixelBuffer target = new(120, 320);
        target.Fill(Background);

        Assert.True(door.Draw(target, new Point(70, 280)));

        // The visible part starts at 202 + 55 and is 5 rows tall (60 - 55).
        Assert.Equal(GlowColor, target[63, 257]);
        Assert.Equal(GlowColor, target[82, 261]);
        Assert.Equal(Background, target[63, 256]);
        Assert.Equal(Background, target[63, 262]);
        Assert.Equal(Background, target[62, 257]);
        Assert.Equal(Background, target[83, 257]);
    }

    [Fact]
    public void Draw_RespectsTheSpriteTransformLayer()
    {
        // The ICN format expresses "keep the destination" as transform id 1; the glow of the door is
        // dithered with it, so a plain copy would paint black rectangles over the artwork.
        PixelBuffer image = new(2, 60);
        image.Fill(GlowColor);

        byte[] transforms = new byte[2 * 60];
        Array.Fill(transforms, (byte)1);
        transforms[(55 * 2) + 1] = 0;

        Sprite sprite = new(image, 63, 202, transforms);
        PixelBuffer target = new(120, 320);
        target.Fill(Background);

        Assert.True(new DoorHighlight(sprite).Draw(target, new Point(70, 280)));

        Assert.Equal(Background, target[63, 257]);
        Assert.Equal(GlowColor, target[64, 257]);
    }

    [Fact]
    public void CreateHighlight_AppliesTheNoCyclePaletteToACopy()
    {
        ReadOnlySpan<byte> table = PaletteTable.Get(PaletteId.NoCycle);

        int cycling = -1;
        for (int index = 0; index < table.Length; ++index)
        {
            if (table[index] != index)
            {
                cycling = index;
                break;
            }
        }

        Assert.True(cycling >= 0, "The NO_CYCLE table must map at least one entry.");

        PixelBuffer image = new(2, 60);
        image.Fill((byte)cycling);

        byte[] transforms = new byte[2 * 60];
        Sprite original = new(image, 63, 202, transforms);

        Sprite highlight = DoorHighlight.CreateHighlight(original);

        Assert.Equal(table[cycling], highlight.Image[0, 0]);
        Assert.Equal((byte)cycling, original.Image[0, 0]);

        // The light must keep the geometry and the dithering of the frame it was cut from.
        Assert.Equal(new Point(63, 202), new Point(highlight.OffsetX, highlight.OffsetY));
        Assert.Equal(new Size(2, 60), highlight.Image.Size);
        Assert.NotNull(highlight.Transform);
    }

    private static Sprite CreateSprite(byte color, int offsetX, int offsetY, int width, int height)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);
        return new Sprite(image, offsetX, offsetY);
    }
}
