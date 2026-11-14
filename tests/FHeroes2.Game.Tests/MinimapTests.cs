using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the minimap: the ground table of <see cref="MapGrounds"/>, the palette indices of
/// <see cref="Minimap"/> and the geometry of the panel and of its cursor.
/// </summary>
public sealed class MinimapTests
{
    /// <summary>A plain tile of a map: water unless the test says otherwise.</summary>
    private static Mp2TileInfo Tile(ushort terrainImageIndex = 1) =>
        new()
        {
            TerrainImageIndex = terrainImageIndex,
            ObjectName1 = 0,
            BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            Quantity1 = 0,
            Quantity2 = 0,
            ObjectName2 = 0,
            TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            TerrainFlags = 0,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

    [Theory]
    [InlineData(0, MapGround.Water)]
    [InlineData(29, MapGround.Water)]
    [InlineData(30, MapGround.Grass)]
    [InlineData(91, MapGround.Grass)]
    [InlineData(92, MapGround.Snow)]
    [InlineData(145, MapGround.Snow)]
    [InlineData(146, MapGround.Swamp)]
    [InlineData(207, MapGround.Swamp)]
    [InlineData(208, MapGround.Lava)]
    [InlineData(261, MapGround.Lava)]
    [InlineData(262, MapGround.Desert)]
    [InlineData(320, MapGround.Desert)]
    [InlineData(321, MapGround.Dirt)]
    [InlineData(360, MapGround.Dirt)]
    [InlineData(361, MapGround.Wasteland)]
    [InlineData(414, MapGround.Wasteland)]
    [InlineData(415, MapGround.Beach)]
    [InlineData(431, MapGround.Beach)]
    [InlineData(432, MapGround.Unknown)]
    public void TerrainImageIndexes_FallIntoTheirGroundRange(int imageIndex, MapGround expected) =>
        Assert.Equal(expected, MapGrounds.FromTerrainImageIndex((ushort)imageIndex));

    [Fact]
    public void Grounds_HaveTheReferencePaletteIndices()
    {
        Assert.Equal(0x76, Minimap.GetGroundColor(MapGround.Desert));
        Assert.Equal(0x0D, Minimap.GetGroundColor(MapGround.Snow));
        Assert.Equal(0x68, Minimap.GetGroundColor(MapGround.Swamp));
        Assert.Equal(0xCE, Minimap.GetGroundColor(MapGround.Wasteland));
        Assert.Equal(0x29, Minimap.GetGroundColor(MapGround.Beach));
        Assert.Equal(0x20, Minimap.GetGroundColor(MapGround.Lava));
        Assert.Equal(0x36, Minimap.GetGroundColor(MapGround.Dirt));
        Assert.Equal(0x62, Minimap.GetGroundColor(MapGround.Grass));
        Assert.Equal(0x4D, Minimap.GetGroundColor(MapGround.Water));
        Assert.Equal(0x00, Minimap.GetGroundColor(MapGround.Unknown));
    }

    [Fact]
    public void Kingdoms_HaveTheReferencePaletteIndices()
    {
        Assert.Equal(0x47, Minimap.GetKingdomColor(PlayerColor.Blue));
        Assert.Equal(0x5D, Minimap.GetKingdomColor(PlayerColor.Green));
        Assert.Equal(0xBD, Minimap.GetKingdomColor(PlayerColor.Red));
        Assert.Equal(0x70, Minimap.GetKingdomColor(PlayerColor.Yellow));
        Assert.Equal(0xCA, Minimap.GetKingdomColor(PlayerColor.Orange));
        Assert.Equal(0x87, Minimap.GetKingdomColor(PlayerColor.Purple));
        Assert.Equal(0x0A, Minimap.GetKingdomColor(PlayerColor.None));
    }

    [Fact]
    public void ARoadIsDrawnInTheRoadColor()
    {
        Mp2TileInfo road = Tile(30) with { ObjectName1 = (byte)((int)Mp2ObjectIcnType.Road << 2) };

        Assert.Equal(Minimap.RoadColor, Minimap.GetTileColor(road));
    }

    [Theory]
    [InlineData(Mp2ObjectIcnType.MtnGras)]
    [InlineData(Mp2ObjectIcnType.MtnSnow)]
    [InlineData(Mp2ObjectIcnType.TreVil)]
    [InlineData(Mp2ObjectIcnType.TreFir)]
    public void MountainsAndTrees_AreThreeShadesLighterThanTheirGround(Mp2ObjectIcnType type)
    {
        Mp2TileInfo tile = Tile(30) with { ObjectName1 = (byte)((int)type << 2) };

        Assert.Equal(Minimap.GrassColor + 3, Minimap.GetTileColor(tile));
    }

    [Fact]
    public void AnUnknownGroundIsDrawnBlack()
    {
        Assert.Equal(Minimap.BlackColor, Minimap.GetTileColor(Tile(432)));
    }

    [Fact]
    public void Heroes_AreDrawnInTheirKingdomColor()
    {
        // MINIHERO frame 22 is 3 * 7 + 1: yellow, barbarian.
        Mp2Map map = SyntheticMap.Create(
            4,
            4,
            index => index == 6
                ? Tile(30) with
                {
                    ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
                    BottomIcnImageIndex = 22,
                    ObjectType = Mp2ObjectType.Hero,
                }
                : Tile(30));

        Minimap minimap = Minimap.Build(map);

        Assert.Equal(Minimap.YellowColor, minimap.ColorAt(2, 1));
        Assert.Equal(Minimap.GrassColor, minimap.ColorAt(1, 1));
    }

    [Fact]
    public void ThePanelIsAClampedSquareOfTheReferenceSize()
    {
        Minimap minimap = Minimap.Build(SyntheticMap.Create(72, 72, _ => Tile(30)));

        Assert.Equal(Minimap.PanelSize, minimap.Image.Width);
        Assert.Equal(Minimap.PanelSize, minimap.Image.Height);
        Assert.Equal(2.0, minimap.Zoom);
    }

    [Fact]
    public void OneTileFillsItsZoomedBlockOfPixels()
    {
        // A 72 tile wide map doubles every tile: columns 0 and 1 share the first tile, 2 and 3 the
        // second one, and the last tile ends exactly on the last pixel of the panel.
        Mp2Map map = SyntheticMap.Create(72, 72, index => index % 72 == 0 ? Tile(208) : Tile(30));

        Minimap minimap = Minimap.Build(map);

        Assert.Equal(Minimap.LavaColor, minimap.Image[0, 0]);
        Assert.Equal(Minimap.LavaColor, minimap.Image[1, 1]);
        Assert.Equal(Minimap.GrassColor, minimap.Image[2, 0]);
        Assert.Equal(Minimap.GrassColor, minimap.Image[Minimap.PanelSize - 1, Minimap.PanelSize - 1]);
    }

    [Fact]
    public void TheCursorCoversTheVisibleTiles()
    {
        Minimap minimap = Minimap.Build(SyntheticMap.Create(72, 72, _ => Tile(30)));

        Rect cursor = minimap.CursorArea(new Rect(10, 20, 20, 15));

        Assert.Equal(new Rect(20, 40, 40, 30), cursor);
    }

    [Fact]
    public void TheCursorIsHiddenWhenTheWholeMapIsVisible()
    {
        Minimap minimap = Minimap.Build(SyntheticMap.Create(36, 36, _ => Tile(30)));

        Assert.True(minimap.CursorArea(new Rect(0, 0, 36, 36)).IsEmpty);
        Assert.True(minimap.CursorArea(new Rect(0, 0, 40, 40)).IsEmpty);
    }

    [Fact]
    public void TheCursorIsClippedToTheMap()
    {
        Minimap minimap = Minimap.Build(SyntheticMap.Create(72, 72, _ => Tile(30)));

        // The viewport hangs over the right and the bottom edge of the map.
        Assert.Equal(new Rect(120, 120, 24, 24), minimap.CursorArea(new Rect(60, 60, 20, 20)));
    }

    [Fact]
    public void TheCursorLeavesEverySixthPixelOut()
    {
        Minimap minimap = Minimap.Build(SyntheticMap.Create(72, 72, _ => Tile(30)));
        PixelBuffer target = new(40, 40);

        // A 12x4 cursor: 12 pixels on the top and the bottom side, 4 on the left and the right one.
        minimap.DrawCursor(target, new Point(0, 0), new Rect(0, 0, 6, 2));

        Assert.Equal(new Rect(0, 0, 12, 4), minimap.CursorArea(new Rect(0, 0, 6, 2)));
        int lit = 0;
        for (int y = 0; y < 4; ++y)
        {
            for (int x = 0; x < 12; ++x)
            {
                if (target[x, y] == Minimap.CursorColor)
                {
                    ++lit;
                }
            }
        }

        // 28 border pixels with the 6th, 12th, 18th and 24th one dropped: 28 - 4 = 24.
        Assert.Equal(24, lit);
    }

    [Fact]
    public void DrawingBlitsThePanelAtTheOrigin()
    {
        Minimap minimap = Minimap.Build(SyntheticMap.Create(72, 72, _ => Tile(208)));
        PixelBuffer target = new(200, 200);

        minimap.Draw(target, new Point(10, 20), new Rect(0, 0, 72, 72));

        Assert.Equal(Minimap.LavaColor, target[10, 20]);
        Assert.Equal(Minimap.LavaColor, target[10 + Minimap.PanelSize - 1, 20 + Minimap.PanelSize - 1]);
        Assert.Equal(0, target[9, 20]);
        Assert.Equal(0, target[10 + Minimap.PanelSize, 20]);
    }

    [Fact]
    public void TileAt_MapsThePanelOntoTiles()
    {
        // 72 tiles over 144 panel pixels: two pixels per tile, the integer division of the reference.
        Minimap minimap = Minimap.Build(SyntheticMap.Create(72, 72, _ => Tile(30)));

        Assert.Equal(new Point(0, 0), minimap.TileAt(new Point(0, 0)));
        Assert.Equal(new Point(1, 1), minimap.TileAt(new Point(2, 2)));
        Assert.Equal(new Point(35, 35), minimap.TileAt(new Point(71, 71)));

        // The last pixel of the panel is the last tile, not one past the map.
        Assert.Equal(new Point(71, 71), minimap.TileAt(new Point(Minimap.PanelSize - 1, Minimap.PanelSize - 1)));
    }

    [Fact]
    public void TileAt_HandlesMapsOtherThanSeventyTwoTiles()
    {
        Minimap minimap = Minimap.Build(SyntheticMap.Create(108, 108, _ => Tile(30)));

        Assert.Equal(new Point(0, 0), minimap.TileAt(new Point(0, 0)));
        Assert.Equal(new Point(72, 72), minimap.TileAt(new Point(96, 96)));
        Assert.Equal(new Point(107, 107), minimap.TileAt(new Point(Minimap.PanelSize - 1, Minimap.PanelSize - 1)));

        // A point outside the panel is not clamped here: the view clamps the scroll it produces.
        Assert.Equal(new Point(108, 108), minimap.TileAt(new Point(Minimap.PanelSize, Minimap.PanelSize)));
    }
}
