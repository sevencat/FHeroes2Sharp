using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

public sealed class ButtonSpriteTests
{
    private const byte ReleasedColor = 5;
    private const byte PressedColor = 7;
    private const byte Background = 3;

    [Fact]
    public void Areas_ComeFromBothSpritesAndTheButtonOffset()
    {
        ButtonSprite button = new(
            CreateSprite(ReleasedColor, 10, 20, 6, 4),
            CreateSprite(PressedColor, 12, 18, 5, 3),
            offsetX: 100,
            offsetY: 200);

        Assert.Equal(new Rect(110, 220, 6, 4), button.ReleasedArea);
        Assert.Equal(new Rect(112, 218, 5, 3), button.PressedArea);
        Assert.Equal(button.ReleasedArea, button.Area);
    }

    [Fact]
    public void Press_SwitchesTheActiveArea()
    {
        ButtonSprite button = new(
            CreateSprite(ReleasedColor, 0, 0, 6, 4),
            CreateSprite(PressedColor, 1, 1, 5, 3));

        Assert.True(button.Press());
        Assert.True(button.IsPressed);
        Assert.Equal(button.PressedArea, button.Area);

        Assert.True(button.Release());
        Assert.False(button.IsPressed);
        Assert.Equal(button.ReleasedArea, button.Area);
    }

    [Fact]
    public void DisabledButton_RefusesPressAndRelease()
    {
        ButtonSprite button = new(CreateSprite(ReleasedColor, 0, 0, 6, 4), CreateSprite(PressedColor, 0, 0, 6, 4));
        button.Press();

        button.Disable();

        // A button cannot be disabled and pressed at the same time.
        Assert.False(button.IsEnabled);
        Assert.False(button.IsPressed);
        Assert.False(button.Press());
        Assert.False(button.Release());

        button.Enable();
        Assert.True(button.IsEnabled);
        Assert.True(button.Press());
    }

    [Fact]
    public void ReleasedArea_FollowsItsOwnDisabledSprite()
    {
        // Dialog buttons often ship a disabled frame of a different size, so the hit box has to
        // follow the sprite the button actually draws.
        ButtonSprite button = new(
            CreateSprite(ReleasedColor, 0, 0, 6, 4),
            CreateSprite(PressedColor, 0, 0, 6, 4),
            CreateSprite(ReleasedColor, 2, 3, 9, 8));

        Assert.Equal(new Rect(0, 0, 6, 4), button.ReleasedArea);

        button.Disable();
        Assert.Equal(new Rect(2, 3, 9, 8), button.ReleasedArea);

        button.Enable();
        Assert.Equal(new Rect(0, 0, 6, 4), button.ReleasedArea);
    }

    [Fact]
    public void SetPosition_MovesSpritesAndAreasTogether()
    {
        ButtonSprite button = new(
            CreateSprite(ReleasedColor, 4, 5, 6, 4),
            CreateSprite(PressedColor, 4, 5, 6, 4));

        button.SetPosition(50, 60);

        Assert.Equal(50, button.OffsetX);
        Assert.Equal(60, button.OffsetY);
        Assert.Equal(new Rect(54, 65, 6, 4), button.ReleasedArea);
        Assert.Equal(new Rect(54, 65, 6, 4), button.PressedArea);
    }

    [Fact]
    public void SetSprite_RecomputesTheAreas()
    {
        ButtonSprite button = new(CreateSprite(ReleasedColor, 0, 0, 2, 2), CreateSprite(PressedColor, 0, 0, 2, 2));

        button.SetSprite(CreateSprite(ReleasedColor, 7, 8, 11, 3), CreateSprite(PressedColor, 7, 8, 11, 3));

        Assert.Equal(new Rect(7, 8, 11, 3), button.ReleasedArea);
        Assert.Equal(new Rect(7, 8, 11, 3), button.PressedArea);
    }

