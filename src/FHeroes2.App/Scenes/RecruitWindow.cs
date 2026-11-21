using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using SpriteFile = FHeroes2.Assets.Icn.IcnSpriteFile;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The recruit window a dwelling of the well opens, <c>Dialog::RecruitMonster()</c>
/// (<c>castle_well.cpp:263-271</c> → <c>dialog_recruit.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 和另两个窗口不同，这个窗口只盖住屏幕中间那一块：底下的水井屏照旧画，窗口贴在上面。
/// 排版、按钮与数量计算都在 <see cref="RecruitWindowView"/> 里，这一层只做三件事——从 AGG 凑素材、
/// 把每帧输入灌进 <see cref="LocalEvent"/>、把窗口给出的答案交回 <see cref="CastleScene"/>（真正扣钱入兵的是它）。
/// </para>
/// <para>
/// 参考里怪物的造型来自 <c>ICN::MONSTER</c> 的静态首帧（<c>dialog_recruit.cpp:173</c>），帧号取自
/// <c>MonsterAnimInfo</c> 的 <c>STATIC</c> 行；端口第一次把这两样接起来，所以顺手把
/// <c>MonsterAnimInfoCache</c> 也用上了。
/// </para>
/// </remarks>
public sealed class RecruitWindow
{
    private const int ScreenWidth = 640;
    private const int ScreenHeight = 480;

    /// <summary>Pixels the window is pushed down, the <c>windowOffsetY</c> argument of the two callers.</summary>
    /// <remarks>井里是 <c>2</c>（<c>castle_well.cpp:266</c>），城镇屏是 <c>-65</c>（<c>castle_dialog.cpp:685</c>）。</remarks>
    public const int WellWindowOffsetY = 2;

    /// <inheritdoc cref="WellWindowOffsetY"/>
    public const int CastleWindowOffsetY = -65;

    /// <summary>Where the pointer is parked while it is outside the window.</summary>
    private static readonly Point OffScreen = new(-1, -1);

    /// <summary>Label of the small MIN button, <c>gettext_noop( "MIN" )</c> (<c>game_assets.cpp:2240</c>).</summary>
    private const string MinLabel = "MIN";

    /// <summary>
    /// The <c>RECRUIT</c> frames carry a drop shadow, so only the button itself is kept,
    /// <c>Crop( originalReleased, 5, 0, 60, 25 )</c> (<c>game_assets.cpp:2223-2224</c>).
    /// </summary>
    private static readonly Rect MaxButtonCrop = new(5, 0, 60, 25);

    /// <summary>
    /// Where the label of the small MIN button is repainted,
    /// <c>Fill( out, 26 - 2 * i, 5 + i, 25, 15, … )</c> widened to hold the whole word: the MAX label of
    /// <c>RECRUIT</c> spans the button almost edge to edge, the reference's 25 pixels only cover the middle
    /// of it (<c>game_assets.cpp:2246-2247</c>).
    /// </summary>
    private static readonly Rect MinLabelArea = new(5, 3, 52, 18);

    private readonly RecruitWindowView _view;
    private readonly LocalEvent _events = new();

    private Point? _cursor;
    private bool _closePressed;

    private RecruitWindow(RecruitWindowView view) => _view = view;

    /// <summary>
    /// Builds the window of one dwelling, <c>Dialog::RecruitMonster()</c> up to its loop.
    /// </summary>
    /// <param name="assets">The archives the sprites come from.</param>
    /// <param name="castle">The town the dwelling belongs to; only its race is read.</param>
    /// <param name="monster">The unit on offer.</param>
    /// <param name="available">How many units the dwelling holds.</param>
    /// <param name="kingdom">The kingdom that pays; <see cref="RecruitDialog"/> only reads its treasury.</param>
    /// <param name="fonts">The text faces of the game.</param>
    /// <param name="windowOffsetY">How far the window is pushed down; see <see cref="WellWindowOffsetY"/>.</param>
    /// <returns>The window, or <see langword="null"/> when an archive is missing frames.</returns>
    public static RecruitWindow? TryCreate(
        AssetBundle assets,
        CastleTown castle,
        MonsterType monster,
        uint available,
        GameKingdom kingdom,
        GameFonts fonts,
        int windowOffsetY = WellWindowOffsetY)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(fonts);

