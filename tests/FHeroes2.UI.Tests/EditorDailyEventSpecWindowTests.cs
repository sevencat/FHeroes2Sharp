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
/// Tests of the editor's daily event spec window: the layout, the colour boxes, the two numbers, the reward panel,
/// the message area and the buttons (<c>editor_daily_event_spec_window.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorDailyEventSpecWindowTests
{
    private const byte AttributeColor = 5;

    private const int BodyLineHeight = 14;

    private const int SmallLineHeight = 11;

    private static readonly Size Screen = new(640, 480);

    private const PlayerColor AllHuman = PlayerColor.Blue | PlayerColor.Red | PlayerColor.Green;

    private const PlayerColor AllComputer = PlayerColor.Yellow | PlayerColor.Orange | PlayerColor.Purple;

    [Fact]
    public void Create_ClampsTheEventToWhatTheMapAllows() => Assert.True(TryWithArtwork(artwork =>
    {
        MapDailyEvent source = new()
        {
            FirstOccurrenceDay = 0,
            RepeatPeriodInDays = 100_000,
            HumanPlayerColors = PlayerColor.Blue | PlayerColor.Yellow,
            ComputerPlayerColors = PlayerColor.Red
        };

        EditorDailyEventSpecWindow window = CreateWindow(artwork, source);

        Assert.Equal(1u, window.Event.FirstOccurrenceDay);
        Assert.Equal(MapDailyEvent.DaysInYear, window.Event.RepeatPeriodInDays);
        Assert.Equal(PlayerColor.Blue, window.Event.HumanPlayerColors);
        Assert.Equal(PlayerColor.None, window.Event.ComputerPlayerColors);
    }));

    [Fact]
    public void Create_CopiesTheEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        MapDailyEvent source = new() { Message = "original" };

        EditorDailyEventSpecWindow window = CreateWindow(artwork, source);

        source.Message = "changed afterwards";

        Assert.Equal("original", window.Event.Message);
    }));

    [Fact]
    public void Layout_BuildsTheTwoColumns() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());
        Rect active = window.ActiveArea;

        Assert.Equal(EditorDailyEventSpecWindow.WindowWidth, active.Width);
        Assert.Equal(EditorDailyEventSpecWindow.WindowHeight, active.Height);
        Assert.Equal(EditorDailyEventSpecWindow.MessageAreaWidth, window.MessageArea.Width);
        Assert.Equal(EditorDailyEventSpecWindow.MessageAreaHeight, window.MessageArea.Height);
        Assert.Equal(active.X + EditorDailyEventSpecWindow.ElementOffset, window.MessageArea.X);
        Assert.Equal(EditorDailyEventSpecWindow.ResourcePanelHeight, window.ResourceArea.Height);
        Assert.True(window.ResourceArea.Y > window.MessageArea.Bottom);
        Assert.Equal(window.MessageArea.X, window.ResourceArea.X);
        Assert.True(window.PlayerAreaX > window.MessageArea.Right);
        Assert.Equal(EditorDailyEventSpecWindow.PlayerAreaWidth, window.DateDescriptionArea.Width);
        Assert.Equal(EditorDailyEventSpecWindow.DateDescriptionHeight, window.DateDescriptionArea.Height);
        Assert.True(window.DateDescriptionArea.Y > window.FirstDaySelection.Area.Bottom);
        Assert.True(window.RepeatPeriodSelection.Area.Y > window.DateDescriptionArea.Bottom);
    }));

    [Fact]
    public void Layout_PlacesTheColourCheckboxes() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        Assert.Equal(3, window.HumanCheckboxes.Count);
        Assert.Equal(3, window.ComputerCheckboxes.Count);

        for (int i = 1; i < window.HumanCheckboxes.Count; ++i)
        {
            Assert.Equal(EditorDailyEventSpecWindow.CheckboxStepX,
                window.HumanCheckboxes[i].Area.X - window.HumanCheckboxes[i - 1].Area.X);
        }

        Assert.Equal(window.HumanCheckboxes[0].Area.X, window.ComputerCheckboxes[0].Area.X);
        Assert.True(window.ComputerCheckboxes[0].Area.Y > window.HumanCheckboxes[0].Area.Bottom);
        Assert.True(window.HumanCheckboxes[0].Area.X > window.PlayerAreaX);
    }));

    [Fact]
    public void Create_CheckboxStatesFollowTheEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        MapDailyEvent source = CreateEvent();
        source.HumanPlayerColors = PlayerColor.Red;

        EditorDailyEventSpecWindow window = CreateWindow(artwork, source);

        foreach (Checkbox checkbox in window.HumanCheckboxes)
        {
            Assert.Equal(checkbox.Color != PlayerColor.Red, checkbox.Checkmark.IsHidden);
        }

        foreach (Checkbox checkbox in window.ComputerCheckboxes)
        {
            Assert.True(checkbox.Checkmark.IsHidden);
        }
    }));

    [Fact]
    public void ClickingAHumanCheckboxTogglesItsColor() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());
        Point box = Center(window.HumanCheckboxes[0].Area);

        window.Process(Click(box));
        Assert.Equal(PlayerColor.Blue, window.Event.HumanPlayerColors);

        window.Process(Click(box));
        Assert.Equal(PlayerColor.None, window.Event.HumanPlayerColors);
    }));

    [Fact]
    public void ClickingAComputerCheckboxLeavesHumanColorsAlone() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.ComputerCheckboxes[2].Area)));

        Assert.Equal(PlayerColor.Purple, window.Event.ComputerPlayerColors);
        Assert.Equal(PlayerColor.None, window.Event.HumanPlayerColors);
    }));

    [Fact]
    public void NumberElementsStartAtTheEventValues() => Assert.True(TryWithArtwork(artwork =>
    {
        MapDailyEvent source = CreateEvent();
        source.FirstOccurrenceDay = 40;
        source.RepeatPeriodInDays = 7;

        EditorDailyEventSpecWindow window = CreateWindow(artwork, source);

        Assert.Equal(40, window.FirstDaySelection.Value);
        Assert.Equal(7, window.RepeatPeriodSelection.Value);
        Assert.Equal(1, window.FirstDaySelection.Minimum);
        Assert.Equal((int)MapDailyEvent.LastDayForEvents, window.FirstDaySelection.Maximum);
        Assert.Equal(0, window.RepeatPeriodSelection.Minimum);
        Assert.Equal((int)MapDailyEvent.DaysInYear, window.RepeatPeriodSelection.Maximum);
    }));

    [Fact]
    public void SetValueField_WritesBothNumbers() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.SetValueField(DailyEventValueField.FirstOccurrenceDay, 25);
        window.SetValueField(DailyEventValueField.RepeatPeriodInDays, 14);

        Assert.Equal(25u, window.Event.FirstOccurrenceDay);
        Assert.Equal(14u, window.Event.RepeatPeriodInDays);
        Assert.Equal(25, window.FirstDaySelection.Value);
        Assert.Equal(14, window.RepeatPeriodSelection.Value);
    }));

    [Fact]
    public void SetValueField_ClampsOutOfRangeAnswers() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.SetValueField(DailyEventValueField.FirstOccurrenceDay, -5);
        Assert.Equal(1u, window.Event.FirstOccurrenceDay);

        window.SetValueField(DailyEventValueField.RepeatPeriodInDays, -5);
        Assert.Equal(0u, window.Event.RepeatPeriodInDays);
    }));

    [Fact]
    public void ClickingAnEditBox_AsksForTheNumberPad() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.FirstDaySelection.EditBox)));

        Assert.NotNull(window.NumpadRequest);
        Assert.Equal(DailyEventValueField.FirstOccurrenceDay, window.NumpadRequest!.Value.Field);
        Assert.Equal(1, window.NumpadRequest.Value.Minimum);
    }));

    [Fact]
    public void ClickingTheMessageArea_AsksForTheInputDialog() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.MessageArea)));

        Assert.True(window.MessageRequested);
    }));

    [Fact]
    public void SetMessage_ReplacesAndCapsTheText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.SetMessage("new text");
        Assert.Equal("new text", window.Event.Message);

        window.SetMessage(new string('a', EditorDailyEventSpecWindow.MessageCharLimit + 10));
        Assert.Equal(EditorDailyEventSpecWindow.MessageCharLimit, window.Event.Message.Length);
    }));

    [Fact]
    public void ClickingAResourceIcon_AsksForTheAmount() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.ResourceIconAreas[0])));

        Assert.NotNull(window.ResourceCountRequest);
        Assert.Equal(Resource.Wood, window.ResourceCountRequest!.Value.ResourceType);
        Assert.Equal(EditorDailyEventSpecWindow.ResourceAmountMinimum,
            window.ResourceCountRequest.Value.Dialog.Minimum);
        Assert.Equal(EditorDailyEventSpecWindow.ResourceAmountMaximum,
            window.ResourceCountRequest.Value.Dialog.Maximum);
    }));

    [Fact]
    public void SetResourceCount_WritesTheAmount() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.SetResourceCount(Resource.Gold, 1500);
        window.SetResourceCount(Resource.Wood, 7);

        Assert.Equal(1500, window.Event.Resources.Gold);
        Assert.Equal(7, window.Event.Resources.Wood);
    }));

    [Fact]
    public void RightClickOnTheEmptyRewardPanel_SaysThereIsNothing() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.ResourceArea)));

        Assert.Equal(EditorDailyEventSpecWindow.ResourcesTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorDailyEventSpecWindow.NoResourcesMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheFilledRewardPanel_ListsTheAmounts() => Assert.True(TryWithArtwork(artwork =>
    {
        MapDailyEvent source = CreateEvent();
        source.Resources = new Funds(Resource.Gold, 500);

        EditorDailyEventSpecWindow window = CreateWindow(artwork, source);

        window.Process(RightPress(Center(window.ResourceArea)));

        Assert.Equal(EditorDailyEventSpecWindow.ResourcesTitle, window.Popup!.Value.Header);
        Assert.Contains(EditorDailyEventSpecWindow.ResourcesMessage, window.Popup.Value.Body);
        Assert.Contains("500", window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheMessageArea_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.MessageArea)));

        Assert.Equal(EditorDailyEventSpecWindow.MessageAreaTitle, window.Popup!.Value.Header);
    }));

    [Fact]
    public void RightClickOnAHumanCheckbox_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.HumanCheckboxes[0].Area)));

        Assert.Equal("Allow Blue human player to get event", window.Popup!.Value.Header);
    }));

    [Fact]
    public void RightClickOnAComputerCheckbox_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.ComputerCheckboxes[0].Area)));

        Assert.Equal("Allow Yellow computer player to get event", window.Popup!.Value.Header);
    }));

    [Theory]
    [InlineData(0, EditorDailyEventSpecWindow.CancelTitle)]
    [InlineData(1, EditorDailyEventSpecWindow.OkayTitle)]
    public void RightClickOnTheButtons_ExplainsThem(int button, string title) => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        Rect area = button == 0 ? window.Cancel.Area : window.Okay.Area;

        window.Process(RightPress(Center(area)));

        Assert.Equal(title, window.Popup!.Value.Header);
    }));

    [Fact]
    public void Cancel_ClosesTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        Assert.Equal(EditorListOutcome.Cancelled, window.Process(Click(Center(window.Cancel.Area))));
    }));

    [Fact]
    public void Okay_AcceptsTheEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorDailyEventSpecWindow window = CreateWindow(artwork, CreateEvent());

        Assert.Equal(EditorListOutcome.Accepted, window.Process(Click(Center(window.Okay.Area))));
    }));

    [Fact]
    public void Draw_PaintsTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        MapDailyEvent source = CreateEvent();
        source.Message = "A long message that has to wrap inside the message area.";
        source.Resources = new Funds(Resource.Gold, 100);

        EditorDailyEventSpecWindow window = CreateWindow(artwork, source);
        PixelBuffer canvas = new(Screen.Width, Screen.Height);
        canvas.Fill(0);

        window.Draw(canvas, new LocalEvent());

        Rect active = window.ActiveArea;
        bool painted = false;

        for (int y = active.Y; y < active.Bottom && !painted; ++y)
        {
            for (int x = active.X; x < active.Right; ++x)
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

    private static MapDailyEvent CreateEvent() => new()
    {
        FirstOccurrenceDay = 1,
        RepeatPeriodInDays = 0,
        Message = string.Empty
    };

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

    private static EditorDailyEventSpecWindow CreateWindow(EditorDailyEventSpecArtwork artwork, MapDailyEvent source) =>
        EditorDailyEventSpecWindow.Create(source, AllHuman, AllComputer, artwork, Screen,
            new FakeGlyphSource(AttributeColor, BodyLineHeight), new FakeGlyphSource(AttributeColor, SmallLineHeight),
            () => 0);

    /// <summary>Opens the game data and hands the artwork to the test body.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArtwork(Action<EditorDailyEventSpecArtwork> body)
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
            EditorDailyEventSpecArtwork artwork = EditorDailyEventSpecArtwork.TryCreate(isEvilInterface: false) ??
                                                  throw new InvalidOperationException(
                                                      "The artwork of the daily event spec window is missing.");

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
