using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The archive-backed artwork of the thieves' guild report, the five <c>Assets::getImage()</c> families
/// <c>dialog_thievesguild.cpp</c> reads.
/// </summary>
/// <remarks>
/// <para>
/// 参考直接 <c>Assets::getImage( ICN::xxx, frame )</c>；端口把整份档案读进来按帧号取：
/// <c>STONEBAK</c>(或 <c>STONEBAK_EVIL</c>) 第 0 帧是整张底纹，
/// <c>TOWNWIND</c> 第 <c>22 + 颜色序号</c> 帧是那面旗，
/// <c>LOCATORS</c>(或 <c>LOCATORE</c>) 第 22 帧是英雄肖像外面的小窗框，
/// <c>MINIPORT</c> 第 <c>英雄编号 - 1</c> 帧是小肖像，
/// <c>MONS32</c> 第 <c>生物编号 - 1</c> 帧是最强兵图标，
/// <c>SMALLBAR</c> 第 0 帧是底栏，EXIT 沿用魔法塔/水井的那两颗（原版 AGG 里没有 <c>BUTTON_GUILDWELL_EXIT</c>）。
/// </para>
/// <para>
/// 缺任何一份都返回 <see langword="null"/>：这张表整屏都是图，少一张就没有意义。
/// </para>
/// </remarks>
public sealed class ThievesGuildArtwork
{
    /// <summary>Frame of <c>TOWNWIND</c> that carries the first player flag, <c>22</c>.</summary>
    public const int FirstFlagFrame = 22;

    /// <summary>How many player colours the game supports, <c>6</c>.</summary>
    public const int FlagFrames = 6;

    /// <summary>Frame of <c>LOCATORS</c>/<c>LOCATORE</c> the hero portrait sits in, <c>22</c>.</summary>
    public const int HeroWindowFrame = 22;

    private ThievesGuildArtwork(
        Sprite background,
        IReadOnlyList<Sprite> flags,
        Sprite heroWindow,
        Sprite bar,
        IReadOnlyList<Sprite> portraits,
        IReadOnlyList<Sprite> monsters,
        Sprite exitReleased,
        Sprite exitPressed)
    {
        Background = background;
        Flags = flags;
        HeroWindow = heroWindow;
        Bar = bar;
        Portraits = portraits;
        Monsters = monsters;
        ExitReleased = exitReleased;
        ExitPressed = exitPressed;
    }

    /// <summary>The whole 640x480 stone plate.</summary>
    public Sprite Background { get; }

    /// <summary><c>TOWNWIND</c> frames 22…27, indexed by <c>PlayerColors.GetIndex</c>.</summary>
    public IReadOnlyList<Sprite> Flags { get; }

    /// <summary>The window frame a small portrait is drawn into.</summary>
    public Sprite HeroWindow { get; }

    /// <summary>The status bar at the bottom.</summary>
    public Sprite Bar { get; }

    /// <summary><c>MINIPORT</c>: the small portraits, frame <c>heroId - 1</c>.</summary>
    public IReadOnlyList<Sprite> Portraits { get; }

    /// <summary><c>MONS32</c>: the mini monsters, frame <c>monsterId - 1</c>.</summary>
    public IReadOnlyList<Sprite> Monsters { get; }

    /// <summary>The exit button, idle.</summary>
    public Sprite ExitReleased { get; }

    /// <summary>The exit button, held down.</summary>
    public Sprite ExitPressed { get; }

    /// <summary>Reads the artwork of one interface style.</summary>
    /// <param name="assets">The archives.</param>
    /// <param name="evilInterface">Whether the evil stone plate and window frames are wanted.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive is missing frames.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static ThievesGuildArtwork? TryCreate(AssetBundle assets, bool evilInterface = false)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return TryCreate(
            Load(assets, evilInterface ? IcnType.StonebakEvil : IcnType.Stonebak),
            Load(assets, IcnType.Townwind),
            Load(assets, evilInterface ? IcnType.Locatore : IcnType.Locators),
            Load(assets, IcnType.Smallbar),
            Load(assets, IcnType.Miniport),
            Load(assets, IcnType.Mons32),
            Load(assets, MageGuildArtwork.GetExitButtonIcn()));
    }

    /// <summary>Picks the frames the report needs out of the raw archives.</summary>
    /// <param name="background"><c>STONEBAK</c>; only frame 0 is used.</param>
    /// <param name="townwind"><c>TOWNWIND</c>; frames 22…27 are the flags.</param>
    /// <param name="locators"><c>LOCATORS</c>/<c>LOCATORE</c>; frame 22 is the portrait window.</param>
    /// <param name="bar"><c>SMALLBAR</c>; only frame 0 is used.</param>
    /// <param name="portraits"><c>MINIPORT</c>.</param>
    /// <param name="monsters"><c>MONS32</c>.</param>
    /// <param name="exit">Idle and held frames of the exit button.</param>
    /// <returns>The artwork, or <see langword="null"/> when a list is missing or too short.</returns>
    public static ThievesGuildArtwork? TryCreate(
        IReadOnlyList<Sprite>? background,
        IReadOnlyList<Sprite>? townwind,
        IReadOnlyList<Sprite>? locators,
        IReadOnlyList<Sprite>? bar,
        IReadOnlyList<Sprite>? portraits,
        IReadOnlyList<Sprite>? monsters,
        IReadOnlyList<Sprite>? exit)
    {
        if (background is not { Count: > 0 }
            || townwind is null || townwind.Count < FirstFlagFrame + FlagFrames
            || locators is null || locators.Count <= HeroWindowFrame
            || bar is not { Count: > 0 }
            || portraits is not { Count: > 0 }
            || monsters is not { Count: > 0 }
            || exit is null || exit.Count < 2)
        {
            return null;
        }

        Sprite[] flags = new Sprite[FlagFrames];

        for (int i = 0; i < FlagFrames; i++)
        {
            flags[i] = townwind[FirstFlagFrame + i];
        }

        return new ThievesGuildArtwork(
            background[0],
            flags,
            locators[HeroWindowFrame],
            bar[0],
            portraits,
            monsters,
            exit[0],
            exit[1]);
    }

    /// <summary>Reads a whole archive; a missing one leaves the caller with a <see langword="null"/> list.</summary>
    private static IReadOnlyList<Sprite>? Load(AssetBundle assets, IcnType type) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(type))?.Sprites;
}
