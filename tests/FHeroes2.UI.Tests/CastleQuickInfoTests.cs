using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The castle quick info box, <c>showQuickInfo( const Castle &amp; )</c>
/// (<c>src/fheroes2/dialog/dialog_quickinfo.cpp:597-691</c>).
/// </summary>
/// <remarks>
/// 全部用假件（自造帧 + 实心块字体），不依赖游戏数据：布局算术是参考的常量，与真实帧的像素无关。
/// 唯一的例外是 <see cref="CastleQuickInfoArtwork.TryCreate"/>，它要真存档，本机无数据时直接返回。
/// </remarks>
public sealed class CastleQuickInfoTests
{
    private const byte GlyphColour = 8;
    private const byte BoxColour = 200;
    private const byte FlagColour = 55;

    [Fact]
    public void TheLabelsAreTheReferenceOnes()
    {
        Assert.Equal("Defenders:", CastleQuickInfoView.DefendersLabel);
        Assert.Equal("None", CastleQuickInfoView.NoneLabel);
        Assert.Equal("Unknown", CastleQuickInfoView.UnknownLabel);
    }

    [Fact]
    public void TheArtworkNeedsAllSevenFlagColours()
    {
        Sprite icon = Fill(20, 14, 101);
        Sprite[] flags = [.. Enumerable.Range(0, CastleQuickInfoArtwork.FlagCount).Select(index => Fill(20, 14, (byte)(FlagColour + index)))];

        Assert.NotNull(CastleQuickInfoArtwork.Create(Fill(236, 160, BoxColour), icon, flags));
        Assert.Throws<ArgumentOutOfRangeException>(() => CastleQuickInfoArtwork.Create(Fill(236, 160, BoxColour), icon, flags[..13]));
    }

    [Fact]
    public void TheContentIsTheBoxInsetByTheReferenceOffsets()
    {
        CastleQuickInfoArtwork artwork = Artwork();
        CastleQuickInfoView? view = Create(new Castle(0, MapRace.Knight), artwork, pinned: new Point(500, 100));

        Assert.NotNull(view);

        // makeRectQuickInfo( le, imageBox, position ) 把框摆在 position 左边，再按 (22, 9) 收出 192x154 的内容区。
        Assert.Equal(new Rect(500 - artwork.Box.Width, 100, artwork.Box.Width, artwork.Box.Height), view.Area);
        Assert.Equal(new Rect(view.Area.X + 22, view.Area.Y + 9, 192, 154), view.Content);
    }

    [Fact]
    public void TheBoxIsRefusedWhenTheInterfaceIsTooSmall()
    {
        Assert.Null(Create(new Castle(0, MapRace.Knight), Artwork(), area: new Rect(0, 0, 100, 100)));
    }

    [Fact]
    public void TheBoxIsPlacedNextToTheCursorAndPaintsItsFrame()
    {
        CastleQuickInfoArtwork artwork = Artwork();
        Rect interfaceArea = new(0, 0, 640, 480);
        CastleQuickInfoView view = Assert.IsType<CastleQuickInfoView>(
            Create(new Castle(0, MapRace.Knight), artwork, cursor: new Point(300, 200), area: interfaceArea));

        Assert.InRange(view.Area.X, interfaceArea.X, interfaceArea.X + interfaceArea.Width - view.Area.Width);
        Assert.InRange(view.Area.Y, interfaceArea.Y, interfaceArea.Y + interfaceArea.Height - view.Area.Height);

        PixelBuffer target = new(interfaceArea.Width, interfaceArea.Height);
        view.Draw(target);

        // 底板左上角那一像素应当就是帧 0 的左上角。
        Assert.Equal(artwork.Box.Image[0, 0], target[view.Area.X, view.Area.Y]);
    }

    [Fact]
    public void ADrawnBoxPutsTheNameAboveTheIcon()
    {
        CastleQuickInfoArtwork artwork = Artwork();
        Castle castle = new(0, MapRace.Knight) { Name = "Ravenshore" };
        CastleQuickInfoView view = Assert.IsType<CastleQuickInfoView>(Create(castle, artwork));

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        // 实心块字体：名字那一行必然有字，且它在内容区顶边下 3 像素处。
        Assert.True(HasGlyph(target, view.Content.X, view.Content.Y + 3, view.Content.Width));
    }

