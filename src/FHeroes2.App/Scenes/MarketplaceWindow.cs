using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The marketplace window of a town, <c>Dialog::Marketplace()</c> (<c>dialog_marketplace.cpp:380-685</c>).
/// </summary>
/// <remarks>
/// <para>
/// 和魔法塔、水井一样，这一层只做「窗口本体」：素材取自 AGG，排版、兑换率与成交都交给
/// <see cref="MarketplaceDialog"/> / <see cref="MarketplaceView"/>，循环留在 <see cref="CastleScene"/>。
/// 与那两个窗口不同的是它只盖住屏幕中间一块，底下的城镇屏还要看得见（参考的 <c>FrameBox</c> 只铺自己那一块）。
/// </para>
/// <para>
/// 参考在窗口里还会开两个子对话框：右键资源格弹「王国每日收入」（<c>Dialog::ZERO</c>，按住右键期间显示），
/// 点兑换率图标弹兑换率说明（<c>Dialog::OK</c>）；两者都是 <see cref="MessageBox"/>，由场景负责摆放与关闭，
/// 这里只把 <see cref="MarketplaceAction"/> 交出去。
/// </para>
/// </remarks>
public sealed class MarketplaceWindow
{
    private const int ScreenWidth = 640;
    private const int ScreenHeight = 480;

    /// <summary>Where the pointer is parked while it is outside the window.</summary>
    private static readonly Point OffScreen = new(-1, -1);

    /// <summary>Frame of <c>TRADPOST</c> with the TRADE button, <c>15</c>.</summary>
    private const int TradeReleasedFrame = 15;

    /// <summary>Frame of <c>TRADPOST</c> with the TRADE button held down, <c>16</c>.</summary>
    private const int TradePressedFrame = 16;

    /// <summary>Frame of <c>TRADPOST</c> with the EXIT button, <c>17</c>.</summary>
    private const int ExitReleasedFrame = 17;

    /// <summary>Frame of <c>TRADPOST</c> with the EXIT button held down, <c>18</c>.</summary>
    private const int ExitPressedFrame = 18;

    /// <summary>The word the gift button carries.</summary>
    private const string GiftLabel = "GIFT";

    /// <summary>Where the label of the EXIT frame sits, which the gift button overwrites.</summary>
    private static readonly Rect GiftLabelArea = new(6, 3, 84, 18);

    private readonly MarketplaceView _view;
    private readonly LocalEvent _events = new();

    private Point? _cursor;
    private bool _closePressed;

    private MarketplaceWindow(MarketplaceView view) => _view = view;

    /// <summary>
    /// Builds the marketplace of a kingdom, <c>Dialog::Marketplace()</c> up to the start of its loop.
    /// </summary>
    /// <param name="assets">The archives the sprites come from.</param>
    /// <param name="kingdom">The kingdom whose resources are traded.</param>
    /// <param name="activePlayers">How many players are still in the game; two or more enable the gift button.</param>
    /// <param name="fonts">The game fonts: the small face, the white normal face and the yellow one.</param>
    /// <returns>The window, or <see langword="null"/> when an archive is missing frames.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static MarketplaceWindow? TryCreate(
        AssetBundle assets,
        GameKingdom kingdom,
        int activePlayers,
        GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(fonts);

        IReadOnlyList<Sprite>? tradpost = Load(assets, IcnType.Tradpost);
        IReadOnlyList<Sprite>? marketplaceIcon = Load(assets, IcnType.MarketplaceIcon);
        IReadOnlyList<Sprite>? resourceIcons = Load(assets, IcnType.Resource);

        // 原版资源里既没有 BUTTON_SMALL_TRADE_GOOD 也没有 BUTTON_GIFT_GOOD，参考在这种数据上用
        // TRADPOST 自己的按钮帧（game_assets.cpp:2196-2210 的 useOriginalResources() 分支）：
        // 15/16 是 TRADE，17/18 是 EXIT；GIFT 由 EXIT 擦掉字再写现拼，和上一批 MIN 的做法一样。
        IReadOnlyList<Sprite>? tradeButton = Take(tradpost, TradeReleasedFrame, TradePressedFrame);
        IReadOnlyList<Sprite>? exitButton = Take(tradpost, ExitReleasedFrame, ExitPressedFrame);
        IReadOnlyList<Sprite>? giftButton = exitButton is { } exit
            ? [Relabel(exit[0], GiftLabel, GiftLabelArea, fonts.White, inset: 0),
               Relabel(exit[1], GiftLabel, GiftLabelArea, fonts.White, inset: 1)]
            : null;

        MarketplaceArtwork? artwork = MarketplaceArtwork.TryCreate(
            tradpost,
            marketplaceIcon,
            resourceIcons,
            tradeButton,
            giftButton,
            exitButton);

        if (artwork is null)
        {
            GameLog.Warn(
                "The marketplace artwork is missing: "
                + $"TRADPOST={tradpost?.Count.ToString() ?? "none"}, "
                + $"MARKETPLACE_ICON={marketplaceIcon?.Count.ToString() ?? "none (h2d only)"}, "
                + $"RESOURCE={resourceIcons?.Count.ToString() ?? "none"}, "
                + $"TRADPOST[15..18]={(tradeButton is null || exitButton is null ? "short" : "ok")}.");

            return null;
        }

        MarketplaceDialog dialog = MarketplaceDialog.Create(kingdom, isFromTradingPost: false, activePlayers);

        MarketplaceView? view = MarketplaceView.TryCreate(
            dialog,
            artwork,
            new MarketplaceFonts(fonts.Small, fonts.Normal, fonts.Yellow),
            new Size(ScreenWidth, ScreenHeight));

