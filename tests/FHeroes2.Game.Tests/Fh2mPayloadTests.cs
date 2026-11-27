using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="Fh2mPayload"/>: the zlib stream after the header of an <c>.fh2m</c> file and the structures
/// it carries (<c>map_format_info.cpp:489-540</c>).
/// </summary>
public sealed class Fh2mPayloadTests
{
    [Fact]
    public void ATwoTileMapRoundTrips()
    {
        Fh2mPayload payload = new()
        {
            Tiles =
            [
                new Fh2mTile(12, 0, []),
                new Fh2mTile(34, 5, [new Fh2mTileObject(7, 3, 11), new Fh2mTileObject(8, 9, 2)]),
            ],
        };

        Fh2mPayload read = RoundTrip(payload);

        Assert.Equal(2, read.Tiles.Count);
        Assert.Equal(12, read.Tiles[0].TerrainIndex);
        Assert.Empty(read.Tiles[0].Objects);

        Assert.Equal(34, read.Tiles[1].TerrainIndex);
        Assert.Equal(5, read.Tiles[1].TerrainFlags);
        Assert.Equal(2, read.Tiles[1].Objects.Count);
        Assert.Equal(new Fh2mTileObject(7, 3, 11), read.Tiles[1].Objects[0]);
        Assert.Equal(new Fh2mTileObject(8, 9, 2), read.Tiles[1].Objects[1]);
    }

    [Fact]
    public void RumorsAndDailyEventsSurvive()
    {
        Fh2mPayload payload = new()
        {
            Rumors = ["A rumor", "Another one"],
            DailyEvents =
            [
                new MapDailyEvent
                {
                    Message = "A gift",
                    HumanPlayerColors = PlayerColor.Blue | PlayerColor.Red,
                    ComputerPlayerColors = PlayerColor.Green,
                    FirstOccurrenceDay = 5,
                    RepeatPeriodInDays = 7,
                    Resources = new Funds(ore: 1, wood: 2, mercury: 3, sulfur: 4, crystal: 5, gems: 6, gold: 700),
                },
            ],
        };

        Fh2mPayload read = RoundTrip(payload);

        Assert.Equal(payload.Rumors, read.Rumors);

        MapDailyEvent item = Assert.Single(read.DailyEvents);

        Assert.Equal("A gift", item.Message);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, item.HumanPlayerColors);
        Assert.Equal(PlayerColor.Green, item.ComputerPlayerColors);
        Assert.Equal(5u, item.FirstOccurrenceDay);
        Assert.Equal(7u, item.RepeatPeriodInDays);

