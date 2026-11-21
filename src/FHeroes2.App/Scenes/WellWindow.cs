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

using CastleTown = FHeroes2.Game.Castle.Castle;
using FHeroes2.Game.Castle;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The well window of a town, <c>Castle::_openWell()</c> (<c>castle_well.cpp:203-306</c>).
/// </summary>
/// <remarks>
/// <para>
/// 和魔法塔窗口一样，这一层只做「窗口本体」：素材取自 AGG，六个住宅的排版与文案都交给
/// <see cref="WellDialog"/>，循环留在 <see cref="CastleScene"/>。窗口铺满整屏，打开时把城镇屏整个盖住。
/// </para>
/// <para>
/// 参考在这里做的事比魔法塔多两件——点住宅开招募窗口、MAX 一键买满。两者都要改城镇数据，留给下一批；
/// 本层先把「进得去、画得出、出得来」跑通，两件事各记一条日志。
/// </para>
/// </remarks>
public sealed class WellWindow
{
    private const int ScreenWidth = 640;
    private const int ScreenHeight = 480;

    /// <summary>Label of the buy-all button, <c>gettext_noop( "MAX" )</c> (<c>game_assets.cpp:1913</c>).</summary>
    private const string MaxLabel = "MAX";

    /// <summary>Where the EXIT label sits inside its frame, <c>Fill( out, 3 - i, 2 + i, 55, 14 )</c>.</summary>
    private static readonly Rect LabelArea = new(3, 2, 55, 14);

    /// <summary>Text area of the empty guildwell button, <c>minimumTextArea</c> (<c>ui_button.cpp:332-333</c>).</summary>
    private static readonly Size MaxTextArea = new(53, 13);

    /// <summary>Where the released MAX label starts, <c>releasedOffset</c>.</summary>
    private static readonly Point MaxReleasedTextOffset = new(4, 2);

    /// <summary>Where the pressed MAX label starts, <c>pressedOffset</c>.</summary>
    private static readonly Point MaxPressedTextOffset = new(3, 3);

    /// <summary>Where the pointer is parked while it is outside the window.</summary>
    private static readonly Point OffScreen = new(-1, -1);

    private WellDialog _model;
    private readonly WellArtwork _artwork;
    private readonly WellLayout _layout;
    private readonly IReadOnlyList<ButtonSprite> _buttons;
    private readonly WellFonts _fonts;
    private readonly LocalEvent _events = new();

    private Point? _cursor;
    private WellHotKeys _hotKeys;

    private WellWindow(
        WellDialog model,
        WellArtwork artwork,
        WellLayout layout,
        IReadOnlyList<ButtonSprite> buttons,
        WellFonts fonts)
    {
        _model = model;
        _artwork = artwork;
        _layout = layout;
        _buttons = buttons;
        _fonts = fonts;
    }

