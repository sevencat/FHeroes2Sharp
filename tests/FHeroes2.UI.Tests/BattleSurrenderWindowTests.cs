using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Battle;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The offer of the enemy, <see cref="BattleSurrenderWindow"/> against <c>DialogBattleSurrender()</c>
/// (<c>battle_dialogs.cpp:1087-1220</c>).
/// </summary>
public sealed class BattleSurrenderWindowTests
{
    /// <summary>A dialog of the reference's size, 375x205, on a 640x480 screen.</summary>
    private static readonly Rect Dialog = BattleSurrenderWindow.FrameFor(new Size(640, 480), new Size(375, 205));

    /// <summary>The dialog is pulled towards the top left by half of its own shadow, the <c>+ 16</c>.</summary>
    [Fact]
    public void TheDialogIsCentredWithASixteenPixelBias()
    {
        Assert.Equal(140, Dialog.X);
        Assert.Equal(145, Dialog.Y);
        Assert.Equal(375, Dialog.Width);
        Assert.Equal(205, Dialog.Height);
    }

    /// <summary>The three buttons sit on the reference's offsets inside the dialog.</summary>
    [Fact]
    public void TheButtonsSitOnTheDialogsBottomHalf()
    {
        Assert.Equal(new Point(231, 297), BattleSurrenderWindow.AcceptPosition(Dialog));
        Assert.Equal(new Point(435, 297), BattleSurrenderWindow.DeclinePosition(Dialog));
        Assert.Equal(new Point(319, 290), BattleSurrenderWindow.MarketPosition(Dialog));
    }

    /// <summary>The title names the enemy, and the offer quotes the price in gold.</summary>
    [Fact]
    public void TheOfferNamesTheEnemyAndThePrice()
    {
        Hero enemy = new(1, MapRace.Knight);
        string title = BattleSurrenderWindow.TitleFor(enemy);

        Assert.StartsWith(enemy.Name, title, StringComparison.Ordinal);
        Assert.EndsWith(" states:", title, StringComparison.Ordinal);

        Assert.Equal(
            "\"I will accept your surrender and grant you and your troops safe passage for the price of 2500 gold.\"",
            BattleSurrenderWindow.BodyFor(2500));
    }

    /// <summary>
    /// The three buttons are assembled from <c>SURRENDR</c> 0-3 and <c>ADVBTNS</c> 4/5, the reference's
    /// custom ICNs; looking them up as archives yields nothing and the dialog must not disappear.
    /// </summary>
    [Fact]
    public void TheButtonsAreAssembledFromTheOriginalArtwork()
    {
        BattleSurrenderWindow? window = BattleSurrenderWindow.TryCreate(
            new DialogSprites(), new Hero(1, MapRace.Knight), 2500, new GameKingdom(),
            new FixedFont(), new FixedFont(), new Size(640, 480));

        Assert.NotNull(window);
        Assert.Equal(Dialog, window.Frame);
        Assert.Equal(2500u, window.Cost);
        Assert.False(window.IsAffordable);
    }

    /// <summary>Without the backdrop there is no dialog to show.</summary>
    [Fact]
    public void MissingArtworkYieldsNoDialog()
    {
        Assert.Null(BattleSurrenderWindow.TryCreate(
            new EmptySprites(), new Hero(1, MapRace.Knight), 2500, new GameKingdom(),
            new FixedFont(), new FixedFont(), new Size(640, 480)));
    }

    /// <summary>ACCEPT is live only while the treasury covers the price.</summary>
    [Fact]
    public void PayingFollowsTheTreasury()
    {
        GameKingdom kingdom = new();
        kingdom.AddFundsResource(new Funds(Resource.Gold, 2000));

        Assert.False(BattleSurrenderWindow.CanPay(kingdom, 2500));
        Assert.True(BattleSurrenderWindow.CanPay(kingdom, 2000));

        kingdom.AddFundsResource(new Funds(Resource.Gold, 1000));

        Assert.True(BattleSurrenderWindow.CanPay(kingdom, 2500));
    }

    /// <summary>
    /// MARKET appears only for a kingdom that owns a marketplace and cannot pay; this kingdom owns no town, so the
    /// button stays away even though the gold is short.
    /// </summary>
    [Fact]
    public void TheMarketButtonNeedsAMarketplace()
    {
        GameKingdom kingdom = new();

        Assert.False(BattleSurrenderWindow.CanPay(kingdom, 2500));
        Assert.False(BattleSurrenderWindow.IsMarketShown(kingdom, 2500));
    }

    /// <summary>A font whose every character is six pixels wide; the dialog only measures, never draws here.</summary>
    private sealed class FixedFont : IGlyphSource
    {
        public int LineHeight => 17;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => character == GlyphText.LineSeparator ? 0 : 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = new Sprite(6, LineHeight);

            return true;
        }
    }

    /// <summary>A source without any artwork, the state of a build without game data.</summary>
    private sealed class EmptySprites : IViewWorldSpriteSource
    {
        public Sprite? GetSprite(IcnType icn, int index) => null;
    }

    /// <summary>
    /// The frames the dialog needs, in the sizes the real archives have; the portrait falls back to a
    /// <c>PORT_BIG</c> face.
    /// </summary>
    private sealed class DialogSprites : IViewWorldSpriteSource
    {
        public Sprite? GetSprite(IcnType icn, int index) => (icn, index) switch
        {
            (IcnType.Surdrbkg, 0) => new Sprite(375, 205),
            (IcnType.Surrendr, 0 or 1 or 2 or 3) => new Sprite(97, 31),
            (IcnType.Surrendr, 4) => new Sprite(129, 111),
            (IcnType.Advbtns, 4 or 5) => new Sprite(36, 36),
            _ => new Sprite(58, 52),
        };
    }
}
