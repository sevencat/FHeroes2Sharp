using FHeroes2.Assets;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The two free functions of <c>ui_text.cpp</c> that the text input field needs:
/// <c>getCursorSprite</c> (<c>ui_text.cpp:1425</c>) and <c>isFontAvailable</c> (<c>ui_text.cpp:1398</c>).
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class FontCharHandlerTests
{
    [Fact]
    public void TheCaretIsTheGlyphOfTheBar()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            Sprite caret = FontCharHandler.GetCursorSprite(FontType.NormalWhite());

            Assert.True(caret.Width > 0);
            Assert.True(caret.Height > 0);
            Assert.Equal((byte)'|', FontCharHandler.CursorChar);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TextOfTheFontIsAvailable()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            Assert.True(FontCharHandler.IsFontAvailable("Hello, world"u8, FontType.NormalWhite()));

            // An empty text cannot miss any glyph.
            Assert.True(FontCharHandler.IsFontAvailable(ReadOnlySpan<byte>.Empty, FontType.SmallWhite()));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void AByteTheFaceHasNoGlyphForIsNotAvailable()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;

        try
        {
            // 0xFF is above every font's character limit, so no face claims to have it.
            Assert.False(FontCharHandler.IsFontAvailable([(byte)0xFF], FontType.NormalWhite()));

            // One unknown byte is enough to make the whole text unusable.
            Assert.False(FontCharHandler.IsFontAvailable([(byte)'A', (byte)0xFF], FontType.NormalYellow()));
        }
        finally
        {
            Assets.Current = null;
        }
    }
}