    /// <summary>
    /// Builds the window of a town's well, <c>Castle::_openWell()</c> up to the start of its loop.
    /// </summary>
    /// <param name="assets">The archives the sprites come from.</param>
    /// <param name="castle">The town whose well is shown; it must have one built.</param>
    /// <param name="fonts">The three text faces of the window.</param>
    /// <returns>The window, or <see langword="null"/> when an archive is missing frames.</returns>
    public static WellWindow? TryCreate(AssetBundle assets, CastleTown castle, GameFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(fonts);

        IReadOnlyList<Sprite>? background = Load(assets, WellArtwork.GetBackgroundIcn(false));
        IReadOnlyList<Sprite>? bar = Load(assets, IcnType.Smallbar);
        IReadOnlyList<Sprite>? maxButton = Load(assets, IcnType.ButtonWellMax);
        IReadOnlyList<Sprite>? exitButton = Load(assets, MageGuildArtwork.GetExitButtonIcn());
        IReadOnlyList<Sprite>? buildings = Load(assets, WellArtwork.GetDwellingIcn(castle.Race));

        // 住宅图是种族 CSTL* 档案里 19~30 号帧，WellArtwork 的列表从 19 号帧开始算。
        IReadOnlyList<Sprite>? dwellingFrames = buildings is null
            ? null
            : [.. buildings.Skip(WellArtwork.FirstDwellingFrame).Take(WellArtwork.DwellingFrameCount)];

        // MAX 按钮在原版资源里没有档案，参考是拿 WELLXTRA 的 EXIT 帧擦掉文字再写 "MAX"
        // （game_assets.cpp:4965 与 1910-1917）；这里有档案就优先用档案。
        DialogButtonArtwork? max = TryGetButton(maxButton, out DialogButtonArtwork fromArchive)
            ? fromArchive
            : TryCreateMaxButton(exitButton, fonts.White);

        DialogButtonArtwork? exit = TryGetButton(exitButton, out DialogButtonArtwork exitArtwork)
            ? exitArtwork
            : null;

        WellArtwork? artwork = max is { } maxArtwork && exit is { } exitFrames
            ? TryBuildArtwork(background, bar, maxArtwork, exitFrames, dwellingFrames)
            : null;

        if (artwork is null)
        {
            GameLog.Warn(
                "The well artwork is missing: "
                + $"{WellArtwork.GetBackgroundIcn(false)}={background?.Count.ToString() ?? "none"}, "
                + $"SMALLBAR={bar?.Count.ToString() ?? "none"}, "
                + $"BUTTON_WELL_MAX={maxButton?.Count.ToString() ?? "none"}, "
                + $"BUTTON_GUILDWELL_EXIT={exitButton?.Count.ToString() ?? "none"}, "
                + $"{WellArtwork.GetDwellingIcn(castle.Race)}={buildings?.Count.ToString() ?? "none"}, "
                + $"MAX={(max is null ? "none" : "built")}.");

            return null;
        }

        WellDialog model = WellDialog.Create(castle);
        WellLayout layout = model.CreateLayout(new Rect(0, 0, ScreenWidth, ScreenHeight), artwork);

        return new WellWindow(
            model,
            artwork,
            layout,
            WellDialog.CreateButtons(layout, artwork),
            new WellFonts(fonts.Small, fonts.Normal, fonts.Yellow));
    }

    /// <summary>Whether the player asked to leave the window.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Records the pointer, which decides which dwelling a click lands on.</summary>
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

    /// <summary>The row of one dwelling, as the window was built.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    /// <returns>The row, or <see langword="null"/> when the town has no such level.</returns>
    public CastleWellDwelling? GetDwelling(int level)
    {
        foreach (CastleWellDwelling dwelling in _model.Dwellings)
        {
            if (dwelling.Level == level)
            {
                return dwelling;
            }
        }

        return null;
    }

    /// <summary>
    /// Rebuilds the six rows from the town, <c>Castle::_wellRedrawAvailableMonsters()</c>.
    /// </summary>
    /// <remarks>招募之后住宅的存货变了，行是快照，不重取就会一直显示旧的可用数。</remarks>
    /// <param name="castle">The town the window shows.</param>
    public void Refresh(CastleTown castle) => _model = WellDialog.Create(castle);

    /// <summary>Marks the close hot key, which the next <see cref="Update"/> sees.</summary>
    public void PressClose() => _hotKeys = _hotKeys with { CloseWindow = true };

    /// <summary>Marks the buy-all hot key, <c>TOWN_WELL_BUY_ALL</c>.</summary>
    public void PressBuyAll() => _hotKeys = _hotKeys with { BuyAll = true };

    /// <summary>Marks a dwelling hot key, the <c>getPressedBuildingHotkey()</c> of the reference.</summary>
    /// <param name="level">Dwelling level, one to six.</param>
    public void PressDwelling(int level) => _hotKeys = _hotKeys with { Dwelling = level };

    /// <summary>
    /// Consumes the events of this frame, <c>castle_well.cpp:246-300</c>.
    /// </summary>
    /// <remarks>一次调用对应一帧：读完之后输入状态被清空，按键标记也一起用掉。</remarks>
    /// <returns>What the caller should do next; the window does not close itself.</returns>
    public WellDialogFrame Update()
    {
        WellDialogFrame frame = _model.ProcessEvents(_events, _layout, _buttons, _hotKeys);

        _hotKeys = default;
        _events.Reset();

        if (frame.Action == WellDialogAction.Exit)
        {
            IsClosed = true;
        }

        return frame;
    }

    /// <summary>Paints the window over the whole screen: plate, bar, title, six dwellings and the two buttons.</summary>
    /// <param name="target">The indexed frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _model.Draw(target, _layout, _artwork, _fonts);

