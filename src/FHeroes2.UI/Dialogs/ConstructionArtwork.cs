using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The frames the construction window is built from, <c>Castle::_openConstructionDialog()</c>
/// (<c>castle_town.cpp:186-480</c>).
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="CastleScreenArtwork"/> 同一套路子：<see cref="TryCreate"/> 时把定型的帧一次取好，缺一张就整体失败。
/// 只有各族的建筑图标不预取——那是二十来张 135×70 的图，按需问 <see cref="GetBuildingIcon"/> 要，缓存由
/// <see cref="GameAssets"/> 负责。
/// </para>
/// <para>
/// 帧号来自参考：<c>CASLWIND</c> 0 是整屏底图；<c>CASLXTRA</c> 0 是没有建筑的格子、1 是「可以建」的文字底、
/// 2 是「不能建」的文字底；<c>BLDGXTRA</c> 0 是格子相框；<c>TOWNWIND</c> 11 是「已建」、12 是「不能建」、
/// 13 是「钱不够」；<c>HSICONS</c> 9/10 是「散开」「集中」两种阵型图标、11 是套在上面的一圈框；
/// <c>STRIP</c> 3 是没有英雄时的空位。
/// </para>
/// <para>
/// <b>与参考的差异</b>：邪恶界面的那一套（<c>CASLWIND_EVIL</c>、<c>CASLXTRA_EVIL</c>、<c>STONEBAK_EVIL</c>）
/// 由参考的「界面风格」设置决定，端口还没有设置层，一律用普通版。
/// </para>
/// </remarks>
public sealed class ConstructionArtwork
{
    private ConstructionArtwork()
    {
    }

    /// <summary>The whole window's background, <c>ICN::CASLWIND</c> frame 0.</summary>
    public required Sprite Background { get; init; }

    /// <summary>The stone plate that covers the captain's stats, <c>ICN::STONEBAK</c> frame 0.</summary>
    public required Sprite CaptainPlate { get; init; }

    /// <summary>The frame of one building cell, <c>ICN::BLDGXTRA</c> frame 0.</summary>
    public required Sprite CellFrame { get; init; }

    /// <summary>What stands in a cell that offers nothing, <c>ICN::CASLXTRA</c> frame 0.</summary>
    public required Sprite EmptyCell { get; init; }

    /// <summary>The strip behind the name of a building that can be built, <c>CASLXTRA</c> frame 1.</summary>
    public required Sprite BuildableStrip { get; init; }

    /// <summary>The strip behind the name of a building that cannot, <c>CASLXTRA</c> frame 2.</summary>
    public required Sprite DeniedStrip { get; init; }

    /// <summary>The tick of a building that already stands, <c>ICN::TOWNWIND</c> frame 11.</summary>
    public required Sprite BuiltMark { get; init; }

    /// <summary>The cross of a building that cannot be built, <c>TOWNWIND</c> frame 12.</summary>
    public required Sprite DeniedMark { get; init; }

    /// <summary>The coin of a building the treasury cannot pay for, <c>TOWNWIND</c> frame 13.</summary>
    public required Sprite UnaffordableMark { get; init; }

    /// <summary>The captain's cell while there is no captain, <c>ICN::getCaptainIcnId()</c> frame 0.</summary>
    public required Sprite CaptainEmpty { get; init; }

    /// <summary>The captain's cell while the quarters stand, the captain's ICN frame 1.</summary>
    public required Sprite CaptainBuilt { get; init; }

    /// <summary>The player's flag on the captain's picture, <c>ICN::getFlagIcnId()</c> frame 0.</summary>
    public required Sprite Flag { get; init; }

    /// <summary>The empty hero slot, <c>ICN::STRIP</c> frame 3.</summary>
    public required Sprite EmptyHero { get; init; }

    /// <summary>The "spread formation" icon, <c>ICN::HSICONS</c> frame 9.</summary>
    public required Sprite SpreadFormation { get; init; }

    /// <summary>The "grouped formation" icon, <c>HSICONS</c> frame 10.</summary>
    public required Sprite GroupedFormation { get; init; }

    /// <summary>The frame drawn around the formation in use, <c>HSICONS</c> frame 11.</summary>
    public required Sprite FormationCursor { get; init; }

    /// <summary>The bottom status bar, <c>ICN::SMALLBAR</c> frame 0.</summary>
    public required Sprite StatusBar { get; init; }

    /// <summary>The "previous town" button, <c>SMALLBAR</c> frame 1.</summary>
    public required Sprite PreviousButton { get; init; }

    /// <summary>The pressed "previous town" button, <c>SMALLBAR</c> frame 2.</summary>
    public required Sprite PreviousPressed { get; init; }

    /// <summary>The "next town" button, <c>SMALLBAR</c> frame 3.</summary>
    public required Sprite NextButton { get; init; }

    /// <summary>The pressed "next town" button, <c>SMALLBAR</c> frame 4.</summary>
    public required Sprite NextPressed { get; init; }

    /// <summary>The EXIT button, original resources: <c>ICN::TREASURY</c> frame 1.</summary>
    public required Sprite ExitButton { get; init; }

