using FHeroes2.App.Localization;
using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// Builds the ten button frames the campaign selection screen draws, <c>createCampaignButtonSet()</c>
/// (<c>game_assets.cpp:544-577</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>GOOD/EVIL/POL_CAMPAIGN_BUTTONS</c> 三张表都不在 AGG 里，参考在运行时现做：五条文案各出一对「抬起/按下」，
/// 底座用 <c>EMPTY_*_BUTTON</c>，底纹用对应的 <c>STONEBAK*</c>。端口同样现做，好处是按钮上的字跟着
/// <see cref="GameText"/> 走翻译，与其它现生成的按钮一致。
/// </para>
/// <para>
/// 帧序是对话框要求的：0/1 VIEW INTRO、2/3 RESTART、4/5 OKAY、6/7 CANCEL、8/9 DIFFICULTY。
/// </para>
/// </remarks>
internal static class CampaignButtonsArtwork
{
    /// <summary>Message of the View Intro button, <c>_( "VIEW INTRO" )</c>.</summary>
    public const string ViewIntroLabelId = "VIEW INTRO";

    /// <summary>Message of the Restart button, <c>_( "RESTART" )</c>.</summary>
    public const string RestartLabelId = "RESTART";

    /// <summary>Message of the Okay button, <c>_( "OKAY" )</c>.</summary>
    public const string OkayLabelId = "OKAY";

    /// <summary>Message of the Cancel button, <c>_( "CANCEL" )</c>.</summary>
    public const string CancelLabelId = "CANCEL";

    /// <summary>Message of the Difficulty button, <c>_( "DIFFICULTY" )</c>.</summary>
    public const string DifficultyLabelId = "DIFFICULTY";

    /// <summary>Number of frames of one set, five buttons of two states.</summary>
    public const int FrameCount = 10;

    /// <summary>
    /// Builds the ten frames of one campaign button set.
    /// </summary>
    /// <param name="assets">Where the plates and the stone texture come from.</param>
    /// <param name="text">Translations of the five labels.</param>
    /// <param name="buttonSet">One of the three generated sets, see <c>CampaignScenarioSelectionDialog.GetButtonSet</c>.</param>
    /// <returns>The frames, or <see langword="null"/> when the stone texture is not installed.</returns>
    public static Sprite[]? TryCreate(AssetBundle assets, GameText text, IcnType buttonSet)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(text);

        (DialogButtonTemplate template, IcnType stoneType) = buttonSet switch
        {
            IcnType.EvilCampaignButtons => (DialogButtonTemplate.Evil, IcnType.StonebakEvil),
            IcnType.PolCampaignButtons => (DialogButtonTemplate.Pol, IcnType.StonebakSmallPol),
            _ => (DialogButtonTemplate.Good, IcnType.Stonebak),
        };

        IcnSpriteFile? stone = assets.TryReadIcn(IcnCatalog.GetFileName(stoneType));

        if (stone is null || stone.Count == 0)
        {
            GameLog.Warn(
                $"The campaign buttons need {IcnCatalog.GetFileName(stoneType)}, and it is missing.");

            return null;
        }

        string[] labels =
        [
            text.Get(ViewIntroLabelId),
            text.Get(RestartLabelId),
            text.Get(OkayLabelId),
            text.Get(CancelLabelId),
            text.Get(DifficultyLabelId),
        ];

        Sprite[] frames = new Sprite[FrameCount];

        for (int index = 0; index < labels.Length; ++index)
        {
            (frames[2 * index], frames[(2 * index) + 1]) =
                ButtonFactory.GetTextAdaptedSprite(labels[index], template, stone.Sprites[0]);
        }

        return frames;
    }
}
