using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using Xunit;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The victory / loss condition drop-down of the map specifications page, <c>showWinLoseList()</c>
/// (<c>editor_map_specs_window.cpp:1881-1955</c>) and its call site (<c>:2336-2369</c>).
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorMapSpecsConditionPopupTests
{
    [Fact]
    public void TheListSitsUnderItsTextRowAndStartsOnTheCurrentCondition()
    {
        MapSpecsState state = CreateState();
        MapSpecsPageLayout layout = CreateLayout();

        EditorMapSpecsConditionPopup popup = EditorMapSpecsConditionPopup.TryOpenVictory(state, layout, CreateFrame(), _ => null)!;

        // 位置照参考 { victoryTextRoi.x - 2, victoryTextRoi.y + victoryTextRoi.height }。
        Assert.Equal(layout.VictoryTextRoi.X - 2, popup.Area.X);
        Assert.Equal(layout.VictoryTextRoi.Y + layout.VictoryTextRoi.Height, popup.Area.Y);
        Assert.False(popup.IsLoss);
        Assert.Equal(EditorMapSpecsConditionPopup.VictoryTitle, popup.MessageTitle);
        Assert.False(popup.IsClosed);
        Assert.Null(popup.ChosenCondition);
    }

    [Fact]
    public void ClickingARowPicksIt()
    {
        MapSpecsState state = CreateState();
        EditorMapSpecsConditionPopup popup = EditorMapSpecsConditionPopup.TryOpenVictory(state, CreateLayout(), CreateFrame(), _ => null)!;
        IReadOnlyList<MapVictoryCondition> conditions = MapSpecsConditionChoices.Victory(state);

        popup.Process(Click(RowPoint(popup.Area, 1, conditions.Count)));

        Assert.True(popup.IsClosed);
        Assert.Equal((int)conditions[1], popup.ChosenCondition);
    }

    [Fact]
    public void EnterPicksTheCurrentRow()
    {
        MapSpecsState state = CreateState();
        state.VictoryCondition = MapVictoryCondition.ObtainArtifact;

        EditorMapSpecsConditionPopup popup = EditorMapSpecsConditionPopup.TryOpenVictory(state, CreateLayout(), CreateFrame(), _ => null)!;

        popup.Process(Press(Key.Enter));

        Assert.True(popup.IsClosed);
        Assert.Equal((int)MapVictoryCondition.ObtainArtifact, popup.ChosenCondition);
    }

    [Fact]
    public void EscapeClosesWithoutAChoice()
    {
        MapSpecsState state = CreateState();
        EditorMapSpecsConditionPopup popup = EditorMapSpecsConditionPopup.TryOpenVictory(state, CreateLayout(), CreateFrame(), _ => null)!;

        popup.Process(Press(Key.Escape));

        Assert.True(popup.IsClosed);
        Assert.Null(popup.ChosenCondition);
    }

    [Fact]
    public void AClickOutsideClosesWithoutAChoice()
    {
        MapSpecsState state = CreateState();
        EditorMapSpecsConditionPopup popup = EditorMapSpecsConditionPopup.TryOpenVictory(state, CreateLayout(), CreateFrame(), _ => null)!;

        // 参考：列表循环里任意左键都退出，但退出时不改条件。
        popup.Process(Click(new Point(popup.Area.X + popup.Area.Width + 40, popup.Area.Y + 4)));

        Assert.True(popup.IsClosed);
        Assert.Null(popup.ChosenCondition);
    }

    [Fact]
    public void ARightClickAsksForTheDescription()
    {
        MapSpecsState state = CreateState();
        EditorMapSpecsConditionPopup popup = EditorMapSpecsConditionPopup.TryOpenVictory(state, CreateLayout(), CreateFrame(), _ => null)!;
        IReadOnlyList<MapVictoryCondition> conditions = MapSpecsConditionChoices.Victory(state);

        popup.Process(RightClick(RowPoint(popup.Area, 1, conditions.Count)));

        // 参考在右键时弹消息但不关列表，正文是 getVictoryConditionText()。
        Assert.False(popup.IsClosed);
        Assert.Equal(MapSpecsConditionRows.GetVictoryText(conditions[1]), popup.Message);

        popup.ClearMessage();

        Assert.Null(popup.Message);
    }

    [Fact]
    public void TheLossListUsesTheLossConditionsAndTitle()
    {
        MapSpecsState state = CreateState();
        EditorMapSpecsConditionPopup popup = EditorMapSpecsConditionPopup.TryOpenLoss(state, CreateLayout(), CreateFrame(), _ => null)!;

        Assert.True(popup.IsLoss);
        Assert.Equal(EditorMapSpecsConditionPopup.LossTitle, popup.MessageTitle);

        popup.Process(RightClick(RowPoint(popup.Area, 1, MapSpecsConditionChoices.Loss(state).Count)));

        Assert.Equal(MapSpecsConditionRows.GetLossText(MapSpecsConditionChoices.Loss(state)[1]), popup.Message);
    }

    [Fact]
    public void ClickingTheDropButtonOpensTheList()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            Assert.Null(input.Window.ConditionPopup);

            input.Process(Click(Center(input.Window.PageLayout.VictoryButtonRoi)));

            Assert.NotNull(input.Window.ConditionPopup);
            Assert.False(input.Window.ConditionPopup!.IsLoss);
            Assert.Equal(EditorMapSpecsOutcome.None, input.Process(Press(Key.Escape)));
            Assert.Null(input.Window.ConditionPopup);
        }
        finally
        {
            GameAssets.Current = null;
            _assets?.Dispose();
        }
    }

    [Fact]
    public void PickingAConditionWritesItToTheState()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapVictoryCondition target = MapSpecsConditionChoices.Victory(input.Window.State)[1];

            input.Process(Click(Center(input.Window.PageLayout.VictoryDropRoi)));

            EditorMapSpecsConditionPopup popup = input.Window.ConditionPopup!;
            input.Process(Click(RowPoint(popup.Area, 1, MapSpecsConditionChoices.Victory(input.Window.State).Count)));

            Assert.Null(input.Window.ConditionPopup);
            Assert.Equal(target, input.Window.State.VictoryCondition);
        }
        finally
        {
            GameAssets.Current = null;
            _assets?.Dispose();
        }
    }

    [Fact]
    public void EscapeClosesTheListWithoutChangingTheCondition()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapVictoryCondition before = input.Window.State.VictoryCondition;
            int lossBefore = (int)input.Window.State.LossCondition;

            input.Process(Click(Center(input.Window.PageLayout.VictoryDropRoi)));
            input.Process(Press(Key.Escape));

            Assert.Null(input.Window.ConditionPopup);
            Assert.Equal(before, input.Window.State.VictoryCondition);
            Assert.Equal(lossBefore, (int)input.Window.State.LossCondition);
        }
        finally
        {
            GameAssets.Current = null;
            _assets?.Dispose();
        }
    }

    [Fact]
    public void RightClickingTheDropButtonShowsItsHint()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            input.Process(RightClick(Center(input.Window.PageLayout.VictoryButtonRoi)));

            Assert.Equal(EditorMapSpecsRequestKind.ShowMessage, input.Request.Kind);
            Assert.Equal(EditorMapSpecsConditionPopup.VictoryTitle, input.Request.Title);
            Assert.Equal(EditorMapSpecsInput.VictoryConditionMessage, input.Request.Body);

            input.Process(RightClick(Center(input.Window.PageLayout.LossButtonRoi)));

            Assert.Equal(EditorMapSpecsConditionPopup.LossTitle, input.Request.Title);
            Assert.Equal(EditorMapSpecsInput.LossConditionMessage, input.Request.Body);
        }
        finally
        {
            GameAssets.Current = null;
            _assets?.Dispose();
        }
    }

    [Fact]
    public void TheListPaintsOnlyInsideItsArea()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            Assert.True(input.Window.OpenConditionList(isLoss: false));

            EditorMapSpecsConditionPopup popup = input.Window.ConditionPopup!;
            PixelBuffer target = new(640, 480);

            popup.Draw(target);

            // 列表自带底框，画的是自己的整块区域；框外一个像素都不许动。
            int inside = CountNonZero(target, popup.Area);
            Assert.True(inside > 0, $"The condition list painted nothing inside {popup.Area}.");
            Assert.Equal(0, CountNonZero(target, new Rect(0, 0, 640, popup.Area.Y)));
        }
        finally
        {
            GameAssets.Current = null;
            _assets?.Dispose();
        }
    }

    private static int CountNonZero(PixelBuffer buffer, Rect area)
    {
        int count = 0;

        for (int y = area.Y; y < area.Y + area.Height; ++y)
        {
            for (int x = area.X; x < area.X + area.Width; ++x)
            {
                if (buffer[x, y] != 0)
                {
                    ++count;
                }
            }
        }

        return count;
    }

    private AssetBundle? _assets;

    private bool TryCreate(out EditorMapSpecsInput input)
    {
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        GameAssets.Current = _assets;

        if (_assets is null)
        {
            input = null!;

            return false;
        }

        MapSpecsPageArtwork artwork = MapSpecsPageArtwork.TryCreate(isEvilInterface: false)
            ?? throw new InvalidOperationException("The installed game data has no map specifications page artwork.");

        EditorMapSpecsWindow window = EditorMapSpecsWindow.Create(artwork,
            new MapSpecsState(Mp2Map.CreateBlank(36, terrainImageIndex: 0)));

        input = new EditorMapSpecsInput(window);

        return true;
    }

    private static MapSpecsState CreateState() => new(Mp2Map.CreateBlank(36, terrainImageIndex: 0));

    private static MapSpecsPageLayout CreateLayout() => MapSpecsPageLayout.Create(
        new Rect(0, 0, 640, 480),
        new Size(220, 30),
        [new Size(62, 45)],
        difficultyIconSideLength: 30,
        dropListButton: new Size(15, 15),
        dropListBackground: new Size(58, 14));

    private static Sprite CreateFrame() => new(58, 14);

    private static Point RowPoint(Rect area, int index, int count)
    {
        int rowHeight = (area.Height - 6) / count;

        return new Point(area.X + 10, area.Y + 5 + (index * rowHeight) + (rowHeight / 2));
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

    private static LocalEvent RightClick(Point position)
    {
        LocalEvent events = new();
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        return events;
    }

    private static LocalEvent Press(Key key)
    {
        LocalEvent events = new();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        return events;
    }
}