        IReadOnlyList<Sprite>? background = Load(assets, IcnType.Recrbkg);
        IReadOnlyList<Sprite>? title = Load(assets, IcnType.Bldgxtra);
        IReadOnlyList<Sprite>? arrows = Load(assets, IcnType.Recruit);
        IReadOnlyList<Sprite>? resourceIcons = Load(assets, IcnType.Resource);
        Sprite? monsterSprite = TryLoadMonsterSprite(assets, monster);

        // 四颗小按钮在原版资源里都是别的档案切出来的，参考在 game_assets.cpp 里现拼：
        // OKAY 取 SPANBTN 0/1（1042-1056），CANCEL 取 CPANEL 8/9（1020-1034），
        // MAX 取 RECRUIT 4/5 裁掉阴影（2214-2239），MIN 是 MAX 擦掉字再写（2240-2257）。
        DialogButtonArtwork? okay = TryGetFrames(Load(assets, IcnType.Spanbtn), 0, 1, null);
        DialogButtonArtwork? cancel = TryGetFrames(Load(assets, IcnType.Cpanel), 8, 9, null);
        DialogButtonArtwork? max = TryGetFrames(arrows, 4, 5, MaxButtonCrop);
        DialogButtonArtwork? min = max is { } maxButton
            ? RelabelButton(maxButton, MinLabel, MinLabelArea, fonts.White)
            : null;

        // 换降级兵的箭头 MONSTER_SWITCH_*_ARROW 在原版资源里也没有档案，参考由 RECRUIT 的上下箭头
        // 转置再上下翻转现拼（game_assets.cpp:3022-3042）。两个方向各两帧（按下、松开）。
        IReadOnlyList<Sprite>? switchLeft = TryBuildSwitchArrows(arrows, 0);
        IReadOnlyList<Sprite>? switchRight = TryBuildSwitchArrows(arrows, 2);

        StandardWindowChrome? chrome = TryCreateChrome(assets);

        if (background is null || title is null || arrows is null || resourceIcons is null || monsterSprite is null
            || okay is null || cancel is null || max is null || min is null || chrome is null
            || switchLeft is null || switchRight is null)
        {
            GameLog.Warn(
                "The recruit window artwork is missing: "
                + $"RECRBKG={background?.Count.ToString() ?? "none"}, "
                + $"BLDGXTRA={title?.Count.ToString() ?? "none"}, "
                + $"RECRUIT={arrows?.Count.ToString() ?? "none"}, "
                + $"RESOURCE={resourceIcons?.Count.ToString() ?? "none"}, "
                + $"MONSTER={(monsterSprite is null ? "none" : monster.ToString())}, "
                + $"SPANBTN={(okay is null ? "none" : "ok")}, "
                + $"CPANEL={(cancel is null ? "none" : "ok")}, "
                + $"MAX={(max is null ? "none" : "ok")}, "
                + $"MIN={(min is null ? "none" : "ok")}, "
                + $"SWITCH_LEFT={(switchLeft is null ? "none" : "ok")}, "
                + $"SWITCH_RIGHT={(switchRight is null ? "none" : "ok")}, "
                + $"chrome={(chrome is null ? "none" : "ok")}.");

            return null;
        }

        RecruitWindowArtwork? artwork = RecruitWindowArtwork.TryCreate(
            background,
            title,
            switchLeft,
            switchRight,
            arrows,
            okay.Value,
            cancel.Value,
            max.Value,
            min.Value,
            resourceIcons,
            monsterSprite,
            fonts.Small);

        if (artwork is null)
        {
            GameLog.Warn($"The recruit window of '{monster}' could not be built from its artwork.");

            return null;
        }

        // 两个调用方（水井与城镇屏）都传 `allowDowngradedMonster = true`：住在升级后的住宅里，
        // 箭头就是用来在基础兵和升级兵之间来回看的。
        RecruitDialog dialog = RecruitDialog.Create(monster, available, kingdom, allowDowngradedMonster: true);
        RecruitWindowView? view = RecruitWindowView.TryCreate(
            dialog,
            chrome,
            artwork,
            new RecruitWindowFonts(fonts.White, fonts.Yellow, fonts.Small),
            new Size(ScreenWidth, ScreenHeight),
            windowOffsetY);

