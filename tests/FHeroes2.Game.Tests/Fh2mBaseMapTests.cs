using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="Fh2mBaseMap"/>: the signature, the big-endian header of
/// <c>Maps::Map_Format::BaseMapFormat</c> and the payload the port keeps as it found it
/// (<c>map_format_info.cpp:442-500</c>, <c>:770-830</c>).
/// </summary>
public sealed class Fh2mBaseMapTests
{
    /// <summary>Payload large enough for the 512 byte minimum file size the reference enforces.</summary>
    private static byte[] Payload() => [.. Enumerable.Range(0, 600).Select(index => (byte)(index % 251))];

    [Fact]
    public void TwoLanguagesRoundTripThroughTheFile()
    {
        Fh2mBaseMap map = CreateMap();

        using MemoryStream stream = new();

        map.Write(stream);

        stream.Position = 0;

        Fh2mBaseMap read = Fh2mBaseMap.Read(stream);

        Assert.Equal(Fh2mBaseMap.CurrentSupportedVersion, read.Version);
        Assert.Equal(map.IsCampaign, read.IsCampaign);
        Assert.Equal(map.Difficulty, read.Difficulty);
        Assert.Equal(map.AvailablePlayerColors, read.AvailablePlayerColors);
        Assert.Equal(map.HumanPlayerColors, read.HumanPlayerColors);
        Assert.Equal(map.ComputerPlayerColors, read.ComputerPlayerColors);
        Assert.Equal(map.Alliances, read.Alliances);
        Assert.Equal(map.PlayerRaces, read.PlayerRaces);
        Assert.Equal(map.VictoryConditionType, read.VictoryConditionType);
        Assert.Equal(map.IsVictoryConditionApplicableForAi, read.IsVictoryConditionApplicableForAi);
        Assert.Equal(map.VictoryConditionMetadata, read.VictoryConditionMetadata);
        Assert.Equal(map.LossConditionType, read.LossConditionType);
        Assert.Equal(map.LossConditionMetadata, read.LossConditionMetadata);
        Assert.Equal(map.Width, read.Width);

        Assert.Equal(SupportedLanguage.German, read.MainLanguage);
        Assert.Equal("Der Turm", read.Name);
        Assert.Equal("Ein Test", read.Description);
        Assert.Equal("made by hand", read.CreatorNotes);

        Assert.Equal(2, read.Translations.Count);

        MapTranslationText french = Assert.Contains(SupportedLanguage.French, read.Translations);
        Assert.Equal("La tour", french.Name);
        Assert.Equal("Un test", french.Description);
        Assert.Equal("fait main", french.CreatorNotes);

        MapTranslationText polish = Assert.Contains(SupportedLanguage.Polish, read.Translations);
        Assert.Equal("Wieza", polish.Name);
        Assert.Equal(string.Empty, polish.Description);

        // 压缩尾段原样带走：这一段是瓦片与各类元数据，端口不解析它。
        Assert.Equal(map.Payload, read.Payload);
    }

