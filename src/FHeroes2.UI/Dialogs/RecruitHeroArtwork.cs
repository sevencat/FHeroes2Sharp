using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the recruit hero dialog, <c>Castle::DialogBuyHero()</c>
/// (<c>src/fheroes2/castle/castle_town.cpp:66-160</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用 <c>SURRENDR.ICN</c> 帧 4 当肖像相框（111x105，大肖像 102x93 贴进内圈），<c>RESOURCE.ICN</c> 的 0~6
/// 帧画价格。对话框底板的六块 <c>BUYBUILD.ICN</c> 边框由 <see cref="FrameBox"/> 自己读
/// （<c>Dialog::FrameBox( dialogHeight, true )</c>）。
/// </para>
/// <para>
/// 大肖像本身不在这里：它是每个英雄的 <c>PORT00xx</c> 帧 0，由窗口按英雄取，这样同一个对话框可以给任何
/// 英雄复用。
/// </para>
/// </remarks>
public sealed class RecruitHeroArtwork
{
    /// <summary>Frame of <c>SURRENDR.ICN</c> that holds the 111x105 portrait frame.</summary>
    public const int FrameIndex = 4;

    private RecruitHeroArtwork(Sprite frame, IReadOnlyList<Sprite> resourceIcons)
    {
        Frame = frame;
        ResourceIcons = resourceIcons;
    }

    /// <summary>The 111x105 frame of <c>SURRENDR.ICN</c>.</summary>
    public Sprite Frame { get; }

    /// <summary>The first seven frames of <c>RESOURCE.ICN</c>.</summary>
    public IReadOnlyList<Sprite> ResourceIcons { get; }

    /// <summary>
    /// Loads the artwork.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static RecruitHeroArtwork? TryCreate(AssetBundle assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Surrendr)) is not { } frames ||
            frames.Count <= FrameIndex)
        {
            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Resource)) is not { } resources ||
            resources.Count < ResourceCostBox.IconCount)
        {
            return null;
        }

        return new RecruitHeroArtwork(frames.Sprites[FrameIndex], resources.Sprites);
    }
}
