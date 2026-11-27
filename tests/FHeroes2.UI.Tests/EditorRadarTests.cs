using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.UI.Interface;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Covers <see cref="EditorRadar"/>: the palette index of a tile of an MP2 map, the heroes stamped over
/// it and the geometry of the panel and of its cursor.
/// </summary>
public sealed class EditorRadarTests
{
    /// <summary>Terrain image of the grass the tests paint with, <c>MapGrounds.GrassStartImageIndex</c>.</summary>
    private const ushort Grass = 30;

    /// <summary>Terrain image the ground table calls unknown.</summary>
    private const ushort Unknown = 432;

    /// <summary>A map of grass with no object on it.</summary>
    private static Mp2Map GrassMap(int size) => Mp2Map.CreateBlank(size, Grass);

    [Fact]
    public void ARoadIsDrawnInTheRoadColor()
    {
        Mp2TileInfo road = WithObject(GrassMap(36).Tiles[0], Mp2ObjectIcnType.Road);

        Assert.Equal(RadarPanel.RoadColor, EditorRadar.GetTileColor(road));
    }

    [Theory]
    [InlineData(Mp2ObjectIcnType.MtnGras)]
    [InlineData(Mp2ObjectIcnType.MtnSnow)]
    [InlineData(Mp2ObjectIcnType.TreVil)]
    [InlineData(Mp2ObjectIcnType.TreFir)]
    public void MountainsAndTrees_AreThreeShadesLighterThanTheirGround(Mp2ObjectIcnType type)
    {
        Mp2TileInfo tile = WithObject(GrassMap(36).Tiles[0], type);

        Assert.Equal(RadarPanel.GrassColor + RadarPanel.HillShadeOffset, EditorRadar.GetTileColor(tile));
    }

    [Fact]
    public void AnUnknownGroundIsDrawnBlack() =>
        Assert.Equal(RadarPanel.BlackColor, EditorRadar.GetTileColor(Mp2Map.CreateBlank(36, Unknown).Tiles[0]));

    [Fact]
    public void Heroes_AreDrawnInTheirKingdomColor()
    {
        // MINIHERO frame 22 is 3 * 7 + 1: yellow, barbarian.
        Mp2Map map = GrassMap(36) with
        {
            Heroes = [new Mp2MapHero(2, 1, 22, PlayerColor.Yellow, MapRace.Barbarian)],
        };

        EditorRadar radar = EditorRadar.Build(map);

        Assert.Equal(RadarPanel.YellowColor, radar.ColorAt(2, 1));
        Assert.Equal(RadarPanel.GrassColor, radar.ColorAt(1, 1));
    }

    [Fact]
    public void AHeroOutsideTheMapIsIgnored()
    {
        Mp2Map map = GrassMap(36) with
        {
            Heroes = [new Mp2MapHero(-1, 0, 22, PlayerColor.Yellow, MapRace.Barbarian)],
        };

        EditorRadar radar = EditorRadar.Build(map);

        Assert.Equal(RadarPanel.GrassColor, radar.ColorAt(0, 0));
    }

    [Fact]
    public void ThePanelIsAClampedSquareOfTheReferenceSize()
    {
        EditorRadar radar = EditorRadar.Build(GrassMap(72));

        Assert.Equal(MapFrame.PanelSize, radar.Image.Width);
        Assert.Equal(MapFrame.PanelSize, radar.Image.Height);
        Assert.Equal(2.0, radar.Zoom);
    }

    [Fact]
    public void TheBuildRejectsAMissingMap() =>
        Assert.Throws<ArgumentNullException>(() => EditorRadar.Build(null!));

    [Fact]
    public void TheCursorCoversTheVisibleTiles()
    {
        EditorRadar radar = EditorRadar.Build(GrassMap(72));

        Assert.Equal(new Rect(20, 40, 40, 30), radar.CursorArea(new Rect(10, 20, 20, 15)));
    }

    [Fact]
    public void TheCursorIsHiddenWhenTheWholeMapIsVisible()
    {
        EditorRadar radar = EditorRadar.Build(GrassMap(36));

        Assert.True(radar.CursorArea(new Rect(0, 0, 36, 36)).IsEmpty);
        Assert.True(radar.CursorArea(new Rect(0, 0, 40, 40)).IsEmpty);
    }

    [Fact]
    public void TheCursorIsClippedToTheMap()
    {
        EditorRadar radar = EditorRadar.Build(GrassMap(72));

        // The viewport hangs over the right and the bottom edge of the map.
        Assert.Equal(new Rect(120, 120, 24, 24), radar.CursorArea(new Rect(60, 60, 20, 20)));
    }

    [Fact]
    public void DrawingBlitsThePanelAtTheOrigin()
    {
        EditorRadar radar = EditorRadar.Build(GrassMap(72));
        PixelBuffer target = new(200, 200);

        radar.Draw(target, new Point(10, 20), new Rect(0, 10, 72, 40));

        Assert.Equal(RadarPanel.GrassColor, target[10, 20]);
        Assert.Equal(RadarPanel.GrassColor, target[10 + MapFrame.PanelSize - 1, 20 + MapFrame.PanelSize - 1]);
        Assert.Equal(0, target[9, 20]);
        Assert.Equal(0, target[10 + MapFrame.PanelSize, 20]);
    }

    [Fact]
    public void TileAt_MapsThePanelOntoTiles()
    {
        // 72 tiles over 144 panel pixels: two pixels per tile, the integer division of the reference.
        EditorRadar radar = EditorRadar.Build(GrassMap(72));

        Assert.Equal(new Point(0, 0), radar.TileAt(new Point(0, 0)));
        Assert.Equal(new Point(1, 1), radar.TileAt(new Point(2, 2)));
        Assert.Equal(new Point(35, 35), radar.TileAt(new Point(71, 71)));

        // The last pixel of the panel is the last tile, not one past the map.
        Assert.Equal(
            new Point(71, 71),
            radar.TileAt(new Point(MapFrame.PanelSize - 1, MapFrame.PanelSize - 1)));
    }

    [Fact]
    public void TileAt_HandlesMapsOtherThanSeventyTwoTiles()
    {
        EditorRadar radar = EditorRadar.Build(GrassMap(108));

        Assert.Equal(new Point(0, 0), radar.TileAt(new Point(0, 0)));
        Assert.Equal(new Point(72, 72), radar.TileAt(new Point(96, 96)));
        Assert.Equal(
            new Point(107, 107),
            radar.TileAt(new Point(MapFrame.PanelSize - 1, MapFrame.PanelSize - 1)));

        // A point outside the panel is not clamped here: the view clamps the scroll it produces.
        Assert.Equal(
            new Point(108, 108),
            radar.TileAt(new Point(MapFrame.PanelSize, MapFrame.PanelSize)));
    }

    /// <summary>Copies <paramref name="tile"/> with an object of <paramref name="icnType"/> on it.</summary>
    private static Mp2TileInfo WithObject(in Mp2TileInfo tile, Mp2ObjectIcnType icnType) =>
        tile with { ObjectName1 = (byte)((int)icnType << 2) };
}
