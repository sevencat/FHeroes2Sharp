using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The frames the castle screen is built from, <c>castle_dialog.cpp:325-398</c>.
/// </summary>
/// <remarks>
/// <para>
/// 一次只能有一份在用的城堡屏，所以这里不做「按需取帧」，而是 <see cref="TryCreate"/> 时把整屏要用的帧全部
/// 取好：任何一张缺失就整体失败，调用方据此知道这张屏画不出来，而不是画到一半发现少一块。
/// </para>
/// <para>
/// 帧号全部来自参考：<c>STRIP</c> 0 是底部条、1 是选中框、2 是槽底、3 是无英雄像、11 是无英雄军队底板；
/// <c>SMALLBAR</c> 0 是状态条、1/2 是「上一个城堡」的常态与按下、3/4 是「下一个城堡」；<c>CREST</c> 的帧号是
/// 玩家颜色序号；<c>RESOURCE</c> 0…6 依次是木、水银、矿、硫、晶、宝、金。
/// </para>
/// <para>
/// EXIT 按钮不是档案里的一张图：参考里 <c>BUTTON_EXIT_TOWN</c> 排在 <c>LAST_VALID_FILE_ICN</c> 之后，
/// 由 <c>game_assets.cpp:918-932</c> 在运行时生成。走原始资源那条分支时它就是 <c>TREASURY</c> 的 1/2 帧
/// （<c>TREASURY.ICN</c> 在档案中确实存在），端口还没有 <c>makeButtonSprites()</c> 的文字按钮生成，
/// 所以这里按原始资源取帧，和参考的 <c>useOriginalResources()</c> 分支一致。
/// </para>
/// </remarks>
public sealed class CastleScreenArtwork
{
    private CastleScreenArtwork()
    {
    }

    /// <summary>The bottom strip behind the portraits and army bars, <c>ICN::STRIP, 0</c>.</summary>
    public required Sprite StripBackground { get; init; }

    /// <summary>One empty army slot, <c>ICN::STRIP, 2</c>.</summary>
    public required Sprite ArmySlot { get; init; }

    /// <summary>The frame drawn over a selected slot, <c>ICN::STRIP, 1</c>.</summary>
    public required Sprite ArmyCursor { get; init; }

    /// <summary>The empty hero portrait, <c>ICN::STRIP, 3</c>.</summary>
    public required Sprite EmptyHeroPortrait { get; init; }

    /// <summary>The plate behind an empty hero army, <c>ICN::STRIP, 11</c>.</summary>
    public required Sprite EmptyHeroArmy { get; init; }

    /// <summary>The castle's flag in the player's colour, <c>ICN::CREST</c>.</summary>
    public required Sprite Crest { get; init; }

    /// <summary>The town name plate, <c>ICN::TOWNNAME</c> frame 0.</summary>
    public required Sprite NamePlate { get; init; }

    /// <summary>The status bar, <c>ICN::SMALLBAR</c> frame 0.</summary>
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

    /// <summary>The pressed EXIT button, original resources: <c>TREASURY</c> frame 2.</summary>
    public required Sprite ExitPressed { get; init; }

    /// <summary>The seven resource icons of <c>ICN::RESOURCE</c>, in the panel's order.</summary>
    public required IReadOnlyList<Sprite> ResourceIcons { get; init; }

    /// <summary>The creature artwork an army bar draws, <see cref="ArmyBarArtwork"/>.</summary>
    public required IArmyBarSprites ArmySprites { get; init; }

    /// <summary>How many <c>RESOURCE</c> frames the panel needs, wood through gold.</summary>
    public const int ResourceIconCount = 7;

    /// <summary>
    /// Loads every frame of the castle screen.
    /// </summary>
    /// <param name="color">The owning player's colour, which picks the <c>CREST</c> frame.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or one of its frames is missing.</returns>
    public static CastleScreenArtwork? TryCreate(PlayerColor color)
    {
        Sprite? strip = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 0);
        Sprite? slot = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 2);
        Sprite? cursor = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 1);
        Sprite? emptyHero = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 3);
        Sprite? emptyHeroArmy = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 11);

        Sprite? crest = GameAssets.GetImage(
            IcnCatalog.GetFileName(IcnType.Crest),
            PlayerColors.GetIndex(color));

        Sprite? plate = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townname), 0);
        Sprite? bar = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 0);
        Sprite? previous = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 1);
        Sprite? previousPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 2);
        Sprite? next = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 3);
        Sprite? nextPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Smallbar), 4);

        Sprite? exit = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Treasury), 1);
        Sprite? exitPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Treasury), 2);

        if (strip is null || slot is null || cursor is null || emptyHero is null || emptyHeroArmy is null
            || crest is null || plate is null || bar is null || previous is null || previousPressed is null
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

        return new CastleScreenArtwork
        {
            StripBackground = strip,
            ArmySlot = slot,
            ArmyCursor = cursor,
            EmptyHeroPortrait = emptyHero,
            EmptyHeroArmy = emptyHeroArmy,
            Crest = crest,
            NamePlate = plate,
            StatusBar = bar,
            PreviousButton = previous,
            PreviousPressed = previousPressed,
            NextButton = next,
            NextPressed = nextPressed,
            ExitButton = exit,
            ExitPressed = exitPressed,
            ResourceIcons = icons,
            ArmySprites = new ArmyBarArtwork(),
        };
    }
}
