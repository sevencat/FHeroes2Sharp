using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="TerrainTransitions"/>: the ground is implied by the terrain image index, so the
/// tests build maps out of plain images and check which offset inside a ground block comes back.
/// </summary>
public sealed class TerrainTransitionsTests
{
    [Fact]
    public void AnIslandOfOneGroundHasNoImage()
    {
        // Snow in the middle of grass: every side is "other", and there is no image for that. The
        // reference paints another ground type in that case; the editor keeps the plain snow image.
        Mp2Map map = CreateMap(4, (x, y) => x == 1 && y == 1 ? MapGround.Snow : MapGround.Grass);

        Assert.False(TerrainTransitions.TryGetBlend(map, new Point(1, 1), out _, out _));
    }

    [Fact]
    public void GrassToTheRightOfWaterBlendsAgainstTheWater()
    {
        // A vertical coastline: water in the first column, grass in the second.
        Mp2Map map = CreateMap(4, (x, _) => x == 0 ? MapGround.Water : MapGround.Grass);

        Assert.True(TerrainTransitions.TryGetBlend(map, new Point(1, 1), out ushort imageIndex, out byte shape));

        // Ground to the right, top and bottom, the open side is the left one, and that side is water:
        // image 8 plus the beach variant, mirrored horizontally.
        Assert.InRange(imageIndex, MapGrounds.StartImageIndex(MapGround.Grass) + 24, MapGrounds.StartImageIndex(MapGround.Grass) + 26);
        Assert.Equal(0b10, shape);
    }

    [Fact]
    public void WaterNextToGrassBlendsAgainstTheGrass()
    {
        Mp2Map map = CreateMap(4, (x, _) => x == 0 ? MapGround.Water : MapGround.Grass);

        Assert.True(TerrainTransitions.TryGetBlend(map, new Point(0, 1), out ushort imageIndex, out byte shape));

        // Water has no beach of its own, so the open right side keeps the plain water transition.
        Assert.InRange(imageIndex, MapGrounds.StartImageIndex(MapGround.Water) + 8, MapGrounds.StartImageIndex(MapGround.Water) + 10);
        Assert.Equal(0, shape);
    }

    [Fact]
    public void ABeachIsNeverBlended()
    {
        Mp2Map map = CreateMap(4, (x, _) => x == 0 ? MapGround.Beach : MapGround.Water);

        Assert.False(TerrainTransitions.TryGetBlend(map, new Point(0, 1), out _, out _));
    }

    [Fact]
    public void TheMapEdgeIsNotATransition()
    {
        // A corner of a map that is grass everywhere: the neighbours outside the map are clamped onto
        // the border, so the tile is surrounded by its own ground and keeps the plain image.
        Mp2Map map = CreateMap(4, (_, _) => MapGround.Grass);

        Assert.False(TerrainTransitions.TryGetBlend(map, new Point(0, 0), out _, out _));
        Assert.False(TerrainTransitions.TryGetBlend(map, new Point(3, 3), out _, out _));
    }

    [Fact]
    public void DirtIgnoresGrassAndOnlySeesWaterAndBeach()
    {
        // Dirt in the middle of grass: grass is not a ground dirt blends with, so the tile counts as
        // surrounded.
        Mp2Map map = CreateMap(4, (x, y) => x == 1 && y == 1 ? MapGround.Dirt : MapGround.Grass);

        Assert.False(TerrainTransitions.TryGetBlend(map, new Point(1, 1), out _, out _));

        // One water neighbour is enough to need a transition.
        Mp2Map withWater = CreateMap(4, (x, y) => x == 0 ? MapGround.Water : x == 1 && y == 1 ? MapGround.Dirt : MapGround.Grass);

        Assert.True(TerrainTransitions.TryGetBlend(withWater, new Point(1, 1), out ushort imageIndex, out _));
        Assert.InRange(imageIndex, MapGrounds.StartImageIndex(MapGround.Dirt) + 8, MapGrounds.StartImageIndex(MapGround.Dirt) + 10);
    }

