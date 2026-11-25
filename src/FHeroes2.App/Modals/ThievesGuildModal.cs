using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The thieves' guild report run as a window of the adventure map, <c>Dialog::ThievesGuild( true )</c> of the oracle
/// (<c>heroes_action.cpp:3039</c>).
/// </summary>
/// <remarks>
/// <para>
/// 城镇屏里的盗贼公会报告同样是城堡屏的一个弹层，地图上的预言所没有第二个场景位，所以借宿主的内层循环，和
/// 招募窗、谜语窗同一条缝。关窗之后不留底：宿主的 <c>Draw</c> 每帧先重画地图再叠窗口。
/// </para>
/// <para>
/// 右键 EXIT 的提示（"Exit this menu."）在参考里是 <c>Dialog::ZERO</c>，这里交给 <see cref="ModalMessage"/> 画在
/// 报告上面，松开右键就消失。
/// </para>
/// </remarks>
internal sealed class ThievesGuildModal : IModalScreen
{
    private readonly ThievesGuildWindow _window;
    private readonly AssetBundle _assets;
    private readonly GameFonts _fonts;

    private ModalMessage? _message;
    private bool? _hasMessageFrame;

    /// <summary>Wraps an already built report window.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="assets">The archives the message frame comes from.</param>
    /// <param name="fonts">The fonts of the message box.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ThievesGuildModal(ThievesGuildWindow window, AssetBundle assets, GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(fonts);

        _window = window;
        _assets = assets;
        _fonts = fonts;
    }

    /// <summary>The window that was run; its <see cref="ThievesGuildWindow.Report"/> is the ranking shown.</summary>
    public ThievesGuildWindow Window => _window;

    /// <inheritdoc />
    public bool IsFinished => _window.IsClosed;

    /// <inheritdoc />
    public void SetCursor(Point? position)
    {
        if (_message is { } message)
        {
            message.SetCursor(position);

            return;
        }

        _window.SetCursor(position);
    }

    /// <inheritdoc />
    public void SetLeftButton(bool down)
    {
        if (_message is { } message)
        {
            message.SetLeftButton(down);

            if (message.IsClosed)
            {
                _message = null;
            }

            return;
        }

        _window.SetLeftButton(down);
    }

    /// <inheritdoc />
    public void SetRightButton(bool down)
    {
        if (_message is { } message)
        {
            message.SetRightButton(down);

            if (message.IsClosed)
            {
                _message = null;
            }

            return;
        }

        _window.SetRightButton(down);
    }

    /// <inheritdoc />
    /// <remarks>ESC 与 Enter 都是「关掉这个窗口」（<c>Game::HotKeyCloseWindow</c>）；提示框开着时先关提示框。</remarks>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (_message is { } message)
        {
            message.HotKey(hotKey);

            if (message.IsClosed)
            {
                _message = null;
            }

            return;
        }

        if (hotKey is ButtonHotKey.Okay or ButtonHotKey.Cancel)
        {
            _window.PressClose();
        }
    }

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels);
        _message?.Draw(frame.Pixels);
    }

    /// <inheritdoc />
    /// <remarks>提示框开着时报告停在参考的 <c>Dialog::ZERO</c> 循环里，只有提示框自己的元素动。</remarks>
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_message is { } message)
        {
            message.Update(frame.Pixels, TimeSpan.FromSeconds(elapsedSeconds));

            return;
        }

        Follow(_window.Update());
    }

    /// <summary>Acts on the answer of the report, <c>dialog_thievesguild.cpp:448-473</c>.</summary>
    /// <param name="action">What the window just produced.</param>
    private void Follow(ThievesGuildAction action)
    {
        switch (action)
        {
            case ThievesGuildAction.Exit:
                GameLog.Info("Closing the thieves' guild report.");

                break;

            case ThievesGuildAction.ExitHelp:
                ShowExitHelp();

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Shows "Exit this menu." while EXIT is right clicked, <c>showStandardTextMessage( _( "Exit this menu." ), …,
    /// Dialog::ZERO )</c> (<c>dialog_thievesguild.cpp:465-470</c>).
    /// </summary>
    private void ShowExitHelp()
    {
        if (!HasMessageFrame())
        {
            GameLog.Warn("The exit help window needs the message frame, and it was not loaded.");

            return;
        }

        _message = new ModalMessage(
            MessageBox.Create("Exit", ThievesGuildView.ExitHelpText, _fonts.Yellow, _fonts.Normal),
            heldByRightButton: true);
    }

    /// <summary>
    /// Whether the archive can provide the message frame the help box is drawn in, checked once. The pieces
    /// themselves are read by <see cref="FrameBox"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the frame is complete.</returns>
    private bool HasMessageFrame()
    {
        _hasMessageFrame ??= _assets.TryReadIcn(FrameBox.ArtworkFileName) is { } file &&
            file.Count >= FrameBox.RequiredSpriteCount;

        return _hasMessageFrame == true;
    }
}
