using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Cursor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Makes the cursor state shareable test by test: <see cref="CursorState.Current"/> is process wide, like the
/// reference's <c>Cursor::Get()</c>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CursorSingletonCollection
{
    /// <summary>Name of the collection.</summary>
    public const string Name = "Cursor singleton";
}

/// <summary>
/// Covers <c>gui/cursor.cpp</c>: the theme state machine, the custom image path, the software pointer handling and
/// <see cref="CursorRestorer"/>'s give-it-back-on-dispose behaviour.
/// </summary>
[Collection(CursorSingletonCollection.Name)]
public sealed class CursorStateTests
{
    [Fact]
    public void CustomImageReplacesTheThemeAndMovesThePointerToTheOffset()
    {
        FakeRenderer renderer = new() { Position = new Point(100, 60) };
        CursorState state = new(renderer);
        state.SetThemes(CursorTheme.Wait);

        Sprite image = new(20, 30);

        state.SetCustomImage(image, new Point(3, 4));

        Assert.Equal(CursorTheme.None, state.Theme);
        Assert.Equal(new Point(3, 4), state.Offset);

        // The pointer is drawn with the negated offset and is no longer clamped into the screen area.
        (Sprite drawn, int offsetX, int offsetY) = Assert.Single(renderer.Updates);
        Assert.Same(image, drawn);
        Assert.Equal(-3, offsetX);
        Assert.Equal(-4, offsetY);
        Assert.False(renderer.LastKeep);

        // The new offset is applied to the current position right away.
        Assert.Equal(new Point(103, 64), renderer.Position);
    }

    [Fact]
    public void AThemeWithoutAnImageIsRememberedButNothingIsDrawn()
    {
        FakeRenderer renderer = new();
        CursorState state = new(renderer);

        state.SetThemes(CursorTheme.SpellFireball);

        // No archive is open, so CursorImages answers nothing: the theme is still recorded, the sprite is not.
        Assert.Equal(CursorTheme.SpellFireball, state.Theme);
        Assert.Empty(renderer.Updates);
        Assert.Empty(renderer.Keeps);
    }

    [Fact]
    public void ASoftwarePointerIsMovedAndReportsThePixelUnderIt()
    {
        FakeRenderer renderer = new() { IsSoftwareEmulation = true, Position = new Point(50, 40) };
        CursorState state = new(renderer);
        state.SetCustomImage(new Sprite(10, 10), new Point(2, 1));

        // The reported area is the mouse position, exactly as in the reference; the pointer itself is moved by the
        // offset on top of it.
        Assert.Equal(new Rect(10, 10, 1, 1), state.UpdateCursorPosition(10, 10));

        renderer.IsVisible = false;

        Assert.Equal(Rect.Empty, state.UpdateCursorPosition(10, 10));

        // Even hidden, the pointer is still moved: the reference does not check visibility for Move.
        Assert.Equal(new Point(12, 11), renderer.Position);
    }

    [Fact]
    public void AHostDrawnPointerIsNeitherMovedNorReported()
    {
        FakeRenderer renderer = new() { IsSoftwareEmulation = false, Position = new Point(50, 40) };
        CursorState state = new(renderer);

        Assert.Equal(Rect.Empty, state.UpdateCursorPosition(10, 10));
        Assert.Equal(new Point(50, 40), renderer.Position);
    }

    [Fact]
    public void TheVideoPointerIsOnlySetForASoftwarePointer()
    {
        FakeRenderer host = new() { IsSoftwareEmulation = false };
        CursorState hostState = new(host);
        hostState.SetThemes(CursorTheme.Pointer);

        hostState.SetVideoPlaybackCursor();
        Assert.Equal(CursorTheme.Pointer, hostState.Theme);

        FakeRenderer software = new() { IsSoftwareEmulation = true };
        CursorState softwareState = new(software);
        softwareState.SetThemes(CursorTheme.Pointer);

        softwareState.SetVideoPlaybackCursor();
        Assert.Equal(CursorTheme.PointerVideo, softwareState.Theme);
    }

    [Fact]
    public void TheRestorerPutsTheVisibilityAndThemeBack()
    {
        FakeRenderer renderer = new() { IsVisible = true };
        CursorState state = new(renderer);

        CursorState? previous = CursorState.Current;
        CursorState.Current = state;

        try
        {
            state.SetThemes(CursorTheme.Wait);

            using (new CursorRestorer(false))
            {
                Assert.False(renderer.IsVisible);
                Assert.Equal(CursorTheme.Wait, state.Theme);
            }

            Assert.True(renderer.IsVisible);
            Assert.Equal(CursorTheme.Wait, state.Theme);

            using (new CursorRestorer(false, CursorTheme.Castle))
            {
                Assert.False(renderer.IsVisible);
                Assert.Equal(CursorTheme.Castle, state.Theme);
            }

            Assert.True(renderer.IsVisible);
            Assert.Equal(CursorTheme.Wait, state.Theme);
        }
        finally
        {
            CursorState.Current = previous;
        }
    }

    [Fact]
    public void TheRestorerNeedsTheCurrentCursor()
    {
        CursorState? previous = CursorState.Current;
        CursorState.Current = null;

        try
        {
            Assert.Throws<InvalidOperationException>(() => new CursorRestorer(false));
        }
        finally
        {
            CursorState.Current = previous;
        }
    }

    private sealed class FakeRenderer : ICursorRenderer
    {
        public bool IsVisible { get; set; } = true;

        public bool IsSoftwareEmulation { get; init; }

        public Point Position { get; set; }

        public List<(Sprite Image, int OffsetX, int OffsetY)> Updates { get; } = [];

        public List<bool> Keeps { get; } = [];

        public bool? LastKeep => Keeps.Count > 0 ? Keeps[^1] : null;

        public void Show(bool enable) => IsVisible = enable;

        public void Update(Sprite image, int offsetX, int offsetY) => Updates.Add((image, offsetX, offsetY));

        public void SetPosition(int x, int y) => Position = new Point(x, y);

        public void KeepInScreenArea(bool keep) => Keeps.Add(keep);
    }
}
