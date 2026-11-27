using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using Xunit;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="EditorMapSpecsInput"/>, the loop body of <c>mapSpecificationsDialog()</c>
/// (<c>editor_map_specs_window.cpp:2256-2516</c>) minus the parts that later issues add.
/// </summary>
/// <remarks>
/// 窗口的构造需要真实美术件，所以整类跟着游戏数据门控。写回（<see cref="EditorMapSpecsInput.Commit"/>）只是把
/// <see cref="MapSpecsState.CreateInfo"/> 转给 <c>MapEditSession.ApplyMapInfo</c>，那条链已由
/// <c>MapSpecsStateTests</c> 覆盖，这里不再重复搭地图。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorMapSpecsInputTests : IDisposable
{
    private AssetBundle? _assets;

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose()
    {
        GameAssets.Current = null;
        _assets?.Dispose();
        _assets = null;
    }

    [Fact]
    public void EscapeCancels()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        Assert.Equal(EditorMapSpecsOutcome.Cancelled, input.Process(KeyPress(Key.Escape)));
    }

    [Fact]
    public void EnterAccepts()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        Assert.Equal(EditorMapSpecsOutcome.Accepted, input.Process(KeyPress(Key.Enter)));
    }

    [Fact]
    public void ClickingTheButtonsReturnsTheirOutcomes()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        Assert.Equal(EditorMapSpecsOutcome.Cancelled, input.Process(Click(Center(input.Window.Cancel.Area))));
        Assert.Equal(EditorMapSpecsOutcome.Accepted, input.Process(Click(Center(input.Window.Okay.Area))));
    }

    [Fact]
    public void ClickingTheMapNameAsksForASingleLineName()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        EditorMapSpecsOutcome outcome = input.Process(Click(Center(input.Window.PageLayout.MapNameRoi)));

        Assert.Equal(EditorMapSpecsOutcome.None, outcome);
        Assert.Equal(EditorMapSpecsRequestKind.EditMapName, input.Request.Kind);
        Assert.Equal(EditorMapSpecsInput.ChangeMapNameBody, input.Request.Body);
        Assert.Equal(string.Empty, input.Request.Title);

        InputStringDialog dialog = Assert.IsType<InputStringDialog>(input.Request.Dialog);
        Assert.False(dialog.IsMultiLine);
        Assert.Equal(input.MaxMapNameLength, dialog.CharacterLimit);
        Assert.Equal(input.Window.State.Name, dialog.InitialText);
    }

    [Fact]
    public void ClickingTheDescriptionAsksForAMultiLineDescription()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        input.Process(Click(Center(input.Window.PageLayout.DescriptionTextRoi)));

        Assert.Equal(EditorMapSpecsRequestKind.EditMapDescription, input.Request.Kind);
        Assert.Equal(EditorMapSpecsInput.ChangeMapDescriptionBody, input.Request.Body);

        InputStringDialog dialog = Assert.IsType<InputStringDialog>(input.Request.Dialog);
        Assert.True(dialog.IsMultiLine);
        Assert.Equal(input.MaxDescriptionLength, dialog.CharacterLimit);
        Assert.Equal(input.Window.State.Description, dialog.InitialText);
    }

    [Fact]
    public void ResolvingAnEditWritesTheTextBack()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        input.Process(Click(Center(input.Window.PageLayout.MapNameRoi)));

        Assert.True(input.Resolve("New name"));
        Assert.Equal("New name", input.Window.State.Name);
        Assert.True(input.Request.IsNone);

        input.Process(Click(Center(input.Window.PageLayout.DescriptionTextRoi)));

        Assert.True(input.Resolve("A new description."));
        Assert.Equal("A new description.", input.Window.State.Description);
    }

    [Fact]
    public void CancellingAnEditKeepsTheText()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        string name = input.Window.State.Name;

        input.Process(Click(Center(input.Window.PageLayout.MapNameRoi)));

        Assert.False(input.Resolve(null));
        Assert.Equal(name, input.Window.State.Name);
        Assert.True(input.Request.IsNone);
    }

    [Fact]
    public void ResolvingWithoutAPendingEditDoesNothing()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        string name = input.Window.State.Name;

        Assert.False(input.Resolve("Ignored"));
        Assert.Equal(name, input.Window.State.Name);
    }

    /// <summary>
    /// Every player icon of a fresh map has an area, so the clicks below hit something.
    /// </summary>
    /// <remarks>
    /// 新地图默认「人类和电脑都能选」，图标走 <c>NGEXTRA</c> 82-87——归档里没有这六帧，由
    /// <see cref="FHeroes2.Game.Gui.EditorPlayerIcons"/> 在取帧时现场拼（<c>game_assets.cpp:3965-3989</c>）。
    /// 这条测试就是那六帧缺一帧就红的哨兵。
    /// </remarks>
    [Fact]
    public void EveryPlayerIconHasAnArea()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        Assert.NotEmpty(input.Window.PageLayout.PlayerRois);

        foreach (Rect area in input.Window.PageLayout.PlayerRois)
        {
            Assert.False(area.IsEmpty, $"A player icon has an empty area: {area}.");
        }
    }

    [Fact]
    public void ClickingAPlayerIconChangesItsType()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        PlayerColor color = PlayerColors.ToList(input.Window.State.AvailablePlayers)[0];
        bool wasComputer = input.Window.State.IsComputerPlayer(color);

        input.Process(Click(Center(input.Window.PageLayout.PlayerRois[0])));

        Assert.NotEqual(wasComputer, input.Window.State.IsComputerPlayer(color));
    }

    [Fact]
    public void ClickingADifficultyIconSelectsIt()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        const int index = 2;

        input.Process(Click(Center(input.Window.PageLayout.DifficultyRois[index])));

        Assert.Equal(MapSpecsPageIcons.GetDifficultyFromIndex(index), input.Window.State.Difficulty);
    }

    [Fact]
    public void RightClickingEveryControlShowsItsMessage()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        AssertMessage(input, input.Window.Cancel.Area, EditorMapSpecsInput.CancelTitle, EditorMapSpecsInput.CancelMessage);
        AssertMessage(input, input.Window.Okay.Area, EditorMapSpecsInput.OkayTitle, EditorMapSpecsInput.OkayMessage);
        AssertMessage(input, input.Window.PageLayout.DescriptionTextRoi, EditorMapSpecsInput.MapDescriptionTitle,
            EditorMapSpecsInput.MapDescriptionMessage);
        AssertMessage(input, input.Window.PageLayout.PlayerRois[0], EditorMapSpecsInput.PlayerTypeTitle,
            EditorMapSpecsInput.PlayerTypeMessage);
        AssertMessage(input, input.Window.PageLayout.DifficultyRois[0], EditorMapSpecsInput.MapDifficultyTitle,
            EditorMapSpecsInput.MapDifficultyMessage);

        // 地图名的提示里带着当前名字，参考 :2399-2403 把名字夹在引号中间。
        input.Process(RightPress(Center(input.Window.PageLayout.MapNameRoi)));

        Assert.Equal(EditorMapSpecsRequestKind.ShowMessage, input.Request.Kind);
        Assert.Equal(EditorMapSpecsInput.MapNameTitle, input.Request.Title);
        Assert.Contains(input.Window.State.Name, input.Request.Body, StringComparison.Ordinal);
        Assert.Null(input.Request.Dialog);
    }

    [Fact]
    public void AnEventOutsideEveryControlChangesNothing()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        string name = input.Window.State.Name;
        GameDifficulty difficulty = input.Window.State.Difficulty;

        Assert.Equal(EditorMapSpecsOutcome.None, input.Process(Click(new Point(1, 1))));
        Assert.True(input.Request.IsNone);
        Assert.Equal(name, input.Window.State.Name);
        Assert.Equal(difficulty, input.Window.State.Difficulty);
    }

    private static void AssertMessage(EditorMapSpecsInput input, Rect area, string title, string body)
    {
        input.Process(RightPress(Center(area)));

        Assert.Equal(EditorMapSpecsRequestKind.ShowMessage, input.Request.Kind);
        Assert.Equal(title, input.Request.Title);
        Assert.Equal(body, input.Request.Body);
        Assert.Null(input.Request.Dialog);
    }

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

    private static LocalEvent KeyPress(Key key)
    {
        LocalEvent events = new();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        return events;
    }
}
