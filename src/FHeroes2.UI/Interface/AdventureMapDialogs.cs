using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The dialogs the adventure map opens itself, i.e. the <c>Dialog::*</c> and <c>fheroes2::showStandardTextMessage()</c>
/// calls of <c>gui/interface_events.cpp</c> whose whole content the port can already draw.
/// </summary>
/// <remarks>
/// <para>
/// 参考在事件里直接调用全局的对话框函数，因为 <c>AGG</c> 缓存与 <c>Font::Get()</c> 都是全局的。端口没有全局缓存，地图屏幕
/// 又已经拿到 <see cref="AssetBundle"/>，所以这里就地把这三样读出来：标准按钮取 <c>SYSTEM.ICN</c> 的 1..4 帧、
/// 标题字取 <c>FONT.ICN</c> 的黄色表、正文字取白色表（参考的 <c>FontType::normalYellow()</c> /
/// <c>normalWhite()</c>），四个大按钮取 <c>APANEL.ICN</c>。所有这些都只在第一次用到时读一次。
/// </para>
/// <para>
/// 只负责「画得出来」的那几个窗口：带自定义美术的（英雄、城堡、王国总览、魔法书、谜题地图、世界地图、场景信息、系统选项）
/// 还要各自的装配器，仍旧走 <see cref="IAdventureHost"/>，等装配器落地后一个一个搬过来。
/// </para>
/// <para>
/// 美术缺帧时不抛异常也不假装成功：记一条警告，然后按「玩家取消了」处理——参考在没有游戏数据时根本起不来，端口不会走到这里。
/// </para>
/// </remarks>
public sealed class AdventureMapDialogs
{
    /// <summary>Archive entry of the standard OKAY/CANCEL pair, as <c>Dialog::FrameBox</c> reads it.</summary>
    private const string SystemAssetName = "SYSTEM.ICN";

    /// <summary>Archive entry of the player icons, the <c>ICN::NGEXTRA</c> of the players panel.</summary>
    private const string NgextraAssetName = "NGEXTRA.ICN";

    /// <summary>Archive entry of the scroll bar buttons and slider, <c>ICN::SCROLL</c>.</summary>
    private const string ScrollAssetName = "SCROLL.ICN";

    /// <summary>The evil variant of <see cref="ScrollAssetName"/>, <c>ICN::SCROLLE</c>.</summary>
    private const string ScrollEvilAssetName = "SCROLLE.ICN";

    /// <summary>Archive entry of the empty and ticked sort marks, <c>ICN::CELLWIN</c> frames 4 and 5.</summary>
    private const string CellwinAssetName = "CELLWIN.ICN";

    /// <summary>Archive entry the scroll bar groove is cut from, <c>ICN::ADVBORD</c> frame 0.</summary>
    private const string BoardAssetName = "ADVBORD.ICN";

    /// <summary>The evil variant of <see cref="BoardAssetName"/>, <c>ICN::ADVBORDE</c>.</summary>
    private const string BoardEvilAssetName = "ADVBORDE.ICN";

    /// <summary>Frame of the released OKAY button in <see cref="SystemAssetName"/>.</summary>
    private const int OkayReleasedFrame = 1;

    /// <summary>Frame of the pressed OKAY button in <see cref="SystemAssetName"/>.</summary>
    private const int OkayPressedFrame = 2;

    /// <summary>Frame of the released CANCEL button in <see cref="SystemAssetName"/>.</summary>
    private const int CancelReleasedFrame = 3;

    /// <summary>Frame of the pressed CANCEL button in <see cref="SystemAssetName"/>.</summary>
    private const int CancelPressedFrame = 4;

    private readonly PixelBuffer _display;
    private readonly AssetBundle _assets;

    /// <summary>The standard buttons, read on the first dialog, <c>null</c> when the archive lacks them.</summary>
    private SystemButtons? _buttons;
    private bool _buttonsLoaded;

    /// <summary>The yellow title font, read on the first dialog.</summary>
    private IGlyphSource? _headerFont;
    private bool _headerFontLoaded;

    /// <summary>The white body font, read on the first dialog.</summary>
    private IGlyphSource? _bodyFont;
    private bool _bodyFontLoaded;

    /// <summary>The small white font, read on the first dialog.</summary>
    private IGlyphSource? _smallFont;
    private bool _smallFontLoaded;

    /// <summary>The <c>NGEXTRA</c> artwork of the players panel, read on the first scenario info window.</summary>
    private IPlayersInfoArtwork? _playersArtwork;
    private bool _playersArtworkLoaded;

