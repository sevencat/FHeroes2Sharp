using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// Where the icons of the View World window come from, the port's form of <c>Assets::getImage( icnId, icnIndex )</c>
/// as used by <c>renderCastleFlags</c>/<c>renderIcon</c>/<c>renderResourceIcon</c> (<c>view_world.cpp:270-355</c>).
/// </summary>
/// <remarks>
/// 参考从全局的 `Assets` 取图；端口没有全局资源，改成由调用方注入一个来源。返回 `null` 表示该图在数据里缺失，
/// 调用方跳过这一次绘制（参考会拿到一张空图，效果一样，但端口能顺便避免一次空解引用）。
/// </remarks>
public interface IViewWorldSpriteSource
{
    /// <summary>One frame of an ICN archive.</summary>
    /// <param name="icn">The archive, e.g. <see cref="IcnType.Misc12"/>.</param>
    /// <param name="index">The frame index inside the archive.</param>
    /// <returns>The frame, or <see langword="null"/> if the archive or the frame is missing.</returns>
    Sprite? GetSprite(IcnType icn, int index);
}

/// <summary>
/// The <see cref="IViewWorldSpriteSource"/> that reads the real <c>*.ICN</c> archives.
/// </summary>
public sealed class IcnViewWorldSpriteSource : IViewWorldSpriteSource
{
    private readonly IcnSpriteCache _cache;

    /// <summary>Creates the source over an open set of archives.</summary>
    /// <param name="assets">The archives, usually the ones the game was started with.</param>
    public IcnViewWorldSpriteSource(AssetBundle assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _cache = new IcnSpriteCache(assets);
    }

    /// <summary>How many archives were opened so far, for the diagnostics of a caller.</summary>
    public int Count => _cache.Count;

    /// <inheritdoc/>
    /// <remarks>
    /// 合成表（<c>BUTTON_SMALL_OKAY_*</c>、次元门方框的 EXIT 等）没有归档条目，先问 <see cref="Assets.GetImage"/>，
    /// 免得归档侧的解码缓存为它们报「缺图」。其余仍是先问注入的归档，再问全局的 <see cref="Assets"/>。
    /// </remarks>
    public Sprite? GetSprite(IcnType icn, int index)
    {
        string fileName = Assets.GetFileName(icn);

        return Assets.IsRuntimeTable(fileName)
            ? Assets.GetImage(fileName, index)
            : _cache.GetSprite(fileName, index) ?? Assets.GetImage(fileName, index);
    }
}
