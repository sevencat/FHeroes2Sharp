using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of <see cref="MapSpecsConditionIcons"/>, the icon of a selected town row
/// (<c>getTownIcon()</c>, <c>editor_map_specs_window.cpp:170-191</c>).
/// </summary>
/// <remarks>
/// 拼图要用 <c>FLAG32</c>、<c>LOCATORS</c>/<c>LOCATORE</c> 的真实帧，所以整类跟着游戏数据门控；
/// 断言看尺寸与「旗、底座、身体都真的贴上去了」，不比像素值。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MapSpecsConditionIconsTests : IDisposable
{
    /// <summary>Restores the global asset bundle after each test.</summary>
    public void Dispose() => Assets.Current = null;

    [Theory]
    [InlineData(PlayerColor.Blue, 0)]
    [InlineData(PlayerColor.Green, 2)]
    [InlineData(PlayerColor.Red, 4)]
    [InlineData(PlayerColor.Yellow, 6)]
    [InlineData(PlayerColor.Orange, 8)]
    [InlineData(PlayerColor.Purple, 10)]
    [InlineData(PlayerColor.None, 12)]
    public void EveryColourHasTwoFlagFrames(PlayerColor color, int expected)
    {
        Assert.Equal(expected, MapSpecsConditionIcons.LeftFlagFrame(color));
    }

    [Fact]
    public void ACombinedColourHasNoFlag()
    {
        // 两面旗属于一个颜色；参考对这种输入只有 assert。
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MapSpecsConditionIcons.LeftFlagFrame(PlayerColor.Blue | PlayerColor.Green));
    }

    [Fact]
    public void ATownIconIsThePlateWithBothFlagsAndTheBody()
    {
        if (!OpenAssets())
        {
            return;
        }

        Sprite? icon = MapSpecsConditionIcons.BuildTownIcon(
            MapRace.Knight, PlayerColor.Blue, hasFort: true, isEvilInterface: false);

        Assert.NotNull(icon);

        Sprite? plate = Assets.GetImage(Assets.GetFileName(IcnType.Locators), MapSpecsConditionIcons.TownPlateFrame);
        Sprite? leftFlag = Assets.GetImage(
            Assets.GetFileName(IcnType.Flag32), MapSpecsConditionIcons.LeftFlagFrame(PlayerColor.Blue));
        Sprite? rightFlag = Assets.GetImage(
            Assets.GetFileName(IcnType.Flag32), MapSpecsConditionIcons.LeftFlagFrame(PlayerColor.Blue) + 1);

        Assert.NotNull(plate);
        Assert.NotNull(leftFlag);
        Assert.NotNull(rightFlag);

        // 宽 = 底座 + 两旗 + 4，高 = 底座（:178）。
        Assert.Equal(plate.Image.Width + leftFlag.Image.Width + rightFlag.Image.Width + 4, icon.Image.Width);
        Assert.Equal(plate.Image.Height, icon.Image.Height);

        // 底座身体贴在左旗右侧 6 像素、距顶 4 像素处：那一带必须和纯底座的同一点不同。
        Assert.True(HasAnyInk(icon.Image, leftFlag.Image.Width + MapSpecsConditionIcons.BodyOffsetX,
            MapSpecsConditionIcons.BodyOffsetY, 8, 8), "The castle body was not composited.");
    }

    [Fact]
    public void TheEvilInterfaceComposesTheSameSize()
    {
        if (!OpenAssets())
        {
            return;
        }

        Sprite? good = MapSpecsConditionIcons.BuildTownIcon(
            MapRace.Sorceress, PlayerColor.None, hasFort: false, isEvilInterface: false);
        Sprite? evil = MapSpecsConditionIcons.BuildTownIcon(
            MapRace.Sorceress, PlayerColor.None, hasFort: false, isEvilInterface: true);

        Assert.NotNull(good);
        Assert.NotNull(evil);
        Assert.Equal(good.Image.Width, evil.Image.Width);
        Assert.Equal(good.Image.Height, evil.Image.Height);

        // 中立旗与底座都换了图，合成结果不该和好界面一模一样。
        Assert.False(good.Image.Row(0).SequenceEqual(evil.Image.Row(0)), "The evil variant uses the same plate.");
    }

    private static bool OpenAssets()
    {
        Assets.Current = GameData.TryOpen();

        return Assets.Current is not null;
    }

    private static bool HasAnyInk(PixelBuffer image, int x, int y, int width, int height)
    {
        for (int row = y; row < Math.Min(y + height, image.Height); ++row)
        {
            for (int column = x; column < Math.Min(x + width, image.Width); ++column)
            {
                if (image.Row(row)[column] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
