using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The scenario info window: which rows it shows, what they say and where they sit,
/// <c>Dialog::GameInfo()</c> (<c>dialog_gameinfo.cpp:52-201</c>).
/// </summary>
public sealed class GameInfoViewTests
{
    private const int AboutButtonWidth = 70;

    [Fact]
    public void TheRowsNameTheMapItsTwoDifficultiesAndItsSize()
    {
        IReadOnlyList<GameInfoRow> rows = BuildRows(hasCreatorNotes: false);

        Assert.Equal("Island", rows[0].Text);
        Assert.Equal("Map\nDifficulty", rows[1].Text);
        Assert.Equal("Game\nDifficulty", rows[2].Text);

        // 玩家的难度与地图里写死的那个是两回事：地图是 Hard，玩家选的是 Normal。
        Assert.Equal("Hard", rows[5].Text);
        Assert.Equal("Normal", rows[6].Text);
        Assert.NotEqual(rows[5].Text, rows[6].Text);

        // 72 是四档边长之一，落在「中等」。
        Assert.Equal("Medium", rows[8].Text);
    }

    [Fact]
    public void TheRatingRowIsTheRatingHelperPlusAPercentSign()
    {
        IReadOnlyList<GameInfoRow> rows = BuildRows(hasCreatorNotes: false);

        // 地图 Hard（+40）与玩家 Normal（+30）叠在基线 50 上。
        Assert.Equal("120 %", rows[7].Text);
    }

    [Fact]
    public void TheConditionsAreDrawnAsGiven()
    {
        IReadOnlyList<GameInfoRow> rows = BuildRows(hasCreatorNotes: false);

        Assert.Equal("Victory\nConditions", rows[12].Text);
        Assert.Equal("Defeat all enemies and flag all mines.", rows[13].Text);
        Assert.Equal("Loss\nConditions", rows[14].Text);
        Assert.Equal("Lose the hero.", rows[15].Text);
    }

    [Fact]
    public void TheColumnsFollowTheOffsetsOfTheReference()
    {
        IReadOnlyList<GameInfoRow> rows = BuildRows(hasCreatorNotes: false);

        Assert.Equal(34, rows[1].X);
        Assert.Equal(125, rows[2].X);
        Assert.Equal(216, rows[3].X);
        Assert.Equal(307, rows[4].X);
        Assert.Equal(80, rows[1].Width);

        // 第一行是地图名，起点与宽度都跟参考的 NameX / NameMaxWidth 一致。
        Assert.Equal(37, rows[0].X);
        Assert.Equal(32, rows[0].Y);
        Assert.Equal(349, rows[0].Width);
    }

    [Fact]
    public void TheTwoOneLineLabelsAreBottomAligned()
    {
        IReadOnlyList<GameInfoRow> rows = BuildRows(hasCreatorNotes: false);

        // dialog_gameinfo.cpp:147-150 把它们按底边 78 画，而不是按顶边。
        Assert.Equal(GameInfoRowAnchor.Bottom, rows[3].Anchor);
        Assert.Equal(GameInfoRowAnchor.Bottom, rows[4].Anchor);
        Assert.Equal(78, rows[3].Y);

        // 其余标签都是按顶边画的。
        Assert.Equal(GameInfoRowAnchor.Top, rows[1].Anchor);
        Assert.Equal(56, rows[1].Y);
    }

    [Fact]
    public void TheNameColumnShrinksForTheAboutButton()
    {
        IReadOnlyList<GameInfoRow> withNotes = BuildRows(hasCreatorNotes: true);
        IReadOnlyList<GameInfoRow> withoutNotes = BuildRows(hasCreatorNotes: false);

        Assert.Equal(349 - AboutButtonWidth, withNotes[0].Width);
        Assert.Equal(349, withoutNotes[0].Width);

        // 只有地图名这一行让位，别的列不受影响。
        Assert.Equal(withoutNotes[1].Width, withNotes[1].Width);
        Assert.Equal(withoutNotes[1].X, withNotes[1].X);
    }

    [Fact]
    public void TheNameAndTheDescriptionAreFitted()
    {
        IReadOnlyList<GameInfoRow> rows = BuildRows(hasCreatorNotes: false);

        Assert.Equal(GameInfoRowFit.SingleRow, rows[0].Fit);
        Assert.Equal(GameInfoRowFit.Area, rows[9].Fit);
        Assert.Equal(GameInfoLayout.DescriptionHeight, rows[9].Height);

        // 胜负条件那两句参考不做适配，超出就超出。
        Assert.Equal(GameInfoRowFit.None, rows[13].Fit);
    }

