using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the editor's secondary skill selection window: the layout of the cells, the toggles and the messages
/// (<c>editor_secondary_skill_selection.cpp</c>).
/// </summary>
/// <remarks>
/// 素材从存档里读，没有游戏数据的机器上用例直接返回。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorSecondarySkillSelectionTests
{
    private const byte AttributeColor = 5;

    private const int NormalLineHeight = 14;

    [Fact]
    public void Grid_WithTwelveSkills_UsesFourColumns()
    {
        EditorSecondarySkillSelectionGrid grid = new(
            CreateSkills(12),
            new bool[12],
            new Point(0, 16),
            new Size(44, 44));

        // 十二个技能装进 4x3，参考 :67-72 挑的是最小的能装下的正方形。
        Assert.Equal(4, grid.Columns);
        Assert.Equal(EditorSecondarySkillSelectionGrid.ItemWidth, grid.Areas[1].X - grid.Areas[0].X);
        Assert.Equal(EditorSecondarySkillSelectionGrid.RowStepY, grid.Areas[4].Y - grid.Areas[0].Y);
    }

    [Fact]
    public void Grid_WithThreeSkills_CentresTheLastRow()
    {
        EditorSecondarySkillSelectionGrid grid = new(
            CreateSkills(3),
            new bool[3],
            new Point(0, 16),
            new Size(44, 44));

        Assert.Equal(2, grid.Columns);

        // 最后一行只有一个，比上面一行少一个，所以往右挪半格（45 像素），参考 :83-97。
        Assert.Equal(45, grid.Areas[2].X - grid.Areas[0].X);
    }

    [Fact]
    public void Grid_RefusesToHoldTwentyFiveSkills() =>
        Assert.Throws<ArgumentException>(() => new EditorSecondarySkillSelectionGrid(
            CreateSkills(25),
            new bool[25],
            Point.Zero,
            new Size(44, 44)));

    [Fact]
    public void Grid_RefusesAnEmptyBlock() =>
        Assert.Throws<ArgumentException>(() => new EditorSecondarySkillSelectionGrid(
            [],
            [],
            Point.Zero,
            new Size(44, 44)));

    [Fact]
    public void Window_StartsWithEverySkillEnabled() => Assert.True(TryWithWindow((window, _) =>
    {
        Assert.Equal(SecondarySkills.WitchsHutSkills.Count, window.Grid.Count);
        Assert.Equal(SkillLevel.Expert, window.Level);

        for (int i = 0; i < window.Grid.Count; ++i)
        {
            Assert.True(window.Grid.IsSelected(i));
        }

        // 全选在游戏里表示「按默认」，参考 :387-390 会把它折成空表。
        Assert.Empty(window.GetSelectedSkills());
    }));

    [Fact]
    public void Window_KeepsOnlyTheSkillsTheMetadataHolds() => Assert.True(TryWithWindow((window, _) =>
    {
        IReadOnlyList<SecondarySkill> offered = SecondarySkills.WitchsHutSkills;

        Assert.True(window.Grid.IsSelected(0));
        Assert.True(window.Grid.IsSelected(1));

        for (int i = 2; i < window.Grid.Count; ++i)
        {
            Assert.False(window.Grid.IsSelected(i));
        }

        Assert.Equal([offered[0], offered[1]], window.GetSelectedSkills());
    }, selectedCount: 2));

    [Fact]
    public void ClickOnACell_TurnsThatSkillOff() => Assert.True(TryWithWindow((window, input) =>
    {
        Assert.True(window.Grid.IsSelected(0));

        input.Process(Click(Center(window.Grid.Areas[0])));

        Assert.False(window.Grid.IsSelected(0));
        Assert.DoesNotContain(window.Grid.Skills[0].Skill, window.GetSelectedSkills());
        Assert.Equal(SkillLevel.Expert, window.Grid.Skills[0].Level);
    }));

    [Fact]
    public void DisableAll_TurnsOkayOff() => Assert.True(TryWithWindow((window, input) =>
    {
        Assert.True(window.IsOkayEnabled);

        input.Process(Click(Center(window.ToggleAllOff.Area)));

        Assert.Equal(0, window.Grid.SelectedCount);
        Assert.False(window.IsOkayEnabled);
        Assert.False(window.ToggleAllOff.IsEnabled);
        Assert.True(window.ToggleAllOn.IsEnabled);
    }));

    [Fact]
    public void EnableAll_TurnsEverySkillBackOn() => Assert.True(TryWithWindow((window, input) =>
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
        Assert.Equal(EditorSecondarySkillSelectionOutcome.Accepted,
            input.Process(Click(Center(window.Okay.Area))))));

    [Fact]
    public void ClickOnOkay_WithEverySkillOff_DoesNothing() => Assert.True(TryWithWindow((window, input) =>
    {
        input.Process(Click(Center(window.ToggleAllOff.Area)));

        Assert.Equal(EditorSecondarySkillSelectionOutcome.None,
            input.Process(Click(Center(window.Okay.Area))));
    }));

    [Fact]
    public void ClickOnCancel_Cancels() => Assert.True(TryWithWindow((window, input) =>
        Assert.Equal(EditorSecondarySkillSelectionOutcome.Cancelled,
            input.Process(Click(Center(window.Cancel.Area))))));

    [Fact]
    public void RightClickOnACell_AsksForItsDescription() => Assert.True(TryWithWindow((window, input) =>
    {
        input.Process(RightPress(Center(window.Grid.Areas[1])));

        Assert.Equal(EditorSecondarySkillSelectionRequestKind.ShowSkill, input.Request.Kind);
        Assert.Equal(window.Grid.Skills[1], input.Request.Skill);
    }));

    [Fact]
    public void RightClickOnDisableAll_ExplainsIt() => Assert.True(TryWithWindow((window, input) =>
    {
        input.Process(RightPress(Center(window.ToggleAllOff.Area)));

        Assert.Equal(EditorSecondarySkillSelectionRequestKind.ShowMessage, input.Request.Kind);
        Assert.Equal(EditorSecondarySkillSelectionInput.DisableAllTitle, input.Request.Title);
    }));

    [Fact]
    public void Paint_DrawsAnInactiveCellDifferently() => Assert.True(TryWithWindow((window, _) =>
    {
        PixelBuffer enabled = CreateCanvas();
        window.Paint(enabled, Hover(Point.Zero));

        window.ToggleSkill(0);
        PixelBuffer disabled = CreateCanvas();
        window.Paint(disabled, Hover(Point.Zero));

        // 关掉一格换的是那一格的帧与图标（都过灰度），画出来的东西必然不同。
        Assert.False(enabled.Pixels.SequenceEqual(disabled.Pixels));
    }));

    private static IReadOnlyList<SecondarySkillLevel> CreateSkills(int count) =>
    [
        .. SecondarySkills.All.Take(count).Select(skill => new SecondarySkillLevel(skill, SkillLevel.Expert))
    ];

    /// <summary>Creates the window against the installed game data and drives it.</summary>
    /// <param name="body">The test body.</param>
    /// <param name="selectedCount">How many skills of the list are already in the metadata.</param>
    /// <returns>Always <see langword="true"/>; the test returns when the machine has no game data.</returns>
    private static bool TryWithWindow(Action<EditorSecondarySkillSelectionWindow, EditorSecondarySkillSelectionInput> body,
        int selectedCount = 0)
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
            EditorSecondarySkillSelectionArtwork artwork = EditorSecondarySkillSelectionArtwork.TryCreate(
                isEvilInterface: false) ?? throw new InvalidOperationException(
                "The artwork of the secondary skill selection window is missing.");

            EditorSecondarySkillSelectionWindow window = EditorSecondarySkillSelectionWindow.Create(
                artwork,
                "Secondary Skills",
                SkillLevel.Expert,
                [.. SecondarySkills.WitchsHutSkills.Take(selectedCount)],
                new FakeGlyphSource(AttributeColor),
                new FakeGlyphSource(NormalLineHeight));

            body(window, new EditorSecondarySkillSelectionInput(window));
        }
        finally
        {
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