    [Fact]
    public void Draw_BlitsTheSpriteOfTheCurrentStateAtItsArea()
    {
        ButtonSprite button = new(
            CreateSprite(ReleasedColor, 2, 3, 4, 2),
            CreateSprite(PressedColor, 2, 3, 4, 2),
            offsetX: 1,
            offsetY: 1);

        PixelBuffer target = new(12, 12);

        Assert.True(button.Draw(target));
        Assert.Equal(ReleasedColor, target[3, 4]);
        Assert.Equal(ReleasedColor, target[6, 5]);
        Assert.Equal(0, target[2, 4]);
        Assert.Equal(0, target[7, 5]);

        button.Press();
        Assert.True(button.Draw(target));
        Assert.Equal(PressedColor, target[3, 4]);
        Assert.Equal(PressedColor, target[6, 5]);
    }

    [Fact]
    public void Draw_OfADisabledButton_DarkensTheReleasedSprite()
    {
        ButtonSprite button = new(CreateSprite(ReleasedColor, 0, 0, 2, 2), CreateSprite(PressedColor, 0, 0, 2, 2));
        button.Disable();

        PixelBuffer target = new(4, 4);
        button.Draw(target);

        // The default disabled look is the released sprite run through the darkening palette, the
        // PAL::PaletteType::DARKENING copy of the reference.
        byte darkened = PaletteTable.Get(PaletteId.Darkening)[ReleasedColor];
        Assert.NotEqual(ReleasedColor, darkened);
        Assert.Equal(darkened, target[0, 0]);
        Assert.Equal(darkened, target[1, 1]);
    }

    [Fact]
    public void Draw_OfAHiddenButton_PaintsNothing()
    {
        ButtonSprite button = new(CreateSprite(ReleasedColor, 0, 0, 2, 2), CreateSprite(PressedColor, 0, 0, 2, 2));
        button.Hide();

        PixelBuffer target = new(4, 4);
        target.Fill(Background);

        Assert.False(button.Draw(target));
        Assert.All(target.AsSpan().ToArray(), pixel => Assert.Equal(Background, pixel));
    }

    [Fact]
    public void Draw_RespectsTheSpriteTransformLayer()
    {
        // The ICN format expresses "keep the destination" as transform id 1, which a dialog button
        // uses for the transparent corners of its artwork.
        PixelBuffer image = new(2, 1);
        image.Fill(ReleasedColor);
        Sprite released = new(image, 0, 0, new byte[] { 0, 1 });
        Sprite pressed = new(image.Clone(), 0, 0, new byte[] { 0, 1 });

        PixelBuffer target = new(2, 1);
        target.Fill(Background);

        new ButtonSprite(released, pressed).Draw(target);

        Assert.Equal(ReleasedColor, target[0, 0]);
        Assert.Equal(Background, target[1, 0]);
    }

    [Fact]
    public void DrawShadow_CastsTheReleasedSpriteFivePixelsLeftAndBelow()
    {
        // A fully opaque silhouette, so every shadow pixel comes from the area of the button.
        Sprite released = new(Filled(6, 6), 0, 0, new byte[36]);
        Sprite pressed = new(Filled(6, 6), 0, 0, new byte[36]);
        ButtonSprite button = new(released, pressed, offsetX: 5, offsetY: 5);

        PixelBuffer target = new(20, 20);
        target.Fill(Background);

        button.DrawShadow(target);

        // The ramp gets stronger towards the artwork: the pixel next to the left edge uses table 5,
        // the one that sits beside the bottom left corner uses the strongest table of the shadow.
        Assert.Equal(PaletteTransformTable.Apply(5, Background), target[4, 6]);
        Assert.Equal(PaletteTransformTable.Apply(2, Background), target[4, 10]);
        Assert.Equal(Background, target[0, 0]);
        Assert.Equal(Background, target[19, 19]);
    }

    [Fact]
    public void DrawShadow_DoesNothingWithoutATransformLayer()
    {
        ButtonSprite button = new(CreateSprite(ReleasedColor, 0, 0, 6, 6), CreateSprite(PressedColor, 0, 0, 6, 6));

        PixelBuffer target = new(20, 20);
        target.Fill(Background);

        button.DrawShadow(target);

        Assert.All(target.AsSpan().ToArray(), pixel => Assert.Equal(Background, pixel));
    }

    private static Sprite CreateSprite(byte color, int offsetX, int offsetY, int width, int height) =>
        new(Filled(width, height, color), offsetX, offsetY);

    private static PixelBuffer Filled(int width, int height, byte color = ReleasedColor)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);
        return image;
    }
}

