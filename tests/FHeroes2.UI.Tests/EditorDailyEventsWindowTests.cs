using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the editor's daily events window: the layout, the list, the sorting, the three small buttons and the
/// messages (<c>editor_daily_events_window.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorDailyEventsWindowTests
{
    private const byte AttributeColor = 5;

    private const int BodyLineHeight = 14;

    /// <summary>高 480 的屏放得下 550×415 的窗。</summary>
    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void Window_SortsTheEventsByTheirFirstDay() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork,
            [CreateEvent(30, "late"), CreateEvent(1, "early"), CreateEvent(10, "middle")]);

        Assert.Equal([1u, 10u, 30u], Days(window));
        Assert.Equal(["early", "middle", "late"], window.Events.Select(item => item.Message));
    }));

    [Fact]
    public void Window_CopiesTheEvents() => Assert.True(TryWithArtwork(artwork =>
    {
        MapDailyEvent source = CreateEvent(5, "a message");
        EditorDailyEventsWindow window = CreateWindow(artwork, [source]);

        source.Message = "changed afterwards";

        Assert.Equal("a message", window.Events[0].Message);
    }));

    [Fact]
    public void Layout_PlacesTheListUnderTheTitle() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, string.Empty)]);

        Rect active = window.ActiveArea;

        Assert.Equal(EditorDailyEventsWindow.ActiveWidth, active.Width);
        Assert.Equal(EditorDailyEventsWindow.ActiveHeight, active.Height);
        Assert.Equal(new Point(active.X + EditorDailyEventsWindow.ElementOffset,
            active.Y + EditorDailyEventsWindow.ElementOffset + BodyLineHeight + EditorDailyEventsWindow.ElementOffset),
            window.ListArea.Position);
        Assert.Equal(EditorDailyEventsWindow.EventsAreaWidth, window.ListArea.Width);
        Assert.Equal(EditorDailyEventsWindow.EventsAreaHeight, window.ListArea.Height);
        Assert.Equal(EditorDailyEventsWindow.EventsAreaHeight - EditorDailyEventsWindow.ListAreaHeightDeduction,
            window.ItemsArea.Height);
        Assert.Equal(EditorDailyEventsWindow.MaxVisibleItemCount, window.VisibleItemCount);
    }));

    [Fact]
    public void Layout_SpreadsTheThreeSmallButtonsUnderTheList() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, string.Empty)]);

        Rect list = window.ListArea;

        Assert.Equal(list.X, window.AddButton.Area.X);
        Assert.Equal(list.Right - window.DeleteButton.Area.Width, window.DeleteButton.Area.X);
        Assert.Equal(list.Bottom + 5, window.AddButton.Area.Y);
        Assert.Equal(window.AddButton.Area.Y, window.EditButton.Area.Y);
        Assert.Equal(window.AddButton.Area.Y, window.DeleteButton.Area.Y);
        Assert.True(window.EditButton.Area.X > window.AddButton.Area.Right);
        Assert.True(window.DeleteButton.Area.X > window.EditButton.Area.Right);
    }));

    [Fact]
    public void Add_AsksForTheSpecWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, string.Empty)]);

        window.Process(Click(Center(window.AddButton.Area)));

        Assert.True(window.AddRequested);
        Assert.Null(window.Popup);
    }));

    [Fact]
    public void Add_InsertsTheEventInDayOrder() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(10, "ten"), CreateEvent(20, "twenty")]);

        window.AddEvent(CreateEvent(15, "fifteen"));

        Assert.Equal([10u, 15u, 20u], Days(window));
    }));

    [Fact]
    public void DoubleClick_AsksToEditTheRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "first"), CreateEvent(2, "second")]);
        Point row = RowCenter(window, 1);

        window.Process(Click(row));
        window.Process(Click(row));

        Assert.True(window.EditRequested);
        Assert.Equal(2u, window.SelectedEvent!.FirstOccurrenceDay);
    }));

    [Fact]
    public void Edit_NeedsASelectedRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "first")]);

        window.Process(Click(Center(window.EditButton.Area)));

        Assert.False(window.EditRequested);

        window.Process(Click(RowCenter(window, 0)));
        window.Process(Click(Center(window.EditButton.Area)));

        Assert.True(window.EditRequested);
    }));

    [Fact]
    public void Edit_OverwritesTheSelectedEventAndReSorts() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(5, "five"), CreateEvent(10, "ten")]);

        window.Process(Click(RowCenter(window, 1)));

        Assert.True(window.ReplaceSelected(CreateEvent(1, "moved to the front")));
        Assert.Equal([1u, 5u], Days(window));
        Assert.Equal("moved to the front", window.Events[0].Message);
    }));

    [Fact]
    public void Edit_WithoutASelectionDoesNothing() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(5, "five")]);

        Assert.False(window.ReplaceSelected(CreateEvent(1, "never")));
        Assert.Equal([5u], Days(window));
    }));

    [Fact]
    public void Delete_DropsTheSelectedEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "a"), CreateEvent(2, "b"), CreateEvent(3, "c")]);

        window.Process(Click(RowCenter(window, 1)));
        window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Equal(["a", "c"], window.Events.Select(item => item.Message));
        Assert.Equal("c", window.SelectedEvent!.Message);
    }));

    [Fact]
    public void Delete_WithoutASelectionDoesNothing() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "a")]);

        window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Single(window.Events);
    }));

    [Fact]
    public void Delete_EmptiesTheList() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(7, "only")]);

        window.Process(Click(RowCenter(window, 0)));
        window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Empty(window.Events);
        Assert.Null(window.SelectedEvent);
    }));

    [Fact]
    public void Cancel_ClosesTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "a")]);

        Assert.Equal(EditorListOutcome.Cancelled, window.Process(Click(Center(window.Cancel.Area))));
    }));

    [Fact]
    public void Okay_AcceptsTheEvents() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "a")]);

        Assert.Equal(EditorListOutcome.Accepted, window.Process(Click(Center(window.Okay.Area))));
    }));

    [Theory]
    [InlineData(0, EditorDailyEventsWindow.CancelTitle)]
    [InlineData(1, EditorDailyEventsWindow.OkayTitle)]
    [InlineData(2, EditorDailyEventsWindow.AddTitle)]
    [InlineData(3, EditorDailyEventsWindow.EditTitle)]
    [InlineData(4, EditorDailyEventsWindow.DeleteTitle)]
    public void RightClick_ExplainsTheButtons(int button, string title) => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "a")]);

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
    public void SetEvents_ReplacesTheListAndClearsTheSelection() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "a")]);

        window.Process(Click(RowCenter(window, 0)));
        window.SetEvents([CreateEvent(9, "nine"), CreateEvent(3, "three")]);

        Assert.Equal([3u, 9u], Days(window));
        Assert.Null(window.SelectedEvent);
    }));

    [Fact]
    public void Draw_PaintsTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventsWindow window = CreateWindow(artwork, [CreateEvent(1, "a"), CreateEvent(2, "b")]);
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

    private static IEnumerable<uint> Days(EditorDailyEventsWindow window) =>
        window.Events.Select(item => item.FirstOccurrenceDay);

    private static MapDailyEvent CreateEvent(uint day, string message) => new()
    {
        FirstOccurrenceDay = day,
        Message = message,
        HumanPlayerColors = PlayerColor.Blue,
        ComputerPlayerColors = PlayerColor.Red,
        RepeatPeriodInDays = 7,
        Resources = new Funds(0, 0, 0, 0, 0, 0, 500)
    };

    private static Point RowCenter(EditorDailyEventsWindow window, int row)
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

    private static EditorDailyEventsWindow CreateWindow(EditorListArtwork artwork, IReadOnlyList<MapDailyEvent> events) =>
        EditorDailyEventsWindow.Create(Screen, events, artwork,
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
                                            "The artwork of the daily events window is missing.");

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
