using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Battle;

/// <summary>What the player answered in <see cref="BattleSurrenderWindow"/>.</summary>
public enum BattleSurrenderAnswer
{
    /// <summary>Nothing was pressed this frame.</summary>
    None,

    /// <summary>The player accepted the price, <c>btnAccept</c>.</summary>
    Accept,

    /// <summary>The player gave up, <c>btnDecline</c> and <c>DEFAULT_CANCEL</c>.</summary>
    Decline,

    /// <summary>The player went to the marketplace to raise the gold.</summary>
    Market,
}

/// <summary>
/// The surrender offer of the enemy hero, <c>Battle::DialogBattleSurrender()</c>
/// (<c>battle_dialogs.cpp:1087-1220</c>).
/// </summary>
/// <remarks>
/// <para>
/// 底图 <c>SURDRBKG</c>、敌方英雄头像框 <c>SURRENDR</c> 的 4 号帧、<c>PORT_BIG</c> 大头像，一句话报价，
/// 以及 ACCEPT / DECLINE / MARKET 三个小按钮。付不起时 ACCEPT 灰掉并在它上方写还差多少金币；开了市场
/// 又付不起时 MARKET 才出现，点它去市场，回来重算一遍。
/// </para>
/// <para>
/// <b>与参考的差异</b>：参考按界面风格挑 <c>SURDRBKE</c> / <c>SURRENDE</c> 与 EVIL 版按钮，端口没有全局的
/// 好坏界面开关，固定用 GOOD 那一套（与 <see cref="BattleSettingsWindow"/>、城堡各窗一致）。
/// 参考的 <c>makeButtonWithShadow()</c> 在这里就是普通 <see cref="ButtonSprite"/>，阴影由窗口底图自己带。
/// </para>
/// <para>
/// 三个按钮没有归档条目（参考的 <c>BUTTON_SMALL_ACCEPT_GOOD</c> / <c>BUTTON_SMALL_DECLINE_GOOD</c> /
/// <c>GOOD_MARKET_BUTTON</c> 都是 <c>game_assets.cpp</c> 现拼的自定义 ICN），所以这里直接用
/// <c>SURRENDR</c> 的 0-3 与 <c>ADVBTNS</c> 的 4/5 现场组装，拼法与 <c>useOriginalResources()</c> 分支一致。
/// </para>
/// <para>
/// 窗口是模态的，但不开自己的循环：场景每帧把输入喂给 <see cref="ProcessEvents"/>，参考的
/// <c>while ( le.HandleEvents() &amp;&amp; !result )</c> 由场景的帧循环顶替。
/// </para>
/// </remarks>
public sealed class BattleSurrenderWindow
{
    /// <summary>Offset of the portrait frame inside the dialog, <c>{ 55, 32 }</c>.</summary>
    private const int PortraitFrameOffsetX = 55;

    /// <summary>Vertical offset of the portrait frame, <c>32</c>.</summary>
    private const int PortraitFrameOffsetY = 32;

    /// <summary>Offset of the <c>PORT_BIG</c> portrait inside the frame, <c>{ 60, 38 }</c>.</summary>
    private const int PortraitOffsetX = 60;

    /// <summary>Vertical offset of the portrait, <c>38</c>.</summary>
    private const int PortraitOffsetY = 38;

    /// <summary>Where the title is centered, <c>pos_rt.x + 312</c>.</summary>
    private const int TitleCenterOffsetX = 312;

    /// <summary>Top of the title, <c>pos_rt.y + 32</c>.</summary>
    private const int TitleOffsetY = 32;

    /// <summary>Left edge of the offer text, <c>pos_rt.x + 175</c>.</summary>
    private const int TextOffsetX = 175;

    /// <summary>Top of the offer text, <c>pos_rt.y + 52</c>.</summary>
    private const int TextOffsetY = 52;

    /// <summary>Width the offer text wraps at, <c>275</c>.</summary>
    private const int TextWidth = 275;

    /// <summary>Offset of the ACCEPT button, <c>{ 91, 152 }</c>.</summary>
    private const int AcceptOffsetX = 91;

    /// <summary>Vertical offset of the ACCEPT button, <c>152</c>.</summary>
    private const int AcceptOffsetY = 152;

    /// <summary>Offset of the DECLINE button, <c>{ 295, 152 }</c>.</summary>
    private const int DeclineOffsetX = 295;

    /// <summary>Vertical offset of the DECLINE button, <c>152</c>.</summary>
    private const int DeclineOffsetY = 152;