    /// <summary>Builds the helper around the frame the screen paints into and the archives it was opened with.</summary>
    /// <param name="display">The frame the dialogs draw into; it must already contain the map behind them.</param>
    /// <param name="assets">The archives the screen reads its own artwork from.</param>
    public AdventureMapDialogs(PixelBuffer display, AssetBundle assets)
    {
        _display = display;
        _assets = assets;
    }

    /// <summary>
    /// Shows a message with an OK button, <c>fheroes2::showStandardTextMessage( header, body, Dialog::OK )</c>.
    /// </summary>
    /// <param name="header">The yellow title; empty for a message without one.</param>
    /// <param name="body">The white body text.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    public void ShowMessage(string header, string body, LocalEvent? events = null)
    {
        if (TryCreateBox(header, body, DialogButton.Ok) is not { } box)
        {
            return;
        }

        MessageBox.Show(_display, box, events);
    }

    /// <summary>
    /// Asks a yes/no question, <c>fheroes2::showStandardTextMessage( header, body, Dialog::YES | Dialog::NO )</c>.
    /// </summary>
    /// <param name="header">The yellow title; empty for a message without one.</param>
    /// <param name="body">The white body text.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>Whether the player chose yes; <see langword="false"/> when the box cannot be drawn.</returns>
    public bool ShowYesNo(string header, string body, LocalEvent? events = null) =>
        TryCreateBox(header, body, DialogButton.Yes | DialogButton.No) is { } box &&
        MessageBox.Show(_display, box, events) == DialogButton.Yes;

    /// <summary>
    /// Opens the adventure options dialog, <c>Dialog::AdventureOptions( enableDig )</c>
    /// (<c>dialog_adventure.cpp:55-110</c>).
    /// </summary>
    /// <param name="enableDig">Whether the dig button answers, the reference's <c>enableDig</c>.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// Which door the player picked, the <c>Adventure::*</c> value of the reference;
    /// <see cref="AdventureOption.Cancel"/> when he closed the dialog or the artwork is missing.
    /// </returns>
    public AdventureOption ShowAdventureOptions(bool enableDig, LocalEvent? events = null)
    {
        if (TryReadIcn(AdventureOptionsView.ButtonsAssetName) is not { } buttons ||
            TryReadIcn(AdventureOptionsView.CancelAssetName) is not { } system ||
            AdventureOptionsView.TryCreate(buttons.Sprites, system.Sprites) is not { } view)
        {
            GameLog.Warn(
                $"The adventure options dialog artwork is missing ('{AdventureOptionsView.ButtonsAssetName}' or " +
                $"'{AdventureOptionsView.CancelAssetName}'); the door stays inactive.");

            return AdventureOption.Cancel;
        }

        return ToAdventureOption(AdventureOptionsDialog.Show(_display, view, enableDig, events));
    }

    /// <summary>
    /// Turns the button that closed the adventure options dialog into the option of the reference, the
    /// <c>switch ( result )</c> of <c>EventAdventureDialog()</c> (<c>interface_events.cpp</c>).
    /// </summary>
    /// <param name="button">The button that closed the dialog.</param>
    /// <returns>The option; <see cref="AdventureOption.Cancel"/> for anything that is not one of the four doors.</returns>
    public static AdventureOption ToAdventureOption(DialogButton button) => button switch
    {
        DialogButton.World => AdventureOption.World,
        DialogButton.Puzzle => AdventureOption.Puzzle,
        DialogButton.Info => AdventureOption.Info,
        DialogButton.Dig => AdventureOption.Dig,
        _ => AdventureOption.Cancel,
    };

