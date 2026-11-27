using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <see cref="MonsterSelectionWindow"/> and <see cref="MonsterSelectDialog"/> against
/// <c>dialog_selectitems.cpp:72-82</c>, <c>131-167</c> and <c>1204-1229</c>.
/// </summary>
public sealed class MonsterSelectionWindowTests
{
    private const byte Canvas = 3;
    private const byte PlateColor = 21;
    private const byte PortraitColor = 31;

    [Fact]
    public void DialogSizeLeavesTheDeductionOnTopOfTheScreenHeight()
    {
        Assert.Equal(new Size(320, 330), MonsterSelectDialog.DialogSize());
        Assert.Equal(new Size(320, 450), MonsterSelectDialog.DialogSize(new Size(800, 600)));
    }

    [Fact]
    public void BuildMonsterListStartsAfterTheUnknownCreature()
    {
        List<int> monsters = MonsterSelectDialog.BuildMonsterList();

        Assert.Equal((int)MonsterType.Unknown + 1, monsters[0]);
        Assert.Equal((int)MonsterType.WaterElement, monsters[^1]);
    }

    [Fact]
    public void BuildMonsterListLeavesTheFiveRandomMarkersOut()
    {
        List<int> monsters = MonsterSelectDialog.BuildMonsterList();

        // 未知之外每只怪兽都在，只有 RandomMonster..RandomMonsterLevel4 这五个标记被丢掉。
        Assert.Equal((int)MonsterType.MonsterCount - 1 - 5, monsters.Count);

        foreach (int id in monsters)
        {
            Assert.False(MonsterStatistics.IsRandomMonster((MonsterType)id), $"{id} is a random marker");
        }

        Assert.Equal(monsters.Count, monsters.Distinct().Count());
    }

    [Fact]
    public void WindowLaysTheRowsIntoTheListArea()
    {
        MonsterSelectionWindow window = CreateWindow();

        // The list area is the reference's active area minus the insets, and 250 / 43 truncates to five rows.
        Assert.Equal(new Rect(170, 105, 280, 260), window.ListArea);
        Assert.Equal(new Rect(175, 110, 270, 250), window.ItemsArea);
        Assert.Equal(250 / MonsterSelectionWindow.RowHeight, window.VisibleItemCount);
    }

    [Fact]
    public void TryCreateStartsOnTheCreatureTheCallerAskedFor()
    {
        IReadOnlyList<int> ids = MonsterSelectDialog.BuildMonsterList();
        MonsterSelectionWindow? window = MonsterSelectDialog.TryCreate(MonsterType.Archer, new FakeSprites(),
            CreatePlate(), CreateArtwork(), CreateFont(), CreateFont());

        Assert.NotNull(window);
        Assert.Equal(ids.Count, window.Monsters.Count);
        Assert.Equal(MonsterType.Archer, window.SelectedMonster);

        window.SelectMonster(MonsterType.GreenDragon);

        Assert.Equal(MonsterType.GreenDragon, window.SelectedMonster);
    }

    [Fact]
    public void RedrawItemPutsThePortraitOnThePlate()
    {
        MonsterSelectionWindow window = CreateWindow();
        PixelBuffer image = CreateBuffer(640, 480, Canvas);

        window.RedrawItem(new ItemSelectionEntry((int)MonsterType.Peasant), window.ItemsArea.X, window.ItemsArea.Y,
            current: false, image);

        int x = window.ItemsArea.X;
        int y = window.ItemsArea.Y;

        // The plate is 43 wide and centred on the reference's middleImageOffsetX of 22, so it starts one pixel in.
        Assert.Equal(PlateColor, image[x + 1 + 2, y + 2]);

        // The 32x32 portrait is centred on the plate and covers its middle.
        Assert.Equal(PortraitColor,
            image[x + MonsterSelectionWindow.IconOffsetX, y + MonsterSelectionWindow.RowMiddleOffsetY]);
    }