    /// <summary>Vertical offset of the MARKET button, <c>145</c>.</summary>
    private const int MarketOffsetY = 145;

    /// <summary>How far above the ACCEPT button the gold warning sits, <c>13</c>.</summary>
    private const int GoldWarningOffsetY = 13;

    /// <summary>Palette index of the ACCEPT/DECLINE label in the original art, <c>ReplaceColorId( ..., 28, 56 )</c>.</summary>
    private const byte ButtonLabelColorId = 28;

    /// <summary>The index the label is recoloured to, <c>56</c>.</summary>
    private const byte ButtonLabelFixedColorId = 56;

    /// <summary>Palette index of the black backdrop of the map button, <c>addTransparency( sprite, 36 )</c>.</summary>
    private const byte MarketBackdropColorId = 36;

    /// <summary>Palette index of the extra border the pressed map button carries, <c>addTransparency( ..., 61 )</c>.</summary>
    private const byte MarketExtraBorderColorId = 61;

    private readonly Rect _frame;
    private readonly Sprite? _background;
    private readonly Sprite _portraitFrame;
    private readonly Sprite? _portrait;
    private readonly string _title;
    private readonly string _body;
    private readonly IGlyphSource _white;
    private readonly IGlyphSource _smallWhite;
    private readonly ButtonSprite _accept;
    private readonly ButtonSprite _decline;
    private readonly ButtonSprite _market;
    private readonly Kingdom _kingdom;
    private readonly uint _cost;

    private bool _marketShown;
    private bool _canPay;

    private BattleSurrenderWindow(Rect frame, Sprite? background, Sprite portraitFrame, Sprite? portrait,
        string title, string body, IGlyphSource white, IGlyphSource smallWhite, ButtonSprite accept,
        ButtonSprite decline, ButtonSprite market, Kingdom kingdom, uint cost)
    {
        _frame = frame;
        _background = background;
        _portraitFrame = portraitFrame;
        _portrait = portrait;
        _title = title;
        _body = body;
        _white = white;
        _smallWhite = smallWhite;
        _accept = accept;
        _decline = decline;
        _market = market;
        _kingdom = kingdom;
        _cost = cost;

        Refresh();
    }

    /// <summary>The whole dialog, <c>pos_rt</c> of the reference.</summary>
    public Rect Frame => _frame;

    /// <summary>The gold the enemy asks for, <c>GetCurrentForce().GetSurrenderCost()</c>.</summary>
    public uint Cost => _cost;

    /// <summary>Whether the treasury covers <see cref="Cost"/>, so ACCEPT is live.</summary>
    public bool IsAffordable => _canPay;

    /// <summary>The ACCEPT button, exposed for tests and for the caller's own hit tests.</summary>
    public ButtonSprite AcceptButton => _accept;

    /// <summary>The DECLINE button.</summary>
    public ButtonSprite DeclineButton => _decline;

    /// <summary>The MARKET button.</summary>
    public ButtonSprite MarketButton => _market;

    /// <summary>
    /// The dialog rect of a screen, <c>pos_rt = ( ( 640 - w + 16 ) / 2, ( 480 - h + 16 ) / 2, w, h )</c>
    /// (<c>battle_dialogs.cpp:1101</c>).
    /// </summary>
    /// <param name="screen">Screen the dialog is centered on.</param>
    /// <param name="dialog">Size of the <c>SURDRBKG</c> frame.</param>
    /// <returns>The dialog rect.</returns>
    public static Rect FrameFor(Size screen, Size dialog) => new(
        (screen.Width - dialog.Width + 16) / 2,
        (screen.Height - dialog.Height + 16) / 2,
        dialog.Width,
        dialog.Height);

    /// <summary>Position of the ACCEPT button, <c>pos_rt.x + 91, pos_rt.y + 152</c>.</summary>
    /// <param name="frame">The dialog rect.</param>
    /// <returns>The button position.</returns>
    public static Point AcceptPosition(Rect frame) => new(frame.X + AcceptOffsetX, frame.Y + AcceptOffsetY);

    /// <summary>Position of the DECLINE button, <c>pos_rt.x + 295, pos_rt.y + 152</c>.</summary>
    /// <param name="frame">The dialog rect.</param>
    /// <returns>The button position.</returns>
    public static Point DeclinePosition(Rect frame) => new(frame.X + DeclineOffsetX, frame.Y + DeclineOffsetY);

