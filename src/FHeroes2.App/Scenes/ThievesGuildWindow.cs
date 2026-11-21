using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The thieves' guild (and oracle) ranking of a town, <c>Dialog::ThievesGuild()</c>
/// (<c>dialog_thievesguild.cpp:242-486</c>).
/// </summary>
/// <remarks>
/// <para>
/// 和魔法塔、水井一样，这一层只做「窗口本体」：素材取自 AGG，排行与绘制交给
/// <see cref="ThievesGuildReport"/> / <see cref="ThievesGuildView"/>，循环留在 <see cref="CastleScene"/>。
/// 窗口铺满整屏，打开时把城镇屏整个盖住。
/// </para>
/// <para>
/// 参考里右键 EXIT 会弹「Exit this menu.」的 <c>Dialog::YES_NO</c>；端口把
/// <see cref="ThievesGuildAction.ExitHelp"/> 交出去，说明框由场景摆放与关闭。
/// </para>
/// </remarks>
public sealed class ThievesGuildWindow
{
    private const int ScreenWidth = 640;
    private const int ScreenHeight = 480;

    /// <summary>Where the pointer is parked while it is outside the window.</summary>
    private static readonly Point OffScreen = new(-1, -1);

    private readonly ThievesGuildView _view;
    private readonly LocalEvent _events = new();

    private Point? _cursor;
    private bool _closePressed;

    private ThievesGuildWindow(ThievesGuildView view) => _view = view;

    /// <summary>Builds the ranking of a kingdom, the body of <c>Dialog::ThievesGuild()</c>.</summary>
    /// <param name="assets">The archives the sprites come from.</param>
    /// <param name="report">The ranking to show.</param>
    /// <param name="fonts">The game fonts: the small face and the white normal face.</param>
    /// <param name="evilInterface">Whether the evil stone plate and window frame are wanted.</param>
    /// <returns>The window, or <see langword="null"/> when an archive is missing frames or there is nobody to rank.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ThievesGuildWindow? TryCreate(
        AssetBundle assets,
        ThievesGuildReport report,
        GameFonts fonts,
        bool evilInterface = false)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(fonts);

        ThievesGuildArtwork? artwork = ThievesGuildArtwork.TryCreate(assets, evilInterface);

        if (artwork is null)
        {
            GameLog.Warn("The thieves' guild artwork is missing: STONEBAK, TOWNWIND, LOCATORS, SMALLBAR, MINIPORT, MONS32 or WELLXTRA.");

            return null;
        }

        ThievesGuildView? view = ThievesGuildView.TryCreate(
            report,
            artwork,
            new ThievesGuildFonts(fonts.Small, fonts.Normal),
            new Size(ScreenWidth, ScreenHeight));

        if (view is null)
        {
            GameLog.Warn("The thieves' guild report could not be laid out.");

            return null;
        }

        return new ThievesGuildWindow(view);
    }

    /// <summary>The ranking the window shows.</summary>
    public ThievesGuildReport Report => _view.Report;

    /// <summary>Whether the player asked to leave the report.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Records the pointer, which decides whether a click lands on EXIT.</summary>
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

    /// <summary>Records the right button; the EXIT help box is asked for while it is held.</summary>
    /// <param name="down">Whether the right button is now held.</param>
    public void SetRightButton(bool down) =>
        _events.OnMouseButton(down, MouseButtonType.Right, _cursor ?? OffScreen);

    /// <summary>Marks the close hot key, which the next <see cref="Update"/> sees.</summary>
    public void PressClose() => _closePressed = true;

    /// <summary>Consumes the events of this frame, <c>dialog_thievesguild.cpp:448-473</c>.</summary>
    /// <remarks>一次调用对应一帧：读完之后输入状态被清空，关闭标记也一起用掉。</remarks>
    /// <returns>What the caller should do next; the window does not close itself.</returns>
    public ThievesGuildAction Update()
    {
        ThievesGuildAction action = _closePressed
            ? ThievesGuildAction.Exit
            : _view.ProcessEvents(_events);

        _closePressed = false;
        _events.Reset();

        if (action == ThievesGuildAction.Exit)
        {
            IsClosed = true;
        }

        return action;
    }

    /// <summary>Paints the whole report.</summary>
    /// <param name="target">The indexed frame buffer of the scene.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _view.Draw(target);
    }
}