    [Fact]
    public void RightClickAsksForTheUnitInformationWindowOnce()
    {
        MonsterSelectionWindow window = CreateWindow();
        Point point = new(window.ItemsArea.X + 60, window.ItemsArea.Y + 10);

        window.ProcessEvents(RightClick(point));

        // 第一行就是列表里的第一只怪兽：列表内容照 BuildMonsterList 的顺序，Peasant 在最前。
        Assert.Equal(MonsterType.Peasant, window.TakeInfoRequest());
        Assert.Null(window.TakeInfoRequest());
        Assert.Equal(DialogButton.None, window.Result);
    }

    [Fact]
    public void CutPlateTakesTheSquareTheReferenceNames()
    {
        PixelBuffer source = CreateBuffer(640, 480, Canvas);

        source[MonsterSelectionWindow.PlateSourceArea.X, MonsterSelectionWindow.PlateSourceArea.Y] = PlateColor;
        source[MonsterSelectionWindow.PlateSourceArea.Right - 1, MonsterSelectionWindow.PlateSourceArea.Bottom - 1] =
            PortraitColor;

        Sprite plate = MonsterSelectionWindow.CutPlate(new Sprite(source));

        Assert.Equal(MonsterSelectionWindow.RowHeight, plate.Width);
        Assert.Equal(MonsterSelectionWindow.RowHeight, plate.Height);
        Assert.Equal(PlateColor, plate.Image[0, 0]);
        Assert.Equal(PortraitColor, plate.Image[plate.Width - 1, plate.Height - 1]);
    }

    private static MonsterSelectionWindow CreateWindow() =>
        new([(int)MonsterType.Peasant, (int)MonsterType.Archer, (int)MonsterType.GreenDragon],
            new FakeSprites(), CreatePlate(), MonsterSelectDialog.DialogSize(), CreateArtwork(), CreateFont(),
            CreateFont());

    private static Sprite CreatePlate()
    {
        PixelBuffer buffer = CreateBuffer(MonsterSelectionWindow.RowHeight, MonsterSelectionWindow.RowHeight,
            PlateColor);

        return new Sprite(buffer);
    }

    private static ItemSelectionWindowArtwork CreateArtwork()
    {
        PixelBuffer groove = CreateBuffer(560, 320, 9);

        for (int y = 176; y < 306; ++y)
        {
            for (int x = 536; x < 552; ++x)
            {
                groove[x, y] = 40;
            }
        }

        return new ItemSelectionWindowArtwork
        {
            ScrollBarBackground = new Sprite(groove),
            ScrollUpReleased = new Sprite(CreateBuffer(14, 15, 11)),
            ScrollUpPressed = new Sprite(CreateBuffer(14, 15, 12)),
            ScrollDownReleased = new Sprite(CreateBuffer(14, 15, 13)),
            ScrollDownPressed = new Sprite(CreateBuffer(14, 15, 14)),
            ScrollSlider = new Sprite(CreateBuffer(10, 25, 15)),
            Okay = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 16)),
                new Sprite(CreateBuffer(40, 20, 17))),
            Cancel = new DialogButtonArtwork(new Sprite(CreateBuffer(40, 20, 18)),
                new Sprite(CreateBuffer(40, 20, 19))),
        };
    }

    private static PixelBuffer CreateBuffer(int width, int height, int value)
    {
        PixelBuffer buffer = new(width, height);
        buffer.Fill((byte)value);

        return buffer;
    }

    private static IGlyphSource CreateFont() => new FakeGlyphSource();

    private static LocalEvent RightClick(Point point)
    {
        LocalEvent events = new();

        events.OnMouseButton(true, MouseButtonType.Right, point);

        return events;
    }

    /// <summary>32x32 portraits of one flat colour, which is enough to tell them from the plate.</summary>
    private sealed class FakeSprites : IMiniMonsterSprites
    {
        public Sprite? GetMiniMonster(MonsterType monster) =>
            monster > MonsterType.Unknown
                ? new Sprite(CreateBuffer(32, 32, PortraitColor))
                : null;
    }

    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph = new(CreateBuffer(6, 12, 1));

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
