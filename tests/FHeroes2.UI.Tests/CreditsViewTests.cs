using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Credits;
using FHeroes2.UI.Dialogs;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="CreditsView"/>, the pages of <c>Game::ShowCredits()</c> (<c>game_credits.cpp:701-820</c>).
/// </summary>
/// <remarks>
/// 页面背景是真的 <c>CBKGLAVA</c>，所以用例需要装好的游戏数据；没有数据时直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CreditsViewTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void ThePagesSitUnderAHeaderStrip() => Assert.True(TryWithArchives(
        view =>
        {
            // 页高按背景的宽高比等比走，余下的高度留给标题条。
            Assert.True(view.PageHeight > 0);
            Assert.True(view.PageHeight < Screen.Height);
            Assert.Equal(Screen.Height - view.PageHeight, view.HeaderHeight);
        }));

    [Fact]
    public void TheExpansionAddsThreePages() => Assert.True(TryWithArchives(
        view =>
        {
            Assert.Equal(5, view.Pages.Count);

            CreditsView? withExpansion = CreditsView.TryCreate(true, "engine", Font(), Font(), Screen);

            Assert.NotNull(withExpansion);
            Assert.Equal(8, withExpansion!.Pages.Count);
        }));

    [Fact]
    public void ABlackPagePaintsTheWholeAreaBlack() => Assert.True(TryWithArchives(
        view =>
        {
            var target = new PixelBuffer(Screen.Width, Screen.Height);
            var palette = new Palette();

            target.Fill(77);

            view.Draw(target, palette, new CreditsSequence(view.Pages.Count));

            // alpha 为 0 的那一格是纯黑：参考在这一格填 0，端口照做。
            for (int y = 0; y < Screen.Height; ++y)
            {
                for (int x = 0; x < Screen.Width; ++x)
                {
                    Assert.Equal(0, target[x, y]);
                }
            }
        }));

    [Fact]
    public void AFullyLitPagePaintsSomething() => Assert.True(TryWithArchives(
        view =>
        {
            CreditsSequence sequence = new(view.Pages.Count);

            while (sequence.Alpha < CreditsSequence.FullAlpha)
            {
                sequence.Increment();
            }

            var target = new PixelBuffer(Screen.Width, Screen.Height);
            var palette = new Palette();

            target.Fill(77);
            view.Draw(target, palette, sequence);

            // 满亮度的页是直接贴图，页面上必然有非零像素。
            int painted = 0;

            for (int y = view.HeaderHeight; y < Screen.Height && painted == 0; ++y)
            {
                for (int x = 0; x < Screen.Width; ++x)
                {
                    if (target[x, y] != 0)
                    {
                        ++painted;

                        break;
                    }
                }
            }

            Assert.True(painted > 0, "the fully lit page painted nothing");
        }));

    [Fact]
    public void MissingArchivesGiveNoView()
    {
        AssetBundle? previous = GameAssets.Current;

        GameAssets.Current = null;

        try
        {
            Assert.Null(CreditsView.TryCreate(false, "engine", Font(), Font(), Screen));
        }
        finally
        {
            GameAssets.Current = previous;
        }
    }

    private static IGlyphSource Font() => new BlockGlyphSource();

    /// <summary>Opens the game data the credits background is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    /// <summary>Runs a test against the real background artwork.</summary>
    /// <param name="assert">Checks the view; it is created here so every test builds it the same way.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArchives(Action<CreditsView> assert)
    {
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return true;
        }

        AssetBundle? previous = GameAssets.Current;

        GameAssets.Current = assets;

        try
        {
            CreditsView? view = CreditsView.TryCreate(false, "fheroes2 engine", Font(), Font(), Screen);

            assert(Assert.IsType<CreditsView>(view));
        }
        finally
        {
            GameAssets.Current = previous;
        }

        return true;
    }

    /// <summary>A 6x15 solid block font, enough to lay the pages out.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockGlyphSource()
        {
            PixelBuffer image = new(6, 15);
            image.Fill(44);

            _glyph = new Sprite(image);
        }

        public int LineHeight => 15;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