        foreach (ButtonSprite button in _buttons)
        {
            button.Draw(target);
        }
    }

    /// <summary>
    /// The artwork, or <see langword="null"/> when one of the archives is short; the two buttons are resolved
    /// by the caller, so that <see cref="WellArtwork"/> keeps taking plain sprites.
    /// </summary>
    private static WellArtwork? TryBuildArtwork(
        IReadOnlyList<Sprite>? background,
        IReadOnlyList<Sprite>? bar,
        DialogButtonArtwork max,
        DialogButtonArtwork exit,
        IReadOnlyList<Sprite>? dwellingFrames)
    {
        if (background is null or [] || bar is null or [] || dwellingFrames is null)
        {
            return null;
        }

        return WellArtwork.TryCreate(background[WellArtwork.BackgroundFrame], bar[WellArtwork.BarFrame], max, exit, dwellingFrames);
    }

    /// <summary>
    /// The MAX button of the original resources, <c>ICN::BUTTON_WELL_MAX</c> (<c>game_assets.cpp:1910-1917</c>).
    /// </summary>
    /// <remarks>
    /// 参考从 <c>EMPTY_GUILDWELL_BUTTON</c> 生成它，而那颗按钮本身是 WELLXTRA 的 EXIT 帧擦掉文字得到的
    /// （<c>game_assets.cpp:4946-4976</c>）：先用按钮底色填掉「EXIT」占的那一块，再按文字排版把标签写上。
    /// 底色在原版是 <c>GetColorId( 216, 184, 152 )</c> 之类的固定 RGB，端口没有 RGB 到调色板索引的换算，
    /// 于是取该区域内出现最多的那个索引——它正好就是按钮底色。
    /// </remarks>
    private static DialogButtonArtwork? TryCreateMaxButton(IReadOnlyList<Sprite>? exitFrames, IGlyphSource white)
    {
        if (exitFrames is not { Count: >= 2 })
        {
            return null;
        }

        Sprite released = EraseExitLabel(exitFrames[0], inset: 0);
        Sprite pressed = EraseExitLabel(exitFrames[1], inset: 1);

        DrawMaxLabel(released, white, MaxReleasedTextOffset);
        DrawMaxLabel(pressed, white, MaxPressedTextOffset);

        return new DialogButtonArtwork(released, pressed);
    }

    /// <summary>
    /// Copies a frame and paints the label area with the colour around it, <c>Fill( out, 3 - i, 2 + i, 55, 14, … )</c>.
    /// </summary>
    /// <param name="frame">The EXIT frame to copy.</param>
    /// <param name="inset">0 for the released state, 1 for the pressed one.</param>
    /// <returns>A copy of the frame without the label.</returns>
    private static Sprite EraseExitLabel(Sprite frame, int inset)
    {
        // 克隆之后再丢掉透明层：那颗按钮是一整块矩形，没有需要保留的镂空，而透明层是共享的，不能就地改。
        Sprite copy = frame.Clone().WithoutTransformLayer();
        PixelBuffer image = copy.Image;

        int x = Math.Max(LabelArea.X - inset, 0);
        int y = Math.Max(LabelArea.Y + inset, 0);
        int width = Math.Min(LabelArea.Width, image.Width - x);
        int height = Math.Min(LabelArea.Height, image.Height - y);
        byte fill = MostCommon(image, new Rect(x, y, width, height));

        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                image[column, row] = fill;
            }
        }

        return copy;
    }

    /// <summary>Writes the label into the text area of a MAX button, <c>getTextAdaptedSprite()</c>.</summary>
    private static void DrawMaxLabel(Sprite button, IGlyphSource white, Point offset)
    {
        int width = GlyphText.Measure(white, MaxLabel);

        GlyphText.DrawLine(
            white,
            button.Image,
            new Point(offset.X + ((MaxTextArea.Width - width) / 2), offset.Y),
            MaxLabel);
    }

    /// <summary>The most frequent index of an area: the flat colour behind a label.</summary>
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

    /// <summary>Frames 0 and 1 of a button archive, or <see langword="false"/> when either is missing.</summary>
    private static bool TryGetButton(IReadOnlyList<Sprite>? sprites, out DialogButtonArtwork button)
    {
        if (sprites is { Count: >= 2 })
        {
            button = new DialogButtonArtwork(sprites[0], sprites[1]);

            return true;
        }

        button = default;

        return false;
    }

    /// <summary>Reads a whole archive; a missing one leaves the caller with a <see langword="null"/> list.</summary>
    private static IReadOnlyList<Sprite>? Load(AssetBundle assets, IcnType type) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(type))?.Sprites;
}
