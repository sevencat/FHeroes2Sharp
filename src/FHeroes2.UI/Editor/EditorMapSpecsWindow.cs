using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The editor's map specifications window, the window assembly and the drawing half of
/// <c>mapSpecificationsDialog()</c> (<c>editor_map_specs_window.cpp:2043-2235</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把窗框、页面、六个按钮和事件循环全写在同一个函数里，按钮在构造阶段就贴到 <c>display</c> 上一次，
/// 之后每帧只按鼠标按下态重画。端口把这一层拆成「装配 + 每帧整块重画」：<see cref="Create"/> 只算尺寸与位置，
/// <see cref="Paint"/> 依次画窗框、页面（<see cref="MapSpecsPageRenderer"/>）和按钮，不依赖上一帧的像素。
/// </para>
/// <para>
/// 窗口大小沿用参考的 <c>StandardWindow( DEFAULT_WIDTH, DEFAULT_HEIGHT, !isDefaultScreenSize )</c>：
/// 本移植只有 640x480 一个屏幕尺寸，于是窗口正好铺满屏幕，活动区是 608x448（四周各 16 像素边框），
/// 石头底由页面自己铺进活动区（参考 :2054-2057 只在该情形下这么做），窗框因此不铺内衬。
/// </para>
/// <para>
/// 六个按钮的偏移照抄参考：OKAY / CANCEL 贴右下（CANCEL 先定，OKAY 再按它的宽度往左让 10 像素），
/// RUMORS / EVENTS / LANGUAGE 贴左下并依次往右让 10 像素，ABOUT 贴右上离边 21/12。
/// 按钮素材按 <see cref="Settings"/> 的界面主题取 GOOD / EVIL 版本，和窗框、页面保持同一主题。
/// </para>
/// <para>
/// 事件循环不在这一类里（<c>a7l.3.5.3.2</c>）；<see cref="Paint"/> 已经接受本帧的
/// <see cref="LocalEvent"/>，只为了画按钮的按下态，传入 <see langword="null"/> 就是全部按松开态画。
/// </para>
/// </remarks>
public sealed class EditorMapSpecsWindow
{
    /// <summary>Width of the screen the window fills, <c>Display::DEFAULT_WIDTH</c>.</summary>
    public const int ScreenWidth = 640;

    /// <summary>Height of the screen the window fills, <c>Display::DEFAULT_HEIGHT</c>.</summary>
    public const int ScreenHeight = 480;

    /// <summary>Distance of the bottom buttons from the left / right edge (<c>:2213-2231</c>).</summary>
    public const int BottomButtonOffsetX = 20;

    /// <summary>Distance of the bottom buttons from the bottom edge (<c>:2213-2231</c>).</summary>
    public const int BottomButtonOffsetY = 6;

    /// <summary>Horizontal gap between two buttons in a row (<c>:2217-2230</c>).</summary>
    public const int ButtonGap = 10;

    /// <summary>Distance of the ABOUT button from the right edge (<c>:2235</c>).</summary>
    public const int AboutOffsetX = 21;

    /// <summary>Distance of the ABOUT button from the top edge (<c>:2235</c>).</summary>
    public const int AboutOffsetY = 12;

    /// <summary>Label of the rumors button, <c>gettext_noop( "RUMORS" )</c> (<c>:2220</c>).</summary>
    public const string RumorsLabel = "RUMORS";

    /// <summary>Label of the daily events button (<c>:2224</c>).</summary>
    public const string EventsLabel = "EVENTS";

    /// <summary>Label of the language button (<c>:2229</c>).</summary>
    public const string LanguageLabel = "LANGUAGE";

    /// <summary>Label of the creator notes button, <c>map|ABOUT</c> (<c>:2234</c>).</summary>
    public const string AboutLabel = "ABOUT";

    /// <summary>
    /// Largest gold amount the victory panel offers, in thousands of pieces; the reference's element goes up to
    /// 1,000,000 pieces (<c>editor_map_specs_window.cpp:1030</c>).
    /// </summary>
    public const int MaxVictoryGoldInThousands = 1_000_000 / MapSpecsState.GoldThousand;

