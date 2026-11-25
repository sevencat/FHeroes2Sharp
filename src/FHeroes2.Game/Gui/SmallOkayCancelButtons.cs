using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The <c>BUTTON_SMALL_CANCEL_*</c> and <c>BUTTON_SMALL_OKAY_*</c> tables of <c>generateDefaultImages</c>
/// (<c>src/fheroes2/game/game_assets.cpp:1020-1063</c>, the original-resources branch).
/// </summary>
/// <remarks>
/// <para>
/// 参考这两对按钮在「非原资材」时是用 <c>EMPTY_*_BUTTON</c> 加上文字现画的；本项目一律走原资材分支
/// （与 <see cref="EmptyButtons"/> 同一做法，见那里的说明），所以这里只转 <c>CPANEL</c> / <c>CPANELE</c>
/// 的 8、9 帧和 <c>SPANBTN</c> / <c>SPANBTNE</c> 的 0、1 帧。
/// </para>
/// <para>
/// 唯一的一步加工是 <c>copyTransformLayer</c>：把按下帧的透明层抄给释放帧，参考的注释说是为了之后画阴影和
/// 贴图时两态透明区一致。
/// </para>
/// </remarks>
public static class SmallOkayCancelButtons
{
    /// <summary>The name <c>BUTTON_SMALL_CANCEL_GOOD</c> is cached under.</summary>
    public const string CancelGoodFileName = "BUTTON_SMALL_CANCEL_GOOD.ICN";

    /// <summary>The name <c>BUTTON_SMALL_CANCEL_EVIL</c> is cached under.</summary>
    public const string CancelEvilFileName = "BUTTON_SMALL_CANCEL_EVIL.ICN";

    /// <summary>The name <c>BUTTON_SMALL_OKAY_GOOD</c> is cached under.</summary>
    public const string OkayGoodFileName = "BUTTON_SMALL_OKAY_GOOD.ICN";

    /// <summary>The name <c>BUTTON_SMALL_OKAY_EVIL</c> is cached under.</summary>
    public const string OkayEvilFileName = "BUTTON_SMALL_OKAY_EVIL.ICN";

    /// <summary>Released frame of <c>SPANBTN</c> / <c>CPANEL</c>.</summary>
    private const int ReleasedFrame = 0;

    /// <summary>Pressed frame of <c>SPANBTN</c> / <c>CPANEL</c>.</summary>
    private const int PressedFrame = 1;

    /// <summary>Released frame of <c>CPANEL</c>; the CANCEL buttons do not start at frame 0.</summary>
    private const int CancelReleasedFrame = 8;

    /// <summary>Pressed frame of <c>CPANEL</c>.</summary>
    private const int CancelPressedFrame = 9;

    /// <summary>
    /// Builds the two frames of one of the four button tables.
    /// </summary>
    /// <param name="fileName">Asset name, one of the constants of this class.</param>
    /// <returns>
    /// The released frame followed by the pressed one, or <see langword="null"/> when
    /// <paramref name="fileName"/> is not one of the four tables or the archive is too short for it.
    /// </returns>
    public static Sprite?[]? Build(string fileName)
    {
        if (string.Equals(fileName, OkayGoodFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildOkay(isEvilInterface: false);
        }

        if (string.Equals(fileName, OkayEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildOkay(isEvilInterface: true);
        }

        if (string.Equals(fileName, CancelGoodFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildCancel(isEvilInterface: false);
        }

        if (string.Equals(fileName, CancelEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildCancel(isEvilInterface: true);
        }

        return null;
    }

    /// <summary><c>BUTTON_SMALL_OKAY_GOOD</c> / <c>BUTTON_SMALL_OKAY_EVIL</c>: <c>SPANBTN</c> (resp.
    /// <c>SPANBTNE</c>) frames 0 and 1.</summary>
    /// <param name="isEvilInterface">Whether the evil table is built.</param>
    private static Sprite?[]? BuildOkay(bool isEvilInterface)
    {
        string originalName = IcnCatalog.GetFileName(isEvilInterface ? IcnType.Spanbtne : IcnType.Spanbtn);

        return BuildFrom(originalName, ReleasedFrame, PressedFrame);
    }

    /// <summary><c>BUTTON_SMALL_CANCEL_GOOD</c> / <c>BUTTON_SMALL_CANCEL_EVIL</c>: <c>CPANEL</c> (resp.
    /// <c>CPANELE</c>) frames 8 and 9.</summary>
    /// <param name="isEvilInterface">Whether the evil table is built.</param>
    private static Sprite?[]? BuildCancel(bool isEvilInterface)
    {
        string originalName = IcnCatalog.GetFileName(isEvilInterface ? IcnType.Cpanele : IcnType.Cpanel);

        return BuildFrom(originalName, CancelReleasedFrame, CancelPressedFrame);
    }

    /// <summary>Takes the two frames of <paramref name="originalName"/> and aligns their transparent areas.</summary>
    /// <param name="originalName">The archive entry the frames come from.</param>
    /// <param name="releasedFrame">Frame of the released state.</param>
    /// <param name="pressedFrame">Frame of the pressed state.</param>
    private static Sprite?[]? BuildFrom(string originalName, int releasedFrame, int pressedFrame)
    {
        if (Assets.GetImageCount(originalName) < pressedFrame + 1)
        {
            return null;
        }

        if (Assets.GetImage(originalName, releasedFrame) is not { } originalReleased ||
            Assets.GetImage(originalName, pressedFrame) is not { } originalPressed)
        {
            return null;
        }

        Sprite released = originalReleased.Clone();
        Sprite pressed = originalPressed.Clone();

        // 参考的 copyTransformLayer( released, pressed )：释放帧拿按下帧的透明层。参考在掩膜没有透明层
        // （整帧不透明）时 assert 后返回，这里跳过；尺寸对不上同样不抄，免得把整块图裁错。
        if (pressed.Transform is not null && released.Width == pressed.Width && released.Height == pressed.Height)
        {
            released = ImageOps.CopyTransformLayer(pressed, released);
        }

        return new Sprite?[] { released, pressed };
    }
}
