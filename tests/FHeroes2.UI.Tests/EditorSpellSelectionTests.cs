using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the editor's spell selection window: the layout of the scrolls, the toggles, the level buttons and the
/// messages (<c>editor_spell_selection.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorSpellSelectionTests
{
    private const byte AttributeColor = 5;

    private const int NormalLineHeight = 14;

    [Fact]
    public void Grid_WithNineSpells_UsesThreeColumns()
    {
        EditorSpellSelectionGrid grid = new(
            [.. Enumerable.Range(0, 9).Select(i => (SpellType)i)],
            new bool[9],
            new Point(0, 16),
            new Size(110, 110));

        // 3x3 正好装下九个，参考 :66-71 挑的是最小的能装下的正方形。
        Assert.Equal(3, grid.Columns);
        Assert.Equal(110, grid.Areas[1].X - grid.Areas[0].X);
        Assert.Equal(EditorSpellSelectionGrid.RowStepY, grid.Areas[3].Y - grid.Areas[0].Y);
    }

    [Fact]
    public void Grid_WithElevenSpells_CentresTheLastRow()
    {
        EditorSpellSelectionGrid grid = new(
            [.. Enumerable.Range(0, 11).Select(i => (SpellType)i)],
            new bool[11],
            new Point(0, 16),
            new Size(110, 110));

        Assert.Equal(4, grid.Columns);

        // 最后一行三张，比上面一行少一张，所以往右挪半格（55 像素），参考 :83-97。
        Assert.Equal(55, grid.Areas[8].X - grid.Areas[4].X);
    }

    [Fact]
    public void Grid_RefusesToHoldTwentyFiveSpells() =>
        Assert.Throws<ArgumentException>(() => new EditorSpellSelectionGrid(
            [.. Enumerable.Range(0, 25).Select(i => (SpellType)i)],
            new bool[25],
            Point.Zero,
            new Size(110, 110)));

    [Fact]
    public void Window_StartsWithEverySpellEnabled() => Assert.True(TryWithWindow((window, _) =>
    {
        Assert.Equal(Spell.SuitableForSpellBook(1).Count, window.Grid.Count);

        for (int i = 0; i < window.Grid.Count; ++i)
        {
            Assert.True(window.Grid.IsSelected(i));
        }

        // 全选在游戏里表示「按默认」，参考 :426-428 会把它折成空表。
        Assert.Empty(window.GetSelectedSpells());
    }));

    [Fact]
    public void Window_KeepsOnlyTheSpellsTheMetadataHolds() => Assert.True(TryWithWindow((window, _) =>
    {
        IReadOnlyList<SpellType> levelSpells = Spell.SuitableForSpellBook(1);

        Assert.True(window.Grid.IsSelected(0));
        Assert.True(window.Grid.IsSelected(1));

        for (int i = 2; i < window.Grid.Count; ++i)
        {
            Assert.False(window.Grid.IsSelected(i));
        }

        Assert.Equal([levelSpells[0], levelSpells[1]], window.GetSelectedSpells());
    }, selectedCount: 2));

    [Fact]
    public void Window_WithPickDisabledSpells_TreatsTheListAsTheDisabledOnes() => Assert.True(TryWithWindow(
        (window, _) =>
        {
            IReadOnlyList<SpellType> levelSpells = Spell.SuitableForSpellBook(1);

            // 参考 :191：清单里的是「禁用法术」，所以清单里的反而没被选中。
            Assert.False(window.Grid.IsSelected(0));
            Assert.False(window.Grid.IsSelected(1));
            Assert.True(window.Grid.IsSelected(2));

            Assert.Equal([levelSpells[0], levelSpells[1]], window.GetSelectedSpells());
        }, selectedCount: 2, pickDisabledSpells: true));

    [Fact]
    public void ClickOnAScroll_TurnsThatSpellOff() => Assert.True(TryWithWindow((window, input) =>
    {
        Point cursor = Center(window.Grid.Areas[0]);

        input.Process(Click(cursor));

        Assert.False(window.Grid.IsSelected(0));
        Assert.Contains(window.Grid.Spells[0], window.GetSelectedSpells());
    }, pickDisabledSpells: true));

    [Fact]
    public void DisableAll_TurnsOkayOff_WhenTheMinimumIsNotMet() => Assert.True(TryWithWindow((window, input) =>
    {
        Assert.True(window.IsOkayEnabled);

        input.Process(Click(Center(window.ToggleAllOff.Area)));

        Assert.Equal(0, window.Grid.SelectedCount);
        Assert.False(window.IsOkayEnabled);
        Assert.False(window.ToggleAllOff.IsEnabled);
        Assert.True(window.ToggleAllOn.IsEnabled);
    }));

    [Fact]
    public void EnableAll_TurnsEverySpellBackOn() => Assert.True(TryWithWindow((window, input) =>
    {
        input.Process(Click(Center(window.ToggleAllOff.Area)));
        input.Process(Click(Center(window.ToggleAllOn.Area)));

        Assert.Equal(window.Grid.Count, window.Grid.SelectedCount);
        Assert.True(window.IsOkayEnabled);
        Assert.True(window.ToggleAllOff.IsEnabled);
        Assert.False(window.ToggleAllOn.IsEnabled);
    }));

    [Fact]
    public void ClickOnOkay_Accepts() => Assert.True(TryWithWindow((window, input) =>
        Assert.Equal(EditorSpellSelectionOutcome.Accepted, input.Process(Click(Center(window.Okay.Area))))));

    [Fact]
    public void ClickOnOkay_WithTooFewSpells_DoesNothing() => Assert.True(TryWithWindow((window, input) =>
    {
        input.Process(Click(Center(window.ToggleAllOff.Area)));

        Assert.Equal(EditorSpellSelectionOutcome.None, input.Process(Click(Center(window.Okay.Area))));
    }));

    [Fact]
    public void ClickOnCancel_Cancels() => Assert.True(TryWithWindow((window, input) =>
        Assert.Equal(EditorSpellSelectionOutcome.Cancelled, input.Process(Click(Center(window.Cancel.Area))))));

    [Fact]
    public void ClickOnAnotherLevel_AsksTheHostToReopen()
    {
        bool ran = TryWithWindow((window, input) =>
        {
            Assert.Equal(1, window.Level);

            EditorSpellSelectionOutcome outcome = input.Process(Click(Center(window.LevelButtons[2].Area)));

            Assert.Equal(EditorSpellSelectionOutcome.LevelChanged, outcome);
            Assert.Equal(3, window.Level);
        }, multiLevelSelection: true);

        Assert.True(ran);
    }

    [Fact]
    public void ClickOnTheCurrentLevel_DoesNothing()
    {
        bool ran = TryWithWindow((window, input) =>
            Assert.Equal(EditorSpellSelectionOutcome.None, input.Process(Click(Center(window.LevelButtons[0].Area)))),
            multiLevelSelection: true);

        Assert.True(ran);
    }

    [Fact]
    public void RightClickOnAScroll_AsksForItsDescription() => Assert.True(TryWithWindow((window, input) =>
    {
        input.Process(RightPress(Center(window.Grid.Areas[1])));

        Assert.Equal(EditorSpellSelectionRequestKind.ShowSpell, input.Request.Kind);
        Assert.Equal(window.Grid.Spells[1], input.Request.Spell);
    }));

    [Fact]
    public void RightClickOnALevelButton_ExplainsIt()
    {
        bool ran = TryWithWindow((window, input) =>
        {
            input.Process(RightPress(Center(window.LevelButtons[1].Area)));

            Assert.Equal(EditorSpellSelectionRequestKind.ShowMessage, input.Request.Kind);
            Assert.Equal(EditorSpellSelectionInput.SpellsLevelTitle, input.Request.Title);
            Assert.Contains("2", input.Request.Body, StringComparison.Ordinal);
        }, multiLevelSelection: true);

        Assert.True(ran);
    }

    [Fact]
    public void RightClickOnDisableAll_ExplainsIt() => Assert.True(TryWithWindow((window, input) =>
    {
        input.Process(RightPress(Center(window.ToggleAllOff.Area)));

        Assert.Equal(EditorSpellSelectionRequestKind.ShowMessage, input.Request.Kind);
        Assert.Equal(EditorSpellSelectionInput.DisableAllTitle, input.Request.Title);
    }));

    [Fact]
    public void Paint_ShowsDifferentLevelsDifferently()
    {
        bool ran = TryWithWindow((window, input) =>
        {
            PixelBuffer first = CreateCanvas();
            window.Paint(first, Hover(Point.Zero));

            window.ChangeLevel(5);
            PixelBuffer second = CreateCanvas();
            window.Paint(second, Hover(Point.Zero));

            // 换等级换的是下半部分的五张卷轴和按下去的那个按钮，画出来的东西必然不同。
            Assert.False(first.Pixels.SequenceEqual(second.Pixels));
        }, multiLevelSelection: true);

        Assert.True(ran);
    }

    /// <summary>Creates the window against the installed game data and paints nothing.</summary>
    /// <param name="body">The test body.</param>
    /// <param name="selectedCount">How many spells of the level are already in the metadata.</param>
    /// <param name="multiLevelSelection">Whether the level buttons are shown.</param>
    /// <param name="pickDisabledSpells">Whether the window should hand back the disabled spells.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithWindow(Action<EditorSpellSelectionWindow, EditorSpellSelectionInput> body,
        int selectedCount = 0, bool multiLevelSelection = false, bool pickDisabledSpells = false)
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
            EditorSpellSelectionArtwork artwork = EditorSpellSelectionArtwork.TryCreate(isEvilInterface: false)
                ?? throw new InvalidOperationException("The artwork of the spell selection window is missing.");

            IReadOnlyList<SpellType> levelSpells = Spell.SuitableForSpellBook(1);

            EditorSpellSelectionWindow window = EditorSpellSelectionWindow.Create(
                artwork,
                "Spells in Mage Guild",
                1,
                [.. levelSpells.Take(selectedCount)],
                multiLevelSelection,
                minimumEnabledSpells: 1,
                pickDisabledSpells,
                new FakeGlyphSource(AttributeColor),
                new FakeGlyphSource(NormalLineHeight));

            body(window, new EditorSpellSelectionInput(window));
        }
        finally
        {
            // 还原成进来时的那个：同一个集合里的其它用例可能还想接着用，置空会让它们莫名其妙地画不出东西。
            GameAssets.Current = previous;
            assets.Dispose();
        }

        return true;
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

    private static PixelBuffer CreateCanvas()
    {
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(0);

        return canvas;
    }

    /// <summary>A glyph source with fixed metrics whose glyphs are solid blocks.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public FakeGlyphSource(byte color)
            : this(color, NormalLineHeight)
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
