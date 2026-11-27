using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="Fh2mMapText"/>: the language block of an <c>.fh2m</c> header to and from the editor's
/// <see cref="MapTranslationState"/>.
/// </summary>
public sealed class Fh2mMapTextTests
{
    [Fact]
    public void TwoLanguagesOfTheFileBecomeTwoLanguagesOfTheState()
    {
        MapTranslationState state = Fh2mMapText.ToState(CreateMap());

        Assert.Equal(SupportedLanguage.German, state.MainLanguage);
        Assert.Equal(3, state.Count);
        Assert.Equal(
            [SupportedLanguage.German, SupportedLanguage.French, SupportedLanguage.Polish],
            state.Languages);

        Assert.Equal("Der Turm", state.Current.Name);
        Assert.Equal("Ein Test", state.Current.Description);
        Assert.Equal("made by hand", state.Current.CreatorNotes);

        Assert.Equal("La tour", state.Find(SupportedLanguage.French)!.Name);
        Assert.Equal("Wieza", state.Find(SupportedLanguage.Polish)!.Name);
    }

    [Fact]
    public void SwitchingTheMainLanguageAndSavingKeepsBothTexts()
    {
        Fh2mBaseMap map = CreateMap();
        MapTranslationState state = Fh2mMapText.ToState(map);

        // 把主语言切成法语：德语文本要退回 translations 里。
        Assert.True(state.ChangeLanguage(SupportedLanguage.French));
        state.Current.Name = "La tour, corrigee";

        Fh2mBaseMap saved = Fh2mMapText.ApplyTo(map, state);

        Assert.Equal(SupportedLanguage.French, saved.MainLanguage);
        Assert.Equal("La tour, corrigee", saved.Name);

        // 德语现在是一条翻译，内容还是原来那份。
        Assert.Equal("Der Turm", saved.Translations[SupportedLanguage.German].Name);
        Assert.Equal("made by hand", saved.Translations[SupportedLanguage.German].CreatorNotes);

        // 波兰语的翻译原封不动。
        Assert.Equal("Wieza", saved.Translations[SupportedLanguage.Polish].Name);
        Assert.Equal(2, saved.Translations.Count);
    }

    [Fact]
    public void SavingThenReadingGivesTheSameLanguagesBack()
    {
        Fh2mBaseMap map = CreateMap() with { Payload = new byte[600] };
        MapTranslationState state = Fh2mMapText.ToState(map);

        state.Add(SupportedLanguage.Spanish);
        state.Find(SupportedLanguage.Spanish)!.Name = "La torre";

        Fh2mBaseMap saved = Fh2mMapText.ApplyTo(map, state);

        using MemoryStream stream = new();

        saved.Write(stream);
        stream.Position = 0;

        MapTranslationState read = Fh2mMapText.ToState(Fh2mBaseMap.Read(stream));

        Assert.Equal(state.Languages, read.Languages);
        Assert.Equal("Der Turm", read.Current.Name);
        Assert.Equal("La torre", read.Find(SupportedLanguage.Spanish)!.Name);
        Assert.Equal("fait main", read.Find(SupportedLanguage.French)!.CreatorNotes);
    }

    [Fact]
    public void TheHeaderFieldsOutsideTheLanguageBlockSurviveTheRoundTrip()
    {
        Fh2mBaseMap map = CreateMap() with { Payload = new byte[600] };
        Fh2mBaseMap saved = Fh2mMapText.ApplyTo(map, Fh2mMapText.ToState(map));

        Assert.Equal(map.Width, saved.Width);
        Assert.Equal(map.Difficulty, saved.Difficulty);
        Assert.Equal(map.AvailablePlayerColors, saved.AvailablePlayerColors);
        Assert.Equal(map.PlayerRaces, saved.PlayerRaces);
        Assert.Equal(map.VictoryConditionType, saved.VictoryConditionType);
        Assert.Same(map.Payload, saved.Payload);
    }

    [Fact]
    public void AMainLanguageInsideTheTranslationTableIsIgnored()
    {
        Fh2mBaseMap map = CreateMap() with
        {
            Translations = new Dictionary<SupportedLanguage, MapTranslationText>
            {
                [SupportedLanguage.German] = new() { Name = "Doppelt" },
                [SupportedLanguage.French] = new() { Name = "La tour" },
            },
        };

        MapTranslationState state = Fh2mMapText.ToState(map);

        // 主语言只会有一份：表里那一条同名记录被跳过，状态里仍是 1 + 1 份。
        Assert.Equal(2, state.Count);
        Assert.Equal("Der Turm", state.Current.Name);
    }

    [Fact]
    public void TheHeaderCanBeSeededFromTheSpecificationsOfASession()
    {
        Mp2Map map = Mp2Map.CreateBlank(36, MapGrounds.GrassStartImageIndex);
        var specs = new MapSpecsState(map);

        specs.Name = "From specs";
        specs.Translations.Add(SupportedLanguage.French);
        specs.Translations.Find(SupportedLanguage.French)!.Name = "Des specs";

        Fh2mBaseMap header = Fh2mMapText.FromSpecs(specs);

        Assert.Equal(Fh2mBaseMap.CurrentSupportedVersion, header.Version);
        Assert.Equal(36, header.Width);

        // 在世颜色是 MapSpecsState 定死的，来自地图头：CreateBlank 给的是六色全开。
        Assert.Equal(map.Info.KingdomColors, header.AvailablePlayerColors);
        Assert.Equal("From specs", header.Name);
        Assert.Equal("Des specs", header.Translations[SupportedLanguage.French].Name);
        Assert.Empty(header.Payload);
    }

    /// <summary>A map written in German with a French and a Polish translation.</summary>
    private static Fh2mBaseMap CreateMap() => new()
    {
        Version = Fh2mBaseMap.CurrentSupportedVersion,
        Difficulty = GameDifficulty.Hard,
        AvailablePlayerColors = PlayerColor.Blue | PlayerColor.Red,
        HumanPlayerColors = PlayerColor.Blue,
        ComputerPlayerColors = PlayerColor.Red,
        PlayerRaces = [MapRace.Knight, MapRace.Necromancer],
        VictoryConditionType = 2,
        VictoryConditionMetadata = [7u],
        LossConditionType = 1,
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
            },
        },
    };
}
