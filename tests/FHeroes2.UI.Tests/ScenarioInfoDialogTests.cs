using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The scenario info screen of <c>game_scenarioinfo.cpp</c>: the text of the difficulty names and the rating line, the
/// pictures <c>generateDefaultImages()</c> cuts out for it, and the layout and clicks of the dialog itself.
/// </summary>
/// <remarks>
/// 本类在测试里临时把 <c>Assets.Current</c> 指向真实 AGG（用完再置 <c>null</c>）。这是进程级全局，必须与其他
/// 读写它的测试串行，否则并行的绘制类测试会在两帧之间看到不同的字体表（<c>FHeroes2Sharp-d8y</c> 记的那次偶发失败）。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class ScenarioInfoDialogTests
{
    private const byte GlyphColour = 200;

    [Fact]
    public void TheDifficultyNames_ReuseTheListOfTheScenarioDialog()
    {
        ScenarioInfoText texts = ScenarioInfoText.Default;

        Assert.Equal(ScenarioDialogText.Default.Difficulties.Count, texts.Difficulties.Count);
        Assert.Equal(5, texts.Difficulties.Count);
        Assert.Equal("difficulty|Easy", texts.Difficulty(GameDifficulty.Easy));
        Assert.Equal("difficulty|Normal", texts.Difficulty(GameDifficulty.Normal));
        Assert.Equal("difficulty|Impossible", texts.Difficulty(GameDifficulty.Impossible));
    }

    [Fact]
    public void ADifficultyOutsideTheRow_HasNoName()
    {
        Assert.Equal(string.Empty, ScenarioInfoText.Default.Difficulty((GameDifficulty)5));
        Assert.Equal(string.Empty, ScenarioInfoText.Default.Difficulty((GameDifficulty)(-1)));
    }

    [Fact]
    public void TheRatingLine_SubstitutesTheNumber()
    {
        Assert.Equal("Rating 100%", ScenarioInfoText.Default.Rating("100"));
    }

    [Fact]
    public void TheDifficultyIcons_AreTheFiveBackgroundCrops()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            ScenarioInfoArtwork good = new(isEvilInterface: false);
            ScenarioInfoArtwork evil = new(isEvilInterface: true);

            for (int i = 0; i < ScenarioInfoIcons.DifficultyFileNames.Length; ++i)
            {
                Sprite icon = good.GetDifficultyIcon((GameDifficulty)i);

                Assert.Equal(65, icon.Width);
                Assert.Equal(65, icon.Height);

                // The row keeps its colours on the evil interface: only the box and the buttons follow it.
                Assert.Equal(Pixels(icon), Pixels(evil.GetDifficultyIcon((GameDifficulty)i)));
            }

            // The five crops are different pictures, not one icon repeated.
            Assert.NotEqual(Pixels(good.GetDifficultyIcon(GameDifficulty.Easy)),
                Pixels(good.GetDifficultyIcon(GameDifficulty.Impossible)));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheBoxAndTheButtons_FollowTheInterface()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            ScenarioInfoArtwork good = new(isEvilInterface: false);
            ScenarioInfoArtwork evil = new(isEvilInterface: true);

            Assert.Equal(371, good.ScenarioBox.Width);
            Assert.Equal(30, good.ScenarioBox.Height);
            Assert.Equal(good.ScenarioBox.Width, evil.ScenarioBox.Width);
            Assert.Equal(good.ScenarioBox.Height, evil.ScenarioBox.Height);
            Assert.NotEqual(Pixels(good.ScenarioBox), Pixels(evil.ScenarioBox));

            // The evil "Select Maps" button is the good one recoloured with PAL::GOOD_TO_EVIL_INTERFACE.
            Assert.Equal(good.MapSelectReleased.Width, evil.MapSelectReleased.Width);
            Assert.Equal(good.MapSelectReleased.Height, evil.MapSelectReleased.Height);
            Assert.NotEqual(Pixels(good.MapSelectReleased), Pixels(evil.MapSelectReleased));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheSmallButtons_HaveAlignedReleasedAndPressedFrames()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            foreach (string fileName in new[]
                     {
                         SmallOkayCancelButtons.OkayGoodFileName,
                         SmallOkayCancelButtons.OkayEvilFileName,
                         SmallOkayCancelButtons.CancelGoodFileName,
                         SmallOkayCancelButtons.CancelEvilFileName,
                     })
            {
                Sprite?[]? frames = SmallOkayCancelButtons.Build(fileName);

                Assert.NotNull(frames);
                Assert.Equal(2, frames.Length);
                Assert.NotNull(frames[0]);
                Assert.NotNull(frames[1]);
                Assert.True(frames[0]!.Width > 0);
                Assert.True(frames[0]!.Height > 0);
                Assert.True(frames[1]!.Width > 0);
                Assert.True(frames[1]!.Height > 0);
            }
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheLayout_HangsOffTheWindowContentArea()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            ScenarioInfoDialog dialog = CreateDialog(out ScenarioInfoArtwork artwork);
            Rect roi = dialog.WindowArea;

            Assert.Equal(roi.X + 20, dialog.OkayArea.X);
            Assert.Equal(roi.X + roi.Width - artwork.CancelIcon.Width - 20, dialog.CancelArea.X);
            Assert.Equal(roi.Y + roi.Height - artwork.OkayIcon.Height - 6, dialog.OkayArea.Y);

            for (int i = 0; i < dialog.DifficultyAreas.Count; ++i)
            {
                Assert.Equal(roi.Y + 78, dialog.DifficultyAreas[i].Y);
                Assert.Equal(artwork.DifficultyCursor.Width, dialog.DifficultyAreas[i].Width);
            }

            int scenarioBoxX = roi.X + ((roi.Width - artwork.ScenarioBox.Width) / 2);
            Assert.Equal(scenarioBoxX + artwork.ScenarioBox.Width - 6 - artwork.MapSelectReleased.Width,
                dialog.SelectMapsArea.X);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void ClickingADifficultyIcon_ChangesTheDifficultyAndNothingElse()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            ScenarioInfoDialog dialog = CreateDialog(out _);
            Rect area = dialog.DifficultyAreas[(int)GameDifficulty.Expert];
            Point centre = new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

            LocalEvent events = new(() => 0);
            events.OnMouseMotion(centre);
            events.OnMouseButton(true, MouseButtonType.Left, centre);

            Assert.Equal(ScenarioInfoResult.None, dialog.ProcessEvents(events));
            Assert.Equal(GameDifficulty.Expert, dialog.Difficulty);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    private static ScenarioInfoDialog CreateDialog(out ScenarioInfoArtwork artwork)
    {
        artwork = new ScenarioInfoArtwork(isEvilInterface: false);

        return new ScenarioInfoDialog(
            [CreateMap()],
            artwork,
            new BlockFont(15, 6, GlyphColour),
            new BlockFont(11, 5, GlyphColour),
            static (_, _) => { });
    }

    private static Mp2FileInfo CreateMap() => new()
    {
        FilePath = @"C:\maps\Scenario Info.mp2",
        Name = "Scenario Info",
        Description = "described",
        Width = 72,
        Height = 72,
        Difficulty = GameDifficulty.Normal,
        KingdomColors = PlayerColor.Blue | PlayerColor.Red,
        HumanPlayers = PlayerColor.Blue,
        ComputerPlayers = PlayerColor.Red,
    };

    /// <summary>The palette indices of a sprite, for "is this the same picture" checks.</summary>
    private static byte[] Pixels(Sprite sprite)
    {
        byte[] pixels = new byte[sprite.Width * sprite.Height];

        for (int y = 0; y < sprite.Height; ++y)
        {
            for (int x = 0; x < sprite.Width; ++x)
            {
                pixels[(y * sprite.Width) + x] = sprite.Image[x, y];
            }
        }

        return pixels;
    }

    /// <summary>A glyph source whose glyphs are solid blocks.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte color)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight { get; }

        public int SpaceWidth { get; }

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