    [Fact]
    public void TheWindowIsCenteredAndTheTextFollowsTheShadowOffset()
    {
        GameInfoView view = CreateView(hasCreatorNotes: true);

        // 横向先给影子让出 16 像素再居中，纵向直接居中（dialog_gameinfo.cpp:95-96）。
        int expectedX = (640 - view.Window.Width - GameInfoLayout.ShadowOffsetX) / 2;
        int expectedY = (480 - view.Window.Height) / 2;

        Assert.Equal(expectedX, view.Position.X);
        Assert.Equal(expectedY, view.Position.Y);
        Assert.Equal(expectedX + GameInfoLayout.ShadowOffsetX, view.ShadowPosition.X);
        Assert.Equal(expectedY + (GameInfoLayout.ShadowOffsetY / 2), view.ShadowPosition.Y);
    }

    [Fact]
    public void TheAboutButtonOnlyAppearsForMapsWithCreatorNotes()
    {
        Assert.NotNull(CreateView(hasCreatorNotes: true).About);
        Assert.Null(CreateView(hasCreatorNotes: false).About);
        Assert.True(CreateView(hasCreatorNotes: true).HasCreatorNotes);
    }

    [Fact]
    public void TheAboutButtonSitsAboveTheRightEdgeOfTheName()
    {
        GameInfoView view = CreateView(hasCreatorNotes: true);

        Assert.Equal(view.ShadowPosition.X + GameInfoLayout.AboutButtonRight - AboutButtonWidth,
            view.About!.Area.X);
        Assert.Equal(view.ShadowPosition.Y + GameInfoLayout.AboutButtonY, view.About.Area.Y);
    }

    [Fact]
    public void TheOkayButtonSitsAtTheBottomCentre()
    {
        GameInfoView view = CreateView(hasCreatorNotes: false);

        int expectedX = view.ShadowPosition.X + (GameInfoLayout.DialogContentWidth / 2) - (view.Okay.Area.Width / 2);

        Assert.Equal(expectedX, view.Okay.Area.X);
        Assert.Equal(view.ShadowPosition.Y + GameInfoLayout.OkayButtonY, view.Okay.Area.Y);
    }

    [Fact]
    public void TheWindowNeedsItsPlateAndBothOkayFrames()
    {
        PixelBuffer display = new(640, 480);

        Assert.Null(GameInfoView.TryCreate(display, null, Frames(2, 70, 20), Frames(2, 60, 25), false));
        Assert.Null(GameInfoView.TryCreate(display, Frames(0, 444, 462), Frames(2, 70, 20), Frames(1, 60, 25), false));
        Assert.Null(GameInfoView.TryCreate(display, Frames(1, 444, 462), Frames(1, 70, 20), Frames(2, 60, 25), true));
        Assert.NotNull(GameInfoView.TryCreate(display, Frames(1, 444, 462), Frames(2, 70, 20), Frames(2, 60, 25), false));

        // 没有关于按钮时也不要求它那一对帧：老格式的地图根本没有制作者说明这一块。
        Assert.NotNull(GameInfoView.TryCreate(display, Frames(1, 444, 462), null, Frames(2, 60, 25), false));
    }

    [Fact]
    public void ThePlateIsCenteredFromItsRealSize()
    {
        PixelBuffer display = new(640, 480);
        GameInfoView view = GameInfoView.TryCreate(display, Frames(1, 400, 400), null, Frames(2, 60, 25), false)!;

        Assert.Equal((640 - 400 - GameInfoLayout.ShadowOffsetX) / 2, view.Position.X);
        Assert.Equal((480 - 400) / 2, view.Position.Y);
    }

    private static IReadOnlyList<GameInfoRow> BuildRows(bool hasCreatorNotes) => GameInfoView.BuildRows(
        Info(), GameDifficulty.Normal, "Defeat all enemies and flag all mines.", "Lose the hero.",
        hasCreatorNotes, AboutButtonWidth);

    private static GameInfoView CreateView(bool hasCreatorNotes) => GameInfoView.TryCreate(
        new PixelBuffer(640, 480), Frames(1, 444, 462), Frames(2, AboutButtonWidth, 20), Frames(2, 60, 25),
        hasCreatorNotes)!;

    private static Mp2FileInfo Info() => new()
    {
        FilePath = "island.mp2",
        Name = "Island",
        Description = "A small island with two castles.",
        Width = 72,
        Height = 72,
        Difficulty = GameDifficulty.Hard,
        CreatorNotes = "Made for the test.",
    };

    private static IReadOnlyList<Sprite> Frames(int count, int width, int height)
    {
        Sprite[] frames = new Sprite[count];

        for (int index = 0; index < count; ++index)
        {
            frames[index] = new Sprite(width, height);
        }

        return frames;
    }
}