    /// <summary>The pressed EXIT button, <c>TREASURY</c> frame 2.</summary>
    public required Sprite ExitPressed { get; init; }

    /// <summary>The seven resource icons of <c>ICN::RESOURCE</c>, in the panel's order.</summary>
    public required IReadOnlyList<Sprite> ResourceIcons { get; init; }

    /// <summary>The archive holding this race's building icons, <c>ICN::getBuildingIcnId()</c>.</summary>
    public required IcnType BuildingIcn { get; init; }

    /// <summary>How many <c>RESOURCE</c> frames the panel needs, wood through gold.</summary>
    public const int ResourceIconCount = 7;

    /// <summary>
    /// Loads every fixed frame of the construction window.
    /// </summary>
    /// <param name="race">The race of the town the window shows, which picks the building icons.</param>
    /// <param name="color">The owner's colour, which picks the flag.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or one of its frames is missing.</returns>
    public static ConstructionArtwork? TryCreate(MapRace race, PlayerColor color)
    {
        IcnType buildingIcn = IcnCatalog.GetBuildingIcnId(race);

        Sprite? background = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Caslwind), 0);
        Sprite? plate = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Stonebak), 0);
        Sprite? cellFrame = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Bldgxtra), 0);
        Sprite? emptyCell = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Caslxtra), 0);
        Sprite? buildableStrip = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Caslxtra), 1);
        Sprite? deniedStrip = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Caslxtra), 2);

        Sprite? builtMark = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townwind), 11);
        Sprite? deniedMark = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townwind), 12);
        Sprite? unaffordableMark = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townwind), 13);

        Sprite? captainEmpty = GameAssets.GetImage(IcnCatalog.GetFileName(IcnCatalog.GetCaptainIcnId(race)), 0);
        Sprite? captainBuilt = GameAssets.GetImage(IcnCatalog.GetFileName(IcnCatalog.GetCaptainIcnId(race)), 1);
        Sprite? flag = GameAssets.GetImage(IcnCatalog.GetFileName(IcnCatalog.GetFlagIcnId(color)), 0);

        Sprite? emptyHero = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 3);
        Sprite? spread = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 9);
        Sprite? grouped = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 10);
        Sprite? formationCursor = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 11);

        Sprite? bar = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 0);
        Sprite? previous = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 1);
        Sprite? previousPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 2);
        Sprite? next = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 3);
        Sprite? nextPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 4);

        Sprite? exit = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Treasury), 1);
        Sprite? exitPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Treasury), 2);

        // The first frame of the race's building archive is the one every cell starts from; if the archive is
        // missing or empty the cells could never be painted, so the window gives up here.
        if (GameAssets.GetImage(IcnCatalog.GetFileName(buildingIcn), 1) is null)
        {
            return null;
        }

        if (background is null || plate is null || cellFrame is null || emptyCell is null
            || buildableStrip is null || deniedStrip is null
            || builtMark is null || deniedMark is null || unaffordableMark is null
            || captainEmpty is null || captainBuilt is null || flag is null
            || emptyHero is null || spread is null || grouped is null || formationCursor is null
            || bar is null || previous is null || previousPressed is null
            || next is null || nextPressed is null || exit is null || exitPressed is null)
        {
            return null;
        }

        List<Sprite> icons = new(ResourceIconCount);
        for (int frame = 0; frame < ResourceIconCount; ++frame)
        {
            Sprite? icon = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Resource), frame);
            if (icon is null)
            {
                return null;
            }

            icons.Add(icon);
        }

        return new ConstructionArtwork
        {
            Background = background,
            CaptainPlate = plate,
            CellFrame = cellFrame,
            EmptyCell = emptyCell,
            BuildableStrip = buildableStrip,
            DeniedStrip = deniedStrip,
            BuiltMark = builtMark,
            DeniedMark = deniedMark,
            UnaffordableMark = unaffordableMark,
            CaptainEmpty = captainEmpty,
            CaptainBuilt = captainBuilt,
            Flag = flag,
            EmptyHero = emptyHero,
            SpreadFormation = spread,
            GroupedFormation = grouped,
            FormationCursor = formationCursor,
            StatusBar = bar,
            PreviousButton = previous,
            PreviousPressed = previousPressed,
            NextButton = next,
            NextPressed = nextPressed,
            ExitButton = exit,
            ExitPressed = exitPressed,
            ResourceIcons = icons,
            BuildingIcn = buildingIcn,
        };
    }

    /// <summary>
    /// The icon of one building in the town's archive, <c>getIndexBuildingSprite()</c>.
    /// </summary>
    /// <param name="building">The building.</param>
    /// <returns>The icon, or <see langword="null"/> for a building that has none.</returns>
    public Sprite? GetBuildingIcon(BuildingType building)
    {
        // 0 is a real frame - the mage guild - so only a negative index means "no sprite at all".
        int index = CastleBuildingInfo.GetSpriteIndex(building);
        if (index < 0)
        {
            return null;
        }

        return GameAssets.GetImage(IcnCatalog.GetFileName(BuildingIcn), index);
    }
}