    [Fact]
    public void AnEmptyGarrisonIsAnnouncedAsNone()
    {
        CastleQuickInfoArtwork artwork = Artwork();
        Castle castle = new(0, MapRace.Knight) { Name = "Ravenshore" };
        CastleQuickInfoView view = Assert.IsType<CastleQuickInfoView>(Create(castle, artwork, detailedView: true));

        Assert.False(castle.Army.IsValid);

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        Assert.True(HasGlyph(target, view.Content.X, GarrisonLabelY(view, artwork) + 47, view.Content.Width));
    }

    [Fact]
    public void AnInvisibleGarrisonIsAnnouncedAsUnknown()
    {
        CastleQuickInfoArtwork artwork = Artwork();
        Castle castle = new(0, MapRace.Knight) { Name = "Ravenshore" };
        CastleQuickInfoView view = Assert.IsType<CastleQuickInfoView>(Create(castle, artwork, detailedView: false));

        PixelBuffer target = new(640, 480);
        view.Draw(target);

        Assert.True(HasGlyph(target, view.Content.X, GarrisonLabelY(view, artwork) + 47, view.Content.Width));
    }

    [Fact]
    public void TheModelOnlyBuildsACastleView()
    {
        CastleQuickInfoArtwork artwork = Artwork();
        QuickInfoDialog model = QuickInfoDialog.CreateForCastle(new Castle(0, MapRace.Knight) { Name = "Ravenshore" });

        CastleQuickInfoView? view = model.TryCreateCastleView(
            artwork,
            Font(),
            new EmptyMonsters(),
            new EmptySprites(),
            evilInterface: false,
            detailedView: true,
            thievesGuildCount: 0,
            new Point(300, 200),
            new Rect(0, 0, 640, 480));

        Assert.NotNull(view);
        Assert.Same(model.Castle, view.Subject);
    }

    private static int GarrisonLabelY(CastleQuickInfoView view, CastleQuickInfoArtwork artwork) =>
        view.Content.Y + 13 + artwork.CastleIcon.Height + 2;

    private static CastleQuickInfoView? Create(
        Castle castle,
        CastleQuickInfoArtwork artwork,
        Point? cursor = null,
        Rect? area = null,
        Point? pinned = null,
        bool detailedView = true) =>
        CastleQuickInfoView.TryCreate(
            castle,
            detailedView,
            thievesGuildCount: 0,
            artwork,
            Font(),
            new EmptyMonsters(),
            new EmptySprites(),
            evilInterface: false,
            cursor ?? new Point(300, 200),
            area ?? new Rect(0, 0, 640, 480),
            pinned);

    private static CastleQuickInfoArtwork Artwork() =>
        CastleQuickInfoArtwork.Create(
            Fill(236, 160, BoxColour),
            Fill(20, 14, 101),
            [.. Enumerable.Range(0, CastleQuickInfoArtwork.FlagCount).Select(index => Fill(20, 14, (byte)(FlagColour + index)))]);

    private static Sprite Fill(int width, int height, byte colour)
    {
        PixelBuffer image = new(width, height);
        image.Fill(colour);

        return new Sprite(image, 0, 0);
    }

    private static bool HasGlyph(PixelBuffer target, int x, int y, int width)
    {
        IGlyphSource font = Font();

        for (int row = y; row < y + font.LineHeight; ++row)
        {
            for (int column = x; column < x + width; ++column)
            {
                if (target[column, row] == GlyphColour)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IGlyphSource Font() => new BlockFont(8, 6, GlyphColour);

    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte colour)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;
            _glyph = Fill(advance, lineHeight, colour);
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

    /// <summary>No army portrait at all; the boxes under test never draw troops.</summary>
    private sealed class EmptyMonsters : IMiniMonsterSprites
    {
        public Sprite? GetMiniMonster(MonsterType monster) => null;
    }

    /// <summary>No icon at all, which is what a missing <c>LOCATORS</c> frame looks like.</summary>
    private sealed class EmptySprites : IViewWorldSpriteSource
    {
        public Sprite? GetSprite(IcnType icn, int index) => null;
    }
}
