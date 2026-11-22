using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The archive-backed lookups of the artifact bag: the <c>ARTIFACT</c> slots, the <c>ARTFX</c> mini icons and
/// the <c>HSICONS</c> plate the two mini bars share (<c>resource/artifact.cpp:1129-1170</c>).
/// </summary>
/// <remarks>
/// 参考把这三张图直接写在 <c>ArtifactsBar::RedrawItem()</c> 里；端口把它们抽成
/// <see cref="IArtifactsBarSprites"/>，这个类就是「从档案里取」的那一份实现，取到的帧按需缓存。
/// 帧号见 <see cref="ArtifactTypes.MiniSpriteIndex"/> 与 <see cref="ArtifactTypes.FullSpriteIndex"/>，
/// 空槽是 <c>ARTIFACT</c> 第 0 帧（<c>artifact.cpp:1129</c>）。
/// </remarks>
public sealed class ArtifactsBarArtwork : IArtifactsBarSprites
{
    private readonly Dictionary<uint, Sprite?> _miniIcons = [];
    private readonly Dictionary<uint, Sprite?> _icons = [];

    /// <summary>Reads the empty slot and builds the mini plate, <c>ArtifactsBar</c>'s constructor of the reference.</summary>
    /// <remarks>
    /// 空槽与底板在构造时一次取好：<see cref="ArtifactsBar"/> 的构造函数要量它们的尺寸，取不到就没法排版。
    /// 两个图标表是懒加载的，一件神器只解一次。
    /// </remarks>
    public ArtifactsBarArtwork()
    {
        EmptySlot = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Artifact), 0);
        MiniPlate = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 0) is { } frame
            ? SkillBarArtwork.CreateMiniPlate(frame, EmptyButtons.GetColorId(0xD0, 0xC0, 0x48))
            : null;
    }

    /// <inheritdoc/>
    public Sprite? MiniPlate { get; }

    /// <inheritdoc/>
    public Sprite? EmptySlot { get; }

    /// <inheritdoc/>
    public Sprite? GetMiniArtifact(uint frame) => TryGet(_miniIcons, IcnType.Artfx, frame);

    /// <inheritdoc/>
    public Sprite? GetArtifact(uint frame) => TryGet(_icons, IcnType.Artifact, frame);

    /// <summary>One frame of an icon archive, decoded once per index.</summary>
    /// <param name="cache">The per-archive table of frames already looked up.</param>
    /// <param name="type">The icon archive.</param>
    /// <param name="frame">Zero based frame number.</param>
    /// <returns>The frame, or <see langword="null"/> when the archive has no such frame.</returns>
    private static Sprite? TryGet(Dictionary<uint, Sprite?> cache, IcnType type, uint frame)
    {
        if (cache.TryGetValue(frame, out Sprite? cached))
        {
            return cached;
        }

        Sprite? sprite = GameAssets.GetImage(IcnCatalog.GetFileName(type), (int)frame);
        cache[frame] = sprite;

        return sprite;
    }
}
