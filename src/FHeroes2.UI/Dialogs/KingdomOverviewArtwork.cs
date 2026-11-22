using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Everything the kingdom overview paints with, the <c>Assets::getImage()</c> lookups of
/// <c>kingdom/kingdom_overview.cpp</c> plus the artwork the four kinds of bar need.
/// </summary>
/// <remarks>
/// As for the other windows, the reference reads frames out of the global archive and the port passes them in; the
/// frame numbers are noted on every member.
/// </remarks>
public sealed class KingdomOverviewArtwork
{
    /// <summary>The window itself, <c>OVERBACK</c> frame 0 (<c>kingdom_overview.cpp:783</c>).</summary>
    public required Sprite Background { get; init; }

    /// <summary>The hero list header, <c>OVERVIEW</c> frame 6 (<c>:379</c>).</summary>
    public required Sprite HeroHeader { get; init; }

    /// <summary>The castle list header, <c>OVERVIEW</c> frame 7 (<c>:670</c>).</summary>
    public required Sprite CastleHeader { get; init; }

    /// <summary>The plate of one list item, <c>OVERVIEW</c> frame 8 (<c>:102</c>).</summary>
    public required Sprite ItemsBackground { get; init; }

    /// <summary>The source of the yellow grid lines, <c>OVERBACK</c> frame 0 (<c>:103</c>).</summary>
    public required Sprite Overback { get; init; }

    /// <summary>The plate of a hero row, <c>OVERVIEW</c> frame 10 (<c>:335</c>).</summary>
    public required Sprite HeroRowBackground { get; init; }

    /// <summary>The plate of a castle row, <c>OVERVIEW</c> frame 11 (<c>:621</c>).</summary>
    public required Sprite CastleRowBackground { get; init; }

    /// <summary>The scrollbar plate, <c>OVERVIEW</c> frame 13 (<c>:98</c>).</summary>
    public required Sprite ScrollbarBackground { get; init; }

    /// <summary>The slider of the scrollbar, <c>SCROLL</c> frame 4 (<c>:227</c>).</summary>
    public required Sprite ScrollSlider { get; init; }

    /// <summary>The lighthouse of the funds line, <c>OVERVIEW</c> frame 14 (<c>:758</c>).</summary>
    public required Sprite Lighthouse { get; init; }

    /// <summary>The padlock of an unbuilt dwelling, <c>CSLMARKER</c> frame 0 (<c>buildinginfo.cpp:671</c>).</summary>
    public required Sprite NotBuiltMarker { get; init; }

    /// <summary>The scrollbar's up arrow, <c>SCROLL</c> frames 0 and 1 (<c>:232</c>).</summary>
    public required Sprite ScrollUpReleased { get; init; }

    /// <summary>The pressed scrollbar's up arrow, <c>SCROLL</c> frame 1.</summary>
    public required Sprite ScrollUpPressed { get; init; }

    /// <summary>The scrollbar's down arrow, <c>SCROLL</c> frames 2 and 3 (<c>:233</c>).</summary>
    public required Sprite ScrollDownReleased { get; init; }

    /// <summary>The pressed scrollbar's down arrow, <c>SCROLL</c> frame 3.</summary>
    public required Sprite ScrollDownPressed { get; init; }

    /// <summary>The "heroes" button, <c>BUTTON_KINGDOM_HEROES</c> frames 0 and 1 (<c>:794</c>).</summary>
    public required Sprite HeroesButtonReleased { get; init; }

    /// <summary>The pressed "heroes" button, which marks the active list.</summary>
    public required Sprite HeroesButtonPressed { get; init; }

    /// <summary>The "towns" button, <c>BUTTON_KINGDOM_TOWNS</c> frames 0 and 1 (<c>:797</c>).</summary>
    public required Sprite TownsButtonReleased { get; init; }

    /// <summary>The pressed "towns" button, which marks the active list.</summary>
    public required Sprite TownsButtonPressed { get; init; }

    /// <summary>The exit button, <c>BUTTON_KINGDOM_EXIT</c> frames 0 and 1 (<c>:800</c>).</summary>
    public required Sprite ExitButtonReleased { get; init; }

    /// <summary>The pressed exit button.</summary>
    public required Sprite ExitButtonPressed { get; init; }

    /// <summary>The creature slots of the army bars, <see cref="IArmyBarSprites"/>.</summary>
    public required IArmyBarSprites Army { get; init; }

    /// <summary>The artifact slots, <see cref="IArtifactsBarSprites"/>.</summary>
    public required IArtifactsBarSprites Artifacts { get; init; }

    /// <summary>The skill slots, <see cref="ISkillBarSprites"/>.</summary>
    public required ISkillBarSprites Skills { get; init; }

    /// <summary>The 32x32 creatures of the dwelling bar, <c>MONS32</c>.</summary>
    public required IMiniMonsterSprites Monsters { get; init; }

    /// <summary>The frames the hero portraits and the castle icons are made of, <c>PORTXTRA</c> and friends.</summary>
    public required IViewWorldSpriteSource Interface { get; init; }

    /// <summary>
    /// The portraits of the captains, by race, <c>Captain::PortraitRedraw( ..., PORT_SMALL, ... )</c> (<c>:639</c>).
    /// </summary>
    /// <remarks>
    /// The port has no captain portrait of its own yet, so a caller that has them hands them over; without an entry
    /// for the town's race the row shows no portrait, and nothing else about the row changes.
    /// </remarks>
    public IReadOnlyDictionary<MapRace, Sprite>? CaptainPortraits { get; init; }

