using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the castle screen's coordinate table, <c>Castle::OpenDialog</c>
/// (<c>castle_dialog.cpp:325-384</c>).
/// </summary>
public sealed class CastleDialogLayoutTests
{
    private static readonly Size Bar = new(90, CastleDialogLayout.StatusBarHeight);
    private static readonly Size Crest = new(30, 32);

    [Fact]
    public void StatusBarIsThreePiecesWide()
    {
        CastleDialogLayout layout = Create();

        Assert.Equal(new Rect(0, 461, 90, 19), layout.PreviousCastleButton);
        Assert.Equal(new Rect(90, 461, 90, 19), layout.StatusBarArea);
        Assert.Equal(new Rect(180, 461, 90, 19), layout.NextCastleButton);

        // 参考 :342：状态文字两端各让 16 像素给 SMALLBAR 的装饰。
        Assert.Equal(new Rect(106, 461, 58, 19), layout.StatusTextArea);
    }

    [Fact]
    public void CrestAndHeroPortraitSitAtTheLeft()
    {
        CastleDialogLayout layout = Create();

        Assert.Equal(new Rect(5, 262, 30, 32), layout.CrestArea);
        Assert.Equal(new Rect(5, 361, 100, 92), layout.HeroPortraitArea);
    }

    [Fact]
    public void ArmyBarsAreOneHundredAndTwelvePixelsIn()
    {
        CastleDialogLayout layout = Create();

        Assert.Equal(new Point(112, 262), layout.CastleArmyBarOrigin);
        Assert.Equal(new Point(112, 361), layout.HeroArmyBarOrigin);
    }

    [Fact]
    public void ResourcePanelKeepsItsOwnHitRectangle()
    {
        CastleDialogLayout layout = Create();

        Assert.Equal(new Rect(552, 262, 82, 192), layout.ResourcePanelArea);

        // 参考 :383：高度到 EXIT 上沿再减 3。
        Assert.Equal(new Rect(552, 262, 82, 163), layout.ResourceHitArea);
    }

    [Fact]
    public void ExitButtonIsPlacedButSizedByItsFrame()
    {
        CastleDialogLayout layout = Create();

        Assert.Equal(new Point(553, 428), layout.ExitButtonPosition);
        Assert.Equal(new Rect(553, 428, 61, 19), layout.GetExitButtonArea(SpriteOf(61, 19)));
    }

    [Fact]
    public void TownNameIsCentredOverTheBuildingArea()
    {
        CastleDialogLayout layout = Create();

        Assert.Equal(new Point(260, 248), layout.GetTownNamePosition(SpriteOf(120, 20)));
    }

    [Fact]
    public void EverythingFollowsTheScreenOrigin()
    {
        CastleDialogLayout layout = CastleDialogLayout.Create(new Point(7, 3), Bar, Crest);

        Assert.Equal(new Rect(7, 464, 90, 19), layout.PreviousCastleButton);
        Assert.Equal(new Rect(97, 464, 90, 19), layout.StatusBarArea);
        Assert.Equal(new Rect(12, 265, 30, 32), layout.CrestArea);
        Assert.Equal(new Rect(12, 364, 100, 92), layout.HeroPortraitArea);
        Assert.Equal(new Point(119, 265), layout.CastleArmyBarOrigin);
        Assert.Equal(new Point(560, 431), layout.ExitButtonPosition);
        Assert.Equal(new Rect(559, 265, 82, 192), layout.ResourcePanelArea);
        Assert.Equal(new Rect(559, 265, 82, 163), layout.ResourceHitArea);
        Assert.Equal(new Point(267, 251), layout.GetTownNamePosition(SpriteOf(120, 20)));
    }

    [Fact]
    public void ABarTooNarrowForTheInsetIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CastleDialogLayout.Create(new Point(0, 0), new Size(32, 19), Crest));
    }

    [Fact]
    public void AnEmptyCrestIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CastleDialogLayout.Create(new Point(0, 0), Bar, new Size(0, 32)));
    }

    private static CastleDialogLayout Create() => CastleDialogLayout.Create(new Point(0, 0), Bar, Crest);

    private static Sprite SpriteOf(int width, int height) => new(new PixelBuffer(width, height), 0, 0);
}
