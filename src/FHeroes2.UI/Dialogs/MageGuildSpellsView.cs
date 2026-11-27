using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Everything the mage guild spells sub-window paints, in the order it has to be painted,
/// <c>mageGuildSpellsDialog()</c> (<c>editor_castle_details_window.cpp:318-606</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这个子窗盖住城堡详情窗的 <c>dialogRoi</c>：铺一整块石头、左边画公会建筑、中间五排卷轴、右上角一个
/// BANNED SPELLS、底下 OKAY 与 EXIT 夹着状态栏。它不处理输入（那是 <c>MageGuildSpellsInput</c>），也不改数据
/// （那是 <see cref="MageGuildSpellTable"/>），只按表把画面摆出来。
/// </para>
/// <para>
/// 参考只重画改动过的那一张卷轴（<c>setCurrentSpell()</c> + <c>redrawCurrentSpell()</c>），端口每帧从背景整块重画：
/// 编辑器没有「只提交脏区」的显示层，整块重画省掉一类「忘了重画」的 bug，代价是每帧多贴二十来张图。
/// </para>
/// <para>
/// 五排卷轴每次 <see cref="Paint"/> 都按表重建——表是唯一真相，行只是它的视图，这样「点 OKAY 之前来回改」也不会
/// 出现表和画面各说一套的情况。
/// </para>
/// </remarks>
public sealed class MageGuildSpellsView
{
    /// <summary>Horizontal offset of the first scroll of a row from the window's left edge, <c>dialogRoi.x + 250</c>.</summary>
    public const int RowsOffsetX = 250;

    /// <summary>Vertical offset of the first level's row from the window's top edge, <c>dialogRoi.y + 365</c>.</summary>
    public const int FirstRowOffsetY = 365;

    /// <summary>Distance between two rows, <c>-90 * levelIndex</c>: level one is the lowest.</summary>
    public const int RowStepY = 90;

    /// <summary>Inset of the BANNED SPELLS button from the top right corner, <c>:386</c>.</summary>
    public const int BannedSpellsInset = 10;

    /// <summary>Drop shadow of the BANNED SPELLS button, <c>{ -5, 5 }</c> (<c>:387</c>).</summary>
    private static readonly Point BannedSpellsShadowOffset = new(-5, 5);

    private readonly MageGuildSpellTable _table;
    private readonly MageGuildSpellsArtwork _artwork;
    private readonly Rect _dialogArea;
    private readonly IGlyphSource _spellFont;
    private readonly SpellScrollRow[] _rows = new SpellScrollRow[MageGuildSpellTable.LevelCount];

    private StatusBar? _statusBar;
    private PixelBuffer? _statusBarOutput;

