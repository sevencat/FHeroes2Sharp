using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.H2D;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the editor's save map window: the layout, the list, the file name field and the messages
/// (<c>editor_save_map_window.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorSaveMapWindowTests
{
    private const byte AttributeColor = 5;

    private const int BodyLineHeight = 14;

    [Fact]
    public void Window_FallsBackToTheDefaultName() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), fileName: string.Empty);

        Assert.Equal(EditorSaveMapWindow.DefaultFileName, window.FileName);
        Assert.True(window.IsOkayEnabled);
    }));

    [Fact]
    public void Window_ClampsTheFileNameTo255() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "map");

        window.FileName = new string('a', 300);

        Assert.Equal(EditorSaveMapWindow.MaxFileNameLength, window.FileName.Length);
    }));

    [Fact]
    public void Window_DisablesOkayWhileTheNameIsEmpty() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "map");

        window.FileName = string.Empty;

        Assert.False(window.IsOkayEnabled);
        Assert.Equal(EditorSaveMapOutcome.None, window.Process(Click(Center(window.Okay.Area))));
    }));

    [Fact]
    public void Layout_FitsElevenRowsForFewMaps() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow few = CreateWindow(artwork, CreateMaps(), "map");
        EditorSaveMapWindow many = CreateWindow(artwork, CreateManyMaps(), "map");

        // 参考 :215-216：可见行夹在 11..22，多出来的地图靠滚动。
        Assert.Equal(EditorSaveMapWindow.MinimumVisibleRows, few.VisibleItemCount);
        Assert.Equal(EditorSaveMapWindow.MaximumVisibleRows, many.VisibleItemCount);
        Assert.True(many.ActiveArea.Height > few.ActiveArea.Height);
    }));

    [Fact]
    public void ClickOnARow_TakesItsName() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        window.Process(Click(RowCenter(window, 1)));

        Assert.Equal("second", window.FileName);
    }));

    [Fact]
    public void ClickOnTheSameRowTwice_Accepts() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        Assert.Equal(EditorSaveMapOutcome.None, window.Process(Click(RowCenter(window, 0))));
        Assert.Equal(EditorSaveMapOutcome.Accepted, window.Process(Click(RowCenter(window, 0))));
    }));

    [Fact]
    public void ClickOnCancel_Cancels() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        Assert.Equal(EditorSaveMapOutcome.Cancelled, window.Process(Click(Center(window.Cancel.Area))));
    }));

    [Fact]
    public void ClickOnOkay_Accepts() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        Assert.Equal(EditorSaveMapOutcome.Accepted, window.Process(Click(Center(window.Okay.Area))));
    }));

    [Fact]
    public void ClickOnTheMapName_AsksForTheInputBox() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        window.Process(Click(Center(window.MapNameArea)));

        Assert.True(window.MapNameRequested);
    }));

    [Fact]
    public void DeleteKeyWithoutSelection_EditsTheName() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "zzz");

        // 点文件名框左端把光标挪到 0，再按 Delete 删掉光标右边的字符。
        window.Process(Click(new Point(window.FileNameArea.X, window.FileNameArea.Y + 4)));
        window.Process(KeyPress(Key.Delete));

        Assert.Equal("zz", window.FileName);
    }));

    [Fact]
    public void DeleteKeyWithSelection_AsksToDeleteIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        window.Process(Click(RowCenter(window, 1)));
        window.Process(KeyPress(Key.Delete));

        Assert.True(window.DeleteRequested);
        Assert.Equal("second.mp2", Path.GetFileName(window.SelectedMap!.FilePath));
    }));

    [Fact]
    public void RemoveSelectedMap_SelectsItsNeighbour() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");
        Assert.True(window.SelectMapByStem("second"));

        Mp2FileInfo? removed = window.RemoveSelectedMap();

        Assert.Equal("second.mp2", Path.GetFileName(removed!.FilePath));
        Assert.Equal("third", window.FileName);
        Assert.Equal(2, window.Size);
    }));

    [Fact]
    public void SelectMapByStem_IgnoresCase() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        Assert.True(window.SelectMapByStem("SECOND"));
        Assert.Equal("second", Path.GetFileNameWithoutExtension(window.SelectedMap!.FilePath));
    }));

    [Fact]
    public void RightClickOnARow_ShowsItsDescription() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        window.Process(RightPress(RowCenter(window, 0)));

        DialogPopup popup = window.Popup!.Value;

        Assert.Equal("first", popup.Header);
        Assert.Contains("72 x 72", popup.Body);
    }));

    [Fact]
    public void RightClickOnCancel_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        window.Process(RightPress(Center(window.Cancel.Area)));

        Assert.Equal(EditorSaveMapWindow.CancelTitle, window.Popup!.Value.Header);
    }));

    [Fact]
    public void Draw_PaintsTheRows() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        PixelBuffer painted = CreateCanvas();
        window.Draw(painted, Hover(Point.Zero));

        PixelBuffer blank = CreateCanvas();

        Assert.False(painted.Pixels.SequenceEqual(blank.Pixels));
    }));

    [Fact]
    public void ClickOnTheVirtualKeyboard_AsksForIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        if (window.VirtualKeyboard is null)
        {
            return;
        }

        window.Process(Click(Center(window.VirtualKeyboard.Area)));

        Assert.True(window.VirtualKeyboardRequested);
    }));

    [Fact]
    public void RightClickOnTheVirtualKeyboard_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSaveMapWindow window = CreateWindow(artwork, CreateMaps(), "first");

        if (window.VirtualKeyboard is null)
        {
            return;
        }

        window.Process(RightPress(Center(window.VirtualKeyboard.Area)));

        Assert.Equal(EditorSaveMapWindow.VirtualKeyboardTitle, window.Popup!.Value.Header);
    }));

    [Fact]
    public void SanitizeFileName_DropsTheExtensionAndTheIllegalCharacters()
    {
        Assert.Equal("map1", EditorSaveMapWindow.SanitizeFileName("map1"));
        Assert.Equal("map1", EditorSaveMapWindow.SanitizeFileName("map1.mp2"));
        Assert.Equal("map1", EditorSaveMapWindow.SanitizeFileName("  map1.MP2  "));
        Assert.Equal("a_b_c", EditorSaveMapWindow.SanitizeFileName("a/b:c"));
    }

    [Fact]
    public void ComposeFilePath_AppendsTheExtension()
    {
        string path = EditorSaveMapWindow.ComposeFilePath(Path.Combine("data", "maps"), "my map.mp2");

        Assert.Equal(Path.Combine("data", "maps", "my map.mp2"), path);
    }

    [Fact]
    public void SaveMap_RejectsANameWithNoUsableCharacters()
    {
        Mp2Map map = new()
        {
            Info = CreateMap("nameless", 36),
            Tiles = [],
            Addons = [],
            Castles = [],
            CaptureObjects = [],
            Heroes = [],
            InfoBlocks = []
        };

        Assert.Throws<ArgumentException>(() =>
            EditorSaveMapWindow.SaveMap(Path.GetTempPath(), "   ", map));
    }

    private static Point RowCenter(EditorSaveMapWindow window, int row)
    {
        int rowHeight = Math.Max(1, window.ItemsArea.Height / Math.Max(1, window.VisibleItemCount));

        return new Point(window.ItemsArea.X + 40, window.ItemsArea.Y + (row * rowHeight) + (rowHeight / 2));
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static LocalEvent Hover(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);

        return events;
    }

    private static LocalEvent Click(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        return events;
    }

    private static LocalEvent RightPress(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        return events;
    }

    private static LocalEvent KeyPress(Key key)
    {
        LocalEvent events = new();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        return events;
    }

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(Screen.Width, Screen.Height);
        canvas.Fill(0);

        return canvas;
    }

    private static EditorSaveMapWindow CreateWindow(EditorSaveMapArtwork artwork, IReadOnlyList<Mp2FileInfo> maps,
        string fileName) =>
        EditorSaveMapWindow.Create(Screen, maps, fileName, "a map", artwork,
            new FakeGlyphSource(AttributeColor), new FakeGlyphSource(BodyLineHeight));

    /// <summary>高屏幕，让列表能长到参考的上限 22 行；480 高的屏只够 14 行。</summary>
    private static readonly Size Screen = new(640, 1024);

    private static IReadOnlyList<Mp2FileInfo> CreateMaps() =>
    [
        CreateMap("first", 72),
        CreateMap("second", 144),
        CreateMap("third", 36)
    ];

    private static IReadOnlyList<Mp2FileInfo> CreateManyMaps() =>
    [
        .. Enumerable.Range(0, 30).Select(index => CreateMap($"map{index}", 72))
    ];

    private static Mp2FileInfo CreateMap(string stem, int width) => new()
    {
        FilePath = Path.Combine("maps", stem + ".mp2"),
        Name = stem,
        Description = "a map",
        Width = width,
        Height = width,
        Difficulty = GameDifficulty.Normal,
        KingdomColors = PlayerColor.Blue | PlayerColor.Red
    };

    /// <summary>Opens the game data and hands the artwork to the test body.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArtwork(Action<EditorSaveMapArtwork> body)
    {
        AssetBundle? previous = GameAssets.Current;
        H2DResources? previousH2D = H2DResources.Current;
        AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        // 虚拟键盘键是 h2d 图案拼出来的，没开 h2d 时窗口按「没有图案」处理、不画那个键。
        H2DResources? h2d = AssetLocator.Resolve() is { } dataDirectory &&
                            H2DResources.FindArchive(dataDirectory) is { } archivePath
            ? H2DResources.Open(archivePath)
            : null;

        H2DResources.Current = h2d;

        try
        {
            EditorSaveMapArtwork artwork = EditorSaveMapArtwork.TryCreate(isEvilInterface: false) ??
                                           throw new InvalidOperationException(
                                               "The artwork of the save map window is missing.");

            body(artwork);
        }
        finally
        {
            H2DResources.Current = previousH2D;
            h2d?.Dispose();
            GameAssets.Current = previous;
            assets.Dispose();
        }

        return true;
    }

    /// <summary>A glyph source with fixed metrics whose glyphs are solid blocks.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
            : this(color, BodyLineHeight)
        {
        }

        public FakeGlyphSource(int lineHeight)
            : this(AttributeColor, lineHeight)
        {
        }

        public FakeGlyphSource(byte color, int lineHeight)
        {
            PixelBuffer image = new(6, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
            LineHeight = lineHeight;
        }

        public int LineHeight { get; }

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => character == GlyphText.SpaceCharacter ? SpaceWidth : 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