    /// <summary>
    /// Opens the scenario info window, <c>Dialog::GameInfo()</c> (<c>dialog_gameinfo.cpp:81-243</c>), which is what
    /// the "info" button of the adventure map shows.
    /// </summary>
    /// <param name="info">The map information block; <c>null</c> for the one in the settings.</param>
    /// <param name="players">The players shown in the "opponents" row; <c>null</c> for the ones in the settings.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <remarks>
    /// 参考从全局的 <c>world.GetMapInfo()</c> 与 <c>Settings::Get().GetPlayers()</c> 取这两样，端口让调用方传，
    /// 默认值与参考一致。美术按 <c>isEvilInterface</c> 分两套，和参考的 <c>GetICN( isEvilInterface ? ICN::…_EVIL : ICN::… )</c> 一样；
    /// 缺帧时记一条警告就返回——参考在没有美术时会直接断言。
    /// </remarks>
    public void ShowGameInfo(Mp2FileInfo? info = null, Players? players = null, LocalEvent? events = null)
    {
        Settings settings = Settings.Get();

        info ??= settings.CurrentMapFileInfo;
        players ??= settings.Players;

        if (info is null || players is null)
        {
            GameLog.Warn("The scenario info window has no map information; the door stays inactive.");

            return;
        }

        bool evil = settings.IsEvilInterfaceEnabled;

        IcnSpriteFile? background = TryReadIcn(evil
            ? GameInfoView.BackgroundEvilAssetName
            : GameInfoView.BackgroundAssetName);
        IcnSpriteFile? okay = TryReadIcn(evil ? GameInfoView.OkayEvilAssetName : GameInfoView.OkayAssetName);
        IcnSpriteFile? about = TryReadIcn(evil ? GameInfoView.AboutEvilAssetName : GameInfoView.AboutAssetName);

        if (background is null || okay is null ||
            GameInfoView.TryCreate(_display, background.Sprites, about?.Sprites, okay.Sprites,
                hasCreatorNotes: !string.IsNullOrEmpty(info.CreatorNotes)) is not { } view)
        {
            GameLog.Warn(
                $"The scenario info artwork is missing ('{GameInfoView.BackgroundAssetName}', " +
                $"'{GameInfoView.OkayAssetName}' or '{GameInfoView.AboutAssetName}'); the door stays inactive.");

            return;
        }

        if (GetHeaderFont() is not { } headerFont || GetBodyFont() is not { } bodyFont ||
            GetSmallFont() is not { } smallFont || GetPlayersArtwork() is not { } playersArtwork)
        {
            return;
        }

        GameInfoDialog.Show(_display, view, info, settings.GameDifficulty, players, playersArtwork,
            bodyFont, smallFont, headerFont, bodyFont, GetButtons()!, events);
    }

    /// <summary>
    /// Asks where to save the game, <c>Dialog::SelectFileSave()</c> (<c>dialog_selectfile.cpp:740-752</c>).
    /// </summary>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the player closed the window.</returns>
    public string? SelectSaveFile(LocalEvent? events = null)
    {
        // 参考：GetSaveDir() + GetSaveFileBaseName() + '_' + CountDay()（补到四位）+ 扩展名。
        string lastFile = System.IO.Path.Combine(GameSave.GetSaveDir(),
            $"{GameSave.GetSaveFileBaseName()}_{World.Current.DayCount:D4}{GameSave.GetSaveFileExtension()}");

        return ShowSelectFile("File to Save:", lastFile, isEditing: true, events);
    }

    /// <summary>
    /// Asks which game to load, <c>Dialog::SelectFileLoad()</c> (<c>dialog_selectfile.cpp:754-766</c>).
    /// </summary>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the player closed the window.</returns>
    public string? SelectLoadFile(LocalEvent? events = null) =>
        ShowSelectFile("File to Load:", GameSave.LastSaveName, isEditing: false, events);

