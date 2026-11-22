using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The sprites the hero dialog paints, <c>Heroes::OpenDialog()</c>'s <c>Assets::getImage()</c> calls
/// (<c>heroes_dialog.cpp:96-415</c>).
/// </summary>
/// <remarks>
/// <para>
/// Only the game path is covered: the reference's editor branches (the race panel built from <c>ICN::NGEXTRA</c>,
/// its <c>ICN::STRIP</c> 3 backdrop and the 61 arrow, the patrol button and the default-value states of the
/// indicators) belong to the editor pass, which the project runs after every core directory.
/// </para>
/// <para>
/// Everything is loaded up front, like <see cref="CastleScreenArtwork"/>: a missing frame means the dialog cannot
/// be drawn, so <see cref="Load"/> fails as a whole instead of leaving a hole.
/// </para>
/// </remarks>
public sealed class HeroDialogArtwork
{
    /// <summary>The dialog itself, <c>ICN::HEROBKG</c> frame 0 (<c>heroes_dialog.cpp:96</c>).</summary>
    public required Sprite Background { get; init; }

    /// <summary>The good-interface extension, <c>ICN::HEROEXTG</c> frame 0 (<c>heroes_dialog.cpp:97</c>).</summary>
    public required Sprite BackgroundExtension { get; init; }

    /// <summary>The spread formation icon, <c>ICN::HSICONS</c> frame 9 (<c>heroes_dialog.cpp:220</c>).</summary>
    public required Sprite SpreadFormation { get; init; }

    /// <summary>The grouped formation icon, <c>ICN::HSICONS</c> frame 10 (<c>heroes_dialog.cpp:226</c>).</summary>
    public required Sprite GroupedFormation { get; init; }

    /// <summary>The cursor that frames the chosen formation, <c>ICN::HSICONS</c> frame 11 (<c>heroes_dialog.cpp:231</c>).</summary>
    public required Sprite FormationCursor { get; init; }

    /// <summary>The status bar plate, <c>ICN::HSBTNS</c> frame 8 (<c>heroes_dialog.cpp:332</c>).</summary>
    public required Sprite StatusBarPlate { get; init; }

    /// <summary>The previous hero button, <c>ICN::HSBTNS</c> frames 4 and 5 (<c>heroes_dialog.cpp:353</c>).</summary>
    public required Sprite PreviousHeroReleased { get; init; }

    /// <summary>The pressed previous hero button.</summary>
    public required Sprite PreviousHeroPressed { get; init; }

    /// <summary>The next hero button, <c>ICN::HSBTNS</c> frames 6 and 7 (<c>heroes_dialog.cpp:359</c>).</summary>
    public required Sprite NextHeroReleased { get; init; }

    /// <summary>The pressed next hero button.</summary>
    public required Sprite NextHeroPressed { get; init; }

    /// <summary>The dismiss button, <c>ICN::BUTTON_VERTICAL_DISMISS</c> frames 0 and 1 (<c>heroes_dialog.cpp:370</c>).</summary>
    public required Sprite DismissReleased { get; init; }

    /// <summary>The pressed dismiss button.</summary>
    public required Sprite DismissPressed { get; init; }

    /// <summary>The disabled dismiss button, <c>ICN::DISMISS_HERO_DISABLED_BUTTON</c> frame 0 (<c>heroes_dialog.cpp:373</c>).</summary>
    public required Sprite DismissDisabled { get; init; }

    /// <summary>The exit button, <c>ICN::BUTTON_VERTICAL_EXIT</c> frames 0 and 1 (<c>heroes_dialog.cpp:404</c>).</summary>
    public required Sprite ExitReleased { get; init; }

    /// <summary>The pressed exit button.</summary>
    public required Sprite ExitPressed { get; init; }

    /// <summary>The four indicators' artwork, <c>ICN::HSICONS</c> frames 1-8.</summary>
    public required HeroIndicatorArtwork Indicators { get; init; }

    /// <summary>The hero's portrait, from the <c>HEROESG</c>/<c>HEROESE</c> family (<c>heroes_dialog.cpp:104</c>).</summary>
    public required Sprite Portrait { get; init; }

    /// <summary>The colour crest of the hero's flag (<c>heroes_dialog.cpp:307</c>).</summary>
    public required Sprite Crest { get; init; }

    /// <summary>One empty army slot, <c>ICN::STRIP</c> frame 2.</summary>
    public required Sprite ArmySlot { get; init; }

    /// <summary>The army slot selection cursor, <c>ICN::STRIP</c> frame 1.</summary>
    public required Sprite ArmyCursor { get; init; }

