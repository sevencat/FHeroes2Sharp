using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the three text information blocks <see cref="Fh2mMapReader"/> builds: a sign, a ground event and a sphinx
/// (<c>world_loadmap.cpp:624-650</c>).
/// </summary>
public sealed class Fh2mTextBlockTests
{
    private const int MapSize = 36;

    private const int TargetTile = (MapSize * MapSize / 2) + 5;

    [Fact]
    public void ASignReachesTheFileAndComesBack()
    {
        ObjectInfo sign = RequireObject(Mp2ObjectType.Sign);
        var metadata = new MapObjectMetadataStore();

        Mp2Map read = RoundTrip(WithObject(sign), metadata, store => store.Signs[7] = new SignMetadata
        {
            Message = "North",
        });

        Mp2InfoBlock block = Assert.Single(read.InfoBlocks);

        Assert.True(Mp2SignInfo.TryRead(block.Data, out Mp2SignInfo info));
        Assert.Equal("North", info.Message);

        // 瓦片靠那 13 位的词找回这一块。
        Assert.Equal(block.ObjectId, (int)read.Tiles[TargetTile].Metadata);
    }

    [Fact]
    public void AnEventReachesTheFileAndComesBack()
    {
        ObjectInfo eventObject = RequireObject(Mp2ObjectType.Event);
        var metadata = new MapObjectMetadataStore();

        metadata.AdventureMapEvents[7] = new AdventureMapEventMetadata
        {
            Message = "Boom",
            Resources = new FHeroes2.Game.Resources.Funds { Gold = 500 },
            Artifact = 4,
            IsRecurringEvent = false,
            HumanPlayerColors = (byte)(PlayerColor.Blue | PlayerColor.Green),
        };

        Mp2Map source = WithObject(eventObject);
        Fh2mBaseMap header = Fh2mMapWriter.Build(source, new MapSpecsState(source), metadata, out _);
        Mp2Map read = Fh2mMapReader.Build(header);

        Mp2InfoBlock block = Assert.Single(read.InfoBlocks);

        Assert.True(Mp2EventInfo.TryRead(block.Data, out Mp2EventInfo info));
        Assert.Equal("Boom", info.Message);
        Assert.Equal(500, info.Resources[6]);
        Assert.Equal(3, info.ArtifactId);
        Assert.True(info.IsSingleTime);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Green, info.Colors);
    }

    [Fact]
    public void ASphinxReachesTheFileAndComesBack()
    {
        ObjectInfo sphinx = RequireObject(Mp2ObjectType.Sphinx);
        var metadata = new MapObjectMetadataStore();

        metadata.Sphinxes[7] = new SphinxMetadata
        {
            Riddle = "Who?",
            Answers = ["Me"],
            Artifact = 2,
            Resources = new FHeroes2.Game.Resources.Funds { Gold = 300 },
        };

        Mp2Map source = WithObject(sphinx);
        Fh2mBaseMap header = Fh2mMapWriter.Build(source, new MapSpecsState(source), metadata, out _);
        Mp2Map read = Fh2mMapReader.Build(header);

        Mp2InfoBlock block = Assert.Single(read.InfoBlocks);

        Assert.True(Mp2SphinxInfo.TryRead(block.Data, out Mp2SphinxInfo info));
        Assert.Equal("Who?", info.Riddle);
        Assert.Equal(["me"], info.Answers);
        Assert.Equal(300, info.Resources[6]);
        Assert.Equal(1, info.ArtifactId);
    }

    [Fact]
    public void APlainMapHasNoBlocks()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        Assert.Empty(RoundTrip(map, new MapObjectMetadataStore(), _ => { }).InfoBlocks);
    }

    /// <summary>Finds the first object of a type in the catalogue and fails when there is none.</summary>
    /// <param name="type">The object type.</param>
    /// <returns>The object.</returns>
    private static ObjectInfo RequireObject(Mp2ObjectType type)
    {
        for (int group = 0; group < (int)ObjectGroup.GroupCount; ++group)
        {
            foreach (ObjectInfo info in MapObjectCatalog.GetObjectsByGroup((ObjectGroup)group))
            {
                if (Mp2ObjectTypes.NonActionType(info.ObjectType) == type)
                {
                    return info;
                }
            }
        }

        throw new InvalidOperationException($"The catalogue has no {type}.");
    }

    /// <summary>A map whose middle tile carries the given object.</summary>
    /// <param name="info">The object.</param>
    /// <returns>The map.</returns>
    private static Mp2Map WithObject(ObjectInfo info)
    {
        ObjectPartInfo part = info.GroundLevelParts[0];
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(TargetTile, map.Tiles[TargetTile] with
        {
            ObjectName1 = (byte)((int)part.IcnType << 2),
            BottomIcnImageIndex = (byte)part.IcnIndex,
            ObjectType = part.ObjectType,
            Level1ObjectUid = 7,
        });

        return map;
    }

    /// <summary>Writes the map with the metadata the callback fills in and reads it back.</summary>
    /// <param name="source">The map to write.</param>
    /// <param name="metadata">An empty store the callback may fill.</param>
    /// <param name="fill">Fills the store.</param>
    /// <returns>The map that came back.</returns>
    private static Mp2Map RoundTrip(Mp2Map source, MapObjectMetadataStore metadata,
        Action<MapObjectMetadataStore> fill)
    {
        fill(metadata);

        Fh2mBaseMap header = Fh2mMapWriter.Build(source, new MapSpecsState(source), metadata, out _);

        return Fh2mMapReader.Build(header);
    }
}
