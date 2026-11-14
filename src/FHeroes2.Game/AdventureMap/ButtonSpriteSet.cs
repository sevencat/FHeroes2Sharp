using FHeroes2.Assets;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The frames of <see cref="ButtonsPanel"/>, looked up by frame index. <see cref="ButtonSpriteSet"/>
/// is the implementation backed by the archives; tests substitute their own.
/// </summary>
public interface IButtonSpriteSource
{
    /// <summary>The released frame of a button, or <see langword="null"/> when the file has none.</summary>
    Sprite? GetReleased(int frame);

    /// <summary>The darkened frame of a disabled button, or <see langword="null"/> when the file has none.</summary>
    Sprite? GetDisabled(int frame);
}

/// <summary>
/// The button frames of <c>ADVBTNS.ICN</c>, the equivalent of <c>ButtonBase::_getReleased</c> and
/// <c>ButtonBase::_getDisabled</c>.
/// </summary>
/// <remarks>
/// The reference builds the disabled sprite once per button and keeps it until the released frame
/// changes; decoding is cached the same way, so a redraw only copies pixels.
/// </remarks>
public sealed class ButtonSpriteSet : IButtonSpriteSource
{
    private readonly IcnSpriteCache _cache;
    private readonly Dictionary<int, Sprite?> _disabled = [];

    private ButtonSpriteSet(IcnSpriteCache cache)
    {
        _cache = cache;
    }

    /// <summary>Number of tile sets that have been decoded so far.</summary>
    public int LoadedCount => _cache.Count;

    /// <summary>Archive entries the panel asked for but the archives do not hold.</summary>
    public IReadOnlyCollection<string> Missing => _cache.Missing;

    /// <summary>
    /// Creates the set for an archive bundle; <see langword="null"/> when there is no data, which
    /// keeps <see cref="ButtonsPanel"/> from painting anything.
    /// </summary>
    public static ButtonSpriteSet? TryCreate(AssetBundle? assets) =>
        assets is null ? null : new ButtonSpriteSet(new IcnSpriteCache(assets));

    /// <inheritdoc/>
    public Sprite? GetReleased(int frame) => _cache.GetSprite(ButtonsPanel.AssetName, frame);

    /// <inheritdoc/>
    public Sprite? GetDisabled(int frame)
    {
        if (_disabled.TryGetValue(frame, out Sprite? cached))
        {
            return cached;
        }

        // PAL::PaletteType::DARKENING over a copy of the released frame. The transform layer of the
        // copy is left alone: a palette only rewrites the indexed image below it.
        Sprite? released = GetReleased(frame);
        Sprite? disabled = released?.Clone();

        if (disabled is not null)
        {
            ImageOps.ApplyPalette(disabled.Image, PaletteId.Darkening);
        }

        _disabled[frame] = disabled;
        return disabled;
    }
}
