using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="VirtualKeyboardView"/>, a port of <c>processVirtualKeyboardEvent()</c> and of
/// the two loops that drive it (<c>ui_keyboard.cpp:1125-1480</c>): the layout of the keys, what a click
/// types, the layout and language keys, and the checks of the numeric keyboard.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class VirtualKeyboardViewTests
{
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void TheLowerCaseKeyboardKeepsTheRowsOfTheReferenceAndHidesTheLanguageKey()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;
        SupportedLanguage savedLanguage = VirtualKeyboardView.LastSelectedLanguage;

        try
        {
            VirtualKeyboardView view = CreateView();
            view.Draw(new PixelBuffer(Screen.Width, Screen.Height));

            Assert.Equal(KeyboardLayoutType.LowerCase, view.LayoutType);
            Assert.Equal(4, view.Rows.Count);

            // Three rows of letters that print capitals, then the row of special keys.
            Assert.Equal(10, view.Rows[0].Count);
            Assert.Equal("Q", view.Rows[0][0].Text);
            Assert.Equal("P", view.Rows[0][9].Text);
            Assert.Equal("M", view.Rows[2][6].Text);

            // "|", "123", SPACE, the language key and the backspace key.
            Assert.Equal(5, view.Rows[3].Count);

            // English is the default language, so it has no second letter set and the key is hidden.
            Assert.False(view.IsExtraLanguageSupported);
            Assert.False(view.Rows[3][3].Sprite.IsVisible);
            Assert.True(view.Rows[3][4].Sprite.IsVisible);
        }
        finally
        {
            VirtualKeyboardView.LastSelectedLanguage = savedLanguage;
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void ClickingALetterKeyTypesTheSmallLetter()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            VirtualKeyboardView view = CreateView();
            view.Draw(new PixelBuffer(Screen.Width, Screen.Height));

            VirtualKeyboardResult result = view.ProcessEvents(Click(Center(view.Rows[2][0].Sprite.Area)));

            Assert.Equal(KeyboardDialogAction.DoNothing, result.Action);
            Assert.Null(result.Popup);
            Assert.Equal("z", view.Text);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheOkayButtonClosesTheKeyboard()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            VirtualKeyboardView view = CreateView();
            view.Draw(new PixelBuffer(Screen.Width, Screen.Height));

            Rect okayArea = view.OkayArea;
            Assert.True(okayArea.Width > 0);

            Assert.Equal(KeyboardDialogAction.Close, view.ProcessEvents(Click(Center(okayArea))).Action);

            // The hot key which closes a window does the same as the okay button.
            Assert.Equal(KeyboardDialogAction.Close,
                view.ProcessEvents(new LocalEvent(() => 0), closeRequested: true).Action);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheLayoutKeySwitchesBetweenTheLetterCases()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            VirtualKeyboardView view = CreateView();
            view.Draw(new PixelBuffer(Screen.Width, Screen.Height));

            VirtualKeyboardResult result = view.ProcessEvents(Click(Center(view.Rows[3][0].Sprite.Area)));

            Assert.Equal(KeyboardDialogAction.UpperCase, result.Action);
            Assert.Equal(KeyboardLayoutType.UpperCase, view.LayoutType);

            // The capital layout types capitals and its "|" key is drawn as pressed while not held.
            Assert.Equal("Q", view.Rows[0][0].Text);
            Assert.True(view.Rows[3][0].IsInvertedRenderingLogic);
            Assert.True(view.Rows[3][0].Sprite.IsPressed);

            view.ProcessEvents(Click(Center(view.Rows[3][1].Sprite.Area)));
            Assert.Equal(KeyboardLayoutType.AlphaNumeric, view.LayoutType);
            Assert.Equal("1", view.Rows[0][0].Text);

            // The key which returns to the letters is the "ABC" one, the second key of the bottom row.
            view.ProcessEvents(Click(Center(view.Rows[3][1].Sprite.Area)));
            Assert.Equal(KeyboardLayoutType.LowerCase, view.LayoutType);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void RightClickingAKeyAsksForItsHelp()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            VirtualKeyboardView view = CreateView();
            view.Draw(new PixelBuffer(Screen.Width, Screen.Height));

            VirtualKeyboardResult result = view.ProcessEvents(RightPress(Center(view.Rows[3][0].Sprite.Area)));

            Assert.Equal(KeyboardDialogAction.DoNothing, result.Action);
            Assert.NotNull(result.Popup);
            Assert.Equal("Uppercase", result.Popup.Value.Header);
            Assert.Equal("Click to switch to uppercase letters.", result.Popup.Value.Body);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void AMultiLineKeyboardHasAnEnterKeyAndGrowsTaller()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            VirtualKeyboardView singleLine = CreateView();
            VirtualKeyboardView multiLine = CreateView(isMultiLineText: true);

            PixelBuffer output = new(Screen.Width, Screen.Height);
            singleLine.Draw(output);
            multiLine.Draw(output);

            // "|", "123", SPACE, the hidden language key and the backspace key.
            Assert.Equal(5, singleLine.Rows[3].Count);

            // English has no second letter set, so the language key is left out of the row entirely:
            // "|", "123", SPACE, ENTER and the backspace key.
            Assert.Equal(5, multiLine.Rows[3].Count);
            Assert.Equal("ENTER", multiLine.Rows[3][3].Text);
            Assert.True(multiLine.WindowArea.Height > singleLine.WindowArea.Height);

            multiLine.ProcessEvents(Click(Center(multiLine.Rows[3][3].Sprite.Area)));
            Assert.Equal("\n", multiLine.Text);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheLanguageKeySwitchesBetweenTheGameLanguageAndEnglish()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;
        SupportedLanguage savedLanguage = VirtualKeyboardView.LastSelectedLanguage;

        try
        {
            VirtualKeyboardView view = CreateView(isMultiLineText: true, gameLanguage: SupportedLanguage.Russian);
            view.Draw(new PixelBuffer(Screen.Width, Screen.Height));

            Assert.True(view.IsExtraLanguageSupported);

            // With a second letter set the language key sits before the backspace key.
            Assert.Equal(6, view.Rows[3].Count);
            Assert.True(view.Rows[3][4].Sprite.IsVisible);

            view.ProcessEvents(Click(Center(view.Rows[3][4].Sprite.Area)));
            Assert.Equal(SupportedLanguage.Russian, view.Language);
            Assert.Equal(SupportedLanguage.Russian, VirtualKeyboardView.LastSelectedLanguage);
            Assert.Equal("Й", view.Rows[0][0].Text);

            view.ProcessEvents(Click(Center(view.Rows[3][4].Sprite.Area)));
            Assert.Equal(SupportedLanguage.English, view.Language);
            Assert.Equal(SupportedLanguage.English, VirtualKeyboardView.LastSelectedLanguage);
            Assert.Equal("Q", view.Rows[0][0].Text);
        }
        finally
        {
            VirtualKeyboardView.LastSelectedLanguage = savedLanguage;
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheNumpadHasTheSignAndTheDigitsOfAKeypad()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            VirtualKeyboardArtwork artwork = CreateArtwork()!;
            PixelBuffer output = new(Screen.Width, Screen.Height);

            VirtualKeyboardView signed = VirtualKeyboardView.TryCreateNumpad(artwork, Screen, 42, -100, 100, false)!;
            signed.Draw(output);

            Assert.Equal(KeyboardLayoutType.SignedNumeric, signed.LayoutType);
            Assert.Equal("42", signed.Text);
            Assert.Equal(4, signed.Rows.Count);
            Assert.Equal(3, signed.Rows[0].Count);
            Assert.Equal("1", signed.Rows[0][0].Text);

            // Only the digits react to the hardware keyboard.
            Assert.Equal(Key.Digit1, signed.Rows[0][0].Key);
            Assert.Equal(Key.None, signed.Rows[3][0].Key);

            // "|", "-", "0", the hidden key and the backspace key.
            Assert.Equal(5, signed.Rows[3].Count);
            Assert.False(signed.Rows[3][0].Sprite.IsVisible);
            Assert.True(signed.Rows[3][1].Sprite.IsVisible);

            signed.ProcessEvents(Click(Center(signed.Rows[3][1].Sprite.Area)));
            Assert.Equal("-42", signed.Text);

            VirtualKeyboardView unsigned = VirtualKeyboardView.TryCreateNumpad(artwork, Screen, 7, 0, 10, false)!;
            unsigned.Draw(output);

            Assert.Equal(KeyboardLayoutType.UnsignedNumeric, unsigned.LayoutType);
            Assert.Equal(5, unsigned.Rows[3].Count);
            Assert.False(unsigned.Rows[3][1].Sprite.IsVisible);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Theory]
    [InlineData("42", 0, 100, VirtualNumpadParse.Valid, 42)]
    [InlineData("-42", -100, 100, VirtualNumpadParse.Valid, -42)]
    [InlineData("0", 0, 100, VirtualNumpadParse.Valid, 0)]
    [InlineData("", 0, 100, VirtualNumpadParse.Invalid, 0)]
    [InlineData("-", -10, 10, VirtualNumpadParse.Invalid, 0)]
    [InlineData("101", 0, 100, VirtualNumpadParse.OutOfRange, 0)]
    [InlineData("-101", -100, 100, VirtualNumpadParse.OutOfRange, 0)]
    [InlineData("99999999999", 0, 100, VirtualNumpadParse.OutOfRange, 0)]
    public void TheNumpadTextIsCheckedLikeTheReference(string text, int minValue, int maxValue,
        VirtualNumpadParse expectedResult, int expectedValue)
    {
        VirtualNumpadParse result = VirtualKeyboardView.ReadNumpad(text, minValue, maxValue, out int value);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void AMissingAssetOrANegativeLimitIsRejected()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            Assert.Null(VirtualKeyboardView.TryCreate(null, Screen, string.Empty, 0, false,
                KeyboardLayoutType.LowerCase, SupportedLanguage.English, SupportedLanguage.English, false));

            VirtualKeyboardArtwork? artwork = CreateArtwork();
            Assert.NotNull(artwork);

            Assert.Throws<ArgumentNullException>(() => VirtualKeyboardView.TryCreate(artwork, Screen, null!, 0,
                false, KeyboardLayoutType.LowerCase, SupportedLanguage.English, SupportedLanguage.English, false));

            Assert.Throws<ArgumentOutOfRangeException>(() => VirtualKeyboardView.TryCreate(artwork, Screen,
                string.Empty, -1, false, KeyboardLayoutType.LowerCase, SupportedLanguage.English,
                SupportedLanguage.English, false));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    private static VirtualKeyboardView CreateView(bool isMultiLineText = false,
        SupportedLanguage gameLanguage = SupportedLanguage.English,
        SupportedLanguage language = SupportedLanguage.English)
    {
        VirtualKeyboardView? view = VirtualKeyboardView.TryCreate(CreateArtwork(), Screen, string.Empty, 0,
            isMultiLineText, KeyboardLayoutType.LowerCase, gameLanguage, language, false);

        Assert.NotNull(view);

        return view;
    }

    private static VirtualKeyboardArtwork? CreateArtwork()
    {
        Sprite? background = GameAssets.GetImage(VirtualKeyboardArtwork.ButtonBackgroundAssetName, 0);
        Sprite? request = GameAssets.GetImage(VirtualKeyboardArtwork.RequestBackgroundAssetName, 0);
        Sprite? okayReleased = GameAssets.GetImage(VirtualKeyboardArtwork.OkayButtonAssetName, 0);
        Sprite? okayPressed = GameAssets.GetImage(VirtualKeyboardArtwork.OkayButtonAssetName, 1);

        if (background is null || request is null || okayReleased is null || okayPressed is null)
        {
            return null;
        }

        return VirtualKeyboardArtwork.TryCreate(background, request, [okayReleased, okayPressed]);
    }

    private static LocalEvent Click(Point position)
    {
        LocalEvent events = new(() => 0);
        events.BeginFrame();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

    private static LocalEvent RightPress(Point position)
    {
        LocalEvent events = new(() => 0);
        events.BeginFrame();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        return events;
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
}
