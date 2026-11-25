using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The standard OKAY / CANCEL / YES / NO buttons of <c>SYSTEM.ICN</c>, the four every
/// <c>Dialog::FrameBox</c> draws at the bottom of its frame.
/// </summary>
/// <remarks>
/// <para>
/// 帧号与 <see cref="SettingsArtwork"/> 同源：1/2 是常态与按下的 OKAY，3/4 是常态与按下的 CANCEL。
/// 设置对话框只用 OKAY，所以自己留了一份；这里四个都要，就单独装一个。
/// </para>
/// <para>
/// YES/NO 没有自己的归档：<c>BUTTON_SMALL_YES_GOOD</c> / <c>BUTTON_SMALL_NO_GOOD</c> 在参考里是
/// <c>game_assets.cpp</c> 现拼出来的「自定义 ICN」，用原版资源时它们就是 <c>SYSTEM.ICN</c> 的 5/6 与 7/8 号帧
/// （<c>useOriginalResources()</c> 分支）。之前按归档名去读，读到的是不存在的 <c>CUSTOM</c>，于是
/// YES/NO 都退化成 OKAY——冒烟里撤退确认框两个按钮都写着 OKAY 就是这个原因。没有 9 帧的 SYSTEM
/// 才退回 OKAY，至少对话框还画得出来。
/// </para>
/// </remarks>
internal sealed class SystemButtonArtwork : IDialogButtonArtworkSource
{
    /// <summary>Archive entry that holds the standard OKAY and CANCEL buttons.</summary>
    internal const string AssetName = "SYSTEM.ICN";

    /// <summary>Frame of the released OKAY button.</summary>
    internal const int OkayReleasedFrame = 1;

    /// <summary>Frame of the pressed OKAY button.</summary>
    internal const int OkayPressedFrame = 2;

    /// <summary>Frame of the released CANCEL button.</summary>
    internal const int CancelReleasedFrame = 3;

    /// <summary>Frame of the pressed CANCEL button.</summary>
    internal const int CancelPressedFrame = 4;

    /// <summary>Frame of the released YES button, <c>Assets::getImage( ICN::SYSTEM, 5 )</c>.</summary>
    internal const int YesReleasedFrame = 5;

    /// <summary>Frame of the pressed YES button, <c>Assets::getImage( ICN::SYSTEM, 6 )</c>.</summary>
    internal const int YesPressedFrame = 6;

    /// <summary>Frame of the released NO button, <c>Assets::getImage( ICN::SYSTEM, 7 )</c>.</summary>
    internal const int NoReleasedFrame = 7;

    /// <summary>Frame of the pressed NO button, <c>Assets::getImage( ICN::SYSTEM, 8 )</c>.</summary>
    internal const int NoPressedFrame = 8;

    private readonly Sprite _okayReleased;
    private readonly Sprite _okayPressed;
    private readonly Sprite _cancelReleased;
    private readonly Sprite _cancelPressed;
    private readonly DialogButtonArtwork? _yes;
    private readonly DialogButtonArtwork? _no;

    private SystemButtonArtwork(Sprite okayReleased, Sprite okayPressed, Sprite cancelReleased,
        Sprite cancelPressed, DialogButtonArtwork? yes, DialogButtonArtwork? no)
    {
        _okayReleased = okayReleased;
        _okayPressed = okayPressed;
        _cancelReleased = cancelReleased;
        _cancelPressed = cancelPressed;
        _yes = yes;
        _no = no;
    }

    /// <summary>Returns the pair of frames a button stands for.</summary>
    /// <param name="button">The button to draw.</param>
    /// <returns>Released and pressed frames.</returns>
    /// <remarks>
    /// YES/NO 用的是 <c>SYSTEM.ICN</c> 的 5/6 与 7/8 号帧；归档不足 9 帧时退回 OKAY，
    /// 与参考在缺资源时的做法一致（按钮只是长得不对，框还是能关）。
    /// </remarks>
    public DialogButtonArtwork Get(DialogButton button) => button switch
    {
        DialogButton.Cancel => new DialogButtonArtwork(_cancelReleased, _cancelPressed),
        DialogButton.Yes when _yes is { } yes => yes,
        DialogButton.No when _no is { } no => no,
        _ => new DialogButtonArtwork(_okayReleased, _okayPressed),
    };

    /// <summary>Loads the four system frames and the two yes/no pairs from the archives.</summary>
    /// <param name="assets">The loaded archives.</param>
    /// <returns>The artwork, or <see langword="null"/> when <c>SYSTEM.ICN</c> is missing or too short.</returns>
    internal static SystemButtonArtwork? TryCreate(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        try
        {
            if (assets.TryReadIcn(AssetName) is not { } system || system.Count <= CancelPressedFrame)
            {
                GameLog.Warn($"The standard buttons of '{AssetName}' are missing; the dialog cannot be drawn.");

                return null;
            }

            return new SystemButtonArtwork(
                system.Sprites[OkayReleasedFrame],
                system.Sprites[OkayPressedFrame],
                system.Sprites[CancelReleasedFrame],
                system.Sprites[CancelPressedFrame],
                TryReadPair(system, YesReleasedFrame, YesPressedFrame, AssetName),
                TryReadPair(system, NoReleasedFrame, NoPressedFrame, AssetName));
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The standard buttons of '{AssetName}' could not be read: {exception.Message}");

            return null;
        }
    }

    /// <summary>Takes two frames of an already decoded archive as one button.</summary>
    /// <param name="file">The archive.</param>
    /// <param name="releasedFrame">Frame of the released look.</param>
    /// <param name="pressedFrame">Frame of the pressed look.</param>
    /// <param name="assetName">Name of the archive, only for the diagnostics.</param>
    /// <returns>The pair, or <see langword="null"/> when the archive holds fewer frames.</returns>
    private static DialogButtonArtwork? TryReadPair(IcnSpriteFile file, int releasedFrame, int pressedFrame,
        string assetName)
    {
        if (file.Count <= Math.Max(releasedFrame, pressedFrame))
        {
            GameLog.Warn($"'{assetName}' has no frames {releasedFrame}/{pressedFrame}; the button falls back to OKAY.");

            return null;
        }

        return new DialogButtonArtwork(file.Sprites[releasedFrame], file.Sprites[pressedFrame]);
    }
}
