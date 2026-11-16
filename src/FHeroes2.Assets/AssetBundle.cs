using FHeroes2.Core.Diagnostics;
using FHeroes2.Assets.Agg;

namespace FHeroes2.Assets;

/// <summary>
/// The set of archives the game reads from, mirroring <c>AGG::AGG</c>:
/// the base archive (<c>heroes2.agg</c>) plus the optional Price of Loyalty archive
/// (<c>heroes2x.agg</c>), which takes precedence when a name exists in both.
/// </summary>
public sealed class AssetBundle : IDisposable
{
    private readonly AssetArchive _baseArchive;
    private readonly AssetArchive? _expansionArchive;

    private AssetBundle(AssetArchive baseArchive, AssetArchive? expansionArchive, string? dataDirectory)
    {
        _baseArchive = baseArchive;
        _expansionArchive = expansionArchive;
        DataDirectory = dataDirectory;
    }

    /// <summary>Directory the archives were loaded from, when it could be determined.</summary>
    public string? DataDirectory { get; }

    public AssetArchive BaseArchive => _baseArchive;

    public AssetArchive? ExpansionArchive => _expansionArchive;

    public int TotalEntryCount => _baseArchive.Count + (_expansionArchive?.Count ?? 0);

    /// <summary>Opens both archives from the resolved game data directory.</summary>
    public static AssetBundle Open(string? explicitDataDirectory = null)
    {
        string? dataDirectory = AssetLocator.Resolve(explicitDataDirectory);
        if (dataDirectory is null)
        {
            throw new DirectoryNotFoundException(
                "No Heroes II data directory found. Pass --data <dir> or set " +
                $"{AssetLocator.DataDirectoryEnvironmentVariable} to the folder that contains 'Data/Heroes2.agg'.");
        }

        string basePath = AssetLocator.FindArchive(dataDirectory, AssetLocator.BaseArchiveName)
            ?? throw new FileNotFoundException($"'{AssetLocator.BaseArchiveName}' not found in '{dataDirectory}'.");

        AssetArchive baseArchive = AssetArchive.Open(basePath);
        AssetArchive? expansionArchive = null;

        string? expansionPath = AssetLocator.FindArchive(dataDirectory, AssetLocator.ExpansionArchiveName);
        if (expansionPath is not null)
        {
            try
            {
                expansionArchive = AssetArchive.Open(expansionPath);
            }
            catch (Exception exception) when (exception is IOException or Core.Io.BinaryFormatException)
            {
                GameLog.Warn($"Expansion archive '{expansionPath}' could not be opened: {exception.Message}");
            }
        }

        GameLog.Info(
            $"Loaded '{basePath}' with {baseArchive.Count} entries" +
            (expansionArchive is null ? "." : $" and '{expansionPath}' with {expansionArchive.Count} entries."));

        return new AssetBundle(baseArchive, expansionArchive, dataDirectory);
    }

    /// <summary>Reads an asset from the expansion archive first, then from the base archive.</summary>
    public byte[]? TryRead(string fileName) => TryRead(fileName, false);

    /// <summary>
    /// Reads an asset, optionally skipping the expansion archive, the reference's
    /// <c>AGG::getDataFromAggFile( name, ignoreExpansion )</c>.
    /// </summary>
    /// <param name="fileName">Archive entry name, for example <c>FONT.ICN</c>.</param>
    /// <param name="ignoreExpansion">
    /// When <see langword="true"/> only the base archive is searched. The reference needs it for the Polish
    /// assets: the expansion overwrote a glyph, so the one frame is re-read from the base archive.
    /// </param>
    /// <returns>The decompressed entry, or <see langword="null"/> when the archives have none.</returns>
    /// <remarks>
    /// 参考：<c>src/fheroes2/agg/agg.cpp:40</c>
    /// <c>std::vector&lt;uint8_t&gt; AGG::getDataFromAggFile( const std::string &amp; key, const bool ignoreExpansion )</c>。
    /// </remarks>
    public byte[]? TryRead(string fileName, bool ignoreExpansion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!ignoreExpansion && _expansionArchive?.TryRead(fileName) is { } fromExpansion)
        {
            return fromExpansion;
        }

        return _baseArchive.TryRead(fileName);
    }

    /// <summary>
    /// Whether the expansion archive is open, the reference's
    /// <c>AGG::isPoLResourceFilePresent</c> — the question the game asks where the Price of Loyalty data
    /// changes what it draws.
    /// </summary>
    /// <remarks>
    /// 参考：<c>agg.cpp:120</c> <c>bool AGG::isPoLResourceFilePresent()</c>（返回 <c>_priceOfLoyaltyArchive != nullptr</c>）；
    /// 它的调用方是 <c>Settings::setPriceOfLoyaltySupported()</c>，也就是
    /// <c>Settings::Get().isPriceOfLoyaltySupported()</c> 的来源。本项目没有 Settings 层，读的是归档本身。
    /// </remarks>
    public bool IsPriceOfLoyaltyPresent => _expansionArchive is not null;

    /// <summary>
    /// Reads and decodes an ICN file, the format every UI sprite table uses
    /// (<c>HEROES.ICN</c>, <c>BTNSHNGL.ICN</c>, <c>FONT.ICN</c>, ...).
    /// </summary>
    /// <returns><see langword="null"/> when neither archive holds <paramref name="fileName"/>.</returns>
    /// <exception cref="Core.Io.BinaryFormatException">The entry exists but is not a valid ICN file.</exception>
    public Icn.IcnSpriteFile? TryReadIcn(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        byte[]? data = TryRead(fileName);
        return data is null ? null : Icn.IcnSpriteFile.Parse(data);
    }

    public bool Contains(string fileName) =>
        (_expansionArchive?.Contains(fileName) ?? false) || _baseArchive.Contains(fileName);

    public void Dispose()
    {
        _expansionArchive?.Dispose();
        _baseArchive.Dispose();
    }
}
