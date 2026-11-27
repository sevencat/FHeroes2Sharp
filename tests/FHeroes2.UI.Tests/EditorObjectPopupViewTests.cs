using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The object popup of the editor on screen: the text of <see cref="EditorObjectPopup"/> laid out by
/// <see cref="StandardPopupWindow"/> where <c>getPopupWindowPosition()</c> puts it
/// (<c>src/fheroes2/gui/ui_map_interface.cpp:39-81</c>), clamped to the map area of the editor instead of
/// the whole display.
/// </summary>
/// <remarks>
/// 参考在右键按下时先取文字（<c>Editor::showPopupWindow()</c>），再把文字和 <c>getGameArea().GetROI()</c>
/// 交给 <c>displayStandardPopupWindow()</c>；端口的视口是边框内的矩形，所以界面区要按
/// <see cref="EditorObjectPopupView.InterfaceAreaOf"/> 换算成「从原点算起、右下角与视口相同」的那一块。
/// </remarks>
public sealed class EditorObjectPopupViewTests
{
    private const byte FrameColor = 55;
    private const byte Glyph = 77;

    private static readonly Size FrameSize = new(158, 94);

    /// <summary>The map area of a 640x480 editor frame, border excluded, as <c>MapFrame</c> lays it out.</summary>
    private static readonly Rect Viewport = new(16, 16, 448, 448);

    [Fact]
    public void InterfaceAreaOf_CountsFromTheDisplayOrigin()
    {
        // 视口从 (16,16) 起、448 见方：界面区是到它右下角为止的 (464,464)。
        Assert.Equal(new Rect(0, 0, 464, 464), EditorObjectPopupView.InterfaceAreaOf(Viewport));
    }

    [Fact]
    public void TryCreate_NamesTheTileAndItsCoordinates()
    {
        StandardPopupWindow? popup = CreatePopup(Tile(3, 4), new Point(100, 100));

        Assert.NotNull(popup);
        Assert.Equal("Grass\n[3; 4]", popup.Text);
    }

    [Fact]
    public void TryCreate_HangsTheWindowOffTheTileUnderTheCursor()
    {
        StandardPopupWindow? popup = CreatePopup(Tile(3, 4), new Point(100, 100));

        Assert.NotNull(popup);

        // 参考的算式：(100 - 16) / 32 = 2，于是 2 * 32 + 32 = 96，再各减半个窗：(96 - 79, 96 - 47)。
        Assert.Equal(new Rect(17, 49, 158, 94), popup.Area);

        // 夹边用的是地图区，不是整块屏幕：弹窗不能压到右边的雷达与下边的仪器面板上。
        Assert.True(popup.Area.Right <= Viewport.Right);
        Assert.True(popup.Area.Bottom <= Viewport.Bottom);
    }

    [Fact]
    public void TryCreate_ClampsTheWindowToTheMapArea()
    {
        // 指针在地图区右下角：窗口被推回地图区里面，而不是压到雷达上。
        StandardPopupWindow? popup = CreatePopup(Tile(13, 13), new Point(462, 462));

        Assert.NotNull(popup);
        Assert.Equal(EditorObjectPopupView.InterfaceAreaOf(Viewport).Right - FrameSize.Width, popup.Area.X);
        Assert.Equal(EditorObjectPopupView.InterfaceAreaOf(Viewport).Bottom - FrameSize.Height, popup.Area.Y);
    }

    [Fact]
    public void TryCreate_RefusesATileOutsideTheMap()
    {
        Mp2Map map = CreateMap();

        Assert.Null(EditorObjectPopupView.TryCreate(
            map, new Point(36, 0), new MapObjectMetadataStore(), CreateFrame(), CreateFont(),
            new Point(100, 100), EditorObjectPopupView.InterfaceAreaOf(Viewport)));
        Assert.Null(EditorObjectPopupView.TryCreate(
            map, new Point(-1, 0), new MapObjectMetadataStore(), CreateFrame(), CreateFont(),
            new Point(100, 100), EditorObjectPopupView.InterfaceAreaOf(Viewport)));
    }

    [Fact]
    public void TryCreate_RefusesAFrameBiggerThanTheMapArea()
    {
        Sprite frame = CreateFrame(new Size(500, 94));

        Assert.Null(EditorObjectPopupView.TryCreate(
            CreateMap(), Tile(3, 4), new MapObjectMetadataStore(), frame, CreateFont(),
            new Point(100, 100), EditorObjectPopupView.InterfaceAreaOf(Viewport)));
    }

    [Fact]
    public void TryCreate_RejectsMissingArguments()
    {
        Mp2Map map = CreateMap();

        Assert.Throws<ArgumentNullException>(() => EditorObjectPopupView.TryCreate(
            null!, Tile(3, 4), new MapObjectMetadataStore(), CreateFrame(), CreateFont(),
            new Point(100, 100), EditorObjectPopupView.InterfaceAreaOf(Viewport)));
        Assert.Throws<ArgumentNullException>(() => EditorObjectPopupView.TryCreate(
            map, Tile(3, 4), null!, CreateFrame(), CreateFont(),
            new Point(100, 100), EditorObjectPopupView.InterfaceAreaOf(Viewport)));
    }

    private static Point Tile(int x, int y) => new(x, y);

    private static Mp2Map CreateMap() => Mp2Map.CreateBlank(36, MapGrounds.GrassStartImageIndex);

    private static StandardPopupWindow? CreatePopup(Point tile, Point cursor) =>
        EditorObjectPopupView.TryCreate(
            CreateMap(), tile, new MapObjectMetadataStore(), CreateFrame(), CreateFont(), cursor,
            EditorObjectPopupView.InterfaceAreaOf(Viewport));

    private static Sprite CreateFrame() => CreateFrame(FrameSize);

    private static Sprite CreateFrame(Size size)
    {
        PixelBuffer pixels = new(size.Width, size.Height);
        pixels.Fill(FrameColor);

        return new Sprite(pixels);
    }

    private static IGlyphSource CreateFont() => new BlockGlyphSource();

    /// <summary>A 6x15 solid block font, wide enough to measure a wrapped line.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockGlyphSource()
        {
            PixelBuffer pixels = new(6, 15);
            pixels.Fill(Glyph);
            _glyph = new Sprite(pixels);
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
