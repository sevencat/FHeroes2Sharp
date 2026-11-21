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

namespace FHeroes2.App.Scenes;

/// <summary>
/// The mage guild window of a town, one of the six windows the town screen opens
/// (<c>castle_mageguild.cpp:56-212</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这一层只做「窗口本体」：素材取自 AGG，模型、布局、行与按钮都交给 <see cref="MageGuildDialog"/>，
/// 循环仍留在 <see cref="CastleScene"/>。指针与按键由场景喂进来，每帧 <see cref="Update"/> 消费一次
/// <see cref="LocalEvent"/> 并交回一帧的结论——关闭、换城、点法术。
/// </para>
/// <para>
/// 参考的窗口铺满整屏（背景是 <c>STONEBAK</c> 的 640×480 石面），所以打开时它把城镇屏整个盖住，
/// 场景只画它一个。
/// </para>
/// </remarks>
public sealed class MageGuildWindow
{
    private const int ScreenWidth = 640;
    private const int ScreenHeight = 480;

    /// <summary>Where the pointer is parked while it is outside the window.</summary>
    private static readonly Point OffScreen = new(-1, -1);

    private readonly MageGuildDialog _model;
    private readonly MageGuildArtwork _artwork;
    private readonly MageGuildLayout _layout;
    private readonly IReadOnlyList<ButtonSprite> _buttons;
    private readonly IReadOnlyList<SpellScrollRow> _rows;
    private readonly Sprite? _building;
    private readonly IGlyphSource _font;
    private readonly LocalEvent _events = new();

    private Point? _cursor;
    private MageGuildHotKeys _hotKeys;

    private MageGuildWindow(
        MageGuildDialog model,
        MageGuildArtwork artwork,
        MageGuildLayout layout,
        IReadOnlyList<ButtonSprite> buttons,
        IReadOnlyList<SpellScrollRow> rows,
        Sprite? building,
        IGlyphSource font)
    {
        _model = model;
        _artwork = artwork;
        _layout = layout;
        _buttons = buttons;
        _rows = rows;
        _building = building;
        _font = font;
    }

    /// <summary>
    /// Builds the window of a town's guild, <c>Castle::_openMageGuild()</c> up to the start of its loop.
    /// </summary>
    /// <param name="assets">The archives the sprites come from.</param>
    /// <param name="castle">The town whose guild is shown; it must have one built.</param>
    /// <param name="heroHasSpellBook">Whether the visiting hero already owns a spell book.</param>
    /// <param name="castleCount">How many towns the kingdom has, which enables the two arrows.</param>
    /// <param name="font">The small font of the spell names and the status line.</param>
    /// <returns>The window, or <see langword="null"/> when an archive is missing frames.</returns>
    public static MageGuildWindow? TryCreate(
        AssetBundle assets,
        CastleTown castle,
        bool heroHasSpellBook,
        int castleCount,
        IGlyphSource font)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(font);

        IReadOnlyList<Sprite>? background = Load(assets, MageGuildArtwork.GetBackgroundIcn(false));
        IReadOnlyList<Sprite>? bar = Load(assets, IcnType.Smallbar);
        IReadOnlyList<Sprite>? townWind = Load(assets, IcnType.Townwind);
        IReadOnlyList<Sprite>? spellIcons = Load(assets, IcnType.Spells);
        IReadOnlyList<Sprite>? exitButton = Load(assets, MageGuildArtwork.GetExitButtonIcn());

        MageGuildArtwork? artwork = MageGuildArtwork.TryCreate(
            background,
            bar,
            townWind,
            spellIcons,
            exitButton);

        if (artwork is null)
        {
            GameLog.Warn(
                "The mage guild artwork is missing: "
                + $"{MageGuildArtwork.GetBackgroundIcn(false)}={background?.Count.ToString() ?? "none"}, "
                + $"SMALLBAR={bar?.Count.ToString() ?? "none"}, TOWNWIND={townWind?.Count.ToString() ?? "none"}, "
                + $"SPELLS={spellIcons?.Count.ToString() ?? "none"}, "
                + $"BUTTON_GUILDWELL_EXIT={exitButton?.Count.ToString() ?? "none"}.");

            return null;
        }

