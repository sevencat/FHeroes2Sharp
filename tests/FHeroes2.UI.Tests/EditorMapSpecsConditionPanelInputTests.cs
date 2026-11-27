using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using Xunit;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the condition panels inside <see cref="EditorMapSpecsInput"/>, the
/// <c>VictoryConditionUI::processEvents()</c> / <c>LossConditionUI::processEvents()</c> pair the reference runs at
/// <c>editor_map_specs_window.cpp:2274-2281</c>.
/// </summary>
/// <remarks>
/// 面板只在窗口拿到字体与调色板时才存在，所以这里用假字体与全黑调色板开窗——面板里的数字与英雄行都只用这两样东西。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorMapSpecsConditionPanelInputTests : IDisposable
{
    private static readonly Point TownTile = new(5, 6);

    private AssetBundle? _assets;

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose()
    {
        GameAssets.Current = null;
        _assets?.Dispose();
        _assets = null;
    }

    [Fact]
    public void ClickingTheGoldArrowRaisesTheAmount()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.CollectGold;
            state.VictoryGoldInThousands = MapSpecsState.MinVictoryGoldInThousands;

            input.Window.Paint(new PixelBuffer(640, 480));

            ValueSelectionElement? gold = input.Window.GoldValue;
            Assert.NotNull(gold);

            input.Process(Click(Center(gold.UpButton.Area)));

            Assert.True(input.Request.IsNone);
            Assert.Equal(MapSpecsState.MinVictoryGoldInThousands + EditorMapSpecsWindow.VictoryGoldStepInThousands,
                state.VictoryGoldInThousands);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void ClickingTheDownArrowLowersTheAmount()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.CollectGold;
            state.VictoryGoldInThousands = MapSpecsState.MinVictoryGoldInThousands + 2;

            input.Window.Paint(new PixelBuffer(640, 480));

            ValueSelectionElement? gold = input.Window.GoldValue;
            Assert.NotNull(gold);

            input.Process(Click(Center(gold.DownButton.Area)));

            Assert.Equal(MapSpecsState.MinVictoryGoldInThousands + 2 - EditorMapSpecsWindow.VictoryGoldStepInThousands,
                state.VictoryGoldInThousands);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void TheAmountStopsAtTheBottomOfItsRange()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.CollectGold;
            state.VictoryGoldInThousands = MapSpecsState.MinVictoryGoldInThousands;

            input.Window.Paint(new PixelBuffer(640, 480));

            ValueSelectionElement gold = input.Window.GoldValue!;

            input.Process(Click(Center(gold.DownButton.Area)));

            // 参考的元素把值夹在自己范围内（ui_dialog.cpp:986-1008），下限就是地图格式的一万金。
            Assert.Equal(MapSpecsState.MinVictoryGoldInThousands, state.VictoryGoldInThousands);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void ClickingTheDayArrowRaisesTheDays()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.LossCondition = MapLossCondition.OutOfTime;
            state.LossDays = 30;

            input.Window.Paint(new PixelBuffer(640, 480));

            ValueSelectionElement? days = input.Window.DaysValue;
            Assert.NotNull(days);

            input.Process(Click(Center(days.UpButton.Area)));

            Assert.Equal(30 + EditorMapSpecsWindow.LossDaysStep, state.LossDays);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void TheGoldPanelTogglesItsTwoCheckBoxes()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.CollectGold;

            input.Window.Paint(new PixelBuffer(640, 480));

            MapSpecsConditionPanelLayout layout = input.Window.VictoryPanel!.Layout;

            input.Process(Click(Center(layout.AllowNormalVictoryRoi)));
            Assert.True(state.AllowNormalVictory);

            input.Process(Click(Center(layout.AllowForAiRoi)));
            Assert.True(state.ComputerAlsoWins);

            input.Process(Click(Center(layout.AllowNormalVictoryRoi)));
            Assert.False(state.AllowNormalVictory);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void ClickingTheCaptureTownRowAsksTheHostToPick()
    {
        if (!TryCreate(out EditorMapSpecsInput input, addTown: true))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.CaptureTown;
            state.UpdateVictoryCondition();

            input.Window.Paint(new PixelBuffer(640, 480));

            MapSpecsConditionPanelLayout layout = input.Window.VictoryPanel!.Layout;

            input.Process(Click(Center(layout.SelectRowRoi)));

            Assert.Equal(EditorMapSpecsRequestKind.PickConditionTarget, input.Request.Kind);
            Assert.Equal(EditorMapSpecsTargetKind.VictoryTown, input.Request.Target);
            Assert.Null(input.Request.Dialog);

            Point picked = new(20, 21);
            Assert.True(input.ResolveTarget(picked));
            Assert.Equal(picked, state.VictoryTarget);
            Assert.True(input.Request.IsNone);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void CancellingAPickKeepsTheTarget()
    {
        if (!TryCreate(out EditorMapSpecsInput input, addTown: true))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.CaptureTown;
            state.UpdateVictoryCondition();

            Point before = state.VictoryTarget;

            input.Window.Paint(new PixelBuffer(640, 480));
            input.Process(Click(Center(input.Window.VictoryPanel!.Layout.SelectRowRoi)));

            Assert.False(input.ResolveTarget(null));
            Assert.Equal(before, state.VictoryTarget);
            Assert.True(input.Request.IsNone);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void RightClickingTheCaptureTownRowExplainsIt()
    {
        if (!TryCreate(out EditorMapSpecsInput input, addTown: true))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.CaptureTown;
            state.UpdateVictoryCondition();

            input.Window.Paint(new PixelBuffer(640, 480));

            input.Process(RightPress(Center(input.Window.VictoryPanel!.Layout.SelectRowRoi)));

            Assert.Equal(EditorMapSpecsRequestKind.ShowMessage, input.Request.Kind);
            Assert.Equal(EditorMapSpecsInput.SpecialVictoryTitle, input.Request.Title);
            Assert.Equal(EditorMapSpecsInput.CaptureTownVictoryMessage, input.Request.Body);
            Assert.Null(input.Request.Dialog);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void RightClickingTheLoseTownRowExplainsIt()
    {
        if (!TryCreate(out EditorMapSpecsInput input, addTown: true))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.LossCondition = MapLossCondition.LoseTown;
            state.UpdateLossCondition();

            input.Window.Paint(new PixelBuffer(640, 480));

            input.Process(RightPress(Center(input.Window.LossPanel!.Layout.SelectRowRoi)));

            Assert.Equal(EditorMapSpecsRequestKind.ShowMessage, input.Request.Kind);
            Assert.Equal(EditorMapSpecsInput.SpecialLossTitle, input.Request.Title);
            Assert.Equal(EditorMapSpecsInput.LoseTownLossMessage, input.Request.Body);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void ClickingTheLoseTownRowAsksForALossPick()
    {
        if (!TryCreate(out EditorMapSpecsInput input, addTown: true))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.LossCondition = MapLossCondition.LoseTown;
            state.UpdateLossCondition();

            input.Window.Paint(new PixelBuffer(640, 480));

            input.Process(Click(Center(input.Window.LossPanel!.Layout.SelectRowRoi)));

            Assert.Equal(EditorMapSpecsTargetKind.LossTown, input.Request.Target);

            Point picked = new(8, 9);
            Assert.True(input.ResolveTarget(picked));
            Assert.Equal(picked, state.LossTarget);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void ClickingAnAllianceCheckBoxMovesTheBoundary()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.DefeatOtherSide;

            IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(state.AvailablePlayers);
            Assert.True(colors.Count >= 3, "The blank map has to offer at least three colours.");

            state.VictorySideCount = 1;

            input.Window.Paint(new PixelBuffer(640, 480));

            IReadOnlyList<IReadOnlyList<Checkbox>> allAlliances = input.Window.VictoryPanel!.AllianceCheckboxes;

            // 第二行（第二方）里点第二个颜色：分界点挪到它前面，颜色从第一方进第二方。
            Checkbox second = FindCheckBox(allAlliances[1], colors[1]);
            input.Process(Click(Center(second.Area)));

            Assert.Equal(1, state.VictorySideCount);
            Assert.True(input.Request.IsNone);

            // 第一行里点同一个颜色：分界点挪到它后面，颜色再回到第一方。
            input.Window.Paint(new PixelBuffer(640, 480));

            Checkbox first = FindCheckBox(input.Window.VictoryPanel!.AllianceCheckboxes[0], colors[1]);
            input.Process(Click(Center(first.Area)));

            Assert.Equal(2, state.VictorySideCount);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void ClickingAColourOfItsOwnAllianceChangesNothing()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.DefeatOtherSide;
            state.VictorySideCount = 2;

            IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(state.AvailablePlayers);

            input.Window.Paint(new PixelBuffer(640, 480));

            IReadOnlyList<IReadOnlyList<Checkbox>> allAlliances = input.Window.VictoryPanel!.AllianceCheckboxes;

            // 第一个颜色本来就在第一方，点它不该改动分界点。
            input.Process(Click(Center(FindCheckBox(allAlliances[0], colors[0]).Area)));

            Assert.Equal(2, state.VictorySideCount);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void RightClickingAnAllianceCheckBoxExplainsIt()
    {
        if (!TryCreate(out EditorMapSpecsInput input))
        {
            return;
        }

        try
        {
            MapSpecsState state = input.Window.State;
            state.VictoryCondition = MapVictoryCondition.DefeatOtherSide;

            IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(state.AvailablePlayers);

            input.Window.Paint(new PixelBuffer(640, 480));

            Checkbox second = FindCheckBox(input.Window.VictoryPanel!.AllianceCheckboxes[1], colors[2]);
            input.Process(RightPress(Center(second.Area)));

            Assert.Equal(EditorMapSpecsRequestKind.ShowMessage, input.Request.Kind);
            Assert.Contains(PlayerColors.GetName(colors[2]), input.Request.Title, StringComparison.Ordinal);
            Assert.Contains(EditorMapSpecsInput.SecondAllianceOrder, input.Request.Title, StringComparison.Ordinal);
            Assert.Contains(PlayerColors.GetName(colors[2]), input.Request.Body, StringComparison.Ordinal);
            Assert.Contains(EditorMapSpecsInput.SecondAllianceOrder, input.Request.Body, StringComparison.Ordinal);
        }
        finally
        {
            Dispose();
        }
    }

    private static Checkbox FindCheckBox(IReadOnlyList<Checkbox> row, PlayerColor color)
    {
        foreach (Checkbox checkbox in row)
        {
            if (checkbox.Color == color)
            {
                return checkbox;
            }
        }

        throw new InvalidOperationException($"The alliance row has no check box for {color}.");
    }

    private bool TryCreate(out EditorMapSpecsInput input, bool addTown = false)
    {
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        GameAssets.Current = _assets;

        if (_assets is null)
        {
            input = null!;

            return false;
        }

        Mp2Map map = Mp2Map.CreateBlank(36, terrainImageIndex: 0);

        if (addTown)
        {
            map.AddCastle(new Mp2CastlePosition(TownTile.X, TownTile.Y, 0x00, MapRace.Knight));
        }

        MapSpecsPageArtwork artwork = MapSpecsPageArtwork.TryCreate(isEvilInterface: false)
            ?? throw new InvalidOperationException("The installed game data has no map specifications page artwork.");

        EditorMapSpecsWindow window = EditorMapSpecsWindow.Create(
            artwork, new MapSpecsState(map), new FakeGlyphSource(1, 14), BlackPalette());

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

    /// <summary>A palette whose every index maps to black, enough for the calls that never reach a resampler.</summary>
    private static Palette BlackPalette()
    {
        byte[] table = new byte[256 * 3];

        return Palette.FromGamePalette(table);
    }

    /// <summary>A font that paints the same block for every character.</summary>
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

        public int GetAdvance(char character) => 6;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