    /// <summary>Position of the MARKET button, centered on <c>pos_rt.x + ( w - 16 ) / 2</c>.</summary>
    /// <param name="frame">The dialog rect.</param>
    /// <returns>The button position.</returns>
    public static Point MarketPosition(Rect frame) =>
        new(frame.X + ((frame.Width - 16) / 2), frame.Y + MarketOffsetY);

    /// <summary>The title line, <c>_( "%{name} states:" )</c> with the enemy hero.</summary>
    /// <param name="enemy">The hero who would accept the surrender.</param>
    /// <returns>For example <c>"Maximus states:"</c>.</returns>
    public static string TitleFor(Hero enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);

        return $"{enemy.Name} states:";
    }

    /// <summary>The offer, <c>_( "\"I will accept your surrender … for the price of %{price} gold.\"" )</c>.</summary>
    /// <param name="cost">The gold the enemy asks for.</param>
    /// <returns>The sentence the dialog shows.</returns>
    public static string BodyFor(uint cost) =>
        "\"I will accept your surrender and grant you and your troops safe passage "
        + $"for the price of {cost} gold.\"";

    /// <summary>Whether the treasury covers the price, <c>kingdom.AllowPayment( Funds( Resource::GOLD, cost ) )</c>.</summary>
    /// <param name="kingdom">The kingdom that would pay.</param>
    /// <param name="cost">The gold the enemy asks for.</param>
    /// <returns><see langword="true"/> when ACCEPT can be live.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="kingdom"/> is <see langword="null"/>.</exception>
    public static bool CanPay(Kingdom kingdom, uint cost)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        return kingdom.AllowPayment(new Funds(Resource.Gold, cost));
    }

    /// <summary>Whether the MARKET button appears: a marketplace exists and the gold is short.</summary>
    /// <param name="kingdom">The kingdom that would pay.</param>
    /// <param name="cost">The gold the enemy asks for.</param>
    /// <returns><see langword="true"/> when the button is drawn.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="kingdom"/> is <see langword="null"/>.</exception>
    public static bool IsMarketShown(Kingdom kingdom, uint cost) =>
        kingdom.CountMarketplaces() > 0 && !CanPay(kingdom, cost);

    /// <summary>
    /// Builds the offer of <paramref name="enemy"/>, or <see langword="null"/> when an archive frame is missing.
    /// </summary>
    /// <param name="sprites">Where the <c>SURDRBKG</c>, <c>SURRENDR</c> and button frames come from.</param>
    /// <param name="enemy">The hero who would accept the surrender, the reference's <c>getEnemyCommander()</c>.</param>
    /// <param name="cost">The gold the enemy asks for, <c>Funds( Resource::GOLD, cost )</c> being the price.</param>
    /// <param name="kingdom">The kingdom that would pay.</param>
    /// <param name="white">The normal white font of the title and the offer.</param>
    /// <param name="smallWhite">The small white font of the gold warning.</param>
    /// <param name="screen">Screen the dialog is centered on.</param>
    /// <returns>The window, or <see langword="null"/> without artwork.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static BattleSurrenderWindow? TryCreate(IViewWorldSpriteSource sprites, Hero enemy, uint cost,
        Kingdom kingdom, IGlyphSource white, IGlyphSource smallWhite, Size screen)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(enemy);
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(white);
        ArgumentNullException.ThrowIfNull(smallWhite);

        // ACCEPT / DECLINE / MARKET 在参考里都不是归档条目，而是 game_assets.cpp 用原版资源现拼的
        // 「自定义 ICN」：ACCEPT 是 SURRENDR 的 0/1、DECLINE 是 2/3、MARKET 是 ADVBTNS 的 4/5。
        // 之前按归档名去读，读到的是不存在的 CUSTOM，整个窗口因此建不起来。
        if (sprites.GetSprite(IcnType.Surdrbkg, 0) is not { } dialog ||
            sprites.GetSprite(IcnType.Surrendr, 4) is not { } portraitFrame ||
            sprites.GetSprite(IcnType.Surrendr, 0) is not { } acceptReleased ||
            sprites.GetSprite(IcnType.Surrendr, 1) is not { } acceptPressed ||
            sprites.GetSprite(IcnType.Surrendr, 2) is not { } declineReleased ||
            sprites.GetSprite(IcnType.Surrendr, 3) is not { } declinePressed)
        {
            return null;
        }

        acceptReleased = FixReleasedButton(acceptReleased, acceptPressed);
        declineReleased = FixReleasedButton(declineReleased, declinePressed);

        Sprite declinePressedFixed = declinePressed.Clone();
        EmptyButtons.MakeButtonCornersTransparent(declinePressedFixed);

        Sprite? marketReleased = sprites.GetSprite(IcnType.Advbtns, 4) is { } marketTileReleased
            ? MakeMarketButton(marketTileReleased, pressed: false)
            : null;
        Sprite? marketPressed = sprites.GetSprite(IcnType.Advbtns, 5) is { } marketTilePressed
            ? MakeMarketButton(marketTilePressed, pressed: true)
            : null;

        if (marketReleased is null || marketPressed is null)
        {
            // 没有市场按钮就画不出 MARKET，但不该因此拒绝整个窗口：付得起时它本来就不该出现。
            marketReleased = acceptReleased;
            marketPressed = acceptPressed;
        }

        // pos_rt = ( ( 640 - w + 16 ) / 2, ( 480 - h + 16 ) / 2, w, h ) of the reference.
        Rect frame = FrameFor(screen, new Size(dialog.Width, dialog.Height));

        Sprite? portrait = sprites.GetSprite(HeroPortraitIcn.Get(enemy.PortraitId), 0);

        Point accept = AcceptPosition(frame);
        Point decline = DeclinePosition(frame);
        Point market = MarketPosition(frame);

        ButtonSprite acceptButton = new(acceptReleased, acceptPressed);
        acceptButton.SetPosition(accept.X, accept.Y);

        ButtonSprite declineButton = new(declineReleased, declinePressedFixed);
        declineButton.SetPosition(decline.X, decline.Y);

        ButtonSprite marketButton = new(marketReleased, marketPressed);
        marketButton.SetPosition(market.X, market.Y);

        return new BattleSurrenderWindow(frame, dialog, portraitFrame, portrait, TitleFor(enemy), BodyFor(cost),
            white, smallWhite, acceptButton, declineButton, marketButton, kingdom, cost);
    }

    /// <summary>
    /// Re-reads the treasury and updates the three buttons, the tail of the reference's loop after the
    /// marketplace returns.
    /// </summary>
    /// <remarks>
    /// 参考的规则：付不起 → ACCEPT 灰；有市场且付得起 → MARKET 灰且不画；有市场且付不起 → MARKET 可点并画出来；
    /// 没市场 → MARKET 灰且不画。
    /// </remarks>
    public void Refresh()
    {
        _canPay = CanPay(_kingdom, _cost);

        if (_canPay)
        {
            _accept.Enable();
        }
        else
        {
            _accept.Disable();
        }

        _marketShown = IsMarketShown(_kingdom, _cost);

        if (_marketShown)
        {
            _market.Enable();
        }
        else
        {
            _market.Disable();
        }
    }

    /// <summary>Paints the dialog: background, buttons, portrait, title, offer and the gold warning.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_background is { } background)
        {
            Blit(target, background, _frame.X, _frame.Y);
        }

        // 参考的顺序：底图 → 三个按钮 → 头像框/头像 → 标题/正文 → 金币提示。
        _accept.Draw(target);
        _decline.Draw(target);

        if (_marketShown)
        {
            _market.Draw(target);
        }

        Blit(target, _portraitFrame, _frame.X + PortraitFrameOffsetX, _frame.Y + PortraitFrameOffsetY);

        if (_portrait is { } portrait)
        {
            Blit(target, portrait, _frame.X + PortraitOffsetX, _frame.Y + PortraitOffsetY);
        }

        int titleWidth = GlyphText.Measure(_white, _title);
        GlyphText.DrawLine(_white, target,
            new Point(_frame.X + TitleCenterOffsetX - (titleWidth / 2), _frame.Y + TitleOffsetY), _title);

        GlyphText.DrawMultiline(_white, target, new Point(_frame.X + TextOffsetX, _frame.Y + TextOffsetY),
            _body, TextWidth);

        if (!_canPay)
        {
            DrawGoldWarning(target);
        }
    }

    /// <summary>Writes how much gold is missing above the ACCEPT button.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    /// <remarks>
    /// 参考把文本放在按钮区域上方 13 像素，再往右挪 2 像素——按钮区域的左边带着立体阴影，
    /// 不挪就看着偏左（<c>battle_dialogs.cpp:1150-1151</c>）。
    /// </remarks>
    private void DrawGoldWarning(PixelBuffer target)
    {
        int missing = (int)_cost - _kingdom.GetFunds().Gold;
        string message = $"Not enough gold ({missing})";

        Rect area = _accept.ReleasedArea;
        int width = GlyphText.Measure(_smallWhite, message);

        GlyphText.DrawLine(_smallWhite, target,
            new Point(area.X + ((area.Width - width) / 2) + 2, area.Y - GoldWarningOffsetY), message);
    }

    /// <summary>
    /// Turns a <c>SURRENDR</c> frame into the released look of ACCEPT or DECLINE, the
    /// <c>useOriginalResources()</c> branch of <c>BUTTON_SMALL_ACCEPT_GOOD</c> / <c>BUTTON_SMALL_DECLINE_GOOD</c>.
    /// </summary>
    /// <param name="released">The released frame, <c>SURRENDR</c> 0 or 2.</param>
    /// <param name="pressed">The pressed frame, <c>SURRENDR</c> 1 or 3.</param>
    /// <returns>The released frame with the fixed label colour and the pressed transform layer.</returns>
    /// <remarks>
    /// 参考做了两件事（<c>game_assets.cpp</c>）：<c>ReplaceColorId( ..., 28, 56 )</c> 修掉字体用错的调色板索引，
    /// 再把按下帧的变换层拷到释放帧上——两帧的透明像素必须一致，否则描边和阴影会错位。归档里的帧是共享缓存，
    /// 改之前先克隆。
    /// </remarks>
    private static Sprite FixReleasedButton(Sprite released, Sprite pressed)
    {
        Sprite fixedButton = released.Clone();
        ImageOps.ReplaceColorId(fixedButton, ButtonLabelColorId, ButtonLabelFixedColorId);

        return pressed.HasTransformLayer
            ? ImageOps.CopyTransformLayer(pressed, fixedButton)
            : fixedButton;
    }

    /// <summary>
    /// Turns an <c>ADVBTNS</c> tile into the market button of <c>GOOD_MARKET_BUTTON</c>.
    /// </summary>
    /// <param name="tile">Frame 4 (released) or 5 (pressed) of <c>ADVBTNS.ICN</c>.</param>
    /// <param name="pressed">Whether this is the pressed frame.</param>
    /// <returns>A clone whose black backdrop is transparent.</returns>
    /// <remarks>
    /// 参考的 <c>addTransparency( sprite, 36 )</c>：地图小按钮自带一块黑底，抠掉才像一颗悬浮的按钮；
    /// 按下帧还多一圈棕色边（索引 61）。同样先克隆，缓存里的帧不能动。
    /// </remarks>
    private static Sprite MakeMarketButton(Sprite tile, bool pressed)
    {
        Sprite button = tile.Clone();

        ImageOps.ReplaceColorIdByTransformId(button, MarketBackdropColorId, 1);

        if (pressed)
        {
            ImageOps.ReplaceColorIdByTransformId(button, MarketExtraBorderColorId, 1);
        }

        return button;
    }

    /// <summary>Blits a sprite onto the frame at a logical position.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    /// <param name="sprite">The sprite to draw.</param>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    private static void Blit(PixelBuffer target, Sprite sprite, int x, int y) =>
        ImageOps.Blit(target, new Point(x, y), sprite.Image, sprite.Image.Bounds);

    /// <summary>
    /// Consumes one frame of input, the body of the reference's <c>while ( le.HandleEvents() )</c> loop.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>The answer of this frame, <see cref="BattleSurrenderAnswer.None"/> while nothing was pressed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 顺序与参考一致：先看 ACCEPT（Enter 只在它可用时才关窗），再看 MARKET（只在它画出来时），
    /// 最后是 DECLINE 与 Esc。
    /// </remarks>
    public BattleSurrenderAnswer ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _accept.UpdatePressState(events);
        _decline.UpdatePressState(events);

        if (_marketShown)
        {
            _market.UpdatePressState(events);
        }

        if (_accept.IsEnabled &&
            (GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultOkay) || events.MouseClickLeft(_accept.Area)))
        {
            return BattleSurrenderAnswer.Accept;
        }

        if (_marketShown && events.MouseClickLeft(_market.Area))
        {
            return BattleSurrenderAnswer.Market;
        }

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel) || events.MouseClickLeft(_decline.Area))
        {
            return BattleSurrenderAnswer.Decline;
        }

        return BattleSurrenderAnswer.None;
    }
}