    [Fact]
    public void TheWrittenFileStartsWithTheSignature()
    {
        using MemoryStream stream = new();

        CreateMap().Write(stream);
        stream.Position = 0;

        Assert.True(Fh2mBaseMap.HasSignature(stream));

        // HasSignature 读完会把位置放回去。
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void AWrongSignatureIsRefused()
    {
        byte[] bytes = Write(CreateMap());
        bytes[0] = (byte)'x';

        Assert.Throws<InvalidDataException>(() => Fh2mBaseMap.Read(new MemoryStream(bytes)));
        Assert.False(Fh2mBaseMap.HasSignature(new MemoryStream(bytes)));
    }

    [Fact]
    public void AShortFileIsRefused()
    {
        // 参考的 minFileSize 是 512：比这更短的一律不是地图。
        Fh2mBaseMap map = CreateMap() with { Payload = new byte[100] };

        using MemoryStream stream = new();

        map.Write(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => Fh2mBaseMap.Read(stream));
    }

    [Fact]
    public void AnUnsupportedVersionIsRefused()
    {
        foreach (int version in new[] { 1, Fh2mBaseMap.CurrentSupportedVersion + 1 })
        {
            byte[] bytes = Write(CreateMap() with { Version = version });

            Assert.Throws<InvalidDataException>(() => Fh2mBaseMap.Read(new MemoryStream(bytes)));
        }
    }

    [Fact]
    public void AVeryOldLayoutHasNeitherCreatorNotesNorTranslations()
    {
        Fh2mBaseMap map = CreateMap() with { Version = 8 };

        using MemoryStream stream = new();

        map.Write(stream);

        // 版本 8 的头里没有这两样，所以比新版本短：少了 creatorNotes 与整张语言表。
        byte[] old = stream.ToArray();
        byte[] modern = Write(CreateMap());

        Assert.True(old.Length < modern.Length);

        stream.Position = 0;

        Fh2mBaseMap read = Fh2mBaseMap.Read(stream);

        Assert.False(read.HasCreatorNotes);
        Assert.False(read.HasTranslations);
        Assert.Equal(string.Empty, read.CreatorNotes);
        Assert.Empty(read.Translations);

        // 其它字段照旧。
        Assert.Equal("Der Turm", read.Name);
        Assert.Equal(map.Payload, read.Payload);
    }

    [Fact]
    public void VersionNineHasCreatorNotesButNoTranslations()
    {
        Fh2mBaseMap map = CreateMap() with { Version = 9 };

        using MemoryStream stream = new();

        map.Write(stream);
        stream.Position = 0;

        Fh2mBaseMap read = Fh2mBaseMap.Read(stream);

        Assert.True(read.HasCreatorNotes);
        Assert.Equal("made by hand", read.CreatorNotes);
        Assert.False(read.HasTranslations);
        Assert.Empty(read.Translations);
    }

    [Fact]
    public void ThePayloadIsWrittenByteForByte()
    {
        byte[] payload = [.. Enumerable.Range(0, 512).Select(index => (byte)(255 - (index % 256)))];
        Fh2mBaseMap map = CreateMap() with { Payload = payload };
        byte[] bytes = Write(map);

        // 尾段就是文件最后的那一段，长度与内容都不变。
        Assert.True(bytes.Length > payload.Length);
        Assert.Equal(payload, bytes[^payload.Length..]);
    }

    [Fact]
    public void AZeroWidthIsRefused()
    {
        byte[] bytes = Write(CreateMap() with { Width = 0 });

        Assert.Throws<InvalidDataException>(() => Fh2mBaseMap.Read(new MemoryStream(bytes)));
    }

    private static byte[] Write(Fh2mBaseMap map)
    {
        using MemoryStream stream = new();

        map.Write(stream);

        return stream.ToArray();
    }

    /// <summary>A map of two languages with the fields the header carries.</summary>
    private static Fh2mBaseMap CreateMap() => new()
    {
        Version = Fh2mBaseMap.CurrentSupportedVersion,
        IsCampaign = false,
        Difficulty = GameDifficulty.Hard,
        AvailablePlayerColors = PlayerColor.Blue | PlayerColor.Red,
        HumanPlayerColors = PlayerColor.Blue,
        ComputerPlayerColors = PlayerColor.Red,
        Alliances = [PlayerColor.Blue, PlayerColor.Red],
        PlayerRaces = [MapRace.Knight, MapRace.Necromancer],
        VictoryConditionType = 2,
        IsVictoryConditionApplicableForAi = true,
        AllowNormalVictory = true,
        VictoryConditionMetadata = [7u, 42u],
        LossConditionType = 1,
        LossConditionMetadata = [3u],
        Width = 72,
        MainLanguage = SupportedLanguage.German,
        Name = "Der Turm",
        Description = "Ein Test",
        CreatorNotes = "made by hand",
        Translations = new Dictionary<SupportedLanguage, MapTranslationText>
        {
            [SupportedLanguage.French] = new()
            {
                Name = "La tour",
                Description = "Un test",
                CreatorNotes = "fait main",
            },
            [SupportedLanguage.Polish] = new()
            {
                Name = "Wieza",
                Description = string.Empty,
                CreatorNotes = string.Empty,
            },
        },
        Payload = Payload(),
    };
}
