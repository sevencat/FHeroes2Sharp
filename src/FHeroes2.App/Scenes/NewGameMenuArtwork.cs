using FHeroes2.App.Localization;
using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The 28 frames of the new game menu, the <c>ICN::BUTTONS_NEW_GAME_MENU_GOOD</c> and
/// <c>ICN::BUTTONS_NEW_GAME_MENU_EVIL</c> tables of
/// <c>game_assets.cpp:1657-1710</c>.
/// </summary>
/// <remarks>
/// <para>
/// The two tables do not exist in the AGG, the reference builds them from the button archives:
/// the good set takes Standard game, Campaign, Multi-player and Cancel from
/// <c>BTNNEWGM.ICN</c>, generates Battle only and Settings as ordinary two line buttons over the
/// stone background, takes Hot seat from <c>BTNMP.ICN</c>, the player counts from
/// <c>BTNHOTST.ICN</c> and the two campaign buttons from <c>X_LOADCM.ICN</c>. The evil set is the
/// good set with every frame recoloured through <c>GOOD_TO_EVIL_BUTTON</c>,
/// <c>game_assets.cpp:1658-1664</c>.
/// </para>
/// <para>
/// The scene keeps the frames once the menu is opened, so the tables are built on the first click
/// on New Game, not at start-up.
/// </para>
/// </remarks>
internal static class NewGameMenuArtwork
{
    /// <summary>Archive of the four original buttons, eight frames.</summary>
    public const string NewGameMenuAssetName = "BTNNEWGM.ICN";

    /// <summary>Archive of the hot seat button, two frames.</summary>
    public const string HotSeatAssetName = "BTNMP.ICN";

    /// <summary>Archive of the five player count buttons, ten frames.</summary>
    public const string PlayerCountAssetName = "BTNHOTST.ICN";

    /// <summary>Archive of the two campaign buttons, four frames.</summary>
    public const string CampaignAssetName = "X_LOADCM.ICN";

    /// <summary>Stone texture the two generated buttons take their corners from.</summary>
    public const string StoneBackgroundAssetName = "STONEBAK.ICN";

    /// <summary>Message of the Battle only button, <c>_( "BATTLE\nONLY" )</c> of the reference.</summary>
    public const string BattleOnlyLabelId = "BATTLE\nONLY";

    /// <summary>Message of the Settings button, <c>_( "SETTINGS" )</c> of the reference.</summary>
    public const string SettingsLabelId = "SETTINGS";

    /// <summary>
    /// Builds the frames of one interface set.
    /// </summary>
    /// <param name="assets">Game resources to read the archives from.</param>
    /// <param name="text">Translations of the two generated button labels.</param>
    /// <param name="isEvilInterface">
    /// Recolour every frame through <c>GOOD_TO_EVIL_BUTTON</c>; the reference builds the good set
    /// and converts it rather than generating the evil one separately.
    /// </param>
    /// <returns>
    /// The 28 frames of <see cref="NewGameMenuView.FrameCount"/>, or <see langword="null"/> when
    /// the button archives are missing, in which case the caller keeps the main menu as it is.
    /// </returns>
    /// <remarks>
    /// The two campaign frames are placeholders when <c>X_LOADCM.ICN</c> is not installed: the
    /// four expansion campaigns are the only thing that shows them, and the caller only enables
    /// them after <c>CampaignData.IsAllCampaignMapsPresent</c> agreed that they are playable.
    /// </remarks>
    public static Sprite[]? TryCreate(AssetBundle assets, GameText text, bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(text);

        IcnSpriteFile? menu = assets.TryReadIcn(NewGameMenuAssetName);
        IcnSpriteFile? hotSeat = assets.TryReadIcn(HotSeatAssetName);
        IcnSpriteFile? playerCounts = assets.TryReadIcn(PlayerCountAssetName);

        if (menu is null || menu.Count < 8 || hotSeat is null || hotSeat.Count < 2 || playerCounts is null
            || playerCounts.Count < 10)
        {
            GameLog.Warn("The new game menu needs BTNNEWGM.ICN, BTNMP.ICN and BTNHOTST.ICN, and they are missing.");
            return null;
        }

        Sprite[] frames = new Sprite[NewGameMenuView.FrameCount];

        for (int index = 0; index < 6; ++index)
        {
            frames[index] = menu.Sprites[index];
        }

        Size textArea = new(menu.Sprites[0].Width - 10, menu.Sprites[0].Height);
        Sprite? background = assets.TryReadIcn(StoneBackgroundAssetName) is { Count: > 0 } stone
            ? stone.Sprites[0]
            : null;

        (frames[6], frames[7]) = ButtonFactory.MakeButtonSprites(text.Get(BattleOnlyLabelId), textArea, false, background);
        (frames[8], frames[9]) = ButtonFactory.MakeButtonSprites(text.Get(SettingsLabelId), textArea, false, background);

        frames[10] = menu.Sprites[6];
        frames[11] = menu.Sprites[7];
        frames[12] = hotSeat.Sprites[0];
        frames[13] = hotSeat.Sprites[1];

        for (int index = 0; index < 5; ++index)
        {
            frames[NewGameMenuView.FirstPlayerCountReleasedFrame + (index * 2)] = playerCounts.Sprites[index * 2];
            frames[NewGameMenuView.FirstPlayerCountReleasedFrame + (index * 2) + 1] = playerCounts.Sprites[(index * 2) + 1];
        }

        IcnSpriteFile? campaigns = assets.TryReadIcn(CampaignAssetName);
        if (campaigns is not null && campaigns.Count >= 4)
        {
            for (int index = 0; index < 4; ++index)
            {
                frames[NewGameMenuView.SuccessionWarsReleasedFrame + index] = campaigns.Sprites[index];
            }
        }
        else
        {
            for (int index = NewGameMenuView.SuccessionWarsReleasedFrame; index < NewGameMenuView.FrameCount; ++index)
            {
                Sprite placeholder = new(1, 1);
                placeholder.Reset();
                frames[index] = placeholder;
            }
        }

        if (isEvilInterface)
        {
            for (int index = 0; index < frames.Length; ++index)
            {
                Sprite evil = frames[index].Clone();
                ImageOps.ApplyPalette(evil.Image, PaletteId.GoodToEvilButton);
                frames[index] = evil;
            }
        }

        return frames;
    }
}
