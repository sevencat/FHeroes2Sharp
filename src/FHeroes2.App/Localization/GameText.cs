using FHeroes2.Assets.Localization;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Io;

namespace FHeroes2.App.Localization;

/// <summary>
/// The text a screen shows: msgids of the original engine translated through the catalog that
/// <c>tools/scripts/BuildTranslations.ps1</c> installs into <c>&lt;data&gt;/files/lang</c>.
/// </summary>
/// <remarks>
/// <para>
/// Message ids are the English strings of fheroes2 itself (<c>"New Game"</c>,
/// <c>"Click to choose the selected language."</c>, ...), so the same <c>.po</c> file can be fed to
/// both implementations and an untranslated id simply renders as English. That keeps the fallback
/// path free of a second string table: <see cref="MoCatalog.Translate"/> already returns the id.
/// </para>
/// <para>
/// Instances are immutable; switching the language means switching the instance
/// (<c>MainMenuScene</c> holds one per available language).
/// </para>
/// </remarks>
public sealed class GameText
{
    /// <summary>Culture used when no catalog is installed: the msgids are already English.</summary>
    public const string FallbackCulture = "en";

    private const string ChineseCulture = "zh_CN";

    private readonly MoCatalog? _catalog;

    private GameText(string culture, string nativeName, MoCatalog? catalog)
    {
        Culture = culture;
        NativeName = nativeName;
        _catalog = catalog;
    }

    /// <summary>Culture name of the catalog, e.g. <c>zh_CN</c>, or <see cref="FallbackCulture"/>.</summary>
    public string Culture { get; }

    /// <summary>Name of the language written in that language, for the language switch label.</summary>
    public string NativeName { get; }

    /// <summary>Whether a catalog was loaded; <see langword="false"/> means English msgids only.</summary>
    public bool IsLocalized => _catalog is not null;

    /// <summary>Number of translated strings, zero for the built-in English table.</summary>
    public int TranslatedEntries => _catalog?.Count ?? 0;

    /// <summary>
    /// Loads every language this client can show. English always comes first, the compiled
    /// catalogs follow in the order they were found.
    /// </summary>
    /// <param name="dataDirectory">Game data directory, used to locate <c>files/lang/*.mo</c>.</param>
    public static IReadOnlyList<GameText> LoadAll(string? dataDirectory)
    {
        List<GameText> languages = [new(FallbackCulture, "English", null)];

        string? catalogPath = MoCatalog.FindCatalog(dataDirectory, ChineseCulture);
        if (catalogPath is null)
        {
            GameLog.Info(
                $"No '{ChineseCulture}.mo' catalog next to the game data; the interface stays English " +
                "(run tools/scripts/BuildTranslations.ps1 to install it).");
            return languages;
        }

        try
        {
            MoCatalog catalog = MoCatalog.Load(catalogPath);
            languages.Add(new GameText(ChineseCulture, "简体中文", catalog));
            GameLog.Info(
                $"Text catalog '{catalogPath}' loaded: {catalog.Count} entries, language " +
                $"'{catalog.Language}'.");
        }
        catch (Exception exception) when (
            exception is IOException or BinaryFormatException or NotSupportedException)
        {
            GameLog.Warn($"The text catalog '{catalogPath}' could not be used: {exception.Message}");
        }

        return languages;
    }

    /// <summary>
    /// Translates <paramref name="messageId"/> and falls back to the id itself, with the
    /// <c>context|</c> prefix removed.
    /// </summary>
    public string Get(string messageId) => _catalog?.Translate(messageId) ?? MoCatalog.StripContext(messageId);
}
