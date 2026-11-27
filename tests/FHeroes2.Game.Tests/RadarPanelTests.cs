using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the half both radars share: the palette indices of <see cref="RadarPanel"/>, the scale onto
/// the panel and the dotted cursor frame.
/// </summary>
public sealed class RadarPanelTests
{
    [Fact]
    public void Grounds_HaveTheReferencePaletteIndices()
    {
        Assert.Equal(0x76, RadarPanel.GetGroundColor(MapGround.Desert));
        Assert.Equal(0x0D, RadarPanel.GetGroundColor(MapGround.Snow));
        Assert.Equal(0x68, RadarPanel.GetGroundColor(MapGround.Swamp));
        Assert.Equal(0xCE, RadarPanel.GetGroundColor(MapGround.Wasteland));
        Assert.Equal(0x29, RadarPanel.GetGroundColor(MapGround.Beach));
        Assert.Equal(0x20, RadarPanel.GetGroundColor(MapGround.Lava));
        Assert.Equal(0x36, RadarPanel.GetGroundColor(MapGround.Dirt));
        Assert.Equal(0x62, RadarPanel.GetGroundColor(MapGround.Grass));
        Assert.Equal(0x4D, RadarPanel.GetGroundColor(MapGround.Water));
        Assert.Equal(0x00, RadarPanel.GetGroundColor(MapGround.Unknown));
    }

    [Fact]
    public void Kingdoms_HaveTheReferencePaletteIndices()
    {
        Assert.Equal(0x47, RadarPanel.GetKingdomColor(PlayerColor.Blue));
        Assert.Equal(0x5D, RadarPanel.GetKingdomColor(PlayerColor.Green));
        Assert.Equal(0xBD, RadarPanel.GetKingdomColor(PlayerColor.Red));
        Assert.Equal(0x70, RadarPanel.GetKingdomColor(PlayerColor.Yellow));
        Assert.Equal(0xCA, RadarPanel.GetKingdomColor(PlayerColor.Orange));
        Assert.Equal(0x87, RadarPanel.GetKingdomColor(PlayerColor.Purple));
        Assert.Equal(0x0A, RadarPanel.GetKingdomColor(PlayerColor.None));
    }

    [Fact]
    public void ScaleColors_FillsThePanelWithTheTilesOfTheGrid()
    {
        // 72 tiles over 144 panel pixels: two pixels per tile, the integer division of the reference.
        byte[] colors = new byte[72 * 72];
        Array.Fill(colors, RadarPanel.GrassColor);
        colors[0] = RadarPanel.LavaColor;

        PixelBuffer image = RadarPanel.ScaleColors(colors, 72, 72);

        Assert.Equal(MapFrame.PanelSize, image.Width);
        Assert.Equal(MapFrame.PanelSize, image.Height);
        Assert.Equal(RadarPanel.LavaColor, image[0, 0]);
        Assert.Equal(RadarPanel.LavaColor, image[1, 1]);
        Assert.Equal(RadarPanel.GrassColor, image[2, 0]);
        Assert.Equal(RadarPanel.GrassColor, image[MapFrame.PanelSize - 1, MapFrame.PanelSize - 1]);
    }

    [Fact]
    public void ScaleColors_RejectsAMissingGrid() =>
        Assert.Throws<ArgumentNullException>(() => RadarPanel.ScaleColors(null!, 72, 72));

    [Fact]
    public void TheCursorCoversTheVisibleTiles() =>
        Assert.Equal(new Rect(20, 40, 40, 30), RadarPanel.CursorArea(new Rect(10, 20, 20, 15), 72, 72));

    [Fact]
    public void TheCursorIsHiddenWhenTheWholeMapIsVisible()
    {
        Assert.True(RadarPanel.CursorArea(new Rect(0, 0, 36, 36), 36, 36).IsEmpty);
        Assert.True(RadarPanel.CursorArea(new Rect(0, 0, 40, 40), 36, 36).IsEmpty);
    }

    [Fact]
    public void TheCursorIsClippedToTheMap()
    {
        // The viewport hangs over the right and the bottom edge of the map.
        Assert.Equal(new Rect(120, 120, 24, 24), RadarPanel.CursorArea(new Rect(60, 60, 20, 20), 72, 72));
    }

    [Fact]
    public void TheCursorLeavesEverySixthPixelOut()
    {
        PixelBuffer target = new(40, 40);

        // A 12x4 cursor: 12 pixels on the top and the bottom side, 4 on the left and the right one.
        RadarPanel.DrawCursorFrame(target, new Rect(0, 0, 12, 4));

        int lit = 0;
        for (int y = 0; y < 4; ++y)
        {
            for (int x = 0; x < 12; ++x)
            {
                if (target[x, y] == RadarPanel.CursorColor)
                {
                    ++lit;
                }
            }
        }

        // 28 border pixels with the 6th, 12th, 18th and 24th one dropped: 28 - 4 = 24.
        Assert.Equal(24, lit);
    }

    [Fact]
    public void ACursorTooThinToHoldAFrameIsNotDrawn()
    {
        PixelBuffer target = new(8, 8);

        RadarPanel.DrawCursorFrame(target, new Rect(1, 1, 1, 6));

        for (int y = 0; y < 8; ++y)
        {
            for (int x = 0; x < 8; ++x)
            {
                Assert.Equal(0, target[x, y]);
            }
        }
    }
}