    /// <summary>Step of the gold element, one thousand pieces (<c>:1030</c>).</summary>
    public const int VictoryGoldStepInThousands = 1;

    /// <summary>Largest day count the loss panel offers (<c>:1690</c>).</summary>
    public const int MaxLossDays = 10 * 365;

    /// <summary>Step of the day element (<c>:1690</c>).</summary>
    public const int LossDaysStep = 1;

    private readonly MapSpecsPageArtwork _artwork;
    private readonly MapSpecsState _state;
    private readonly StandardWindow _window;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite _rumors;
    private readonly ButtonSprite _events;
    private readonly ButtonSprite _language;
    private readonly ButtonSprite _about;
    private readonly IGlyphSource? _bodyFont;
    private readonly Palette? _gamePalette;
    private IReadOnlyList<Sprite>? _valueFrames;
    private ValueSelectionElement? _goldValue;
    private ValueSelectionElement? _daysValue;
    private Point _goldOffset;
    private Point _daysOffset;

    private EditorMapSpecsWindow(MapSpecsPageArtwork artwork, MapSpecsState state, bool isEvilInterface,
        IGlyphSource? bodyFont, Palette? gamePalette)
    {
        _artwork = artwork;
        _state = state;
        IsEvilInterface = isEvilInterface;
        _bodyFont = bodyFont;
        _gamePalette = gamePalette;

        _window = StandardWindow.CreateCentered(
            new Size(ScreenWidth - (StandardWindow.BorderWidth * 2), ScreenHeight - (StandardWindow.BorderWidth * 2)),
            renderBackground: false);

        List<Size> playerIcons = [];

        foreach (PlayerColor color in PlayerColors.ToList(state.AvailablePlayers))
        {
            Sprite? icon = artwork.GetPlayerIcon(
                PlayerColors.GetIndex(color) + MapSpecsPageIcons.GetPlayerIconIndex(state, color));

            // 逐帧图标缺失时参考拿到的是空图（宽高为 0），布局照样算出一个零宽格子；这里照办，不因为
            // 少一帧图标就打不开整页（见 MapSpecsPageArtwork 的类注释）。
            playerIcons.Add(icon is null ? Size.Empty : new Size(icon.Width, icon.Height));
        }

        PageLayout = MapSpecsPageLayout.Create(
            _window.ActiveArea,
            new Size(artwork.NameBox.Width, artwork.NameBox.Height),
            playerIcons,
            artwork.DifficultyCursor.Height,
            new Size(artwork.DropButton.Released.Width, artwork.DropButton.Released.Height),
            new Size(artwork.ConditionBackground.Width, artwork.ConditionBackground.Height));

        IcnType okayAsset = isEvilInterface ? IcnType.ButtonSmallOkayEvil : IcnType.ButtonSmallOkayGood;
        IcnType cancelAsset = isEvilInterface ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood;

        _cancel = CreateSpriteButton(
            RequireFrame(cancelAsset, 0),
            RequireFrame(cancelAsset, 1),
            new Point(BottomButtonOffsetX, BottomButtonOffsetY),
            WindowPadding.BottomRight);

        // 参考 :2217 的横偏移按 CANCEL 的实际宽度累加，OKAY 落在它左边。
        _okay = CreateSpriteButton(
            RequireFrame(okayAsset, 0),
            RequireFrame(okayAsset, 1),
            new Point(BottomButtonOffsetX + _cancel.Area.Width + ButtonGap, BottomButtonOffsetY),
            WindowPadding.BottomRight);

        _rumors = CreateTextButton(RumorsLabel, new Point(BottomButtonOffsetX, BottomButtonOffsetY),
            WindowPadding.BottomLeft);

        _events = CreateTextButton(EventsLabel,
            new Point(BottomButtonOffsetX + _rumors.Area.Width + ButtonGap, BottomButtonOffsetY),
            WindowPadding.BottomLeft);

        _language = CreateTextButton(LanguageLabel,
            new Point(BottomButtonOffsetX + _rumors.Area.Width + _events.Area.Width + (ButtonGap * 2), BottomButtonOffsetY),
            WindowPadding.BottomLeft);

        _about = CreateTextButton(AboutLabel, new Point(AboutOffsetX, AboutOffsetY), WindowPadding.TopRight);
    }

