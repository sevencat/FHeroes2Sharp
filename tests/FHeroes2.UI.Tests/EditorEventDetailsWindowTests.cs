using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the editor's event details window: the two columns, the artifact and skill elements, the experience, the
/// rewards, the conditions and the buttons (<c>editor_event_details_window.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorEventDetailsWindowTests
{
    private const byte AttributeColor = 5;

    private const int BodyLineHeight = 14;

    private const int SmallLineHeight = 11;

    private static readonly Size Screen = new(640, 480);

    private const PlayerColor AllHuman = PlayerColor.Blue | PlayerColor.Red | PlayerColor.Green;

    private const PlayerColor AllComputer = PlayerColor.Yellow | PlayerColor.Orange | PlayerColor.Purple;

    [Fact]
    public void Create_ClampsTheColoursToWhatTheMapAllows() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.HumanPlayerColors = (byte)(PlayerColor.Blue | PlayerColor.Yellow);
        source.ComputerPlayerColors = (byte)PlayerColor.Red;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

        Assert.Equal((byte)PlayerColor.Blue, window.Event.HumanPlayerColors);
        Assert.Equal(0, window.Event.ComputerPlayerColors);
    }));

    [Fact]
    public void Create_CopiesTheEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.Message = "original";

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

        source.Message = "changed afterwards";

        Assert.Equal("original", window.Event.Message);
    }));

    [Fact]
    public void Layout_BuildsTheTwoColumns() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());
        Rect active = window.ActiveArea;

        Assert.Equal(EditorEventDetailsWindow.WindowWidth, active.Width);
        Assert.Equal(EditorEventDetailsWindow.WindowHeight, active.Height);
        Assert.Equal(EditorEventDetailsWindow.SectionWidth, window.MessageArea.Width);
        Assert.Equal(EditorEventDetailsWindow.MessageAreaHeight, window.MessageArea.Height);
        Assert.Equal(active.X + EditorEventDetailsWindow.ElementOffset, window.MessageArea.X);
        Assert.Equal(EditorEventDetailsWindow.ResourcePanelHeight, window.ResourceArea.Height);
        Assert.Equal(window.MessageArea.X + EditorEventDetailsWindow.SectionWidth +
                     EditorEventDetailsWindow.ElementOffset, window.ArtifactArea.X);
        Assert.Equal(window.ResourceArea.X, window.ArtifactArea.X);
        Assert.Equal(window.MessageArea.Y, window.ResourceArea.Y);
        Assert.True(window.ArtifactArea.Y > window.ResourceArea.Bottom);
        Assert.True(window.ExperienceArea.X > window.ArtifactArea.Right);
        Assert.True(window.SecondarySkillArea.X > window.ArtifactArea.Right);
        Assert.True(window.SecondarySkillArea.Right < window.ExperienceArea.X);
        Assert.Equal((window.ArtifactArea.X + window.ArtifactArea.Width + window.ExperienceArea.X -
                      window.SecondarySkillArea.Width) / 2, window.SecondarySkillArea.X);
        Assert.True(window.DeleteArtifactArea.Y > window.ArtifactArea.Bottom);
        Assert.True(window.DeleteSecondarySkillArea.Y > window.SecondarySkillArea.Bottom);
        Assert.Equal(window.DeleteArtifactArea.Y, window.ExperienceValueArea.Y);
        Assert.True(window.RecurringEventArea.Y > window.MessageArea.Bottom);
        Assert.True(window.RecurringEventArea.Height > 0);
    }));

    [Fact]
    public void Layout_PlacesTheColourCheckboxes() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        Assert.Equal(3, window.HumanCheckboxes.Count);
        Assert.Equal(3, window.ComputerCheckboxes.Count);

        for (int i = 1; i < window.HumanCheckboxes.Count; ++i)
        {
            Assert.Equal(EditorEventDetailsWindow.CheckboxStepX,
                window.HumanCheckboxes[i].Area.X - window.HumanCheckboxes[i - 1].Area.X);
        }

        Assert.Equal(window.HumanCheckboxes[0].Area.X, window.ComputerCheckboxes[0].Area.X);
        Assert.True(window.ComputerCheckboxes[0].Area.Y > window.HumanCheckboxes[0].Area.Bottom);
        Assert.True(window.HumanCheckboxes[0].Area.X > window.ArtifactArea.X);
    }));

    [Fact]
    public void Create_CheckboxStatesFollowTheEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.HumanPlayerColors = (byte)PlayerColor.Red;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

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
    public void ClickingAHumanCheckboxTogglesItsColour() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());
        Point box = Center(window.HumanCheckboxes[0].Area);

        window.Process(Click(box));
        Assert.Equal((byte)PlayerColor.Blue, window.Event.HumanPlayerColors);

        window.Process(Click(box));
        Assert.Equal(0, window.Event.HumanPlayerColors);
    }));

    [Fact]
    public void ClickingAComputerCheckboxLeavesTheHumanColoursAlone() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.ComputerCheckboxes[2].Area)));

        Assert.Equal((byte)PlayerColor.Purple, window.Event.ComputerPlayerColors);
        Assert.Equal(0, window.Event.HumanPlayerColors);
    }));

    [Fact]
    public void ClickingTheRecurringCheckbox_FlipsTheEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.IsRecurringEvent = true;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);
        Point box = Center(window.RecurringEventArea);

        window.Process(Click(box));
        Assert.False(window.Event.IsRecurringEvent);

        window.Process(Click(box));
        Assert.True(window.Event.IsRecurringEvent);
    }));

    [Fact]
    public void ClickingTheMessageArea_AsksForTheInputDialog() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.MessageArea)));

        Assert.True(window.MessageRequested);
    }));

    [Fact]
    public void SetMessage_ReplacesAndCapsTheText() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.SetMessage("new text");
        Assert.Equal("new text", window.Event.Message);

        window.SetMessage(new string('a', EditorEventDetailsWindow.MessageCharLimit + 10));
        Assert.Equal(EditorEventDetailsWindow.MessageCharLimit, window.Event.Message.Length);
    }));

    [Fact]
    public void ClickingAResourceIcon_AsksForTheAmount() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.ResourceIconAreas[0])));

        Assert.NotNull(window.ResourceCountRequest);
        Assert.Equal(Resource.Wood, window.ResourceCountRequest!.Value.ResourceType);
        Assert.Equal(EditorEventDetailsWindow.ResourceAmountMinimum, window.ResourceCountRequest.Value.Dialog.Minimum);
        Assert.Equal(EditorEventDetailsWindow.ResourceAmountMaximum, window.ResourceCountRequest.Value.Dialog.Maximum);
    }));

    [Fact]
    public void SetResourceCount_WritesTheAmount() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.SetResourceCount(Resource.Gold, 1500);
        window.SetResourceCount(Resource.Wood, 7);

        Assert.Equal(1500, window.Event.Resources.Gold);
        Assert.Equal(7, window.Event.Resources.Wood);
    }));

    [Fact]
    public void ClickingTheArtifact_AsksForThePicker() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.ArtifactArea)));

        Assert.True(window.ArtifactRequested);
    }));

    [Fact]
    public void SetArtifact_WritesTheSlotAndTheElement() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.SetArtifact(ArtifactType.UltimateSword, 42);

        Assert.Equal((int)ArtifactType.UltimateSword, window.Event.Artifact);
        Assert.Equal(42, window.Event.ArtifactMetadata);
        Assert.True(window.ArtifactElement.Area.Width > 0);

        window.ClearArtifact();

        Assert.Equal(0, window.Event.Artifact);
        Assert.Equal(0, window.Event.ArtifactMetadata);
    }));

    [Fact]
    public void ClickingTheDeleteArtifactButton_EmptiesTheSlot() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.Artifact = (int)ArtifactType.UltimateSword;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

        window.Process(Click(Center(window.DeleteArtifactArea)));

        Assert.Equal(0, window.Event.Artifact);
    }));

    [Fact]
    public void ClickingTheSecondarySkill_AsksForThePicker() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(Click(Center(window.SecondarySkillArea)));

        Assert.True(window.SecondarySkillRequested);
    }));

    [Fact]
    public void SetSecondarySkill_WritesTheSlotAndTheElement() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.SetSecondarySkill(SecondarySkill.Pathfinding, SkillLevel.Expert);

        Assert.Equal((byte)SecondarySkill.Pathfinding, window.Event.SecondarySkill);
        Assert.Equal((byte)SkillLevel.Expert, window.Event.SecondarySkillLevel);

        window.ClearSecondarySkill();

        Assert.Equal(0, window.Event.SecondarySkill);
        Assert.Equal(0, window.Event.SecondarySkillLevel);
    }));

    [Fact]
    public void ClickingTheDeleteSkillButton_EmptiesTheSlot() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.SecondarySkill = (byte)SecondarySkill.Pathfinding;
        source.SecondarySkillLevel = (byte)SkillLevel.Expert;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

        window.Process(Click(Center(window.DeleteSecondarySkillArea)));

        Assert.Equal(0, window.Event.SecondarySkill);
    }));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ClickingTheExperience_AsksForTheAmount(int part) => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.Experience = 2500;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);
        Rect area = part == 0 ? window.ExperienceArea : window.ExperienceValueArea;

        window.Process(Click(Center(area)));

        Assert.NotNull(window.ExperienceRequest);
        Assert.Equal(2500, window.ExperienceRequest!.Value.Value);
        Assert.Equal(0, window.ExperienceRequest.Value.Dialog.Minimum);
        Assert.Equal((int)HeroExperience.MaxValue, window.ExperienceRequest.Value.Dialog.Maximum);
    }));

    [Fact]
    public void SetExperience_ClampsTheAmount() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.SetExperience(-100);
        Assert.Equal(0, window.Event.Experience);

        window.SetExperience(int.MaxValue);
        Assert.Equal((int)HeroExperience.MaxValue, window.Event.Experience);
    }));

    [Fact]
    public void RightClickOnTheEmptyArtifact_SaysThereIsNone() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.ArtifactArea)));

        Assert.Equal(EditorEventDetailsWindow.ArtifactTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorEventDetailsWindow.NoArtifactMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheArtifact_ShowsItsInfo() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.Artifact = (int)ArtifactType.UltimateSword;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

        window.Process(RightPress(Center(window.ArtifactArea)));

        Assert.NotNull(window.Popup);
        Assert.Contains(ArtifactTable.Get(ArtifactType.UltimateSword).Name, window.Popup!.Value.Header);
    }));

    [Fact]
    public void RightClickOnTheEmptySkill_SaysThereIsNone() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.SecondarySkillArea)));

        Assert.Equal(EditorEventDetailsWindow.SecondarySkillTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorEventDetailsWindow.NoSecondarySkillMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheSkill_ShowsItsInfo() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.SecondarySkill = (byte)SecondarySkill.Pathfinding;
        source.SecondarySkillLevel = (byte)SkillLevel.Expert;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

        window.Process(RightPress(Center(window.SecondarySkillArea)));

        Assert.NotNull(window.Popup);
        Assert.Contains(SecondarySkills.GetName(SecondarySkill.Pathfinding), window.Popup!.Value.Header);
    }));

    [Fact]
    public void RightClickOnTheRecurringCheckbox_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.RecurringEventArea)));

        Assert.Equal(EditorEventDetailsWindow.RecurringEventLabel, window.Popup!.Value.Header);
        Assert.Equal(EditorEventDetailsWindow.RecurringEventMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheMessageArea_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.MessageArea)));

        Assert.Equal(EditorEventDetailsWindow.MessageAreaTitle, window.Popup!.Value.Header);
    }));

    [Fact]
    public void RightClickOnTheEmptyRewardPanel_SaysThereIsNothing() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.ResourceArea)));

        Assert.Equal(EditorEventDetailsWindow.ResourcesTitle, window.Popup!.Value.Header);
        Assert.Equal(EditorEventDetailsWindow.NoResourcesMessage, window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnTheFilledRewardPanel_ListsTheAmounts() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.Resources = new Funds(Resource.Gold, 500);

        EditorEventDetailsWindow window = CreateWindow(artwork, source);

        window.Process(RightPress(Center(window.ResourceArea)));

        Assert.Equal(EditorEventDetailsWindow.ResourcesTitle, window.Popup!.Value.Header);
        Assert.Contains(EditorEventDetailsWindow.ResourcesMessage, window.Popup.Value.Body);
        Assert.Contains("500", window.Popup.Value.Body);
    }));

    [Fact]
    public void RightClickOnAHumanCheckbox_ExplainsIt() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        window.Process(RightPress(Center(window.HumanCheckboxes[0].Area)));

        Assert.Equal("Allow Blue human player to get event", window.Popup!.Value.Header);
    }));

    [Theory]
    [InlineData(0, EditorEventDetailsWindow.CancelTitle)]
    [InlineData(1, EditorEventDetailsWindow.OkayTitle)]
    public void RightClickOnTheButtons_ExplainsThem(int button, string title) => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        Rect area = button == 0 ? window.Cancel.Area : window.Okay.Area;

        window.Process(RightPress(Center(area)));

        Assert.Equal(title, window.Popup!.Value.Header);
    }));

    [Fact]
    public void Cancel_ClosesTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        Assert.Equal(EditorListOutcome.Cancelled, window.Process(Click(Center(window.Cancel.Area))));
    }));

    [Fact]
    public void Okay_AcceptsTheEvent() => Assert.True(TryWithArtwork(artwork =>
    {
        EditorEventDetailsWindow window = CreateWindow(artwork, CreateEvent());

        Assert.Equal(EditorListOutcome.Accepted, window.Process(Click(Center(window.Okay.Area))));
    }));

    [Fact]
    public void Draw_PaintsTheWindow() => Assert.True(TryWithArtwork(artwork =>
    {
        AdventureMapEventMetadata source = CreateEvent();
        source.Message = "A message long enough to be wrapped inside the message area of the window.";
        source.Resources = new Funds(Resource.Gold, 100);
        source.Artifact = (int)ArtifactType.UltimateSword;
        source.SecondarySkill = (byte)SecondarySkill.Pathfinding;
        source.SecondarySkillLevel = (byte)SkillLevel.Basic;
        source.Experience = 1000;

        EditorEventDetailsWindow window = CreateWindow(artwork, source);
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

    private static AdventureMapEventMetadata CreateEvent() => new()
    {
        Message = string.Empty,
        HumanPlayerColors = 0,
        ComputerPlayerColors = 0,
        IsRecurringEvent = false
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

    private static EditorEventDetailsWindow CreateWindow(EditorEventDetailsArtwork artwork,
        AdventureMapEventMetadata source) =>
        EditorEventDetailsWindow.Create(source, AllHuman, AllComputer, artwork, isEvilInterface: false, Screen,
            new FakeGlyphSource(AttributeColor, BodyLineHeight), new FakeGlyphSource(AttributeColor, BodyLineHeight),
            new FakeGlyphSource(AttributeColor, SmallLineHeight));

    /// <summary>Opens the game data and hands the artwork to the test body.</summary>
    /// <param name="body">The test body.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithArtwork(Action<EditorEventDetailsArtwork> body)
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
            EditorEventDetailsArtwork artwork =
                EditorEventDetailsArtwork.TryCreate(isEvilInterface: false) ??
                throw new InvalidOperationException("The artwork of the event details window is missing.");

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
