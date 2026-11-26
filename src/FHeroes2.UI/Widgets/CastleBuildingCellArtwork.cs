using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The frames one building slot of the editor's castle details window is painted from,
/// <c>BuildingData::redraw()</c> (<c>editor_castle_details_window.cpp:201-273</c>).
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="Dialogs.ConstructionArtwork"/> 同一套路子：<see cref="TryCreate"/> 一次把定型的帧取好，缺一张就整体
/// 失败；各族的建筑图标不预取，按需问 <see cref="GetBuildingIcon"/> 要，缓存由 <see cref="GameAssets"/> 负责。
/// </para>
/// <para>
/// 两张被压暗的图（灰叉、灰底）在这里一次压好：参考是每次重画都复制一张再压一遍
/// （<c>:228-229</c>、<c>:251-253</c>），结果一样，省下每次重画的两份复制。
/// </para>
/// <para>
/// <b>与参考的差异</b>：邪恶界面的那一套（<c>CASLXTRA_EVIL</c>、<c>CELLWIN_EVIL</c>）由参考的「界面风格」设置
/// 决定，端口还没有设置层，一律用普通版。
/// </para>
/// </remarks>
public sealed class CastleBuildingCellArtwork
{
    private CastleBuildingCellArtwork()
    {
    }

    /// <summary>The archive holding this race's building icons, <c>ICN::getBuildingIcnId()</c>; <c>Unknown</c> when the race has none.</summary>
    public required IcnType BuildingIcn { get; init; }

    /// <summary>What a race without a building archive shows, <c>ICN::CASLXTRA</c> frame 0.</summary>
    public required Sprite FallbackIcon { get; init; }

    /// <summary>The frame of a cell and the strip under a cell that holds something, <c>ICN::BLDGXTRA</c> frame 0.</summary>
    public required Sprite CellFrame { get; init; }

    /// <summary>The strip under a cell that offers something, <c>ICN::CASLXTRA</c> frame 1.</summary>
    public required Sprite BuildableStrip { get; init; }

    /// <summary>The same strip pressed into grey, which is what a banned cell shows, <c>CASLXTRA</c> frame 2 greyed and darkened.</summary>
    public required Sprite DeniedStrip { get; init; }

    /// <summary>The tick of a level that is built, <c>ICN::TOWNWIND</c> frame 11.</summary>
    public required Sprite BuiltMark { get; init; }

    /// <summary>The dot of a level that is not built yet, <c>ICN::CELLWIN</c> frame 5.</summary>
    public required Sprite UnbuiltMark { get; init; }

    /// <summary>The grey cross of a level that is banned, <c>TOWNWIND</c> frame 12 greyed and darkened.</summary>
    public required Sprite DeniedMark { get; init; }

    /// <summary>
    /// Loads every fixed frame of the slot.
    /// </summary>
    /// <param name="race">The race of the castle, which picks the building archive.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or one of its frames is missing.</returns>
    public static CastleBuildingCellArtwork? TryCreate(MapRace race)
    {
        IcnType buildingIcn = IcnCatalog.GetBuildingIcnId(race);

        Sprite? fallbackIcon = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Caslxtra), 0);
        Sprite? cellFrame = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Bldgxtra), 0);
        Sprite? buildableStrip = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Caslxtra), 1);
        Sprite? deniedStrip = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Caslxtra), 2);

        Sprite? builtMark = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townwind), 11);
        Sprite? unbuiltMark = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Cellwin), 5);
        Sprite? deniedMark = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townwind), 12);

        if (fallbackIcon is null || cellFrame is null || buildableStrip is null || deniedStrip is null
            || builtMark is null || unbuiltMark is null || deniedMark is null)
        {
            return null;
        }

        // 树精那类没有建筑档案的族，一格一律画 CASLXTRA 0；有档案的族第 1 帧必须在，否则连格子都画不出来。
        if (buildingIcn != IcnType.Unknown && GameAssets.GetImage(IcnCatalog.GetFileName(buildingIcn), 1) is null)
        {
            return null;
        }

        Sprite greyStrip = deniedStrip.Clone();
        ImageOps.ApplyPalette(greyStrip.Image, new Rect(6, 1, 125, 12), PaletteTable.Combine(PaletteId.Gray, PaletteId.Darkening));

        Sprite greyMark = deniedMark.Clone();
        ImageOps.ApplyPalette(greyMark.Image, new Rect(0, 0, greyMark.Width, greyMark.Height),
            PaletteTable.Combine(PaletteId.Gray, PaletteId.Darkening));

        return new CastleBuildingCellArtwork
        {
            BuildingIcn = buildingIcn,
            FallbackIcon = fallbackIcon,
            CellFrame = cellFrame,
            BuildableStrip = buildableStrip,
            DeniedStrip = greyStrip,
            BuiltMark = builtMark,
            UnbuiltMark = unbuiltMark,
            DeniedMark = greyMark,
        };
    }

    /// <summary>
    /// The icon of one building in the town's archive, <c>getIndexBuildingSprite()</c>.
    /// </summary>
    /// <param name="building">The building.</param>
    /// <returns>The icon, or <see langword="null"/> for a race without an archive or a building without an icon.</returns>
    public Sprite? GetBuildingIcon(BuildingType building)
    {
        if (BuildingIcn == IcnType.Unknown)
        {
            return null;
        }

        // 0 is a real frame - the mage guild - so only a negative index means "no sprite at all".
        int index = CastleBuildingInfo.GetSpriteIndex(building);
        if (index < 0)
        {
            return null;
        }

        return GameAssets.GetImage(IcnCatalog.GetFileName(BuildingIcn), index);
    }
}
