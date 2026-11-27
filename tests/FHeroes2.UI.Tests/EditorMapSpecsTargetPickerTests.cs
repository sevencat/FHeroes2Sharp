using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using Xunit;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="EditorMapSpecsTownPicker"/> and <see cref="EditorMapSpecsHeroPicker"/>, the reference's
/// <c>SelectMapCastle</c> / <c>SelectMapHero</c> (<c>editor_map_specs_window.cpp:180-395</c>).
/// </summary>
/// <remarks>
/// 窗口的图标来自真实的归档（<c>MapSpecsConditionIcons</c> 读 <c>Assets.Current</c>），所以除了纯尺寸的几条，
/// 其它用例在没有游戏数据时直接跳过。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class EditorMapSpecsTargetPickerTests : IDisposable
{
    private const byte Canvas = 3;

    private static readonly Point FirstTownTile = new(5, 6);

    private static readonly Point SecondTownTile = new(10, 11);

    private AssetBundle? _assets;

    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose()
    {
        GameAssets.Current = null;
        _assets?.Dispose();
        _assets = null;
    }

    [Fact]
    public void DialogSizeKeepsRoomForFiveToTwelveRows()
    {
        // 参考的 100 + 35 * n，夹在 5 行与 12 行之间，再被屏幕高减 100 压住（:1232-1250）。
        Assert.Equal(new Size(450, 275), EditorMapSpecsTargetPicker.GetDialogSize(3, new Size(640, 480)));
        Assert.Equal(new Size(450, 520), EditorMapSpecsTargetPicker.GetDialogSize(20, new Size(640, 900)));
        Assert.Equal(new Size(450, 380),
            EditorMapSpecsTargetPicker.GetDialogSize(20, new Size(640, 480)));
    }

    [Fact]
    public void DialogSizeOfAnEmptyListIsTheFiveRowOne()
    {
        Assert.Equal(
            EditorMapSpecsTargetPicker.GetDialogSize(EditorMapSpecsTargetPicker.MinSizedRows),
            EditorMapSpecsTargetPicker.GetDialogSize(0));
    }

    [Fact]
    public void CreateReturnsNullWithoutATargetToPick()
    {
        MapSpecsState state = new(Mp2Map.CreateBlank(36, terrainImageIndex: 0));

        Assert.Null(EditorMapSpecsTargetPicker.Create(
            EditorMapSpecsTargetKind.None, state, CreateArtwork(), CreateFont(), CreateFont()));
    }

    [Fact]
    public void TheTownPickerListsEveryTownAndStartsOnTheCurrentTarget()
    {
        if (!TryCreate(out MapSpecsState state))
        {
            return;
        }

        try
        {
            state.VictoryCondition = MapVictoryCondition.CaptureTown;
            state.UpdateVictoryCondition();

            EditorMapSpecsTargetPicker? picker = EditorMapSpecsTargetPicker.Create(
                EditorMapSpecsTargetKind.VictoryTown, state, CreateArtwork(), CreateFont(), CreateFont());

            Assert.IsType<EditorMapSpecsTownPicker>(picker);
            Assert.Equal(2, picker.Candidates.Count);
            Assert.Equal((FirstTownTile.Y * 36) + FirstTownTile.X, picker.Candidates[0].Id);
            Assert.Equal((SecondTownTile.Y * 36) + SecondTownTile.X, picker.Candidates[1].Id);

            // 参考开窗前 SetCurrent( 当前目标 )，端口的条件刷新把第一个城镇当默认目标。
            Assert.Equal(state.VictoryTarget, picker.SelectedPosition);
            Assert.Equal(EditorMapSpecsTargetPicker.VictoryTownTitle, picker.Title);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void SelectTileMovesTheSelectionAndIgnoresATileThatIsNotListed()
    {
        if (!TryCreate(out MapSpecsState state))
        {
            return;
        }

        try
        {
            EditorMapSpecsTargetPicker picker = EditorMapSpecsTargetPicker.Create(
                EditorMapSpecsTargetKind.LossTown, state, CreateArtwork(), CreateFont(), CreateFont())!;

            picker.SelectTile(SecondTownTile);
            Assert.Equal(SecondTownTile, picker.SelectedPosition);
            Assert.Equal(EditorMapSpecsTargetPicker.LossTownTitle, picker.Title);

            picker.SelectTile(new Point(30, 30));
            Assert.Equal(SecondTownTile, picker.SelectedPosition);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void TheTownPickerHandsTheNewTargetBackToTheInput()
    {
        if (!TryCreate(out MapSpecsState state))
        {
            return;
        }

        try
        {
            EditorMapSpecsWindow window = EditorMapSpecsWindow.Create(
                MapSpecsPageArtwork.TryCreate(isEvilInterface: false)!, state, CreateFont(), BlackPalette());
            EditorMapSpecsInput input = new(window);

            state.VictoryCondition = MapVictoryCondition.CaptureTown;
            state.UpdateVictoryCondition();
            window.Paint(new PixelBuffer(640, 480));

            input.Process(Click(Center(window.VictoryPanel!.Layout.SelectRowRoi)));
            Assert.Equal(EditorMapSpecsRequestKind.PickConditionTarget, input.Request.Kind);

            EditorMapSpecsTargetPicker picker = input.CreateTargetPicker(CreateArtwork(), CreateFont(), CreateFont())!;

            picker.SelectTile(SecondTownTile);
            Assert.True(input.ResolveTarget(picker.SelectedPosition));
            Assert.Equal(SecondTownTile, state.VictoryTarget);
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void TheTownRowPaintsItsIconBetweenTheLeftEdgeAndTheTitle()
    {
        if (!TryCreate(out MapSpecsState state))
        {
            return;
        }

        try
        {
            EditorMapSpecsTargetPicker picker = EditorMapSpecsTargetPicker.Create(
                EditorMapSpecsTargetKind.VictoryTown, state, CreateArtwork(), CreateFont(), CreateFont())!;
            PixelBuffer image = new(400, 200);
            image.Fill(Canvas);

            picker.RedrawItem(picker.Candidates[0], 10, picker.ItemsArea.Y, current: false, image);

            // 图标以 x + 45 居中、以行中点 17 居中；标签取得到图，所以行内左侧必然被画上东西。
            int painted = 0;

            for (int y = picker.ItemsArea.Y; y < picker.ItemsArea.Y + EditorMapSpecsTargetPicker.RowHeight; ++y)
            {
                for (int x = 10; x < 10 + EditorMapSpecsTownPicker.TextOffsetX; ++x)
                {
                    if (image[x, y] != Canvas)
                    {
                        ++painted;
                    }
                }
            }

            Assert.True(painted > 0, "The icon of the town row should be visible.");
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void TheHeroPickerListsEveryCandidateOfTheGivenList()
    {
        if (!TryCreate(out _))
        {
            return;
        }

        try
        {
            MapSpecsConditionHeroRowData[] heroes =
            [
                new(PlayerColor.Red, MapRace.Knight, string.Empty, 100),
                new(PlayerColor.Blue, MapRace.Wizard, string.Empty, 200),
                new(PlayerColor.Green, MapRace.Necromancer, string.Empty, 300),
            ];

            EditorMapSpecsHeroPicker picker = new(
                heroes, 36, new Point(100 % 36, 100 / 36), EditorMapSpecsTargetPicker.VictoryHeroTitle,
                CreateArtwork(), CreateFont(), CreateFont());

            Assert.Equal(3, picker.Candidates.Count);
            Assert.Equal(100, picker.Candidates[0].Id);
            Assert.Equal(300, picker.Candidates[2].Id);
            Assert.Equal(new Point(100 % 36, 100 / 36), picker.SelectedPosition);

            PixelBuffer image = new(400, 200);
            image.Fill(Canvas);
            picker.RedrawItem(picker.Candidates[1], 10, picker.ItemsArea.Y, current: true, image);

            int painted = 0;

            for (int y = picker.ItemsArea.Y; y < picker.ItemsArea.Y + EditorMapSpecsTargetPicker.RowHeight; ++y)
            {
                for (int x = 10; x < 10 + EditorMapSpecsHeroPicker.TextOffsetX; ++x)
                {
                    if (image[x, y] != Canvas)
                    {
                        ++painted;
                    }
                }
            }

            Assert.True(painted > 0, "The icon of the hero row should be visible.");
        }
        finally
        {
            Dispose();
        }
    }

    [Fact]
    public void RightClickingATownRowShowsItsTitle()
    {
        if (!TryCreate(out MapSpecsState state))
        {
            return;
        }

        try
        {
            EditorMapSpecsTargetPicker picker = EditorMapSpecsTargetPicker.Create(
                EditorMapSpecsTargetKind.VictoryTown, state, CreateArtwork(), CreateFont(), CreateFont())!;

            picker.ActionListPressRight(picker.Candidates[0]);

            DialogPopup popup = Assert.IsType<DialogPopup>(picker.Popup);
            Assert.Equal(string.Empty, popup.Header);
            Assert.False(string.IsNullOrEmpty(popup.Body));
        }
        finally
        {
            Dispose();
        }
    }

    private bool TryCreate(out MapSpecsState state)
    {
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
        GameAssets.Current = _assets;

        if (_assets is null)
        {
            state = null!;

            return false;
        }

        Mp2Map map = Mp2Map.CreateBlank(36, terrainImageIndex: 0);
        map.AddCastle(new Mp2CastlePosition(FirstTownTile.X, FirstTownTile.Y, 0x00, MapRace.Knight));
        map.AddCastle(new Mp2CastlePosition(SecondTownTile.X, SecondTownTile.Y, 0x00, MapRace.Wizard));

        state = new MapSpecsState(map);

        return true;
    }

    private static IGlyphSource CreateFont() => new FakeGlyphSource();

    private static ItemSelectionWindowArtwork CreateArtwork() => new()
    {
        ScrollBarBackground = new Sprite(CreateBuffer(560, 320, 9)),
        ScrollUpReleased = new Sprite(CreateBuffer(14, 15, 11)),
        ScrollUpPressed = new Sprite(CreateBuffer(14, 15, 12)),
        ScrollDownReleased = new Sprite(CreateBuffer(14, 15, 13)),
        ScrollDownPressed = new Sprite(CreateBuffer(14, 15, 14)),
        ScrollSlider = new Sprite(CreateBuffer(10, 25, 15)),
        Okay = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 16)), new Sprite(CreateBuffer(40, 20, 17))),
        Cancel = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 18)), new Sprite(CreateBuffer(40, 20, 19))),
    };

    private static PixelBuffer CreateBuffer(int width, int height, byte value)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill(value);

        return buffer;
    }

    private static Palette BlackPalette()
    {
        byte[] table = new byte[256 * 3];

        return Palette.FromGamePalette(table);
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

    /// <summary>A font that paints the same block for every character.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = new(CreateBuffer(6, 12, 1), 0, 0);

        public int LineHeight => 12;

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
