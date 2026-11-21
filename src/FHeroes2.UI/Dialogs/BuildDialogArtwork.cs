using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the building dialog, <c>Dialog::BuyBuilding()</c>
/// (<c>src/fheroes2/castle/buildinginfo.cpp:435-537</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考只用到三样东西：<c>BLDGXTRA.ICN</c> 帧 0（135x70 的建筑相框）、种族建筑图
/// （<c>IcnCatalog.GetBuildingIcnId()</c> 的整包，按 <c>CastleBuildingInfo.GetSpriteIndex()</c> 取帧）和
/// <c>RESOURCE.ICN</c> 的 0~6 帧（价格框）。窗框的六块 <c>BUYBUILD.ICN</c> 由 <see cref="FrameBox"/>
/// 自己从档案读，和参考的 <c>Dialog::FrameBox</c> 一样。
/// </para>
/// <para>
/// 相框底下的「允许/拒绝」色条与角标是城镇屏建筑格子的东西（<c>BuildingInfo::Redraw()</c>），
/// 这个对话框里没有，所以不装 <c>CASLXTRA</c>/<c>TOWNWIND</c>。
/// </para>
/// </remarks>
public sealed class BuildDialogArtwork
{
    /// <summary>Frame of <c>BLDGXTRA.ICN</c> that holds the 135x70 building frame.</summary>
    public const int FrameIndex = 0;

    private BuildDialogArtwork(
        Sprite frame,
        IReadOnlyList<Sprite> buildingImages,
        IReadOnlyList<Sprite> resourceIcons)
    {
        Frame = frame;
        BuildingImages = buildingImages;
        ResourceIcons = resourceIcons;
    }

    /// <summary>The 135x70 frame of <c>BLDGXTRA.ICN</c>.</summary>
    public Sprite Frame { get; }

    /// <summary>Frames of the race building archive, indexed by <c>CastleBuildingInfo.GetSpriteIndex()</c>.</summary>
    public IReadOnlyList<Sprite> BuildingImages { get; }

    /// <summary>The first seven frames of <c>RESOURCE.ICN</c>.</summary>
    public IReadOnlyList<Sprite> ResourceIcons { get; }

    /// <summary>
    /// Loads the artwork.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="race">Race of the town the dialog belongs to.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static BuildDialogArtwork? TryCreate(AssetBundle assets, MapRace race)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Bldgxtra)) is not { } frameFile ||
            frameFile.Count <= FrameIndex)
        {
            return null;
        }

        IcnType buildingIcn = IcnCatalog.GetBuildingIcnId(race);

        if (assets.TryReadIcn(IcnCatalog.GetFileName(buildingIcn)) is not { } buildings ||
            buildings.Count == 0)
        {
            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Resource)) is not { } resources ||
            resources.Count < ResourceCostBox.IconCount)
        {
            return null;
        }

        return new BuildDialogArtwork(
            frameFile.Sprites[FrameIndex],
            buildings.Sprites,
            resources.Sprites);
    }
}
