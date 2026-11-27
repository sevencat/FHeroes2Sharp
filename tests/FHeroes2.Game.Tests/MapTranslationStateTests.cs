using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="MapTranslationState"/>, the port's <c>mainLanguage</c> / <c>translations</c>
/// (<c>map_format_info.h:375-386</c>), and the three operations the language window drives on it:
/// <c>changeLanguage</c> (<c>map_format_helper.cpp:1904</c>), <c>loadTranslation</c> (<c>:1977</c>) and
/// <c>removeTranslation</c> (<c>:2080</c>).
/// </summary>
public sealed class MapTranslationStateTests
{
    [Fact]
    public void AFreshMapHasOnlyItsMainLanguage()
    {
        MapTranslationState state = new();

        Assert.Equal(SupportedLanguage.English, state.MainLanguage);
        Assert.Equal(1, state.Count);
        Assert.Equal(new[] { SupportedLanguage.English }, state.Languages);
        Assert.Empty(state.Translations);
        Assert.Equal(string.Empty, state.Current.Name);
    }

    [Fact]
    public void AddedLanguagesComeAfterTheMainOneInEnumOrder()
    {
        MapTranslationState state = new();

        // Added German first: the list still reads French before German, as the std::map of the reference does.
        Assert.True(state.Add(SupportedLanguage.German));
        Assert.True(state.Add(SupportedLanguage.French));

        Assert.Equal(
            new[] { SupportedLanguage.English, SupportedLanguage.French, SupportedLanguage.German },
            state.Languages);
        Assert.Equal(3, state.Count);
        Assert.True(state.Has(SupportedLanguage.French));
        Assert.Equal(string.Empty, state.Find(SupportedLanguage.French)!.Name);
    }

    [Fact]
    public void AddingALanguageTwiceOrTheMainOneChangesNothing()
    {
        MapTranslationState state = new();

        Assert.False(state.Add(SupportedLanguage.English));
        Assert.True(state.Add(SupportedLanguage.German));
        Assert.False(state.Add(SupportedLanguage.German));
        Assert.Equal(2, state.Count);
    }

    [Fact]
    public void SwitchingLanguageKeepsTheOldTextsAsATranslation()
    {
        MapTranslationState state = new(
            SupportedLanguage.English,
            new MapTranslationText { Name = "Beltway", Description = "Two roads" });
        Assert.True(state.Add(SupportedLanguage.German));

        Assert.True(state.ChangeLanguage(SupportedLanguage.German));

        Assert.Equal(SupportedLanguage.German, state.MainLanguage);

        // A language is only registered when it is added: its texts are empty on purpose, not missing.
        Assert.Equal(string.Empty, state.Current.Name);
        Assert.Equal("Beltway", state.Find(SupportedLanguage.English)!.Name);
        Assert.Equal("Two roads", state.Find(SupportedLanguage.English)!.Description);
        Assert.False(state.Translations.ContainsKey(SupportedLanguage.German));

        // The main language always heads the list; the old one is now one of the translations.
        Assert.Equal(
            new[] { SupportedLanguage.German, SupportedLanguage.English },
            state.Languages);
    }

    [Fact]
    public void SwitchingToALanguageWithoutTextsKeepsTheCurrentOnes()
    {
        MapTranslationState state = new(
            SupportedLanguage.English,
            new MapTranslationText { Name = "Beltway" });

        // The window may switch to a language that was never added: loadTranslation finds no slot and leaves the
        // texts alone (<c>:1984-1989</c>).
        Assert.True(state.ChangeLanguage(SupportedLanguage.French));

        Assert.Equal(SupportedLanguage.French, state.MainLanguage);
        Assert.Equal("Beltway", state.Current.Name);
        Assert.Equal("Beltway", state.Find(SupportedLanguage.English)!.Name);
    }

    [Fact]
    public void TextsSurviveTwoSwitches()
    {
        MapTranslationState state = new(
            SupportedLanguage.English,
            new MapTranslationText { Name = "Beltway" });
        _ = state.Add(SupportedLanguage.German);

        _ = state.ChangeLanguage(SupportedLanguage.German);
        state.Current.Name = "Ringstraße";

        _ = state.ChangeLanguage(SupportedLanguage.English);
        Assert.Equal("Beltway", state.Current.Name);

        _ = state.ChangeLanguage(SupportedLanguage.German);
        Assert.Equal("Ringstraße", state.Current.Name);
        Assert.Equal("Beltway", state.Find(SupportedLanguage.English)!.Name);
    }

    [Fact]
    public void TheMainLanguageCannotBeRemoved()
    {
        MapTranslationState state = new();
        _ = state.Add(SupportedLanguage.German);

        Assert.False(state.RemoveTranslation(SupportedLanguage.English));
        Assert.True(state.RemoveTranslation(SupportedLanguage.German));
        Assert.False(state.Has(SupportedLanguage.German));
        Assert.Equal(1, state.Count);

        // Dropping it again is harmless, as erase() on a missing key is.
        Assert.False(state.RemoveTranslation(SupportedLanguage.German));
    }

    [Fact]
    public void TheSpecsWindowEditsTheTextsOfTheMainLanguage()
    {
        Mp2Map map = SyntheticMap.Create(8, 8);
        map.Info = map.Info with { Name = "Beltway", Description = "Two roads" };

        MapSpecsState state = new(map);
        Assert.Equal("Beltway", state.Name);

        state.Name = "Ring road";
        Assert.Equal("Ring road", state.Translations.Current.Name);

        // Switching languages carries the name along: the header written back belongs to the new main language.
        _ = state.Translations.Add(SupportedLanguage.German);
        _ = state.Translations.ChangeLanguage(SupportedLanguage.German);
        Assert.Equal(string.Empty, state.CreateInfo().Name);

        // Switching back stores the German texts as they stand—empty, which the reference also treats as intentional.
        _ = state.Translations.ChangeLanguage(SupportedLanguage.English);
        Assert.Equal("Ring road", state.CreateInfo().Name);
        Assert.Equal("Two roads", state.CreateInfo().Description);
        Assert.Equal(string.Empty, state.Translations.Find(SupportedLanguage.German)!.Name);
    }
}