    /// <summary>
    /// Loads every sprite the dialog needs.
    /// </summary>
    /// <param name="hero">The hero whose portrait the dialog shows.</param>
    /// <param name="color">The colour whose crest is drawn, <c>GetColor()</c> of the reference.</param>
    /// <param name="evilInterface">Whether the evil interface extension is used, <c>isEvilInterface</c>.</param>
    /// <returns>The artwork, or <see langword="null"/> when the game data is not available.</returns>
    public static HeroDialogArtwork? Load(Hero hero, PlayerColor color, bool evilInterface)
    {
        Sprite? background = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Herobkg), 0);
        Sprite? extension = GameAssets.GetImage(
            IcnCatalog.GetFileName(evilInterface ? IcnType.Heroexte : IcnType.Heroextg),
            0);
        Sprite? spread = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 9);
        Sprite? grouped = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 10);
        Sprite? formationCursor = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsicons), 11);
        Sprite? statusBar = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsbtns), 8);
        Sprite? previousReleased = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsbtns), 4);
        Sprite? previousPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsbtns), 5);
        Sprite? nextReleased = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsbtns), 6);
        Sprite? nextPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Hsbtns), 7);
        Sprite? dismissReleased = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.ButtonVerticalDismiss), 0);
        Sprite? dismissPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.ButtonVerticalDismiss), 1);
        Sprite? dismissDisabled = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.DismissHeroDisabledButton), 0);
        Sprite? exitReleased = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.ButtonVerticalExit), 0);
        Sprite? exitPressed = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.ButtonVerticalExit), 1);
        Sprite? portrait = GameAssets.GetImage(IcnCatalog.GetFileName(HeroPortraitIcn.Get(hero.PortraitId)), 0);
        Sprite? crest = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Crest), PlayerColors.GetIndex(color));
        Sprite? armySlot = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 2);
        Sprite? armyCursor = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Strip), 1);
        HeroIndicatorArtwork? indicators = LoadIndicators();

        if (background is null || extension is null || spread is null || grouped is null || formationCursor is null
            || statusBar is null || previousReleased is null || previousPressed is null || nextReleased is null
            || nextPressed is null || dismissReleased is null || dismissPressed is null || dismissDisabled is null
            || exitReleased is null || exitPressed is null || portrait is null || crest is null || armySlot is null
            || armyCursor is null || indicators is null)
        {
            return null;
        }

        return new HeroDialogArtwork
        {
            Background = background,
            BackgroundExtension = extension,
            SpreadFormation = spread,
            GroupedFormation = grouped,
            FormationCursor = formationCursor,
            StatusBarPlate = statusBar,
            PreviousHeroReleased = previousReleased,
            PreviousHeroPressed = previousPressed,
            NextHeroReleased = nextReleased,
            NextHeroPressed = nextPressed,
            DismissReleased = dismissReleased,
            DismissPressed = dismissPressed,
            DismissDisabled = dismissDisabled,
            ExitReleased = exitReleased,
            ExitPressed = exitPressed,
            Indicators = indicators,
            Portrait = portrait,
            Crest = crest,
            ArmySlot = armySlot,
            ArmyCursor = armyCursor,
        };
    }

    /// <summary>
    /// The four indicators' sprites, <c>ICN::HSICONS</c> frames 1-8 (<c>heroes_indicator.cpp:96-285</c>).
    /// </summary>
    private static HeroIndicatorArtwork? LoadIndicators()
    {
        string fileName = IcnCatalog.GetFileName(IcnType.Hsicons);
        Sprite? luckGood = GameAssets.GetImage(fileName, 2);
        Sprite? luckBad = GameAssets.GetImage(fileName, 3);
        Sprite? luckNeutral = GameAssets.GetImage(fileName, 6);
        Sprite? moraleGood = GameAssets.GetImage(fileName, 4);
        Sprite? moraleBad = GameAssets.GetImage(fileName, 5);
        Sprite? moraleNeutral = GameAssets.GetImage(fileName, 7);
        Sprite? experience = GameAssets.GetImage(fileName, 1);
        Sprite? spellPoints = GameAssets.GetImage(fileName, 8);

        if (luckGood is null || luckBad is null || luckNeutral is null || moraleGood is null || moraleBad is null
            || moraleNeutral is null || experience is null || spellPoints is null)
        {
            return null;
        }

        return new HeroIndicatorArtwork
        {
            LuckGood = luckGood,
            LuckBad = luckBad,
            LuckNeutral = luckNeutral,
            MoraleGood = moraleGood,
            MoraleBad = moraleBad,
            MoraleNeutral = moraleNeutral,
            Experience = experience,
            SpellPoints = spellPoints,
        };
    }
}
