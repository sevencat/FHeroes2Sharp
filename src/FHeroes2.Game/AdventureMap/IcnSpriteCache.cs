using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// Decodes an ICN file on first use and keeps it for the rest of the session, keyed by the archive
/// entry name. It is the shared half of <see cref="ObjectSpriteSet"/> and <see cref="HeroSpriteSet"/>,
/// the equivalent of <c>Assets::getImage( icnId, icnIndex )</c> plus the <c>ICN::GetString</c> table.
/// </summary>
/// <remarks>
/// The reference decodes every ICN up front; doing it on demand keeps the first frame of the
/// adventure map cheap on machines where most tile sets are never needed. A missing or broken file is
/// remembered as well, so a map that references it does not retry it for every tile.
/// </remarks>
internal sealed class IcnSpriteCache
{
    private readonly AssetBundle _assets;
    private readonly Dictionary<string, IcnSpriteFile?> _files = new(StringComparer.OrdinalIgnoreCase);

    public IcnSpriteCache(AssetBundle assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
    }

    /// <summary>Number of tile sets that have been decoded so far.</summary>
    public int Count => _files.Count;

    /// <summary>Archive entries the renderer asked for but the archives do not hold.</summary>
    public IReadOnlyCollection<string> Missing =>
        _files.Where(pair => pair.Value is null).Select(pair => pair.Key).ToArray();

    /// <summary>
    /// The sprite of <paramref name="index"/> inside <paramref name="archiveName"/>, or
    /// <see langword="null"/> when the file is missing or the frame does not exist.
    /// </summary>
    public Sprite? GetSprite(string archiveName, int index)
    {
        IcnSpriteFile? file = GetFile(archiveName);
        if (file is null || index < 0 || index >= file.Count)
        {
            return null;
        }

        return file.Sprites[index];
    }

    private IcnSpriteFile? GetFile(string archiveName)
    {
        if (_files.TryGetValue(archiveName, out IcnSpriteFile? cached))
        {
            return cached;
        }

        IcnSpriteFile? file;
        try
        {
            file = _assets.TryReadIcn(archiveName);
        }
        catch (BinaryFormatException exception)
        {
            GameLog.Error($"The tile set '{archiveName}' could not be parsed", exception);
            file = null;
        }

        if (file is null)
        {
            GameLog.Warn($"The tile set '{archiveName}' is missing; its sprites stay invisible.");
        }

        _files[archiveName] = file;
        return file;
    }
}
