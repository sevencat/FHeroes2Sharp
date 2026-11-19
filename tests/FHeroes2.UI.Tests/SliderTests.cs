using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The horizontal slider, <c>fheroes2::HorizontalSlider</c>
/// (<c>src/fheroes2/gui/ui_slider.h:37-77</c>, <c>ui_slider.cpp:40-155</c>).
/// </summary>
public sealed class SliderTests
{
    private static readonly Point Origin = new(100, 100);

    [Fact]
    public void Constructor_RejectsAStartingValueOutsideTheRange()
    {
        List<Sprite> sprites = CreateSprites();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Slider(sprites, Slider.DefaultWidth, Origin, 2, 8, 9));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Slider(sprites, Slider.DefaultWidth, Origin, 2, 8, 1));

        // The reference asserts on a reversed range as well.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Slider(sprites, Slider.DefaultWidth, Origin, 8, 2, 5));
    }

    [Fact]
    public void Constructor_RejectsAnIncompleteFrameSet()
    {
        List<Sprite> sprites = CreateSprites();

        Assert.Throws<ArgumentException>(() => new Slider(sprites.Take(6).ToList(), 230, Origin, 0, 4, 0));
    }

    [Fact]
    public void Constructor_CutsTheGrooveFromBothEndsOfTheArtwork()
    {
        List<Sprite> sprites = CreateSprites();
        Sprite bar = sprites[1];

        var slider = new Slider(sprites, Slider.DefaultWidth, Origin, 0, 4, 2);

        const int LeftArea = Slider.LeftOffset + (Slider.DefaultWidth / 2);
        const int RightArea = Slider.RightOffset + Slider.DefaultWidth - (Slider.DefaultWidth / 2);

        Assert.Equal(Slider.DefaultWidth + Slider.LeftOffset + Slider.RightOffset, slider.Background.Width);
        Assert.Equal(bar.Height, slider.Background.Height);

        // The left cap is the artwork's left edge, the right cap its right edge.
        for (int y = 0; y < bar.Height; ++y)
        {
            ReadOnlySpan<byte> artwork = bar.Image.Row(y);
            ReadOnlySpan<byte> groove = slider.Background.Image.Row(y);

            Assert.True(artwork[..LeftArea].SequenceEqual(groove[..LeftArea]));

            ReadOnlySpan<byte> artworkRight = artwork.Slice(bar.Width - RightArea, RightArea);
            Assert.True(artworkRight.SequenceEqual(groove.Slice(LeftArea, RightArea)));
        }
    }

    [Fact]
    public void LeftArrow_StepsBackAndStopsAtTheLowerBound()
    {
        var slider = CreateSlider(0, 4, 2);

        Assert.True(slider.ProcessEvents(ClickIn(slider.LeftButton.Area)).Changed);
        Assert.Equal(1, slider.CurrentValue);

        Assert.True(slider.ProcessEvents(ClickIn(slider.LeftButton.Area)).Changed);
        Assert.Equal(0, slider.CurrentValue);

        // A step at the bound is reported as no change: the thumb sits still.
        Assert.False(slider.ProcessEvents(ClickIn(slider.LeftButton.Area)).Changed);
        Assert.Equal(0, slider.CurrentValue);
    }

    [Fact]
    public void RightArrow_StepsForwardAndStopsAtTheUpperBound()
    {
        var slider = CreateSlider(0, 2, 0);

        Assert.True(slider.ProcessEvents(ClickIn(slider.RightButton.Area)).Changed);
        Assert.Equal(1, slider.CurrentValue);

        Assert.True(slider.ProcessEvents(ClickIn(slider.RightButton.Area)).Changed);
        Assert.Equal(2, slider.CurrentValue);

        Assert.False(slider.ProcessEvents(ClickIn(slider.RightButton.Area)).Changed);
        Assert.Equal(2, slider.CurrentValue);
    }

    [Fact]
    public void Wheel_StepsForwardAndBackwardOverTheGroove()
    {
        var slider = CreateSlider(0, 4, 2);
        Rect groove = slider.GrooveArea;
        Point cursor = new(groove.X + (groove.Width / 2), groove.Y + (groove.Height / 2));

        LocalEvent up = CreateEvents(cursor);
        up.OnMouseWheel(new Point(0, 1));

        Assert.True(slider.ProcessEvents(up).Changed);
        Assert.Equal(3, slider.CurrentValue);

        LocalEvent down = CreateEvents(cursor);
        down.OnMouseWheel(new Point(0, -1));

        Assert.True(slider.ProcessEvents(down).Changed);
        Assert.Equal(2, slider.CurrentValue);
    }

    [Fact]
    public void Drag_MovesTheThumbUnderTheCursorAndReportsTheChange()
    {
        var slider = CreateSlider(0, 4, 0);
        Rect groove = slider.GrooveArea;
        Point cursor = new(groove.Right - 1, groove.Y + (groove.Height / 2));

        LocalEvent events = CreateEvents(cursor);
        events.OnMouseButton(true, MouseButtonType.Left, cursor);

        Assert.True(slider.ProcessEvents(events).Changed);
        Assert.Equal(4, slider.CurrentValue);

        // The thumb is pressed: a second frame with the same position has nothing left to move.
        Assert.False(slider.ProcessEvents(events).Changed);
    }

    [Fact]
    public void RightClick_OverTheGroove_AsksForThePopup()
    {
        var slider = CreateSlider(0, 4, 2);
        slider.PopupTitle = "Scroll Speed";
        slider.PopupDescription = "How fast the map scrolls.";

        Rect groove = slider.GrooveArea;
        Point cursor = new(groove.X + (groove.Width / 2), groove.Y + (groove.Height / 2));

        LocalEvent events = CreateEvents(cursor);
        events.OnMouseButton(true, MouseButtonType.Right, cursor);

        SliderFrame frame = slider.ProcessEvents(events);

        Assert.False(frame.Changed);
        Assert.NotNull(frame.Tooltip);
        Assert.Equal("Scroll Speed", frame.Tooltip.Value.Title);
        Assert.Equal("How fast the map scrolls.", frame.Tooltip.Value.Text);
    }

    [Fact]
    public void Disable_DarkensTheThumbAndIgnoresBothArrows()
    {
        var slider = CreateSlider(0, 4, 2);
        List<byte[]> enabled = Rows(slider.Thumb!);

        slider.Disable();

        Assert.False(slider.IsEnabled);
        Assert.False(slider.ProcessEvents(ClickIn(slider.LeftButton.Area)).Changed);
        Assert.False(slider.ProcessEvents(ClickIn(slider.RightButton.Area)).Changed);
        Assert.Equal(2, slider.CurrentValue);

        Assert.False(Rows(slider.Thumb!).SequenceEqual(enabled, RowsComparer.Instance));

        slider.Enable();

        Assert.True(slider.IsEnabled);
        Assert.True(Rows(slider.Thumb!).SequenceEqual(enabled, RowsComparer.Instance));
    }

    [Fact]
    public void SetRange_ClampsTheValueAndRestretchesTheThumb()
    {
        var slider = CreateSlider(0, 9, 7);

        slider.SetRange(0, 3);

        Assert.Equal(3, slider.MaxValue);
        Assert.Equal(3, slider.CurrentValue);

        // Nine slots need a longer thumb than four; the range change has to redraw it.
        int narrow = slider.Thumb!.Width;

        slider.SetRange(0, 99);

        Assert.Equal(3, slider.CurrentValue);
        Assert.True(slider.Thumb!.Width < narrow);
    }

    private static Slider CreateSlider(int min, int max, int current) =>
        new(CreateSprites(), Slider.DefaultWidth, Origin, min, max, current);

    /// <summary>
    /// Seven frames with distinct sizes, the two end caps and the middle of the groove filled with different values so
    /// the cut can be checked pixel by pixel.
    /// </summary>
    private static List<Sprite> CreateSprites()
    {
        Sprite bar = new(Slider.DefaultWidth, 17);

        for (int y = 0; y < bar.Height; ++y)
        {
            Span<byte> row = bar.Image.Row(y);

            for (int x = 0; x < bar.Width; ++x)
            {
                row[x] = (byte)((x % 200) + 1);
            }
        }

        return
        [
            new Sprite(15, 17),
            bar,
            CreateThumb(),
            new Sprite(15, 17),
            new Sprite(15, 17),
            new Sprite(15, 17),
            new Sprite(15, 17),
        ];
    }

    /// <summary>
    /// The thumb frame with a non transparent body, so <see cref="PaletteId.Darkening"/> has something to change —
    /// palette index 0 is transparent and stays 0 through every transform.
    /// </summary>
    private static Sprite CreateThumb()
    {
        Sprite thumb = new(10, 11);

        for (int y = 0; y < thumb.Height; ++y)
        {
            thumb.Image.Row(y).Fill(200);
        }

        return thumb;
    }

    private static LocalEvent CreateEvents(Point cursor)
    {
        LocalEvent events = new(() => 0);
        events.OnMouseMotion(cursor);

        return events;
    }

    /// <summary>A left click whose press and release both land inside <paramref name="area"/>.</summary>
    private static LocalEvent ClickIn(Rect area)
    {
        Point point = new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = CreateEvents(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        return events;
    }

    /// <summary>Every row of the sprite as an array, so two sprites can be compared without hash codes.</summary>
    private static List<byte[]> Rows(Sprite sprite)
    {
        List<byte[]> rows = new(sprite.Height);

        for (int y = 0; y < sprite.Height; ++y)
        {
            rows.Add(sprite.Image.Row(y).ToArray());
        }

        return rows;
    }

    private sealed class RowsComparer : IEqualityComparer<byte[]>
    {
        public static readonly RowsComparer Instance = new();

        public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(byte[] obj) => obj.Length;
    }
}