        MageGuildDialog model = MageGuildDialog.Create(castle, heroHasSpellBook, castleCount);
        MageGuildLayout layout = model.CreateLayout(new Rect(0, 0, ScreenWidth, ScreenHeight), artwork);

        return new MageGuildWindow(
            model,
            artwork,
            layout,
            model.CreateButtons(layout, artwork),
            model.CreateRows(layout, artwork),
            Load(assets, MageGuildArtwork.GetGuildBuildingIcn(model.Race), model.BuildingFrameIndex),
            font);
    }

    /// <summary>Whether the player asked to leave the window.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Records the pointer, which also picks the spell row under it.</summary>
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

    /// <summary>Marks the close hot key, which the next <see cref="Update"/> sees.</summary>
    public void PressClose() => _hotKeys = _hotKeys with { CloseWindow = true };

    /// <summary>Marks the previous town hot key, the left arrow.</summary>
    public void PressPreviousTown() => _hotKeys = _hotKeys with { PreviousTown = true };

    /// <summary>Marks the next town hot key, the right arrow.</summary>
    public void PressNextTown() => _hotKeys = _hotKeys with { NextTown = true };

    /// <summary>
    /// Consumes the events of this frame, <c>castle_mageguild.cpp:178-200</c>.
    /// </summary>
    /// <remarks>一次调用对应一帧：读完之后输入状态被清空，按键标记也一起用掉。</remarks>
    /// <returns>What the caller should do next; the window does not close itself.</returns>
    public MageGuildFrame Update()
    {
        MageGuildFrame frame = _model.ProcessEvents(_events, _layout, _buttons, _rows, _hotKeys);

        _hotKeys = default;
        _events.Reset();

        if (frame.Action == MageGuildDialogAction.Exit)
        {
            IsClosed = true;
        }

        return frame;
    }

    /// <summary>Paints the window over the whole screen: plate, guild, rows, bar, buttons and status text.</summary>
    /// <param name="target">The indexed frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _model.Draw(target, _layout, _artwork, _font, _building, _rows);

        foreach (ButtonSprite button in _buttons)
        {
            button.Draw(target);
        }

        DrawStatusText(target);
    }

    private void DrawStatusText(PixelBuffer target)
    {
        string message = _model.GetStatusText(_cursor ?? OffScreen, _layout);

        if (message.Length == 0)
        {
            return;
        }

        // 状态栏矩形的高度是 0（参考的 setRoi 就传 0，让状态条自己决定文字位置），所以横向用它的 ROI，
        // 纵向借状态条底板的高度：居中且裁在底板之内，和城镇屏的状态行一致。
        Rect area = _layout.StatusBar;
        Rect bar = _layout.Bar;
        string text = FitToWidth(_font, message, area.Width);
        int width = GlyphText.Measure(_font, text);

        Point position = new(
            area.X + ((area.Width - width) / 2),
            bar.Y + ((bar.Height - _font.LineHeight) / 2));

        GlyphText.DrawLine(_font, target, position, text, new Rect(area.X, bar.Y, area.Width, bar.Height));
    }

    /// <summary>Reads a whole archive; a missing one leaves the caller with a <see langword="null"/> list.</summary>
    private static IReadOnlyList<Sprite>? Load(AssetBundle assets, IcnType type) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(type))?.Sprites;

    /// <summary>Reads one frame of an archive, or <see langword="null"/> when it is not there.</summary>
    private static Sprite? Load(AssetBundle assets, IcnType type, int frame) =>
        assets.TryReadIcn(IcnCatalog.GetFileName(type)) is { } file && frame >= 0 && frame < file.Sprites.Count
            ? file.Sprites[frame]
            : null;

    /// <summary>Cuts a line down to the last character that still fits, <c>fitToOneRow()</c>.</summary>
    private static string FitToWidth(IGlyphSource font, string text, int width)
    {
        if (GlyphText.Measure(font, text) <= width)
        {
            return text;
        }

        int end = text.Length - 1;

        while (end > 0 && GlyphText.Measure(font, text[..end]) > width)
        {
            --end;
        }

        return text[..end];
    }
}