    /// <summary>
    /// Opens the save-file list window, <c>selectFileListSimple()</c> (<c>dialog_selectfile.cpp:323-733</c>).
    /// </summary>
    /// <param name="header">The yellow title, <c>_( "File to Save:" )</c> or <c>_( "File to Load:" )</c>.</param>
    /// <param name="lastFile">The file to preselect, <c>lastFile</c> of the reference.</param>
    /// <param name="isEditing">Whether the name can be typed into, i.e. whether the game is being saved.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the player closed the window.</returns>
    /// <remarks>
    /// 参考的排序标记取 <c>ICN::CELLWIN</c> / <c>ICN::CELLWINE</c>，而 <c>CELLWINE.ICN</c> 在 <c>AGG</c> 里并不存在：
    /// fheroes2 是在 <c>game_assets.cpp</c> 里拿 <c>CELLWIN.ICN</c> 的前 19 帧套 <c>GOOD_TO_EVIL_INTERFACE</c> 调色板现算出来的。
    /// 端口还没有调色板合成这一层，于是两种界面都用 <c>CELLWIN.ICN</c>——勾选框是单色小图，差别只在界面主题，不影响操作。
    /// </remarks>
    private string? ShowSelectFile(string header, string? lastFile, bool isEditing, LocalEvent? events)
    {
        Settings settings = Settings.Get();
        SaveFileSortingMethod sorting = settings.SaveFileSortType;

        // 扫一次目录：窗口高度要看行数，列表也用这一份，免得每个存档头解析两遍。
        List<SaveFileEntry> files = SelectFileDialog.GetSaveFiles(sorting);

        if (GetHeaderFont() is not { } headerFont || GetBodyFont() is not { } bodyFont ||
            GetButtons() is not { } buttons)
        {
            return null;
        }

        bool evil = settings.IsEvilInterfaceEnabled;

        IcnSpriteFile? scroll = TryReadIcn(evil ? ScrollEvilAssetName : ScrollAssetName);
        IcnSpriteFile? cellwin = TryReadIcn(CellwinAssetName);
        IcnSpriteFile? board = TryReadIcn(evil ? BoardEvilAssetName : BoardAssetName);

        if (scroll is null || cellwin is null || board is null || board.Count == 0)
        {
            GameLog.Warn(
                $"The save file window artwork is missing ('{ScrollAssetName}' or '{CellwinAssetName}' or " +
                $"'{BoardAssetName}'); the window stays closed.");

            return null;
        }

        SelectFileArtwork artwork = new()
        {
            ScrollBarFrames = scroll.Sprites,
            SortMarkFrames = cellwin.Sprites,
            ScrollBarBackground = board.Sprites[0],
            Buttons = buttons,
        };

        if (SelectFileView.TryCreate(new Size(_display.Width, _display.Height), GameSave.GetSaveDir(),
                files.Count, sorting, isEditing, artwork, headerFont, bodyFont) is not { } view)
        {
            GameLog.Warn(
                $"'{ScrollAssetName}' or '{CellwinAssetName}' has too few frames for the save file window; " +
                "the window stays closed.");

            return null;
        }

        return SelectFileDialog.Show(_display, view, header, lastFile, isEditing, files, events);
    }

    /// <summary>Composes a standard message box, or returns <see langword="null"/> when its artwork is missing.</summary>
    /// <param name="header">The title, drawn with the yellow font.</param>
    /// <param name="body">The body, drawn with the white font.</param>
    /// <param name="buttons">The buttons on the bottom edge.</param>
    /// <returns>The box, ready to run.</returns>
    private MessageBox? TryCreateBox(string header, string body, DialogButton buttons)
    {
        if (GetHeaderFont() is not { } headerFont || GetBodyFont() is not { } bodyFont ||
            GetButtons() is not { } artwork)
        {
            return null;
        }

        return MessageBox.Create(header, body, headerFont, bodyFont, buttons: buttons, buttonArtwork: artwork);
    }

    /// <summary>The yellow title font, <c>Font::Get( FontSize::NORMAL, YELLOW_FONT )</c>.</summary>
    /// <returns>The font, or <see langword="null"/> when <c>FONT.ICN</c> is missing or broken.</returns>
    private IGlyphSource? GetHeaderFont()
    {
        if (!_headerFontLoaded)
        {
            _headerFontLoaded = true;
            _headerFont = LoadFont(BitmapFontSize.Normal, PaletteId.YellowFont, "header");
        }

        return _headerFont;
    }

    /// <summary>The white body font, <c>Font::Get( FontSize::NORMAL, WHITE_FONT )</c>.</summary>
    /// <returns>The font, or <see langword="null"/> when <c>FONT.ICN</c> is missing or broken.</returns>
    private IGlyphSource? GetBodyFont()
    {
        if (!_bodyFontLoaded)
        {
            _bodyFontLoaded = true;
            _bodyFont = LoadFont(BitmapFontSize.Normal, PaletteId.WhiteFont, "body");
        }

        return _bodyFont;
    }

    /// <summary>The small white font, <c>Font::Get( FontSize::SMALL, WHITE_FONT )</c>.</summary>
    /// <returns>The font, or <see langword="null"/> when <c>FONT.ICN</c> is missing or broken.</returns>
    private IGlyphSource? GetSmallFont()
    {
        if (!_smallFontLoaded)
        {
            _smallFontLoaded = true;
            _smallFont = LoadFont(BitmapFontSize.Small, PaletteId.WhiteFont, "small");
        }

        return _smallFont;
    }