        if (view is null)
        {
            GameLog.Warn("The marketplace window could not be laid out.");

            return null;
        }

        return new MarketplaceWindow(view);
    }

    /// <summary>The state the window shows; the caller reads the exchange rate off it.</summary>
    public MarketplaceDialog Dialog => _view.Dialog;

    /// <summary>Whether the player asked to leave the window.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Records the pointer, which decides which cell a click lands on.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor)
    {
        _cursor = cursor;
        _events.OnMouseMotion(cursor ?? OffScreen);
    }

    /// <summary>Records the left button; the click itself is decided by <see cref="Update"/>.</summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down) =>
        _events.OnMouseButton(down, MouseButtonType.Left, _cursor ?? OffScreen);

    /// <summary>Records the right button; the income popup is asked for while it is held.</summary>
    /// <param name="down">Whether the right button is now held.</param>
    public void SetRightButton(bool down) =>
        _events.OnMouseButton(down, MouseButtonType.Right, _cursor ?? OffScreen);

    /// <summary>Marks the close hot key, which the next <see cref="Update"/> sees.</summary>
    public void PressClose() => _closePressed = true;

    /// <summary>
    /// Consumes the events of this frame, <c>dialog_marketplace.cpp:385-685</c>.
    /// </summary>
    /// <remarks>一次调用对应一帧：读完之后输入状态被清空，关闭标记也一起用掉。</remarks>
    /// <returns>What the caller should do next; the window does not close itself.</returns>
    public MarketplaceAction Update()
    {
        MarketplaceAction action = _closePressed
            ? MarketplaceAction.Exit
            : _view.ProcessEvents(_events);

        _closePressed = false;
        _events.Reset();

        if (action == MarketplaceAction.Exit)
        {
            IsClosed = true;
        }

        return action;
    }

    /// <summary>Paints the window: frame, title, shopkeeper, two resource rows, exchange rate and five buttons.</summary>
    /// <param name="target">The indexed frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _view.Draw(target);
    }

    /// <summary>Reads a whole archive; a missing one leaves the caller with a <see langword="null"/> list.</summary>
    private static IReadOnlyList<Sprite>? Load(AssetBundle assets, IcnType type) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(type))?.Sprites;

    /// <summary>Picks the idle and the held-down look of a button out of an archive.</summary>
    /// <param name="sprites">The archive, or <see langword="null"/>.</param>
    /// <param name="releasedFrame">Index of the idle look.</param>
    /// <param name="pressedFrame">Index of the held-down look.</param>
    /// <returns>The pair, or <see langword="null"/> when the archive is short.</returns>
    private static IReadOnlyList<Sprite>? Take(IReadOnlyList<Sprite>? sprites, int releasedFrame, int pressedFrame)
    {
        if (sprites is null
            || releasedFrame < 0 || pressedFrame < 0
            || releasedFrame >= sprites.Count || pressedFrame >= sprites.Count)
        {
            return null;
        }

        return [sprites[releasedFrame], sprites[pressedFrame]];
    }

    /// <summary>
    /// Repaints the label of a button: the old letters are painted over with the colour of the button face, then
    /// the new word is drawn in their place - the port's stand-in for the <c>Assets::getImage()</c> rewrites of
    /// <c>game_assets.cpp:2240-2257</c>.
    /// </summary>
    /// <param name="frame">The button whose label is replaced.</param>
    /// <param name="label">The new label.</param>
    /// <param name="area">Where the old label sits, in the button's own pixels.</param>
    /// <param name="font">The face the label is laid out with, <c>buttonReleasedWhite()</c>.</param>
    /// <param name="inset">Offset of the pressed look, which the original art shifts by one pixel.</param>
    /// <returns>The button with the new label.</returns>
    private static Sprite Relabel(Sprite frame, string label, in Rect area, IGlyphSource font, int inset)
    {
        Sprite copy = frame.Clone();
        PixelBuffer image = copy.Image;

        int x = Math.Max(area.X - inset, 0);
        int y = Math.Max(area.Y + inset, 0);
        int width = Math.Min(area.Width, image.Width - x);
        int height = Math.Min(area.Height, image.Height - y);

        if (width <= 0 || height <= 0)
        {
            return copy;
        }

        Rect text = new(x, y, width, height);

        // 原版这里用的是固定 RGB（getButtonFillingColor），端口没有 RGB 到调色板索引的换算，
        // 于是取这块区域里出现最多的那个索引——它正好就是按钮底色。
        ImageOps.FillRect(image, text, MostCommon(image, text));

        if (copy.Transform is { } transform)
        {
            // 填平之后这些像素不再是透明或阴影，变换层得一并清掉，不然底色底下还会透出来。
            for (int row = y; row < text.Bottom; row++)
            {
                Array.Clear(transform, (row * image.Width) + x, width);
            }
        }

        int start = x + ((width - GlyphText.Measure(font, label)) / 2);

        GlyphText.DrawLine(font, image, new Point(start, y + 2), label);

        return copy;
    }

    /// <summary>The most frequent index of an area: the flat colour of a button face behind its label.</summary>
    private static byte MostCommon(PixelBuffer image, in Rect area)
    {
        Span<int> counts = stackalloc int[256];

        for (int row = area.Y; row < area.Bottom; row++)
        {
            for (int column = area.X; column < area.Right; column++)
            {
                counts[image[column, row]]++;
            }
        }

        byte best = 0;

        for (int index = 1; index < counts.Length; index++)
        {
            if (counts[index] > counts[best])
            {
                best = (byte)index;
            }
        }

        return best;
    }
}