    [Fact]
    public void ThePlainImageIsOneOfEightVariantsOfItsBlock()
    {
        Point tile = new(3, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => TerrainTransitions.PlainImageIndex(MapGround.Unknown, tile));

        foreach (MapGround ground in Enum.GetValues<MapGround>().Where(candidate => candidate != MapGround.Unknown))
        {
            ushort imageIndex = TerrainTransitions.PlainImageIndex(ground, tile);

            Assert.Equal(ground, MapGrounds.FromTerrainImageIndex(imageIndex));

            int offset = imageIndex - MapGrounds.StartImageIndex(ground);
            int expected = ground switch
            {
                MapGround.Water or MapGround.Dirt => 16,
                MapGround.Beach => 0,
                _ => 38,
            };

            Assert.InRange(offset, expected, expected + 7);

            // The same tile always gets the same variant, so undo and redo are byte for byte stable.
            Assert.Equal(imageIndex, TerrainTransitions.PlainImageIndex(ground, tile));
        }
    }

    [Fact]
    public void PaintingWaterOnGrassBlendsTheNeighbours()
    {
        Mp2Map map = SyntheticMap.Create(4, 4, _ => PlainTile(MapGround.Grass));
        MapEditSession session = new(new MapEditorView(new AdventureMapView(map, SyntheticTerrain.Create(4, 32), new Size(96, 96))));

        session.SetBrush(MapGround.Water);

        Assert.True(session.Paint(new Point(1, 1)));

        // The water itself is plain, but the four grass neighbours now draw an edge against it.
        Assert.Equal(MapGround.Water, MapGrounds.FromTerrainImageIndex(map.Tile(1, 1).TerrainImageIndex));

        foreach (Point neighbour in new[] { new Point(0, 1), new Point(2, 1), new Point(1, 0), new Point(1, 2) })
        {
            Mp2TileInfo tile = map.Tile(neighbour.X, neighbour.Y);

            Assert.Equal(MapGround.Grass, MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex));
            Assert.True(
                tile.TerrainImageIndex < MapGrounds.StartImageIndex(MapGround.Grass) + 38,
                $"tile {neighbour} still looks like plain grass");
        }

        // One stroke, so one undo takes the water and all four edges back.
        Assert.Equal(1, session.UndoDepth);
        Assert.True(session.Undo());

        Assert.Equal(MapGrounds.StartImageIndex(MapGround.Grass) + 38, map.Tile(1, 1).TerrainImageIndex);
    }

    [Fact]
    public void ThePlainOffsetOfAGroundMatchesItsBlock()
    {
        Assert.Equal(0, MapGrounds.PlainImageOffset(MapGround.Beach));
        Assert.Equal(16, MapGrounds.PlainImageOffset(MapGround.Water));
        Assert.Equal(16, MapGrounds.PlainImageOffset(MapGround.Dirt));
        Assert.Equal(38, MapGrounds.PlainImageOffset(MapGround.Grass));
        Assert.Equal(38, MapGrounds.PlainImageOffset(MapGround.Wasteland));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGrounds.PlainImageOffset(MapGround.Unknown));
    }

    [Fact]
    public void TheVariantIsStableAndInsideItsRange()
    {
        Assert.InRange(MapGrounds.VariantOffset(3, 5, 8), 0, 7);
        Assert.Equal(MapGrounds.VariantOffset(3, 5, 8), MapGrounds.VariantOffset(3, 5, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGrounds.VariantOffset(3, 5, 0));
    }

    /// <summary>Builds a map whose tiles are the plain image of a ground.</summary>
    private static Mp2Map CreateMap(int size, Func<int, int, MapGround> ground)
        => SyntheticMap.Create(size, size, index => PlainTile(ground(index % size, index / size)));

    private static Mp2TileInfo PlainTile(MapGround ground) =>
        new()
        {
            TerrainImageIndex = ground switch
            {
                MapGround.Water or MapGround.Dirt => (ushort)(MapGrounds.StartImageIndex(ground) + 16),
                MapGround.Beach => MapGrounds.StartImageIndex(ground),
                _ => (ushort)(MapGrounds.StartImageIndex(ground) + 38),
            },
            ObjectName1 = 0,
            BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            Quantity1 = 0,
            Quantity2 = 0,
            ObjectName2 = 0,
            TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
            TerrainFlags = 0,
        };
}