    /// <summary>The <c>NGEXTRA</c> artwork the players panel draws, read once.</summary>
    /// <returns>The artwork, or <see langword="null"/> when <c>NGEXTRA.ICN</c> is missing or broken.</returns>
    private IPlayersInfoArtwork? GetPlayersArtwork()
    {
        if (_playersArtworkLoaded)
        {
            return _playersArtwork;
        }

        _playersArtworkLoaded = true;

        if (TryReadIcn(NgextraAssetName) is not { } extra)
        {
            return null;
        }

        _playersArtwork = new NgextraArtwork(extra);

        return _playersArtwork;
    }

    /// <summary>The standard OKAY/CANCEL pair, read once.</summary>
    /// <returns>The artwork, or <see langword="null"/> when <c>SYSTEM.ICN</c> is missing or too short.</returns>
    private IDialogButtonArtworkSource? GetButtons()
    {
        if (_buttonsLoaded)
        {
            return _buttons;
        }

        _buttonsLoaded = true;

        if (TryReadIcn(SystemAssetName) is not { } system || system.Count <= CancelPressedFrame)
        {
            GameLog.Warn($"The standard buttons of '{SystemAssetName}' are missing; the message cannot be drawn.");

            return null;
        }

        _buttons = new SystemButtons(
            system.Sprites[OkayReleasedFrame],
            system.Sprites[OkayPressedFrame],
            system.Sprites[CancelReleasedFrame],
            system.Sprites[CancelPressedFrame]);

        return _buttons;
    }

    /// <summary>Loads one recoloured face of <c>FONT.ICN</c>.</summary>
    /// <param name="size">The face size, the <c>FontSize</c> of the reference.</param>
    /// <param name="palette">The recolouring table, i.e. the <c>PAL::PaletteType</c> of the face.</param>
    /// <param name="role">Which font it is, for the warning only.</param>
    /// <returns>The font, or <see langword="null"/> when it cannot be read.</returns>
    private IGlyphSource? LoadFont(BitmapFontSize size, PaletteId palette, string role)
    {
        try
        {
            return BitmapFont.Load(_assets, size, palette);
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The {role} font could not be read: {exception.Message}");

            return null;
        }
    }

    /// <summary>Reads an archive entry, turning a broken one into a warning.</summary>
    /// <param name="name">Name of the entry.</param>
    /// <returns>The entry, or <see langword="null"/> when it is missing or broken.</returns>
    private IcnSpriteFile? TryReadIcn(string name)
    {
        try
        {
            return _assets.TryReadIcn(name);
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"'{name}' could not be read: {exception.Message}");

            return null;
        }
    }

    /// <summary>
    /// The four frames of <c>SYSTEM.ICN</c> the standard buttons are drawn from — the same frames the App side
    /// <c>SystemButtonArtwork</c> holds, duplicated here because the UI layer cannot see the App.
    /// </summary>
    private sealed class SystemButtons : IDialogButtonArtworkSource
    {
        private readonly Sprite _okayReleased;
        private readonly Sprite _okayPressed;
        private readonly Sprite _cancelReleased;
        private readonly Sprite _cancelPressed;

        internal SystemButtons(Sprite okayReleased, Sprite okayPressed, Sprite cancelReleased, Sprite cancelPressed)
        {
            _okayReleased = okayReleased;
            _okayPressed = okayPressed;
            _cancelReleased = cancelReleased;
            _cancelPressed = cancelPressed;
        }

        /// <inheritdoc />
        public DialogButtonArtwork Get(DialogButton button) => button == DialogButton.Cancel
            ? new DialogButtonArtwork(_cancelReleased, _cancelPressed)
            : new DialogButtonArtwork(_okayReleased, _okayPressed);
    }

    /// <summary>
    /// The <c>NGEXTRA.ICN</c> frames the players panel draws, <c>Interface::PlayersInfo</c>'s
    /// <c>Assets::getImage( ICN::NGEXTRA, n )</c>.
    /// </summary>
    /// <remarks>
    /// 参考每画一个图标都去 <c>AGG</c> 缓存里取一次同一样的图；端口把整份帧表留住，取不到的帧退回第 0 帧——
    /// 参考在那里是越界，而这份帧表是数据文件决定的，不该让一张缺帧的图把整局弄崩。
    /// </remarks>
    private sealed class NgextraArtwork : IPlayersInfoArtwork
    {
        private readonly IcnSpriteFile _extra;

        internal NgextraArtwork(IcnSpriteFile extra) => _extra = extra;

        /// <inheritdoc />
        public Sprite GetIcon(uint frame) =>
            frame < _extra.Count ? _extra.Sprites[(int)frame] : _extra.Sprites[0];
    }
}
