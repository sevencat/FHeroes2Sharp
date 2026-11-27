using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the condition rows of the map specifications window (ADR-303):
/// <c>editor_map_specs_window.cpp:499-623</c>.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MapSpecsConditionRowsTests
{
    [Fact]
    public void EveryVictoryConditionHasItsRowText()
    {
        // 「全灭所有人」是「没有特殊条件」的默认项，参考在这里写 None.，不写 GameOver::GetString 的长句。
        Assert.Equal("None.", MapSpecsConditionRows.GetVictoryText(MapVictoryCondition.DefeatEveryone));
        Assert.Equal("Capture a specific town.", MapSpecsConditionRows.GetVictoryText(MapVictoryCondition.CaptureTown));
        Assert.Equal("Defeat a specific hero.", MapSpecsConditionRows.GetVictoryText(MapVictoryCondition.KillHero));
        Assert.Equal("Find a specific artifact.", MapSpecsConditionRows.GetVictoryText(MapVictoryCondition.ObtainArtifact));
        Assert.Equal("One side defeats the other.", MapSpecsConditionRows.GetVictoryText(MapVictoryCondition.DefeatOtherSide));
        Assert.Equal("Accumulate gold.", MapSpecsConditionRows.GetVictoryText(MapVictoryCondition.CollectGold));

        Assert.Equal("Unknown", MapSpecsConditionRows.GetVictoryText((MapVictoryCondition)200));
    }

    [Fact]
    public void EveryLossConditionHasItsRowText()
    {
        Assert.Equal("None.", MapSpecsConditionRows.GetLossText(MapLossCondition.Everything));
        Assert.Equal("Lose a specific town.", MapSpecsConditionRows.GetLossText(MapLossCondition.LoseTown));
        Assert.Equal("Lose a specific hero.", MapSpecsConditionRows.GetLossText(MapLossCondition.LoseHero));
        Assert.Equal("Run out of time.", MapSpecsConditionRows.GetLossText(MapLossCondition.OutOfTime));

        Assert.Equal("Unknown", MapSpecsConditionRows.GetLossText((MapLossCondition)200));
    }

    [Fact]
    public void TheVictoryIconsComeFromTheRequestsFramesThirtyToThirtyFive()
    {
        Assert.Equal(30, MapSpecsConditionRows.GetVictoryIconIndex(MapVictoryCondition.DefeatEveryone));
        Assert.Equal(31, MapSpecsConditionRows.GetVictoryIconIndex(MapVictoryCondition.CaptureTown));
        Assert.Equal(32, MapSpecsConditionRows.GetVictoryIconIndex(MapVictoryCondition.KillHero));
        Assert.Equal(33, MapSpecsConditionRows.GetVictoryIconIndex(MapVictoryCondition.ObtainArtifact));
        Assert.Equal(34, MapSpecsConditionRows.GetVictoryIconIndex(MapVictoryCondition.DefeatOtherSide));
        Assert.Equal(35, MapSpecsConditionRows.GetVictoryIconIndex(MapVictoryCondition.CollectGold));

        Assert.Equal(0, MapSpecsConditionRows.GetVictoryIconIndex((MapVictoryCondition)200));
    }

    [Fact]
    public void TheLossIconsComeFromTheRequestsFramesThirtySixToThirtyNine()
    {
        Assert.Equal(36, MapSpecsConditionRows.GetLossIconIndex(MapLossCondition.Everything));
        Assert.Equal(37, MapSpecsConditionRows.GetLossIconIndex(MapLossCondition.LoseTown));
        Assert.Equal(38, MapSpecsConditionRows.GetLossIconIndex(MapLossCondition.LoseHero));
        Assert.Equal(39, MapSpecsConditionRows.GetLossIconIndex(MapLossCondition.OutOfTime));

        Assert.Equal(0, MapSpecsConditionRows.GetLossIconIndex((MapLossCondition)200));
    }

    [Fact]
    public void TheAllianceOrderIsAnOrdinal()
    {
        Assert.Equal("1st", MapSpecsConditionRows.GetAllianceOrderText(0));
        Assert.Equal("2nd", MapSpecsConditionRows.GetAllianceOrderText(1));
        Assert.Equal("3rd", MapSpecsConditionRows.GetAllianceOrderText(2));
        Assert.Equal("4th", MapSpecsConditionRows.GetAllianceOrderText(3));
        Assert.Equal("5th", MapSpecsConditionRows.GetAllianceOrderText(4));

        // 六个颜色最多五份同盟。
        Assert.Equal("Unknown", MapSpecsConditionRows.GetAllianceOrderText(5));
        Assert.Equal("Unknown", MapSpecsConditionRows.GetAllianceOrderText(-1));
    }

    [Fact]
    public void TheIconIsPaintedOnePixelInAndTheTextAfterIt()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        IcnSpriteFile? requests = assets.TryReadIcn("REQUESTS.ICN");
        if (requests is null || requests.Count < 40)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite icon = requests.Sprites[MapSpecsConditionRows.GetVictoryIconIndex(MapVictoryCondition.CaptureTown)];
            Rect roi = new(10, 20, 140, 40);
            PixelBuffer target = new(200, 100);
            target.Fill(0);

            MapSpecsConditionRows.DrawVictory(MapVictoryCondition.CaptureTown, roi, yellowFont: false, icon, target);

            // 参考 editor_map_specs_window.cpp:612：图标贴在 roi.x + 1、roi.y，逐像素复制。
            for (int row = 0; row < icon.Height; ++row)
            {
                for (int column = 0; column < icon.Width; ++column)
                {
                    Assert.Equal(icon.Image[column, row], target[roi.X + 1 + column, roi.Y + row]);
                }
            }

            // 参考 :614：文字从 roi.x + 20、roi.y + 2 起画，所以图标右侧那一条里有像素被画上。
            bool painted = false;
            for (int y = roi.Y + 2; y < roi.Y + 2 + 13 && !painted; ++y)
            {
                for (int x = roi.X + 20; x < roi.X + 100; ++x)
                {
                    if (target[x, y] != 0)
                    {
                        painted = true;
                        break;
                    }
                }
            }

            Assert.True(painted, "the condition text must be painted after the icon");
        }
        finally
        {
            Assets.Current = null;
        }
    }
}