    /// <summary>Whether the evil interface artwork is used.</summary>
    public bool IsEvilInterface { get; }

    /// <summary>The window the page and the buttons sit in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The area the page may paint on, <c>background.activeArea()</c> (<c>:2050</c>).</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>Where everything of the page goes.</summary>
    public MapSpecsPageLayout PageLayout { get; }

    /// <summary>The specifications being edited; the page reads it on every draw.</summary>
    public MapSpecsState State => _state;

    /// <summary>
    /// The open victory / loss condition list, or <see langword="null"/> when none is open; the page draws it on top
    /// (<c>editor_map_specs_window.cpp:2382-2389</c>).
    /// </summary>
    public EditorMapSpecsConditionPopup? ConditionPopup { get; private set; }

    /// <summary>
    /// The victory panel as it was drawn last frame, i.e. where its rows and checkboxes are; <see langword="null"/>
    /// when the window has no font / palette or when the condition's target is missing.
    /// </summary>
    public MapSpecsConditionPanel? VictoryPanel { get; private set; }

    /// <summary>The loss panel as it was drawn last frame; <see langword="null"/> when it could not be drawn.</summary>
    public MapSpecsConditionPanel? LossPanel { get; private set; }

    /// <summary>The gold element of the victory panel, or <see langword="null"/> when the window has no font.</summary>
    public ValueSelectionElement? GoldValue => _goldValue;

    /// <summary>The day element of the loss panel, or <see langword="null"/> when the window has no font.</summary>
    public ValueSelectionElement? DaysValue => _daysValue;

    /// <summary>The OKAY button, bottom right (<c>:2215-2217</c>).</summary>
    public ButtonSprite Okay => _okay;

    /// <summary>The CANCEL button, bottom right to the right of OKAY (<c>:2211-2213</c>).</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>The RUMORS button, bottom left (<c>:2219-2221</c>).</summary>
    public ButtonSprite Rumors => _rumors;

    /// <summary>The EVENTS button, bottom left after RUMORS (<c>:2223-2226</c>).</summary>
    public ButtonSprite Events => _events;

    /// <summary>The LANGUAGE button, bottom left after EVENTS (<c>:2228-2231</c>).</summary>
    public ButtonSprite Language => _language;

    /// <summary>The ABOUT button, top right (<c>:2233-2235</c>).</summary>
    public ButtonSprite About => _about;

    /// <summary>
    /// Assembles the window out of the artwork of the installed game data.
    /// </summary>
    /// <param name="artwork">The page artwork, usually <see cref="MapSpecsPageArtwork.TryCreate"/>.</param>
    /// <param name="state">The specifications being edited.</param>
    /// <param name="bodyFont">
    /// The font the number elements of the condition panels draw their digits with; <see langword="null"/> leaves those
    /// panels out (no <c>TOWNWIND</c> numbers, no gold / day arrows).
    /// </param>
    /// <param name="gamePalette">
    /// The palette hero rows are drawn with, <c>Display::instance()-&gt;getPalette()</c> of the reference;
    /// <see langword="null"/> leaves the condition panels out entirely.
    /// </param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The archive lacks a button frame or the window border art, i.e. the game data is not open.
    /// </exception>
    public static EditorMapSpecsWindow Create(MapSpecsPageArtwork artwork, MapSpecsState state,
        IGlyphSource? bodyFont = null, Palette? gamePalette = null)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(state);