    /// <summary>
    /// Lays the sub-window out inside the area it covers.
    /// </summary>
    /// <param name="table">The spells the window shows.</param>
    /// <param name="artwork">The plates and pictures.</param>
    /// <param name="dialogArea">The area of the castle details window the sub-window takes over, <c>dialogRoi</c>.</param>
    /// <param name="spellFont">The font of the spell names, <c>FontType::smallWhite()</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MageGuildSpellsView(MageGuildSpellTable table, MageGuildSpellsArtwork artwork, Rect dialogArea,
        IGlyphSource spellFont)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(spellFont);

        _table = table;
        _artwork = artwork;
        _dialogArea = dialogArea;
        _spellFont = spellFont;

        Refresh();
    }

    /// <summary>The spells the window shows.</summary>
    public MageGuildSpellTable Table => _table;

    /// <summary>The area the sub-window covers.</summary>
    public Rect DialogArea => _dialogArea;

    /// <summary>The five rows, lowest level first, as they were laid out by the last <see cref="Refresh"/>.</summary>
    public IReadOnlyList<SpellScrollRow> Rows => _rows;

    /// <summary>
    /// The strip OKAY and EXIT sit in: the bottom of the window, as tall as <c>CASLBAR</c> (<c>:364</c>).
    /// </summary>
    public Rect StatusBarArea => new(
        _dialogArea.X,
        _dialogArea.Bottom - _artwork.Frame.StatusBar.Height,
        _dialogArea.Width,
        _artwork.Frame.StatusBar.Height);

    /// <summary>The area of OKAY, hugging the left end of the status bar (<c>:366-367</c>).</summary>
    public Rect OkayArea => new(
        StatusBarArea.X,
        StatusBarArea.Y,
        _artwork.Frame.OkayReleased.Width,
        StatusBarArea.Height);

    /// <summary>The area of EXIT, hugging the right end of the status bar (<c>:370-372</c>).</summary>
    public Rect ExitArea => new(
        StatusBarArea.Right - _artwork.Frame.ExitReleased.Width,
        StatusBarArea.Y,
        _artwork.Frame.ExitReleased.Width,
        StatusBarArea.Height);

    /// <summary>
    /// The area of BANNED SPELLS: ten pixels from the top and the right edge of the window, as wide as its
    /// own plate (<c>:386</c>).
    /// </summary>
    public Rect BannedSpellsArea => new(
        _dialogArea.Right - BannedSpellsInset - _artwork.BannedSpellsReleased.Width,
        _dialogArea.Y + BannedSpellsInset,
        _artwork.BannedSpellsReleased.Width,
        _artwork.BannedSpellsReleased.Height);

    /// <summary>
    /// The strip the status message goes into: between OKAY and EXIT, clear of each by
    /// <see cref="CastleDetailsFrame.StatusBarDecorationsWidth"/> (<c>:401</c>). Its height is zero in the
    /// reference too — the bar only centres the message horizontally.
    /// </summary>
    public Rect StatusBarMessageArea => new(
        OkayArea.Right + CastleDetailsFrame.StatusBarDecorationsWidth,
        StatusBarArea.Y,
        StatusBarArea.Width - ExitArea.Width - OkayArea.Width - (2 * CastleDetailsFrame.StatusBarDecorationsWidth),
        0);

    /// <summary>The status message the window shows; empty until <see cref="ShowMessage"/> is called.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// Rebuilds the five rows from the table and lays them out, <c>:339-361</c>.
    /// </summary>
    /// <remarks>
    /// 行是从表里拷出来的快照，所以任何改了表的操作之后都得再调一次；<see cref="Paint"/> 每帧都会调，调用方也就
    /// 不必记着这件事。
    /// </remarks>
    public void Refresh()
    {
        Size openedSize = new(_artwork.OpenedScroll.Width, _artwork.OpenedScroll.Height);
        Size foldedSize = new(_artwork.FoldedScroll.Width, _artwork.FoldedScroll.Height);

        for (int levelIndex = 0; levelIndex < _rows.Length; ++levelIndex)
        {
            SpellScrollRow row = new(_table.GetRow(levelIndex));

            // 一级在最下面，往上每级高 90 像素（:359）。
            row.SetPosition(
                new Point(_dialogArea.X + RowsOffsetX, _dialogArea.Y + FirstRowOffsetY - (RowStepY * levelIndex)),
                openedSize, foldedSize);

            _rows[levelIndex] = row;
        }
    }

    /// <summary>
    /// Index of the row the pointer is over.
    /// </summary>
    /// <param name="point">A point in frame coordinates.</param>
    /// <returns>The level index, or <c>-1</c> when no scroll is under the pointer.</returns>
    public int RowIndexAt(in Point point)
    {
        for (int levelIndex = 0; levelIndex < _rows.Length; ++levelIndex)
        {
            if (_rows[levelIndex].GetIndexAt(point) >= 0)
            {
                return levelIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// Remembers the message the next <see cref="Paint"/> shows.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void ShowMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Message = message;
    }

    /// <summary>
    /// Draws the whole sub-window: plate, guild, rows, three buttons, status bar and its message,
    /// <c>:331-403</c>.
    /// </summary>
    /// <remarks>
    /// 状态栏必须压在两个按钮之间、消息必须最后写，所以这两步排在末尾；按钮的「按住」状态由
    /// <paramref name="events"/> 决定，传 <see langword="null"/> 就全部画成松开的样子。
    /// </remarks>
    /// <param name="output">Frame the sub-window goes into.</param>
    /// <param name="events">Input of the frame, which decides the pressed look of the buttons.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Paint(PixelBuffer output, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(output);

        Refresh();

        Rect dialog = _dialogArea;
        int statusBarHeight = _artwork.Frame.StatusBar.Height;

        // 石头底只铺到状态栏上沿，底下那一条留给 CASLBAR。
        ImageOps.Blit(output, dialog.Position, _artwork.Frame.StoneBackground.Image,
            new Rect(0, 0, dialog.Width, dialog.Height - statusBarHeight));

        MageGuildDialog.DrawGuildBuilding(output, dialog.Position, ScreenBuffer.DefaultSize.Height,
            _artwork.GuildBuilding, _table.Race);

        foreach (SpellScrollRow row in _rows)
        {
            row.Draw(output, _artwork.OpenedScroll, _artwork.FoldedScroll, _spellFont, _artwork.GetSpellIcon);
        }

        ButtonSprite okay = new(_artwork.Frame.OkayReleased, _artwork.Frame.OkayPressed);
        okay.SetPosition(OkayArea.X, OkayArea.Y);

        ButtonSprite exit = new(_artwork.Frame.ExitReleased, _artwork.Frame.ExitPressed);
        exit.SetPosition(ExitArea.X, ExitArea.Y);

        ButtonSprite banned = new(_artwork.BannedSpellsReleased, _artwork.BannedSpellsPressed);
        banned.SetPosition(BannedSpellsArea.X, BannedSpellsArea.Y);

        ImageOps.AddGradientShadow(_artwork.BannedSpellsReleased, output, banned.Area.Position,
            BannedSpellsShadowOffset);

        foreach (ButtonSprite button in new[] { exit, okay, banned })
        {
            if (events is not null)
            {
                button.UpdatePressState(events);
            }

            button.Draw(output);
        }

        PaintStatusBarPlate(output);

        if (Message.Length != 0)
        {
            StatusBar statusBar = GetStatusBar(output);
            statusBar.Reset();
            statusBar.ShowMessage(Message);
        }
    }

    /// <summary>
    /// Copies the left and the right end of <c>CASLBAR</c> between OKAY and EXIT, <c>:391-398</c>.
    /// </summary>
    /// <remarks>
    /// 原图比窗口宽得多，中间那段用不上：左半从第 0 列取，右半从倒数第 <c>secondPart</c> 列取。
    /// </remarks>
    /// <param name="output">Frame the bar goes into.</param>
    private void PaintStatusBarPlate(PixelBuffer output)
    {
        Rect statusBar = StatusBarArea;
        Rect okay = OkayArea;
        Rect exit = ExitArea;

        int barWidth = statusBar.Width - exit.Width - okay.Width;
        int firstPart = barWidth / 2;
        int secondPart = barWidth - firstPart;

        ImageOps.Blit(output, new Point(okay.Right, statusBar.Y), _artwork.Frame.StatusBar.Image,
            new Rect(0, 0, firstPart, statusBar.Height));
        ImageOps.Blit(output, new Point(okay.Right + firstPart, statusBar.Y), _artwork.Frame.StatusBar.Image,
            new Rect(_artwork.Frame.StatusBar.Width - secondPart, 0, secondPart, statusBar.Height));
    }

    /// <summary>
    /// The status bar of the buffer being painted into, created on first use.
    /// </summary>
    /// <param name="output">Frame the bar paints into.</param>
    /// <returns>The bar, ready to take a message.</returns>
    private StatusBar GetStatusBar(PixelBuffer output)
    {
        if (_statusBar is null || !ReferenceEquals(_statusBarOutput, output))
        {
            _statusBar = new StatusBar(output);
            _statusBar.SetRoi(StatusBarMessageArea);
            _statusBarOutput = output;
        }

        return _statusBar;
    }
}
