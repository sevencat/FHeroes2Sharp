using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The three archives the high score screen is made of, <c>game_highscores.cpp</c>: <c>HSBKG</c> (the background),
/// <c>HISCORE</c> (the titles and the six vertical buttons) and <c>MINIMON</c> (the creatures of the rows).
/// </summary>
/// <remarks>
/// <para>
/// 参考的垂直按钮不是档案里的成品：<c>createHighScoresButtonSet()</c>（<c>game_assets.cpp:1432-1482</c>）把
/// <c>HISCORE</c> 的 0…5 帧擦掉原来的字再按当前语言重画。端口不做这一步：<c>HISCORE</c> 原版那六帧本来就带着
/// 竖排的 <c>CAMPAIGN</c> / <c>STANDARD</c> / <c>EXIT</c>，直接当按钮的两种状态用，像素与参考的英文版本一致。
/// </para>
/// <para>
/// 标题是 6、7 两帧（标准榜 / 战役榜），<c>:360-363</c> 里的 <c>titleImageIndex</c>。兵种图是 <c>MINIMON</c>：
/// 每只九帧，第 <c>monsterId - 1</c> 组的九帧是站立、六帧呼吸与两帧攻击。
/// </para>
/// </remarks>
public sealed class HighScoresArtwork
{
    /// <summary>How many frames one creature takes in <c>MINIMON</c>, <c>Monster::GetSpriteIndex() * 9</c>.</summary>
    public const int FramesPerMonster = 9;

    /// <summary>Frame of <c>HISCORE</c> that carries the standard title, <c>6</c>.</summary>
    public const int StandardTitleFrame = 6;

    /// <summary>Frame of <c>HISCORE</c> that carries the campaign title, <c>7</c>.</summary>
    public const int CampaignTitleFrame = 7;

    /// <summary>Frames of <c>HISCORE</c> that carry the released and held campaign button, <c>0</c> and <c>1</c>.</summary>
    public const int CampaignButtonFrame = 0;

    /// <summary>Frames of <c>HISCORE</c> that carry the released and held standard button, <c>2</c> and <c>3</c>.</summary>
    public const int StandardButtonFrame = 2;

    /// <summary>Frames of <c>HISCORE</c> that carry the released and held exit button, <c>4</c> and <c>5</c>.</summary>
    public const int ExitButtonFrame = 4;

    /// <summary>How many frames <c>HISCORE</c> has to have for the titles and the buttons, <c>8</c>.</summary>
    public const int HiscoreFrames = 8;

    private HighScoresArtwork(
        Sprite background,
        Sprite standardTitle,
        Sprite campaignTitle,
        IReadOnlyList<Sprite> standardButton,
        IReadOnlyList<Sprite> campaignButton,
        IReadOnlyList<Sprite> exitButton,
        IReadOnlyList<Sprite> monsters)
    {
        Background = background;
        StandardTitle = standardTitle;
        CampaignTitle = campaignTitle;
        StandardButton = standardButton;
        CampaignButton = campaignButton;
        ExitButton = exitButton;
        Monsters = monsters;
    }

    /// <summary>The 640x480 background, <c>ICN::HSBKG</c> frame 0.</summary>
    public Sprite Background { get; }

    /// <summary>The \"High Scores\" title of the standard table, <c>ICN::HISCORE</c> frame 6.</summary>
    public Sprite StandardTitle { get; }

    /// <summary>The \"Campaign High Scores\" title, <c>ICN::HISCORE</c> frame 7.</summary>
    public Sprite CampaignTitle { get; }

    /// <summary>The released and held sprites of the table switch button, <c>HISCORE</c> frames 2 and 3.</summary>
    public IReadOnlyList<Sprite> StandardButton { get; }

    /// <summary>The released and held sprites of the campaign table button, <c>HISCORE</c> frames 0 and 1.</summary>
    public IReadOnlyList<Sprite> CampaignButton { get; }

    /// <summary>The released and held sprites of the exit button, <c>HISCORE</c> frames 4 and 5.</summary>
    public IReadOnlyList<Sprite> ExitButton { get; }

    /// <summary>The frames of <c>MINIMON</c>, nine per creature.</summary>
    public IReadOnlyList<Sprite> Monsters { get; }

    /// <summary>Reads the three archives.</summary>
    /// <param name="assets">The archives of the installed game data.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static HighScoresArtwork? TryCreate(AssetBundle assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return TryCreate(
            assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Hsbkg))?.Sprites,
            assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Hiscore))?.Sprites,
            assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Minimon))?.Sprites);
    }

    /// <summary>Picks the frames the screen needs out of the raw archives.</summary>
    /// <param name="background"><c>HSBKG</c>.</param>
    /// <param name="hiscore"><c>HISCORE</c>, at least <see cref="HiscoreFrames"/> frames.</param>
    /// <param name="monsters"><c>MINIMON</c>, at least nine frames per creature.</param>
    /// <returns>The artwork, or <see langword="null"/> when a list is missing or too short.</returns>
    public static HighScoresArtwork? TryCreate(
        IReadOnlyList<Sprite>? background,
        IReadOnlyList<Sprite>? hiscore,
        IReadOnlyList<Sprite>? monsters)
    {
        if (background is not { Count: > 0 }
            || hiscore is null || hiscore.Count < HiscoreFrames
            || monsters is null || monsters.Count < FramesPerMonster)
        {
            return null;
        }

        return new HighScoresArtwork(
            background[0],
            hiscore[StandardTitleFrame],
            hiscore[CampaignTitleFrame],
            [hiscore[StandardButtonFrame], hiscore[StandardButtonFrame + 1]],
            [hiscore[CampaignButtonFrame], hiscore[CampaignButtonFrame + 1]],
            [hiscore[ExitButtonFrame], hiscore[ExitButtonFrame + 1]],
            monsters);
    }
}