    /// <summary>
    /// Reads every frame of the window and builds the five sprite sets of the bars,
    /// the <c>Assets::getImage()</c> calls of <c>kingdom_overview.cpp:763-800</c>.
    /// </summary>
    /// <param name="assets">The opened game data.</param>
    /// <returns>The artwork, or <see langword="null"/> when the archives lack one of the frames.</returns>
    /// <remarks>
    /// 与 <see cref="HeroDialogArtwork.Load"/> 同样的分工:帧从传入的档案取,三个精灵源是控件层自己造
    /// (<see cref="ArmyBarArtwork"/>、<see cref="ArtifactsBarArtwork"/>、<see cref="SkillBarSpriteSet"/>),
    /// 小生物与「世界地图那一套图标」也从同一份档案装。缺帧时整体返回 <see langword="null"/>,
    /// 由调用方决定怎么报告——总览窗口没有半套美术的版本。
    /// </remarks>
    public static KingdomOverviewArtwork? Load(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        IcnViewWorldSpriteSource sprites = new(assets);

        Sprite? background = sprites.GetSprite(IcnType.Overback, 0);
        Sprite? heroHeader = sprites.GetSprite(IcnType.Overview, 6);
        Sprite? castleHeader = sprites.GetSprite(IcnType.Overview, 7);
        Sprite? itemsBackground = sprites.GetSprite(IcnType.Overview, 8);
        Sprite? overback = sprites.GetSprite(IcnType.Overback, 0);
        Sprite? heroRowBackground = sprites.GetSprite(IcnType.Overview, 10);
        Sprite? castleRowBackground = sprites.GetSprite(IcnType.Overview, 11);
        Sprite? scrollbarBackground = sprites.GetSprite(IcnType.Overview, 13);
        Sprite? scrollSlider = sprites.GetSprite(IcnType.Scroll, 4);
        Sprite? lighthouse = sprites.GetSprite(IcnType.Overview, 14);
        Sprite? notBuiltMarker = sprites.GetSprite(IcnType.Cslmarker, 0);
        Sprite? scrollUpReleased = sprites.GetSprite(IcnType.Scroll, 0);
        Sprite? scrollUpPressed = sprites.GetSprite(IcnType.Scroll, 1);
        Sprite? scrollDownReleased = sprites.GetSprite(IcnType.Scroll, 2);
        Sprite? scrollDownPressed = sprites.GetSprite(IcnType.Scroll, 3);
        Sprite? heroesButtonReleased = sprites.GetSprite(IcnType.ButtonKingdomHeroes, 0);
        Sprite? heroesButtonPressed = sprites.GetSprite(IcnType.ButtonKingdomHeroes, 1);
        Sprite? townsButtonReleased = sprites.GetSprite(IcnType.ButtonKingdomTowns, 0);
        Sprite? townsButtonPressed = sprites.GetSprite(IcnType.ButtonKingdomTowns, 1);
        Sprite? exitButtonReleased = sprites.GetSprite(IcnType.ButtonKingdomExit, 0);
        Sprite? exitButtonPressed = sprites.GetSprite(IcnType.ButtonKingdomExit, 1);

        if (background is null || heroHeader is null || castleHeader is null || itemsBackground is null ||
            overback is null || heroRowBackground is null || castleRowBackground is null ||
            scrollbarBackground is null || scrollSlider is null || lighthouse is null ||
            notBuiltMarker is null || scrollUpReleased is null || scrollUpPressed is null ||
            scrollDownReleased is null || scrollDownPressed is null || heroesButtonReleased is null ||
            heroesButtonPressed is null || townsButtonReleased is null || townsButtonPressed is null ||
            exitButtonReleased is null || exitButtonPressed is null)
        {
            return null;
        }

        if (Mons32Sprites.TryCreate(assets) is not { } monsters)
        {
            return null;
        }

        return new KingdomOverviewArtwork
        {
            Background = background,
            HeroHeader = heroHeader,
            CastleHeader = castleHeader,
            ItemsBackground = itemsBackground,
            Overback = overback,
            HeroRowBackground = heroRowBackground,
            CastleRowBackground = castleRowBackground,
            ScrollbarBackground = scrollbarBackground,
            ScrollSlider = scrollSlider,
            Lighthouse = lighthouse,
            NotBuiltMarker = notBuiltMarker,
            ScrollUpReleased = scrollUpReleased,
            ScrollUpPressed = scrollUpPressed,
            ScrollDownReleased = scrollDownReleased,
            ScrollDownPressed = scrollDownPressed,
            HeroesButtonReleased = heroesButtonReleased,
            HeroesButtonPressed = heroesButtonPressed,
            TownsButtonReleased = townsButtonReleased,
            TownsButtonPressed = townsButtonPressed,
            ExitButtonReleased = exitButtonReleased,
            ExitButtonPressed = exitButtonPressed,
            Army = new ArmyBarArtwork(),
            Artifacts = new ArtifactsBarArtwork(),
            Skills = new SkillBarSpriteSet(),
            Monsters = monsters,
            Interface = sprites,
        };
    }
}
