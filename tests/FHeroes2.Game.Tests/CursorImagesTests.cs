using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Cursor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="CursorImages"/> and the drawing half of <see cref="CursorState"/> against the real archives: which
/// archive entries a theme reads and whether the pointer is actually given a new image.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CursorImagesTests
{
    [Fact]
    public void TheColorSetsComeFromTheirIcnFiles()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            Assert.NotNull(CursorImages.GetFrame(CursorImageSet.AdventureMap, 0));
            Assert.NotNull(CursorImages.GetFrame(CursorImageSet.Combat, 0));
            Assert.NotNull(CursorImages.GetFrame(CursorImageSet.Spell, 1));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheMonochromeSetsComeFromThePerFrameBmpEntries()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            // ADVMBW01.BMP and CMSEBW01.BMP count from one, SPELBW00.BMP counts from zero - an upstream quirk.
            Sprite? adventure = CursorImages.GetFrame(CursorImageSet.AdventureMapMonochrome, 0);
            Sprite? combat = CursorImages.GetFrame(CursorImageSet.CombatMonochrome, 0);
            Sprite? spell = CursorImages.GetFrame(CursorImageSet.SpellMonochrome, 0);

            Assert.NotNull(adventure);
            Assert.NotNull(combat);
            Assert.NotNull(spell);

            Assert.True(adventure!.Width > 0);
            Assert.True(spell.Height > 0);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheDistanceSetsAreNotComposedYet()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            // The reference builds these at startup out of ADVMCO frames 4..11 plus drawn digits; that asset
            // generation is not ported, so the port answers nothing and SetThemes keeps the previous image.
            Assert.Null(CursorImages.GetFrame(CursorImageSet.AdventureMapDistanceColor, 0));
            Assert.Null(CursorImages.GetFrame(CursorImageSet.AdventureMapDistanceMonochrome, 0));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void AMissingFrameIsNothingRatherThanAnErrorImage()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            Assert.Null(CursorImages.GetFrame(CursorImageSet.AdventureMap, 500));
            Assert.Null(CursorImages.GetFrame(CursorImageSet.AdventureMap, -1));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void ThePointerThemeDrawsTheFirstAdventureMapFrame()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            RecordingRenderer renderer = new();
            CursorState state = new(renderer);

            state.SetThemes(CursorTheme.Pointer);

            Assert.Equal(CursorTheme.Pointer, state.Theme);
            Assert.Equal(new Point(0, 0), state.Offset);

            Assert.Single(renderer.Updates);
            Assert.False(renderer.LastKeep);

            // Same theme again: nothing is redrawn until the image is refreshed.
            state.SetThemes(CursorTheme.Pointer);
            Assert.Single(renderer.Updates);

            state.Refresh();
            Assert.Equal(2, renderer.Updates.Count);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheScrollCursorsAreKeptInsideTheScreen()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            RecordingRenderer renderer = new();
            CursorState state = new(renderer);

            state.SetThemes(CursorTheme.ScrollTopRight);

            // The scroll arrows are pinned to their tip instead of being centered on the anchor.
            Assert.Equal(new Point(-15, 0), state.Offset);
            Assert.True(renderer.LastKeep);

            state.SetThemes(CursorTheme.Wait);

            Assert.False(renderer.LastKeep);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheMonochromeSettingSelectsTheBmpSet()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            RecordingRenderer renderer = new();
            CursorState state = new(renderer) { MonochromeThemes = true };

            state.SetThemes(CursorTheme.Pointer);

            (Sprite drawn, int offsetX, int offsetY) = Assert.Single(renderer.Updates);
            Assert.True(drawn.Width > 0);
            Assert.Equal(0, offsetX);
            Assert.Equal(0, offsetY);

            // The video pointer has no usable monochrome artwork, so it stays in the color set.
            state.SetThemes(CursorTheme.PointerVideo);
            Assert.Equal(2, renderer.Updates.Count);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    private sealed class RecordingRenderer : ICursorRenderer
    {
        public bool IsVisible { get; private set; } = true;

        public bool IsSoftwareEmulation => false;

        public Point Position { get; private set; } = new(10, 20);

        public List<(Sprite Image, int OffsetX, int OffsetY)> Updates { get; } = [];

        public List<bool> Keeps { get; } = [];

        public bool? LastKeep => Keeps.Count > 0 ? Keeps[^1] : null;

        public void Show(bool enable) => IsVisible = enable;

        public void Update(Sprite image, int offsetX, int offsetY) => Updates.Add((image, offsetX, offsetY));

        public void SetPosition(int x, int y) => Position = new Point(x, y);

        public void KeepInScreenArea(bool keep) => Keeps.Add(keep);
    }
}
