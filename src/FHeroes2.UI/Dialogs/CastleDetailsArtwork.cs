using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork the editor's castle details window is assembled from,
/// <c>castleDetailsDialog()</c> (<c>editor_castle_details_window.cpp:625-798</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口本身是满屏的标准窗（<see cref="CastleDetailsFrame.CreateWindow"/>），里面不再铺石头底，而是左右两半各用一份
/// 素材：左边 <c>CASLWIND</c> 的前 438 像素（城镇建造窗的整块面板），右边 <c>STONEBAK</c> 从第 438 列起补齐，
/// 接缝靠 <c>CASLWIND</c> 自己那两条 4 像素横线盖住。
/// </para>
/// <para>
/// 名字条与屏幕底部的状态栏是同一份 <c>CASLBAR</c>：名字条取它的第 17 列起的 170 像素，状态栏取左右两头拼在
/// OKAY 与 EXIT 之间。
/// </para>
/// <para>
/// OKAY、EXIT、RESET 这三个按钮在存档里没有对应的 <c>.ICN</c>：参考在
/// <c>game_assets.cpp</c> 里用空板子加文字现场合成（<c>BUTTON_OKAY_TOWN</c> 与 <c>BUTTON_GUILDWELL_EXIT</c> 走
/// <c>EMPTY_GUILDWELL_BUTTON</c>，<c>BUTTON_RESET_*</c> 走 <c>EMPTY_GOOD_BUTTON</c> / <c>EMPTY_EVIL_BUTTON</c> 加
/// <c>STONEBAK</c>）。端口照这条路径调 <see cref="ButtonFactory.GetTextAdaptedSprite"/>，而不是去读那几个
/// 并不存在的 .ICN——第一版照着参考的 <c>ICN::</c> 编号去读，结果 <c>TryReadIcn</c> 全部返回空。
/// </para>
/// </remarks>
public sealed class CastleDetailsArtwork
{
    /// <summary>Label of the OKAY button, <c>gettext_noop( "OKAY" )</c> of <c>game_assets.cpp</c>.</summary>
    public const string OkayText = "OKAY";

    /// <summary>Label of the EXIT button, <c>gettext_noop( "EXIT" )</c> of <c>game_assets.cpp</c>.</summary>
    public const string ExitText = "EXIT";

    /// <summary>Label of the two reset buttons, <c>gettext_noop( "RESET" )</c>.</summary>
    public const string ResetText = "RESET";

    /// <summary>Frame index of every background piece and of the captain's frame, <c>0</c>.</summary>
    public const int BackgroundFrameIndex = 0;

    private CastleDetailsArtwork(
        Sprite constructionBackground,
        Sprite stoneBackground,
        Sprite statusBar,
        Sprite okayReleased,
        Sprite okayPressed,
        Sprite exitReleased,
        Sprite exitPressed,
        Sprite resetReleased,
        Sprite resetPressed)
    {
        ConstructionBackground = constructionBackground;
        StoneBackground = stoneBackground;
        StatusBar = statusBar;
        OkayReleased = okayReleased;
        OkayPressed = okayPressed;
        ExitReleased = exitReleased;
        ExitPressed = exitPressed;
        ResetReleased = resetReleased;
        ResetPressed = resetPressed;
    }

    /// <summary>
    /// Frame 0 of <c>CASLWIND</c> (or of <c>CASLWIND_EVIL</c>): the left 438 pixels of the window and the
    /// two horizontal separators.
    /// </summary>
    public Sprite ConstructionBackground { get; }

    /// <summary>Frame 0 of <c>STONEBAK</c> (or of <c>STONEBAK_EVIL</c>): the texture of the right panel.</summary>
    public Sprite StoneBackground { get; }

    /// <summary>Frame 0 of <c>CASLBAR</c>, used both for the name bar and for the status bar.</summary>
    public Sprite StatusBar { get; }

    /// <summary>Released look of the OKAY button, frame 0 of <c>BUTTON_OKAY_TOWN</c>.</summary>
    public Sprite OkayReleased { get; }

    /// <summary>Pressed look of the OKAY button.</summary>
    public Sprite OkayPressed { get; }

    /// <summary>Released look of the EXIT button, frame 0 of <c>BUTTON_GUILDWELL_EXIT</c>.</summary>
    public Sprite ExitReleased { get; }

    /// <summary>Pressed look of the EXIT button.</summary>
    public Sprite ExitPressed { get; }

    /// <summary>Released look of a reset button, frame 0 of <c>BUTTON_RESET_GOOD</c> or <c>_EVIL</c>.</summary>
    public Sprite ResetReleased { get; }

    /// <summary>Pressed look of a reset button.</summary>
    public Sprite ResetPressed { get; }

    /// <summary>
    /// Loads the artwork of the window.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="isEvilInterface">Whether the gray <c>*_EVIL</c> variants are used.</param>
    /// <returns>The artwork, or <see langword="null"/> when an archive or a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static CastleDetailsArtwork? TryCreate(AssetBundle assets, bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(assets);

        Sprite? construction = TryGetSprite(assets, isEvilInterface ? IcnType.CaslwindEvil : IcnType.Caslwind,
            BackgroundFrameIndex);
        Sprite? stone = TryGetSprite(assets, isEvilInterface ? IcnType.StonebakEvil : IcnType.Stonebak,
            BackgroundFrameIndex);
        Sprite? statusBar = TryGetSprite(assets, IcnType.Caslbar, BackgroundFrameIndex);

        if (construction is null || stone is null || statusBar is null)
        {
            return null;
        }

        // 三个按钮都是现场合成的：OKAY 与 EXIT 用井栏那块窄板，RESET 用主题板加石头底。
        (Sprite okayReleased, Sprite okayPressed) =
            ButtonFactory.GetTextAdaptedSprite(OkayText, DialogButtonTemplate.GuildWell);
        (Sprite exitReleased, Sprite exitPressed) =
            ButtonFactory.GetTextAdaptedSprite(ExitText, DialogButtonTemplate.GuildWell);
        (Sprite resetReleased, Sprite resetPressed) = ButtonFactory.GetTextAdaptedSprite(ResetText,
            isEvilInterface ? DialogButtonTemplate.Evil : DialogButtonTemplate.Good, stone);

        return new CastleDetailsArtwork(construction, stone, statusBar, okayReleased, okayPressed, exitReleased,
            exitPressed, resetReleased, resetPressed);
    }

    private static Sprite? TryGetSprite(AssetBundle assets, IcnType type, int index)
    {
        if (assets.TryReadIcn(IcnCatalog.GetFileName(type)) is not { } file || file.Count <= index)
        {
            return null;
        }

        return file.Sprites[index];
    }
}
