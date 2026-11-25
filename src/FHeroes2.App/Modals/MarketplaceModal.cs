using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.App.Modals;

/// <summary>
/// The marketplace run as a window of the adventure map, <c>Dialog::Marketplace( hero.GetKingdom(), true )</c> of the
/// trading post (<c>heroes_action.cpp:1288</c>).
/// </summary>
/// <remarks>
/// <para>
/// 城镇屏里的市场是城堡屏的一个弹层（<see cref="CastleScene"/> 自己转它的帧），地图上的交易站没有第二个场景位，
/// 只能像招募窗那样借宿主的内层循环。窗口本身带整块背景，关窗后宿主每帧先重画场景再叠窗口，不需要留底。
/// </para>
/// <para>
/// 两种提示框（右键资源格看日收入、点市场图标看汇率）在参考里是从窗口的循环里再开一层模态循环，这里改由
/// <see cref="ModalMessage"/> 画在窗口上面：<c>Dialog::ZERO</c> 跟着右键松开消失，汇率框按一下收起。
/// </para>
/// </remarks>
internal sealed class MarketplaceModal : IModalScreen
{
    private readonly MarketplaceWindow _window;
    private readonly World _world;
    private readonly PlayerKingdom _kingdom;
    private readonly AssetBundle _assets;
    private readonly GameFonts _fonts;

    private ModalMessage? _message;
    private bool? _hasMessageFrame;
    private IReadOnlyList<Sprite>? _resourceIcons;

    /// <summary>Wraps an already built marketplace window.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="world">The map the kingdom plays on, needed for the daily income of the mines.</param>
    /// <param name="kingdom">The kingdom whose resources are traded.</param>
    /// <param name="assets">The archives the frames of the message boxes come from.</param>
    /// <param name="fonts">The fonts of the message boxes.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MarketplaceModal(
        MarketplaceWindow window,
        World world,
        PlayerKingdom kingdom,
        AssetBundle assets,
        GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(fonts);

        _window = window;
        _world = world;
        _kingdom = kingdom;
        _assets = assets;
        _fonts = fonts;
    }

    /// <summary>The window that was run; its <see cref="MarketplaceWindow.Dialog"/> holds the trades it made.</summary>
    public MarketplaceWindow Window => _window;

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
        // 提示框是模态的：它开着时点一下先收起它，市场窗口收不到这一下。
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
    /// <remarks>
    /// 提示框开着时市场窗口停在参考的 <c>showStandardTextMessage</c> 循环里，只有提示框自己的元素动；
    /// 买卖只改王国金库，地图上的资源栏每帧重画，所以不需要额外的刷新。
    /// </remarks>
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

    /// <summary>Acts on what the window asked for, <c>dialog_marketplace.cpp:385-685</c>.</summary>
    /// <param name="action">What the window just produced.</param>
    private void Follow(MarketplaceAction action)
    {
        switch (action)
        {
            case MarketplaceAction.Exit:
                GameLog.Info($"Closing the marketplace of the {_kingdom.Color} kingdom.");

                break;

            case MarketplaceAction.Trade:
                // 金库已经在这一帧改过；地图的资源栏每帧从王国重画，下次绘制就是新的数字。
                GameLog.Info($"A trade went through in the marketplace of the {_kingdom.Color} kingdom.");

                break;

            case MarketplaceAction.ResourceIncome:
                ShowIncome(heldByRightButton: true);

                break;

            case MarketplaceAction.ExchangeRate:
                ShowExchangeRate();

                break;

            case MarketplaceAction.Gift:
                GameLog.Warn("The gift window is not ported yet; the press is ignored.");

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Shows the daily income of the kingdom, <c>showKingdomIncome()</c> behind a right click on a resource
    /// (<c>dialog_marketplace.cpp:657-666</c>).
    /// </summary>
    /// <param name="heldByRightButton">Whether the box lives only while the right button is held.</param>
    private void ShowIncome(bool heldByRightButton)
    {
        if (ResourceIcons() is not { } icons || !HasMessageFrame())
        {
            GameLog.Warn("The income window needs RESOURCE and the message frame, and they were not loaded.");

            return;
        }

        Funds income = _kingdom.GetIncome(resource => _world.CountCapturedMines(resource, _kingdom.Color));

        Show(KingdomIncome.Create(income, icons, _fonts.Yellow, _fonts.Normal), heldByRightButton);
    }

    /// <summary>
    /// Shows the exchange rate sentence, <c>showStandardTextMessage( _( "Marketplace" ), getExchangeRateText() )</c>
    /// (<c>dialog_marketplace.cpp:596-604</c>).
    /// </summary>
    private void ShowExchangeRate()
    {
        if (!HasMessageFrame())
        {
            GameLog.Warn("The exchange rate window needs the message frame, and it was not loaded.");

            return;
        }

        Show(
            MessageBox.Create(
                MarketplaceView.HeaderText,
                _window.Dialog.ExchangeRateText,
                _fonts.Yellow,
                _fonts.Normal),
            heldByRightButton: false);
    }

    /// <summary>Puts a box over the window; the next press or ESC takes it down again.</summary>
    /// <param name="box">The box to show.</param>
    /// <param name="heldByRightButton">Whether it lives only while the right button is held.</param>
    private void Show(MessageBox box, bool heldByRightButton) => _message = new ModalMessage(box, heldByRightButton);

    /// <summary>
    /// Whether the archive can provide the message frame every box is drawn in, checked once. The pieces
    /// themselves are read by <see cref="FrameBox"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the frame is complete.</returns>
    private bool HasMessageFrame()
    {
        _hasMessageFrame ??= _assets.TryReadIcn(FrameBox.ArtworkFileName) is { } file &&
            file.Count >= FrameBox.RequiredSpriteCount;

        return _hasMessageFrame == true;
    }

    /// <summary>Frames of <c>RESOURCE.ICN</c>, loaded once for the income box.</summary>
    /// <returns>The sprites, or <see langword="null"/> when the archive is missing.</returns>
    private IReadOnlyList<Sprite>? ResourceIcons() =>
        _resourceIcons ??= _assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Resource))?.Sprites;
}
