using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

public sealed class ButtonTests
{
    private const byte ReleasedColor = 5;
    private const byte HoveredColor = 6;
    private const byte PressedColor = 7;

    [Fact]
    public void Area_ComesFromSpriteAnchorAndSize()
    {
        Button button = new(CreateSprite(ReleasedColor, 482, 176, 12, 20));

        Assert.Equal(new Rect(482, 176, 12, 20), button.Area);
        Assert.Equal(ButtonState.Released, button.State);
    }

    [Fact]
    public void HitTest_UsesTheReleasedSpriteBounds()
    {
        Button button = new(CreateSprite(ReleasedColor, 10, 20, 8, 8));

        Assert.True(button.HitTest(new Point(10, 20)));
        Assert.True(button.HitTest(new Point(17, 27)));
        Assert.False(button.HitTest(new Point(18, 27)));
        Assert.False(button.HitTest(new Point(17, 28)));
        Assert.False(button.HitTest(new Point(9, 20)));
    }

    [Fact]
    public void Update_HoverThenPressThenLeave_ReportsEveryTransition()
    {
        Button button = new(CreateSprite(ReleasedColor, 0, 0, 4, 4));

        Assert.True(button.Update(new Point(1, 1), leftButtonDown: false));
        Assert.Equal(ButtonState.Hovered, button.State);

        // Staying inside without a state change reports nothing, so the caller can skip a redraw.
        Assert.False(button.Update(new Point(2, 2), leftButtonDown: false));

        Assert.True(button.Update(new Point(2, 2), leftButtonDown: true));
        Assert.Equal(ButtonState.Pressed, button.State);
        Assert.True(button.IsPressed);

        Assert.True(button.Update(new Point(40, 40), leftButtonDown: false));
        Assert.Equal(ButtonState.Released, button.State);
    }

    [Fact]
    public void Update_WithoutCursor_ReturnsToReleased()
    {
        Button button = new(CreateSprite(ReleasedColor, 0, 0, 4, 4));
        button.Update(new Point(1, 1), leftButtonDown: true);

        Assert.True(button.Update(cursor: null, leftButtonDown: true));
        Assert.Equal(ButtonState.Released, button.State);
    }

    [Fact]
    public void CurrentSprite_FallsBackToReleasedWhenStatesAreMissing()
    {
        Sprite released = CreateSprite(ReleasedColor, 0, 0, 4, 4);
        Button button = new(released);

        Assert.Same(released, button.CurrentSprite);

        button.Update(new Point(1, 1), leftButtonDown: true);
        Assert.Same(released, button.CurrentSprite);
    }

    [Fact]
    public void CurrentSprite_FollowsTheState()
    {
        Sprite released = CreateSprite(ReleasedColor, 0, 0, 4, 4);
        Sprite hovered = CreateSprite(HoveredColor, 0, 0, 4, 4);
        Sprite pressed = CreateSprite(PressedColor, 0, 0, 4, 4);
        Button button = new(released, hovered, pressed);

        Assert.Same(released, button.CurrentSprite);

        button.Update(new Point(1, 1), leftButtonDown: false);
        Assert.Same(hovered, button.CurrentSprite);

        button.Update(new Point(1, 1), leftButtonDown: true);
        Assert.Same(pressed, button.CurrentSprite);
    }

    [Fact]
    public void Draw_BlitsTheCurrentStateAtItsAnchor()
    {
        Sprite released = CreateSprite(ReleasedColor, 2, 3, 3, 2);
        Sprite hovered = CreateSprite(HoveredColor, 2, 3, 3, 2);
        Button button = new(released, hovered);
        PixelBuffer target = new(8, 8);

        button.Draw(target);
        Assert.Equal(ReleasedColor, target[2, 3]);
        Assert.Equal(ReleasedColor, target[4, 4]);
        Assert.Equal(0, target[1, 3]);
        Assert.Equal(0, target[5, 4]);

        button.Update(new Point(3, 3), leftButtonDown: false);
        button.Draw(target);
        Assert.Equal(HoveredColor, target[2, 3]);
        Assert.Equal(HoveredColor, target[4, 4]);
    }

    [Fact]
    public void Draw_RespectsTheSpriteTransformLayer()
    {
        // The ICN format expresses "keep the destination" as transform id 1, which the original
        // engine uses for holes inside a button; a single layer blit would cover them.
        PixelBuffer image = new(2, 1);
        image.Fill(ReleasedColor);
        Sprite sprite = new(image, 0, 0, new byte[] { 0, 1 });

        PixelBuffer target = new(2, 1);
        target.Fill(9);

        new Button(sprite).Draw(target);

        Assert.Equal(ReleasedColor, target[0, 0]);
        Assert.Equal(9, target[1, 0]);
    }

    private static Sprite CreateSprite(byte color, int offsetX, int offsetY, int width, int height)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);
        return new Sprite(image, offsetX, offsetY);
    }
}