        if (view is null)
        {
            GameLog.Warn($"The recruit window of '{monster}' could not be built from its artwork.");

            return null;
        }

        return new RecruitWindow(view);
    }

    /// <summary>The state the window shows and changes; the caller reads <see cref="RecruitDialog.Result"/>.</summary>
    public RecruitDialog Dialog => _view.Dialog;

    /// <summary>Whether the player left the window with OKAY or CANCEL.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The stack the player chose, or <see langword="null"/> when the window was cancelled.</summary>
    public Troop? Selected { get; private set; }

    /// <summary>Records the pointer, which decides which button a click lands on.</summary>
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

    /// <summary>Records the right button, which the window turns into a tooltip or a unit description.</summary>
    /// <param name="down">Whether the right button is now held.</param>
    public void SetRightButton(bool down) =>
        _events.OnMouseButton(down, MouseButtonType.Right, _cursor ?? OffScreen);

    /// <summary>Marks ESC/Enter, which the next <see cref="Update"/> answers as CANCEL.</summary>
    public void PressClose() => _closePressed = true;

    /// <summary>Consumes the events of this frame, <c>dialog_recruit.cpp</c>'s loop.</summary>
    /// <remarks>一次调用对应一帧：读完之后输入状态被清空，ESC 标记也一起用掉。</remarks>
    public void Update()
    {
        if (_closePressed)
        {
            _closePressed = false;

            Dialog.Choose(null);
            IsClosed = true;

            return;
        }

        RecruitWindowAction action = _view.ProcessEvents(_events);

        _events.Reset();

        switch (action)
        {
            case RecruitWindowAction.Okay:
                Selected = Dialog.Result;
                IsClosed = true;
                break;

            case RecruitWindowAction.Cancel:
                IsClosed = true;
                break;

            case RecruitWindowAction.Tooltip when _view.Tooltip is { } tooltip:
                GameLog.Info($"Recruit tooltip: {tooltip.Header} -- {tooltip.Body}");
                break;

            case RecruitWindowAction.ChooseCount:
                GameLog.Info("The numpad of the count box is not wired up yet.");
                break;

            case RecruitWindowAction.MonsterInfo or RecruitWindowAction.MonsterInfoWithButtons:
                GameLog.Info($"The description window of '{Dialog.Monster}' is not wired up yet.");
                break;

            default:
                break;
        }
    }

    /// <summary>Paints the window; the screen behind it is the caller's business.</summary>
    /// <param name="target">The indexed frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _view.Draw(target);
    }

    private static StandardWindowChrome? TryCreateChrome(AssetBundle assets)
    {
        IReadOnlyList<Sprite>? horizontal = Load(assets, IcnType.Surdrbkg);
        IReadOnlyList<Sprite>? corner = Load(assets, IcnType.Winlose);
        IReadOnlyList<Sprite>? stone = Load(assets, IcnType.Stonebak);

        return StandardWindowChrome.TryCreate(horizontal, corner, stone);
    }

    /// <summary>
    /// The static frame of a unit, <c>ICN::MONSTER</c> at the first <c>STATIC</c> frame of its animation row.
    /// </summary>
    /// <param name="assets">The archives the sprite comes from.</param>
    /// <param name="monster">The unit.</param>
    /// <returns>The sprite, or <see langword="null"/> when the archive or the frame is missing.</returns>
    private static Sprite? TryLoadMonsterSprite(AssetBundle assets, MonsterType monster)
    {
        if (Load(assets, MonsterIcnIds.GetSpriteIcn(monster)) is not { Count: > 0 } frames)
        {
            return null;
        }

        int frame = 0;

        if (new MonsterAnimInfoCache(assets).Get(monster) is { AnimationFrames: { } rows }
            && rows.Length > (int)MonsterAnimId.Static
            && rows[(int)MonsterAnimId.Static] is { Length: > 0 } staticFrames)
        {
            frame = staticFrames[0];
        }

        return frame >= 0 && frame < frames.Count ? frames[frame] : frames[0];
    }

    /// <summary>
    /// Reads two frames of an archive as the looks of one button, optionally cropped; the port's stand-in for
    /// the <c>Assets::getImage()</c> pairs of <c>game_assets.cpp:1020-1056</c> and <c>:2216-2226</c>.
    /// </summary>
    /// <param name="sprites">The archive the frames come from.</param>
    /// <param name="releasedFrame">Index of the idle look.</param>
    /// <param name="pressedFrame">Index of the held-down look.</param>
    /// <param name="crop">Area to keep, or <see langword="null"/> for the whole frame.</param>
    /// <returns>The button, or <see langword="null"/> when either frame is missing.</returns>
    private static DialogButtonArtwork? TryGetFrames(
        IReadOnlyList<Sprite>? sprites,
        int releasedFrame,
        int pressedFrame,
        Rect? crop)
    {
        if (sprites is null
            || releasedFrame < 0 || pressedFrame < 0
            || releasedFrame >= sprites.Count || pressedFrame >= sprites.Count)
        {
            return null;
        }

        return new DialogButtonArtwork(Cut(sprites[releasedFrame], crop), Cut(sprites[pressedFrame], crop));
    }

    /// <summary>
    /// Turns the up arrow of <c>ICN::RECRUIT</c> into a sideways one, two frames (released and pressed) of the
    /// <c>MONSTER_SWITCH_LEFT_ARROW</c> / <c>MONSTER_SWITCH_RIGHT_ARROW</c> pairs the reference synthesizes in
    /// <c>game_assets.cpp:3022-3042</c>: transpose, then flip vertically.
    /// </summary>
    /// <param name="arrows">The <c>RECRUIT</c> frames.</param>
    /// <param name="firstFrame">Frame of the idle look; the pressed look is the next one.</param>
    /// <returns>The pair, or <see langword="null"/> when the archive is short.</returns>
    private static IReadOnlyList<Sprite>? TryBuildSwitchArrows(IReadOnlyList<Sprite>? arrows, int firstFrame)
    {
        if (arrows is null || firstFrame < 0 || firstFrame + 1 >= arrows.Count)
        {
            return null;
        }

        return [arrows[firstFrame].Transpose().Flip(horizontal: false, vertical: true),
                arrows[firstFrame + 1].Transpose().Flip(horizontal: false, vertical: true)];
    }

    /// <summary>
    /// A private copy of a frame, cropped to <paramref name="crop"/> when one is given. The transform layer comes
    /// along: the frames carve their shadows, highlights and rounded corners out of it, so a crop that kept only
    /// the image layer would paint those pixels as palette noise.
    /// </summary>
    private static Sprite Cut(Sprite frame, Rect? crop)
    {
        if (crop is not { } area)
        {
            return frame.Clone();
        }

        PixelBuffer image = new(area.Width, area.Height);

        ImageOps.Blit(image, Point.Zero, frame.Image, area);

        if (frame.Transform is not { } transform)
        {
            return new Sprite(image);
        }

        byte[] cut = new byte[area.Width * area.Height];

        for (int row = 0; row < area.Height; row++)
        {
            Array.Copy(
                transform,
                ((area.Y + row) * frame.Width) + area.X,
                cut,
                row * area.Width,
                area.Width);
        }

        return new Sprite(image, 0, 0, cut);
    }

    /// <summary>
    /// Repaints the label of a button, <c>ICN::BUTTON_SMALL_MIN_GOOD</c>'s rewrite of the MAX button
    /// (<c>game_assets.cpp:2240-2257</c>): the old letters are painted over with the colour of the button face,
    /// then the new word is drawn in their place.
    /// </summary>
    /// <param name="source">The button whose label is replaced.</param>
    /// <param name="label">The new label.</param>
    /// <param name="area">Where the old label sits, in the button's own pixels.</param>
    /// <param name="font">The face the label is laid out with, <c>buttonReleasedWhite()</c>.</param>
    /// <returns>The button with the new label.</returns>
    private static DialogButtonArtwork RelabelButton(
        DialogButtonArtwork source,
        string label,
        Rect area,
        IGlyphSource font)
    {
        // 原版按下态整体比放开态右下偏一格：每个 Fill/Copy 的坐标都带上 i。
        return new DialogButtonArtwork(
            RecutLabel(source.Released, area, inset: 0, label, font),
            RecutLabel(source.Pressed, area, inset: 1, label, font));
    }

    private static Sprite RecutLabel(Sprite frame, in Rect area, int inset, string label, IGlyphSource font)
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

    private static IReadOnlyList<Sprite>? Load(AssetBundle assets, IcnType type) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(type))?.Sprites;
}