        // Funds 的序列化顺序是 wood, mercury, ore, sulfur, crystal, gems, gold。
        Assert.Equal(2, item.Resources.Wood);
        Assert.Equal(3, item.Resources.Mercury);
        Assert.Equal(1, item.Resources.Ore);
        Assert.Equal(4, item.Resources.Sulfur);
        Assert.Equal(5, item.Resources.Crystal);
        Assert.Equal(6, item.Resources.Gems);
        Assert.Equal(700, item.Resources.Gold);
    }

    [Fact]
    public void AllTenMetadataTablesSurvive()
    {
        Fh2mPayload payload = new()
        {
            Castles = new SortedDictionary<uint, CastleMetadata>
            {
                [10] = new()
                {
                    CustomName = "Keep",
                    CustomBuildings = true,
                    BuiltBuildings = [1u, 32u],
                    BannedBuildings = [64u],
                    MustHaveSpells = new SortedDictionary<byte, int> { [3] = 12 },
                    BannedSpells = [7, 8],
                    AvailableToHireMonsterCount = [1, -1, 2, -1, -1, -1],
                },
            },
            Heroes = new SortedDictionary<uint, HeroMetadata>
            {
                [11] = new()
                {
                    CustomName = "Hero",
                    CustomPortrait = 4,
                    IsOnPatrol = true,
                    PatrolRadius = 3,
                    CustomLevel = 7,
                    CustomExperience = 1500,
                    CustomAttack = 2,
                    SecondarySkill = [1, 0, 0, 0, 0, 0, 0, 0],
                    SecondarySkillLevel = [3, 0, 0, 0, 0, 0, 0, 0],
                    MagicPoints = 20,
                    Race = 2,
                },
            },
            Sphinxes = new SortedDictionary<uint, SphinxMetadata>
            {
                [12] = new() { Riddle = "Who?", Answers = ["Me"], Artifact = 5, Resources = new Funds { Gold = 99 } },
            },
            Signs = new SortedDictionary<uint, SignMetadata> { [13] = new() { Message = "North" } },
            Events = new SortedDictionary<uint, AdventureMapEventMetadata>
            {
                [14] = new() { Message = "Boom", Attack = 1, Defense = 2, Experience = 300, MonsterCount = 4 },
            },
            Selections = new SortedDictionary<uint, SelectionObjectMetadata> { [15] = new() { SelectedItems = [1, 2] } },
            Capturables = new SortedDictionary<uint, CapturableObjectMetadata>
            {
                [16] = new() { OwnerColor = PlayerColor.Red },
            },
            Monsters = new SortedDictionary<uint, MonsterMetadata>
            {
                [17] = new() { Count = 42, JoinCondition = 1, IsWeeklyGrowthDisabled = true, Selected = [3] },
            },
            Artifacts = new SortedDictionary<uint, ArtifactMetadata>
            {
                [18] = new() { Radius = 5, CaptureCondition = 1, Selected = [9] },
            },
            Resources = new SortedDictionary<uint, ResourceMetadata> { [19] = new() { Count = 1500 } },
        };

        Fh2mPayload read = RoundTrip(payload);

        Assert.True(read.Castles.TryGetValue(10u, out CastleMetadata? castle));

        Assert.Equal("Keep", castle.CustomName);
        Assert.True(castle.CustomBuildings);
        Assert.Equal([1u, 32u], castle.BuiltBuildings);
        Assert.Equal([64u], castle.BannedBuildings);
        Assert.Equal(12, castle.MustHaveSpells[3]);
        Assert.Equal([7, 8], castle.BannedSpells);
        Assert.Equal([1, -1, 2, -1, -1, -1], castle.AvailableToHireMonsterCount);

        Assert.True(read.Heroes.TryGetValue(11u, out HeroMetadata? hero));

        Assert.Equal("Hero", hero.CustomName);
        Assert.Equal(4, hero.CustomPortrait);
        Assert.True(hero.IsOnPatrol);
        Assert.Equal(3, hero.PatrolRadius);
        Assert.Equal(7, hero.CustomLevel);
        Assert.Equal(1500, hero.CustomExperience);
        Assert.Equal(2, hero.CustomAttack);
        Assert.Equal(1, hero.SecondarySkill[0]);
        Assert.Equal(3, hero.SecondarySkillLevel[0]);
        Assert.Equal(20, hero.MagicPoints);
        Assert.Equal(2, hero.Race);

        Assert.True(read.Sphinxes.TryGetValue(12u, out SphinxMetadata? sphinx));

        Assert.Equal("Who?", sphinx.Riddle);
        Assert.Equal(["Me"], sphinx.Answers);
        Assert.Equal(5, sphinx.Artifact);
        Assert.Equal(99, sphinx.Resources.Gold);

        Assert.True(read.Signs.TryGetValue(13u, out SignMetadata? sign));
        Assert.Equal("North", sign.Message);

        Assert.True(read.Events.TryGetValue(14u, out AdventureMapEventMetadata? item));

        Assert.Equal("Boom", item.Message);
        Assert.Equal(1, item.Attack);
        Assert.Equal(2, item.Defense);
        Assert.Equal(300, item.Experience);
        Assert.Equal(4, item.MonsterCount);

        Assert.True(read.Selections.TryGetValue(15u, out SelectionObjectMetadata? selection));
        Assert.Equal([1, 2], selection.SelectedItems);

        Assert.True(read.Capturables.TryGetValue(16u, out CapturableObjectMetadata? capturable));
        Assert.Equal(PlayerColor.Red, capturable.OwnerColor);

        Assert.True(read.Monsters.TryGetValue(17u, out MonsterMetadata? stack));

        Assert.Equal(42, stack.Count);
        Assert.Equal(1, stack.JoinCondition);
        Assert.True(stack.IsWeeklyGrowthDisabled);
        Assert.Equal([3], stack.Selected);

        Assert.True(read.Artifacts.TryGetValue(18u, out ArtifactMetadata? artifact));

        Assert.Equal(5, artifact.Radius);
        Assert.Equal(1, artifact.CaptureCondition);
        Assert.Equal([9], artifact.Selected);

        Assert.True(read.Resources.TryGetValue(19u, out ResourceMetadata? pile));
        Assert.Equal(1500, pile.Count);
    }

    [Fact]
    public void ThePerLanguageBlockRoundTrips()
    {
        Fh2mPayload payload = new()
        {
            TranslationInfo = new SortedDictionary<SupportedLanguage, Fh2mTranslationFormat>
            {
                [SupportedLanguage.French] = new()
                {
                    DailyEvents = [new MapDailyEvent { Message = "Un cadeau", FirstOccurrenceDay = 2 }],
                    Rumors = ["Une rumeur"],
                    Signs = new SortedDictionary<uint, SignMetadata> { [5] = new() { Message = "Nord" } },
                },
            },
        };

        Fh2mPayload read = RoundTrip(payload);

        Assert.True(read.TranslationInfo.TryGetValue(SupportedLanguage.French, out Fh2mTranslationFormat? french));

        Assert.Equal("Un cadeau", Assert.Single(french.DailyEvents).Message);
        Assert.Equal(["Une rumeur"], french.Rumors);
        Assert.True(french.Signs.TryGetValue(5u, out SignMetadata? sign));
        Assert.Equal("Nord", sign.Message);
        Assert.Empty(french.Castles);
    }

    [Fact]
    public void AdditionalInfoAndAnEmptyPayloadRoundTrip()
    {
        Fh2mPayload empty = RoundTrip(new Fh2mPayload());

        Assert.Empty(empty.AdditionalInfo);
        Assert.Empty(empty.Tiles);
        Assert.Empty(empty.Rumors);
        Assert.Empty(empty.DailyEvents);

        Fh2mPayload campaign = RoundTrip(new Fh2mPayload { AdditionalInfo = [1u, 2u, 3u] });

        Assert.Equal([1u, 2u, 3u], campaign.AdditionalInfo);
    }

    [Fact]
    public void ARepeatingMapCompresses()
    {
        // 一张全是同一格瓦片的图：序列化后很长，压完应当明显更短。
        Fh2mTile tile = new(7, 0, []);
        Fh2mPayload payload = new() { Tiles = [.. Enumerable.Repeat(tile, 2000)] };

        byte[] compressed = payload.Write();

        Assert.True(compressed.Length < 2000 * 3, $"the payload is {compressed.Length} bytes");

        // 解压回来还是两千格。
        Assert.Equal(2000, Fh2mPayload.Read(compressed).Tiles.Count);
    }

    [Fact]
    public void AnEmptyStreamIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => Fh2mPayload.Read([]));
    }

    [Fact]
    public void ACorruptStreamIsRefused()
    {
        // 不是 zlib：前两个字节没有 zlib 头。
        byte[] garbage = [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x11];

        Assert.Throws<InvalidDataException>(() => Fh2mPayload.Read(garbage));
    }

    [Fact]
    public void ATruncatedBodyIsRefused()
    {
        // 压缩得对，但里面声明的瓦片数远超实际内容。
        using var raw = new MemoryStream();
        BigEndianWriter writer = new(raw);
        writer.WriteCount(0);
        writer.WriteCount(1000);
        writer.Flush();

        using var packed = new MemoryStream();

        using (var zlib = new global::System.IO.Compression.ZLibStream(packed,
                   global::System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        Assert.Throws<InvalidDataException>(() => Fh2mPayload.Read(packed.ToArray()));
    }

    private static Fh2mPayload RoundTrip(Fh2mPayload payload) => Fh2mPayload.Read(payload.Write());
}
