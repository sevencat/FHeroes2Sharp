using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
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
/// Tests of the editor's sphinx window: the riddle, the answer list and its three buttons, the reward, the colours
/// are not here (there are none) and the buttons (<c>editor_sphinx_window.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorSphinxWindowTests
{
    private const byte AttributeColor = 7;

    private const int BodyLineHeight = 14;

    private static readonly Size Screen = new(640, 480);

    [Fact]
    public void Create_CopiesTheSphinx() => Assert.True(TryWithArtwork(artwork =>
    {
        SphinxMetadata source = CreateSphinx();
        source.Riddle = "original";

        EditorSphinxWindow window = CreateWindow(artwork, source);
        source.Riddle = "changed afterwards";

        Assert.Equal("original", window.Riddle);
        Assert.Equal(2, window.Answers.Count);
    }));

    [Fact]
    public void Layout_BuildsTheColumns() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());
        Rect active = window.ActiveArea;

        Assert.Equal(EditorSphinxWindow.ActiveWidth, active.Width);
        Assert.Equal(EditorSphinxWindow.ActiveHeight, active.Height);
        Assert.Equal(EditorSphinxWindow.RiddleAreaWidth, window.RiddleArea.Width);
        Assert.Equal(EditorSphinxWindow.RiddleAreaHeight, window.RiddleArea.Height);
        Assert.Equal(EditorSphinxWindow.AnswerAreaWidth, window.AnswerArea.Width);
        Assert.Equal(EditorSphinxWindow.AnswerAreaHeight, window.AnswerArea.Height);
        Assert.Equal(window.RiddleArea.Right + EditorSphinxWindow.ElementOffset, window.AnswerArea.X);
        Assert.Equal(window.RiddleArea.Y, window.AnswerArea.Y);
        Assert.True(window.ResourceArea.Y > window.RiddleArea.Bottom);
        Assert.Equal(window.AnswerArea.X, window.ResourceArea.X);
        Assert.Equal(EditorSphinxWindow.ResourcePanelHeight, window.ResourceArea.Height);
        Assert.True(window.ArtifactArea.Y > window.RiddleArea.Bottom);
        Assert.True(window.ArtifactArea.Right < window.ResourceArea.X);
        Assert.Equal(window.AnswerArea.Bottom + 5, window.AddButton.Area.Y);
        Assert.Equal(window.AnswerArea.Bottom + 5, window.EditButton.Area.Y);
        Assert.Equal(window.AnswerArea.Bottom + 5, window.DeleteButton.Area.Y);
        Assert.Equal(window.AnswerArea.X, window.AddButton.Area.X);
        Assert.Equal(window.AnswerArea.Right - window.DeleteButton.Area.Width, window.DeleteButton.Area.X);
        Assert.True(window.EditButton.Area.X > window.AddButton.Area.Right);
        Assert.True(window.EditButton.Area.X < window.DeleteButton.Area.X);
        Assert.True(window.DeleteArtifactButton.Area.Y > window.ArtifactArea.Bottom);
        Assert.True(window.Cancel.Area.X > window.Okay.Area.X);
        Assert.True(window.Okay.Area.Y > window.ResourceArea.Bottom);
    }));

    [Fact]
    public void SetRiddle_ReplacesAndCapsTheText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.SetRiddle("new riddle");
        Assert.Equal("new riddle", window.Riddle);

        window.SetRiddle(new string('a', EditorSphinxWindow.MessageCharLimit + 20));
        Assert.Equal(EditorSphinxWindow.MessageCharLimit, window.Riddle.Length);
    }));

    [Fact]
    public void AddAnswer_AppendsTheText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        Assert.True(window.AddAnswer("closed"));
        Assert.Equal(3, window.Answers.Count);
        Assert.Equal("closed", window.Answers[2]);
    }));

    [Fact]
    public void AddAnswer_CapsTheText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        Assert.True(window.AddAnswer(new string('b', EditorSphinxWindow.LongestAnswer + 10)));

        Assert.Equal(EditorSphinxWindow.LongestAnswer, window.Answers[^1].Length);
    }));

    [Fact]
    public void AddAnswer_RejectsADuplicate() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        Assert.False(window.AddAnswer("door"));
        Assert.Equal(2, window.Answers.Count);
        Assert.Equal(EditorSphinxWindow.DuplicateAnswerTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorSphinxWindow.DuplicateAnswerMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void ReplaceAnswer_NeedsASelection() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        Assert.Null(window.SelectedAnswer);
        Assert.False(window.ReplaceAnswer("neither"));
        Assert.Equal("door", window.Answers[0]);
    }));

    [Fact]
    public void ReplaceAnswer_WritesTheSelectedRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());
        window.Process(Click(Center(window.RowArea(0))));

        Assert.Equal("door", window.SelectedAnswer);
        Assert.True(window.ReplaceAnswer("gate"));
        Assert.Equal("gate", window.Answers[0]);
    }));

    [Fact]
    public void ReplaceAnswer_RejectsAnotherRowsText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());
        window.Process(Click(Center(window.RowArea(0))));

        Assert.False(window.ReplaceAnswer("key"));

        Assert.Equal("door", window.Answers[0]);
        Assert.Equal(EditorSphinxWindow.DuplicateAnswerTitle, window.Popup!.Value.Header);
    }));

    [Fact]
    public void ReplaceAnswer_AcceptsTheSameText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());
        window.Process(Click(Center(window.RowArea(0))));

        Assert.True(window.ReplaceAnswer("door"));
        Assert.Null(window.Popup);
    }));

    [Fact]
    public void RemoveSelectedAnswer_DropsTheRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        Assert.Null(window.RemoveSelectedAnswer());

        window.Process(Click(Center(window.RowArea(1))));
        Assert.Equal("key", window.RemoveSelectedAnswer());
        Assert.Single(window.Answers);
        Assert.Equal("door", window.Answers[0]);
    }));

    [Fact]
    public void SetArtifact_WritesAndClearsTheSlot() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.SetArtifact(ArtifactType.UltimateSword, 42);

        Assert.Equal(ArtifactType.UltimateSword, window.Artifact);
        Assert.Equal(42, window.ArtifactMetadata);

        window.ClearArtifact();

        Assert.Equal(ArtifactType.Unknown, window.Artifact);
        Assert.Equal(0, window.ArtifactMetadata);
    }));

    [Fact]
    public void SetResourceCount_WritesTheAmount() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.SetResourceCount(Resource.Gold, 2500);
        window.SetResourceCount(Resource.Wood, 3);

        Assert.Equal(2500, window.Resources.Gold);
        Assert.Equal(3, window.Resources.Wood);

        window.SetResourceCount(Resource.Gold, int.MaxValue);
        Assert.Equal(EditorSphinxWindow.ResourceAmountMaximum, window.Resources.Gold);
    }));

    [Fact]
    public void ClickingTheRiddle_AsksForTheInputDialog() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(Click(Center(window.RiddleArea)));

        Assert.True(window.RiddleRequested);
    }));

    [Fact]
    public void ClickingTheAddButton_AsksForANewAnswer() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(Click(Center(window.AddButton.Area)));

        Assert.True(window.AnswerAddRequested);
    }));

    [Fact]
    public void ClickingTheEditButton_AsksForTheSelectedAnswer() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(Click(Center(window.EditButton.Area)));
        Assert.False(window.AnswerEditRequested);

        window.Process(Click(Center(window.RowArea(0))));
        window.Process(Click(Center(window.EditButton.Area)));
        Assert.True(window.AnswerEditRequested);
    }));

    [Fact]
    public void DoubleClickingARow_AsksForTheAnswer() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());
        Point row = Center(window.RowArea(0));

        // 第一下选中，第二下落在已选中的行上才算双击（ListBox 的语义）。
        window.Process(Click(row));
        window.Process(Click(row));

        Assert.True(window.AnswerEditRequested);
        Assert.Equal("door", window.SelectedAnswer);
    }));

    [Fact]
    public void ClickingTheDeleteButton_DropsTheSelectedRow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(Click(Center(window.RowArea(0))));
        window.Process(Click(Center(window.DeleteButton.Area)));

        Assert.Single(window.Answers);
        Assert.Equal("key", window.Answers[0]);
    }));

    [Fact]
    public void ClickingTheArtifact_AsksForThePicker() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(Click(Center(window.ArtifactArea)));

        Assert.True(window.ArtifactRequested);
    }));

    [Fact]
    public void ClickingTheDeleteArtifactButton_EmptiesTheSlot() => Assert.True(TryWithArtwork(artwork =>
    {
        SphinxMetadata source = CreateSphinx();
        source.Artifact = (int)ArtifactType.UltimateSword;

        EditorSphinxWindow window = CreateWindow(artwork, source);
        window.Process(Click(Center(window.DeleteArtifactButton.Area)));

        Assert.Equal(ArtifactType.Unknown, window.Artifact);
    }));

    [Fact]
    public void ClickingAResourceIcon_AsksForTheAmount() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(Click(Center(window.ResourceIconAreas[0])));

        Assert.NotNull(window.ResourceCountRequest);
        Assert.Equal(Resource.Wood, window.ResourceCountRequest!.Value.ResourceType);
        Assert.Equal(EditorSphinxWindow.ResourceAmountMinimum, window.ResourceCountRequest.Value.Dialog.Minimum);
        Assert.Equal(EditorSphinxWindow.ResourceAmountMaximum, window.ResourceCountRequest.Value.Dialog.Maximum);
    }));

    [Fact]
    public void Cancel_ClosesTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        Assert.Equal(EditorListOutcome.Cancelled, window.Process(Click(Center(window.Cancel.Area))));
    }));

    [Fact]
    public void Okay_AcceptsTheSphinx() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        Assert.Equal(EditorListOutcome.Accepted, window.Process(Click(Center(window.Okay.Area))));
    }));

    [Theory]
    [InlineData(0, EditorSphinxWindow.CancelTitle)]
    [InlineData(1, EditorSphinxWindow.OkayTitle)]
    [InlineData(2, EditorSphinxWindow.AddTitle)]
    [InlineData(3, EditorSphinxWindow.EditTitle)]
    [InlineData(4, EditorSphinxWindow.DeleteTitle)]
    public void RightClickOnTheButtons_ExplainsThem(int button, string title) => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

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
    public void RightClickOnTheRiddle_ShowsTheRiddle() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(RightPress(Center(window.RiddleArea)));

        Assert.Equal(EditorSphinxWindow.RiddleTitle, window.Popup!.Value.Header);
        Assert.Equal("A riddle that nobody can answer.", window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheEmptyArtifact_SaysThereIsNone() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(RightPress(Center(window.ArtifactArea)));

        Assert.Equal(EditorSphinxWindow.ArtifactTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorSphinxWindow.NoArtifactMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheArtifact_ShowsItsInfo() => Assert.True(TryWithArtwork(artwork =>
    {
        SphinxMetadata source = CreateSphinx();
        source.Artifact = (int)ArtifactType.UltimateSword;

        EditorSphinxWindow window = CreateWindow(artwork, source);
        window.Process(RightPress(Center(window.ArtifactArea)));

        Assert.NotNull(window.Popup);
        Assert.Contains("Sword", window.Popup!.Value.Header);
    }));

    [Fact]
    public void RightClickOnTheEmptyResources_SaysThereIsNothing() => Assert.True(TryWithArtwork(artwork =>
    {
        SphinxMetadata source = CreateSphinx();
        source.Resources = default;

        EditorSphinxWindow window = CreateWindow(artwork, source);
        window.Process(RightPress(Center(window.ResourceArea)));

        Assert.Equal(EditorSphinxWindow.ResourcesTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorSphinxWindow.NoResourcesMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheFilledResources_ListsTheAmounts() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(RightPress(Center(window.ResourceArea)));

        Assert.Equal(EditorSphinxWindow.ResourcesTitle, window.Popup!.Value.Header);
        Assert.Contains(EditorSphinxWindow.ResourcesMessage, window.Popup.Value.Body);
        Assert.Contains("100", window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheDeleteArtifactButton_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorSphinxWindow window = CreateWindow(artwork, CreateSphinx());

        window.Process(RightPress(Center(window.DeleteArtifactButton.Area)));

        Assert.Equal(EditorSphinxWindow.DeleteArtifactTitle, window.Popup!.Value.Header);
    }));

    [Fact]
    public void Draw_PaintsTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        SphinxMetadata source = CreateSphinx();
        source.Artifact = (int)ArtifactType.UltimateSword;

        EditorSphinxWindow window = CreateWindow(artwork, source);
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

    private static SphinxMetadata CreateSphinx() => new()
    {
        Riddle = "A riddle that nobody can answer.",
        Answers = ["door", "key"],
        Resources = new Funds(Resource.Gold, 100)
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

    private static EditorSphinxWindow CreateWindow(EditorSphinxArtwork artwork, SphinxMetadata source) =>
        EditorSphinxWindow.Create(source, artwork, Screen, new FakeGlyphSource(AttributeColor, BodyLineHeight),
            new FakeGlyphSource(AttributeColor, BodyLineHeight));

    /// <summary>Opens the game data and hands the artwork to the test body.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArtwork(Action<EditorSphinxArtwork> body)
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
            EditorSphinxArtwork artwork = EditorSphinxArtwork.TryCreate(isEvilInterface: false) ??
                                          throw new InvalidOperationException("The artwork of the sphinx is missing.");

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