        // 参考 :2052 取一次 Settings，窗框、页面与按钮共用同一主题。
        return new EditorMapSpecsWindow(artwork, state, Settings.Get().IsEvilInterfaceEnabled, bodyFont, gamePalette);
    }

    /// <summary>
    /// Draws one frame: the window frame, the page, the six buttons and the open condition list.
    /// </summary>
    /// <param name="target">Frame buffer of the scene, in screen coordinates.</param>
    /// <param name="events">
    /// The events of this frame, for the pressed look of the buttons; <see langword="null"/> draws them all released.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The window border art is missing.</exception>
    public void Paint(PixelBuffer target, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        // 参考 :2049 先建窗（画边框），:2054-2057 再把石头底贴进活动区，顺序不能换。
        _window.Draw(target);

        // 页面绘制器绑定目标缓冲，于是每帧新建一个；它只持有引用，构造没有开销。
        new MapSpecsPageRenderer(_artwork, PageLayout, _state, target).Draw(
            events is not null && events.IsMouseLeftButtonPressedAndHeldInArea(PageLayout.VictoryButtonRoi),
            events is not null && events.IsMouseLeftButtonPressedAndHeldInArea(PageLayout.LossButtonRoi));

        // 参考的标题条下面就是条件面板本体（:2152-2208），它每帧重画，位置也每帧重算。
        VictoryPanel = PaintVictoryPanel(target);
        LossPanel = PaintLossPanel(target);

        PaintButton(_okay, target, events);
        PaintButton(_cancel, target, events);
        PaintButton(_rumors, target, events);
        PaintButton(_events, target, events);
        PaintButton(_language, target, events);
        PaintButton(_about, target, events);

        // 参考 :2382-2389 把列表画在页面之上、按钮之后。
        ConditionPopup?.Draw(target);
    }

    /// <summary>
    /// Opens the victory or loss condition list under its drop button, the call of <c>showWinLoseList()</c> in the
    /// event loop (<c>editor_map_specs_window.cpp:2362-2369</c>).
    /// </summary>
    /// <param name="isLoss">Whether to open the loss condition list.</param>
    /// <returns>
    /// <see langword="true"/> when a list was opened; <see langword="false"/> when the map allows no condition of
    /// that kind, in which case nothing changes.
    /// </returns>
    public bool OpenConditionList(bool isLoss)
    {
        ConditionPopup = isLoss
            ? EditorMapSpecsConditionPopup.TryOpenLoss(_state, PageLayout, _artwork.ConditionBackground, _artwork.GetConditionIcon)
            : EditorMapSpecsConditionPopup.TryOpenVictory(_state, PageLayout, _artwork.ConditionBackground, _artwork.GetConditionIcon);

        return ConditionPopup is not null;
    }

    /// <summary>Closes the open condition list without touching the state.</summary>
    public void CloseConditionList() => ConditionPopup = null;

    /// <summary>Paints the victory panel of the current condition, <c>VictoryConditionUI::render()</c> (<c>:1021-1210</c>).</summary>
    private MapSpecsConditionPanel? PaintVictoryPanel(PixelBuffer target)
    {
        if (_gamePalette is null)
        {
            return null;
        }

        MapSpecsVictoryPanelView view = MapSpecsPanelViews.CreateVictory(_state);

        // 参考在「占领城镇」「击败英雄」没有候选时靠 assert 终止；端口的地图上没有城镇就没有这一格可画。
        if (!MapSpecsPanelViews.CanDrawVictory(view))
        {
            return null;
        }

        MapSpecsConditionPanelLayout layout =
            MapSpecsConditionPanelRenderer.GetVictoryLayout(view, PageLayout.VictoryPanelRoi, IsEvilInterface);

        ValueSelectionElement? gold = layout.ValueAreaRoi == Rect.Empty
            ? null
            : CreateValueSelection(layout.ValueAreaRoi.Position, _state.VictoryGoldInThousands,
                MapSpecsState.MinVictoryGoldInThousands, MaxVictoryGoldInThousands, VictoryGoldStepInThousands,
                ref _goldValue, ref _goldOffset);

        // 没有字体时数字画不出来，参考的整块面板也就没有意义。
        if (view.Condition == MapVictoryCondition.CollectGold && gold is null)
        {
            return null;
        }

        return MapSpecsConditionPanelRenderer.DrawVictory(
            view, PageLayout.VictoryPanelRoi, target, IsEvilInterface, _gamePalette, gold);
    }

    /// <summary>Paints the loss panel of the current condition, <c>LossConditionUI::render()</c> (<c>:1668-1743</c>).</summary>
    private MapSpecsConditionPanel? PaintLossPanel(PixelBuffer target)
    {
        if (_gamePalette is null)
        {
            return null;
        }

        MapSpecsLossPanelView view = MapSpecsPanelViews.CreateLoss(_state);

        if (!MapSpecsPanelViews.CanDrawLoss(view))
        {
            return null;
        }

        MapSpecsConditionPanelLayout layout =
            MapSpecsConditionPanelRenderer.GetLossLayout(view, PageLayout.LossPanelRoi, IsEvilInterface);

        ValueSelectionElement? days = layout.ValueAreaRoi == Rect.Empty
            ? null
            : CreateValueSelection(layout.ValueAreaRoi.Position, _state.LossDays, 1, MaxLossDays, LossDaysStep,
                ref _daysValue, ref _daysOffset);

        if (view.Condition == MapLossCondition.OutOfTime && days is null)
        {
            return null;
        }

        return MapSpecsConditionPanelRenderer.DrawLoss(
            view, PageLayout.LossPanelRoi, target, IsEvilInterface, _gamePalette, days);
    }

    /// <summary>
    /// Gives back the number element sitting at <paramref name="offset"/>, rebuilt when the panel moved and re-synced
    /// to <paramref name="value"/> otherwise.
    /// </summary>
    /// <remarks>
    /// 参考的数值控件是条件对象里的成员，位置在构造时定好；端口的面板位置随条件变化，于是位置一变就重造一个。
    /// 值每帧从状态同步，保证「点箭头改状态、下一帧再读回来」这一圈不会来回抖。
    /// </remarks>
    private ValueSelectionElement? CreateValueSelection(Point offset, int value, int minimum, int maximum, int step,
        ref ValueSelectionElement? element, ref Point currentOffset)
    {
        if (_bodyFont is null)
        {
            return null;
        }

        if (element is null || currentOffset != offset)
        {
            element = new ValueSelectionElement(
                minimum, maximum, Math.Clamp(value, minimum, maximum), step, offset, _bodyFont, GetValueFrames());
            currentOffset = offset;

            return element;
        }

        element.Value = value;

        return element;
    }

    /// <summary>The <c>TOWNWIND</c> frames the number element draws its arrows and digits with.</summary>
    private IReadOnlyList<Sprite> GetValueFrames()
    {
        if (_valueFrames is not null)
        {
            return _valueFrames;
        }

        Sprite[] frames = new Sprite[ValueSelectionElement.RequiredFrameCount];

        for (int frame = 0; frame < frames.Length; ++frame)
        {
            frames[frame] = RequireFrame(IcnType.Townwind, frame);
        }

        _valueFrames = frames;

        return _valueFrames;
    }

    private ButtonSprite CreateSpriteButton(Sprite released, Sprite pressed, Point offset, WindowPadding padding)
    {
        Point position = _window.RenderPosition(offset, new Size(released.Width, released.Height), padding);

        ButtonSprite button = new(released, pressed);
        button.SetPosition(position.X, position.Y);

        return button;
    }

    private ButtonSprite CreateTextButton(string text, Point offset, WindowPadding padding)
    {
        (Sprite released, Sprite pressed) = ButtonFactory.GetTextAdaptedSprite(
            text, IsEvilInterface ? DialogButtonTemplate.Evil : DialogButtonTemplate.Good);

        return CreateSpriteButton(released, pressed, offset, padding);
    }

    private static void PaintButton(ButtonSprite button, PixelBuffer target, LocalEvent? events)
    {
        ImageOps.AddGradientShadow(button.Released, target, button.Area.Position, new Point(-5, 5));

        bool isPressed = events is not null && events.IsMouseLeftButtonPressedAndHeldInArea(button.Area);

        button.DrawOnState(isPressed, target);
    }

    private static Sprite RequireFrame(IcnType type, int frameId) =>
        GameAssets.GetImage(GameAssets.GetFileName(type), frameId)
        ?? throw new InvalidOperationException($"The archive has no frame {frameId} of {GameAssets.GetFileName(type)}.");
}
