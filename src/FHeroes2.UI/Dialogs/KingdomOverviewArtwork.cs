using FHeroes2.Assets;
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
}
