using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the editor's rumor window: the layout, the list, the three small buttons and the messages
/// (<c>editor_rumor_window.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorRumorWindowTests
{
    private const byte AttributeColor = 5;

    private const int BodyLineHeight = 14;

    /// <summary>高 480 的屏也放得下 600×400 的窗，取默认屏即可。</summary>
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void Window_DropsTheEmptyRumors() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first", string.Empty, "second"]);

        Assert.Equal(["first", "second"], window.Rumors);
    }));

    [Fact]
    public void Layout_PlacesTheListUnderTheTitle() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Rect active = window.ActiveArea;

        Assert.Equal(EditorRumorWindow.ActiveWidth, active.Width);
        Assert.Equal(EditorRumorWindow.ActiveHeight, active.Height);
        Assert.Equal(new Point(active.X + EditorRumorWindow.ElementOffset,
            active.Y + EditorRumorWindow.ElementOffset + BodyLineHeight + EditorRumorWindow.ElementOffset),
            window.ListArea.Position);
        Assert.Equal(EditorRumorWindow.RumorAreaWidth, window.ListArea.Width);
        Assert.Equal(EditorRumorWindow.RumorAreaHeight, window.ListArea.Height);
        Assert.Equal(EditorRumorWindow.RumorAreaHeight - EditorRumorWindow.ListAreaHeightDeduction,
            window.ItemsArea.Height);
        Assert.Equal(EditorRumorWindow.MaxVisibleItemCount, window.VisibleItemCount);
    }));

    [Fact]
    public void Layout_SpreadsTheThreeSmallButtonsUnderTheList() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Rect list = window.ListArea;

        Assert.Equal(list.X, window.AddButton.Area.X);
        Assert.Equal(list.Right - window.DeleteButton.Area.Width, window.DeleteButton.Area.X);
        Assert.Equal(list.Bottom + 5, window.AddButton.Area.Y);
        Assert.Equal(window.AddButton.Area.Y, window.EditButton.Area.Y);
        Assert.True(window.EditButton.Area.X > window.AddButton.Area.Right);
        Assert.True(window.DeleteButton.Area.X > window.EditButton.Area.Right);
    }));

    [Fact]
    public void Add_AsksForTheInputBox() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        window.Process(Click(Center(window.AddButton.Area)));

        Assert.True(window.AddRequested);
        Assert.Null(window.Popup);
    }));

    [Fact]
    public void Add_AppendsTheTypedRumor() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Assert.True(window.TryAddRumor("second"));

        Assert.Equal(["first", "second"], window.Rumors);
    }));

    [Fact]
    public void Add_RejectsADuplicate() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Assert.False(window.TryAddRumor("first"));
        Assert.Equal(["first"], window.Rumors);
        Assert.Equal(EditorRumorWindow.DuplicateMessage, window.Popup!.Value.Body);
    }));

    [Fact]
    public void DoubleClick_AsksToEditTheRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first", "second"]);
        Point row = RowCenter(window, 1);

        window.Process(Click(row));
        window.Process(Click(row));

        Assert.True(window.EditRequested);
        Assert.Equal("second", window.SelectedRumor);
    }));

    [Fact]
    public void Edit_NeedsASelectedRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        window.Process(Click(Center(window.EditButton.Area)));

        Assert.False(window.EditRequested);

        window.Process(Click(RowCenter(window, 0)));
        window.Process(Click(Center(window.EditButton.Area)));

        Assert.True(window.EditRequested);
    }));

    [Fact]
    public void Edit_OverwritesTheSelectedRumor() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first", "second"]);

        window.Process(Click(RowCenter(window, 0)));

        Assert.True(window.TryReplaceSelected("changed"));
        Assert.Equal(["changed", "second"], window.Rumors);
    }));

    [Fact]
    public void Edit_RejectsADuplicateOfAnotherRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first", "second"]);

        window.Process(Click(RowCenter(window, 0)));
        Assert.False(window.TryReplaceSelected("second"));

        Assert.Equal(["first", "second"], window.Rumors);
        Assert.Equal(EditorRumorWindow.DuplicateMessage, window.Popup!.Value.Body);
    }));

    [Fact]
    public void Edit_KeepsItsOwnText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        window.Process(Click(RowCenter(window, 0)));

        Assert.True(window.TryReplaceSelected("first"));
        Assert.Null(window.Popup);
    }));

    [Fact]
    public void Edit_WithoutASelectionDoesNothing() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Assert.False(window.TryReplaceSelected("second"));
        Assert.Null(window.Popup);
        Assert.Equal(["first"], window.Rumors);
    }));

    [Fact]
    public void Delete_DropsTheSelectedRumor() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first", "second", "third"]);

        window.Process(Click(RowCenter(window, 1)));
        window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Equal(["first", "third"], window.Rumors);
        Assert.Equal("third", window.SelectedRumor);
    }));

    [Fact]
    public void Delete_WithoutASelectionDoesNothing() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Equal(["first"], window.Rumors);
    }));

    [Fact]
    public void Delete_EmptiesTheList() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        window.Process(Click(RowCenter(window, 0)));
        window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Empty(window.Rumors);
        Assert.Null(window.SelectedRumor);
    }));

    [Fact]
    public void Cancel_ClosesTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Assert.Equal(EditorListOutcome.Cancelled, window.Process(Click(Center(window.Cancel.Area))));
    }));

    [Fact]
    public void Okay_AcceptsTheRumors() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Assert.Equal(EditorListOutcome.Accepted, window.Process(Click(Center(window.Okay.Area))));
    }));

    [Theory]
    [InlineData(0, EditorRumorWindow.CancelTitle)]
    [InlineData(1, EditorRumorWindow.OkayTitle)]
    [InlineData(2, EditorRumorWindow.AddTitle)]
    [InlineData(3, EditorRumorWindow.EditTitle)]
    [InlineData(4, EditorRumorWindow.DeleteTitle)]
    public void RightClick_ExplainsTheButtons(int button, string title) => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        Rect area = button switch
        {
            0 => window.Cancel.Area,
            1 => window.Okay.Area,
            2 => window.AddButton.Area,
            3 => window.EditButton.Area,
            _ => window.DeleteButton.Area
        };

        window.Process(RightPress(Center(area)));

        Assert.Equal(title, window.Popup!.Value.Header);
    }));

    [Fact]
    public void SetRumors_ReplacesTheListAndClearsTheSelection() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first"]);

        window.Process(Click(RowCenter(window, 0)));
        window.SetRumors(["other", string.Empty, "last"]);

        Assert.Equal(["other", "last"], window.Rumors);
        Assert.Null(window.SelectedRumor);
    }));

    [Fact]
    public void Draw_PaintsTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorRumorWindow window = CreateWindow(artwork, ["first", "second"]);
        PixelBuffer canvas = CreateCanvas();
        canvas.Fill(0);

        window.Draw(canvas, new LocalEvent());

        bool painted = false;

        for (int y = window.ActiveArea.Y; y < window.ActiveArea.Bottom && !painted; ++y)
        {
            for (int x = window.ActiveArea.X; x < window.ActiveArea.Right; ++x)
            {
                if (canvas[x, y] != 0)
                {
                    painted = true;
                    break;
                }
            }
        }

        Assert.True(painted);
    }));

    private static Point RowCenter(EditorRumorWindow window, int row)
    {
        int rowHeight = Math.Max(1, window.ItemsArea.Height / Math.Max(1, window.VisibleItemCount));

        return new Point(window.ItemsArea.X + 40, window.ItemsArea.Y + (row * rowHeight) + (rowHeight / 2));
    }

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

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

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(Screen.Width, Screen.Height);
        canvas.Fill(0);

        return canvas;
    }

    private static EditorRumorWindow CreateWindow(EditorListArtwork artwork, IReadOnlyList<string> rumors) =>
        EditorRumorWindow.Create(Screen, rumors, artwork,
            new FakeGlyphSource(AttributeColor), new FakeGlyphSource(BodyLineHeight));

    /// <summary>Opens the game data and hands the artwork to the test body.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArtwork(Action<EditorListArtwork> body)
    {
        AssetBundle? previous = GameAssets.Current;
        AssetBundle? assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (assets is null)
        {
            return true;
        }

        GameAssets.Current = assets;

        try
        {
            EditorListArtwork artwork = EditorListArtwork.TryCreate(isEvilInterface: false) ??
                                         throw new InvalidOperationException(
                                             "The artwork of the rumor window is missing.");

            body(artwork);
        }
        finally
        {
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
