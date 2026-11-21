using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork the virtual keyboard is composed of: the window frame, the plate the letter keys are
/// drawn on, the frame of a single line text input and the okay button.
/// </summary>
/// <remarks>
/// <para>
/// 参考在 <c>openVirtualKeyboard()</c> / <c>openVirtualNumpad()</c> 里就地取图
/// （<c>ui_keyboard.cpp:1190-1200</c>、<c>1318-1328</c>）：<c>ICN::STONEBAK</c> 是字母键的底板，
/// <c>ICN::REQBKG</c> 的第 0 帧是单行输入框的边框，确定键则来自 <c>ICN::BUTTON_SMALL_OKAY_GOOD</c>。
/// 端口把这些图先装进 <see cref="VirtualKeyboardArtwork"/>，对话框本身不碰资源层，测试可以直接给图。
/// </para>
/// <para>
/// 窗口框不在这里：<see cref="StandardWindow"/> 按 <c>Settings.IsEvilInterfaceEnabled</c> 自己取图，
/// 与参考的窗口一致；这一层只管按键底板、输入框和确定键。
/// </para>
/// </remarks>
public sealed class VirtualKeyboardArtwork
{
    private VirtualKeyboardArtwork(
        Sprite buttonBackground,
        Sprite requestBackground,
        Sprite okayReleased,
        Sprite okayPressed)
    {
        ButtonBackground = buttonBackground;
        RequestBackground = requestBackground;
        OkayReleased = okayReleased;
        OkayPressed = okayPressed;
    }

    /// <summary>File name of the plate the letter keys are drawn on, <c>ICN::STONEBAK</c>.</summary>
    public static string ButtonBackgroundAssetName => IcnCatalog.GetFileName(IcnType.Stonebak);

    /// <summary>File name of the evil plate, <c>ICN::STONEBAK_EVIL</c>.</summary>
    public static string EvilButtonBackgroundAssetName => IcnCatalog.GetFileName(IcnType.StonebakEvil);

    /// <summary>File name of the text input frames, <c>ICN::REQBKG</c>.</summary>
    public static string RequestBackgroundAssetName => IcnCatalog.GetFileName(IcnType.Reqbkg);

    /// <summary>
    /// File name of the okay button, <c>ICN::BUTTON_SMALL_OKAY_GOOD</c>, here the original plate
    /// <c>SPANBTN.ICN</c>.
    /// </summary>
    /// <remarks>
    /// <c>BUTTON_SMALL_OKAY_GOOD</c> 不是一个档案里的图：参考在 <c>game_assets.cpp:1042-1054</c> 里用
    /// 「空白底板 + 贴文字」现做，或者（<c>useOriginalResources()</c> 分支）直接取 <c>SPANBTN.ICN</c>。
    /// 端口的资源层只有档案帧，所以走原始资源那条路，确定键上「OKAY」是图上本来就有的。
    /// </remarks>
    public static string OkayButtonAssetName => IcnCatalog.GetFileName(IcnType.Spanbtn);

    /// <summary>File name of the evil okay button, <c>ICN::BUTTON_SMALL_OKAY_EVIL</c>, here <c>SPANBTNE.ICN</c>.</summary>
    public static string EvilOkayButtonAssetName => IcnCatalog.GetFileName(IcnType.Spanbtne);

    /// <summary>Frame 0 of <c>ICN::STONEBAK</c>, the plate of a letter key.</summary>
    public Sprite ButtonBackground { get; }

    /// <summary>Frame 0 of <c>ICN::REQBKG</c>, the frame of a single line text input.</summary>
    public Sprite RequestBackground { get; }

    /// <summary>Frame 0 of the okay button, <c>SPANBTN.ICN</c>.</summary>
    public Sprite OkayReleased { get; }

    /// <summary>Frame 1 of the okay button, the pressed look.</summary>
    public Sprite OkayPressed { get; }

    /// <summary>
    /// Takes the sprites the keyboard needs, <c>ui_keyboard.cpp:1190-1199</c> and
    /// <c>ui_keyboard.cpp:1318-1327</c>.
    /// </summary>
    /// <param name="buttonBackground">Frame 0 of STONEBAK or STONEBAK_EVIL.</param>
    /// <param name="requestBackground">Frame 0 of REQBKG.</param>
    /// <param name="okayButton">
    /// The two frames of BUTTON_SMALL_OKAY_GOOD or BUTTON_SMALL_OKAY_EVIL: released first,
    /// pressed second.
    /// </param>
    /// <returns>The artwork, or <see langword="null"/> when a sprite is missing.</returns>
    public static VirtualKeyboardArtwork? TryCreate(
        Sprite? buttonBackground,
        Sprite? requestBackground,
        IReadOnlyList<Sprite>? okayButton)
    {
        if (buttonBackground is null || requestBackground is null ||
            okayButton is null || okayButton.Count < 2)
        {
            return null;
        }

        return new VirtualKeyboardArtwork(buttonBackground, requestBackground, okayButton[0], okayButton[1]);
    }
}
