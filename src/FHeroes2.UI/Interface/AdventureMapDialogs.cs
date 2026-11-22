using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameAssets = FHeroes2.Game.Assets;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

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
/// 只负责「画得出来」的那几个窗口：带自定义美术的（城堡、世界地图、系统选项）还要各自的装配器，仍旧走
/// <see cref="IAdventureHost"/>，等装配器落地后一个一个搬过来。文件菜单的六块大按钮是
/// <c>game_assets.cpp:1563-1578</c> 用 <c>makeSymmetricBackgroundSprites()</c> 现生成的，生成器（
/// <see cref="ButtonFactory.MakeSymmetricBackgroundSprites"/>）已在控件层，所以 <see cref="ShowFileOptions"/>
/// 直接现生成十二帧。
/// 游戏结束的几个窗口（淘汰通知、胜/负框）也只有文字与 <c>BRCREST</c>/<c>SYSTEM</c>/<c>FONT</c>，所以
/// <see cref="CheckGameOver"/> 一并放在这里；拼图地图与魔法书同样只靠 <c>PUZZLE</c>/<c>VIEWPUZL</c>/<c>BOOK</c>/<c>SPELLS</c>
/// 与字体，装配器也已经在下面（<see cref="ShowPuzzleMap"/>、<see cref="OpenSpellBook"/>）。英雄窗口的
/// <see cref="HeroDialogArtwork.Load"/> 与 <see cref="ArmyBarArtwork"/> 都是自取的，另外两个精灵源
/// （<see cref="ArtifactsBarArtwork"/>、<see cref="SkillBarSpriteSet"/>）也在控件层补上了；王国总览的
/// <see cref="KingdomOverviewArtwork.Load"/> 同样自取二十余帧，再自己装五个精灵源（<see cref="OpenKingdomOverview"/>）。
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

    /// <summary>Archive entry of the hero faces, <c>ICN::MINIPORT</c> of the hero panel.</summary>
    private const string MiniportAssetName = "MINIPORT.ICN";

    /// <summary>Archive entry of the player signs, <c>ICN::BRCREST</c> of the elimination notice.</summary>
    private const string BrcrestAssetName = "BRCREST.ICN";

    /// <summary>Archive entry of the jigsaw pieces, <c>ICN::PUZZLE</c> of the puzzle map.</summary>
    private const string PuzzleAssetName = "PUZZLE.ICN";

    /// <summary>Archive entry of the whole picture on the radar, <c>ICN::VIEWPUZL</c>.</summary>
    private const string PuzzleRadarAssetName = "VIEWPUZL.ICN";

    /// <summary>Archive entry of the evil twin of <see cref="PuzzleRadarAssetName"/>, <c>ICN::EVIWPUZL</c>.</summary>
    private const string EvilPuzzleRadarAssetName = "EVIWPUZL.ICN";

    /// <summary>Archive entry of the spell book pages, <c>ICN::BOOK</c>.</summary>
    private const string BookAssetName = "BOOK.ICN";

    /// <summary>Archive entry of the spell icons, <c>ICN::SPELLS</c>.</summary>
    private const string SpellsAssetName = "SPELLS.ICN";

    /// <summary>Frame of the straight corner in <see cref="BookAssetName"/>.</summary>
    private const int StraightCornerFrame = 7;

    /// <summary>Frame of the released OKAY button in <see cref="SystemAssetName"/>.</summary>
    private const int OkayReleasedFrame = 1;

    /// <summary>Frame of the pressed OKAY button in <see cref="SystemAssetName"/>.</summary>
    private const int OkayPressedFrame = 2;

    /// <summary>Frame of the released CANCEL button in <see cref="SystemAssetName"/>.</summary>
    private const int CancelReleasedFrame = 3;

    /// <summary>Frame of the pressed CANCEL button in <see cref="SystemAssetName"/>.</summary>
    private const int CancelPressedFrame = 4;

    /// <summary>Frame of the empty player sign in <see cref="BrcrestAssetName"/>.</summary>
    private const int SignBorderFrame = 6;

    /// <summary>Title of the elimination notice, <c>_( "Major Event!" )</c>.</summary>
    public const string VanquishedTitle = "Major Event!";

    /// <summary>Body of the elimination notice, <c>_( "%{color} player has been vanquished!" )</c>.</summary>
    public const string VanquishedMessage = "%{color} player has been vanquished!";

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

    /// <summary>The small grey font of the spell book, read on the first casting.</summary>
    private IGlyphSource? _smallGrayFont;
    private bool _smallGrayFontLoaded;

    /// <summary>The small yellow font of the kingdom overview, read on the first opening.</summary>
    private IGlyphSource? _smallYellowFont;
    private bool _smallYellowFontLoaded;

    /// <summary>The <c>NGEXTRA</c> artwork of the players panel, read on the first scenario info window.</summary>
    private IPlayersInfoArtwork? _playersArtwork;
    private bool _playersArtworkLoaded;

    /// <summary>The <c>QWIKINFO</c> frame of the tile popup, read on the first right click.</summary>
    private Sprite? _quickInfoFrame;
    private bool _quickInfoFrameLoaded;

    /// <summary>The <c>QWIKTOWN</c> artwork of the castle panel, read on the first castle right click.</summary>
    private CastleQuickInfoArtwork? _castleQuickInfoArtwork;
    private bool _castleQuickInfoArtworkLoaded;

    /// <summary>The <c>QWIKHERO</c> artwork of the hero panel, read on the first hero right click.</summary>
    private HeroQuickInfoArtwork? _heroQuickInfoArtwork;
    private bool _heroQuickInfoArtworkLoaded;

    /// <summary>The army portraits of the panels, read on the first castle or hero right click.</summary>
    private IMiniMonsterSprites? _monsters;
    private bool _monstersLoaded;

    /// <summary>The <c>LOCATORS</c> icons of the castle panel, read on the first castle right click.</summary>
    private IViewWorldSpriteSource? _icons;
    private bool _iconsLoaded;

    /// <summary>The <c>BRCREST</c> sign of the elimination notice, read on the first verdict.</summary>
    private IVanquishedSignArtworkSource? _signArtwork;
    private bool _signArtworkLoaded;

    /// <summary>Builds the helper around the frame the screen paints into and the archives it was opened with.</summary>
    /// <param name="display">The frame the dialogs draw into; it must already contain the map behind them.</param>
    /// <param name="assets">The archives the screen reads its own artwork from.</param>
    public AdventureMapDialogs(PixelBuffer display, AssetBundle assets)
    {
        _display = display;
        _assets = assets;
    }

    /// <summary>
    /// Where a quick info box goes instead of <see cref="QuickInfoPopup"/>, which blocks until the right button
    /// is released.
    /// </summary>
    /// <remarks>
    /// 参考的速览是按住右键期间的阻塞循环，端口照样做了；但渲染循环自己管帧的场景（冒险地图）与不跑事件循环的
    /// 测试都不该被卡住，所以给一个接管点：设了它，面板交出去由调用方负责画与还原。
    /// </remarks>
    public Action<IQuickInfoPanel>? PresentQuickInfo { get; set; }

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
    /// Shows the jigsaw the obelisks uncover, <c>Puzzle::ShowMapsDialog()</c>
    /// (<c>kingdom/puzzle.cpp:344-381</c> plus <c>ShowStandardDialog()</c> at <c>100-210</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 拼图整图由地图屏幕生成：<see cref="GameArea.GenerateUltimateArtifactAreaSurface"/> 把神器周围十二格拼成
    /// 448×448 的图，<see cref="PuzzleMapDialog"/> 再把 <c>PUZZLE.ICN</c> 的 48 块盖上去、逐块淡出。
    /// 参考用隐藏界面模式时改走 <c>ShowExtendedDialog()</c>（把同样的图放进中央的 <c>StandardWindow</c>）；
    /// 端口只有雷达版，隐藏界面时也画在雷达上，等扩展窗口有调用方再补。
    /// </para>
    /// <para>
    /// 神器还没被挖出时参考的 <c>GetPuzzleMapSurface()</c> 返回空图，函数就什么都不做；端口把
    /// <see cref="UltimateArtifact.Position"/> 与生成结果两道检查都留着。
    /// </para>
    /// </remarks>
    /// <param name="gameArea">The viewport that renders the picture around the artifact.</param>
    /// <param name="radarArea">Where the radar sits, <c>AdventureMap::getRadar().GetArea()</c>.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    public void ShowPuzzleMap(GameArea gameArea, Rect radarArea, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(gameArea);

        Settings settings = Settings.Get();
        UltimateArtifact artifact = World.Current.UltimateArtifact;

        if (artifact.IsFound || artifact.Position < 0)
        {
            return;
        }

        PixelBuffer picture = gameArea.GenerateUltimateArtifactAreaSurface(artifact.Position, artifact.Offset);

        if (picture.Width != PuzzleMapDialog.PictureSize || picture.Height != PuzzleMapDialog.PictureSize)
        {
            return;
        }

        if (TryReadIcn(PuzzleAssetName) is not { } puzzleFile || puzzleFile.Count < Puzzle.NumOfPuzzleTiles)
        {
            GameLog.Warn($"'{PuzzleAssetName}' needs {Puzzle.NumOfPuzzleTiles} frames to draw the puzzle map.");

            return;
        }

        bool evil = settings.IsEvilInterfaceEnabled;
        string radarName = evil ? EvilPuzzleRadarAssetName : PuzzleRadarAssetName;

        if (TryReadIcn(radarName) is not { Count: > 0 } radarFile)
        {
            GameLog.Warn($"'{radarName}' is missing; the puzzle map stays closed.");

            return;
        }

        string buttonName = evil ? EmptyButtons.SmallExitEvilFileName : EmptyButtons.SmallExitGoodFileName;

        if (GameAssets.GetImage(buttonName, 0) is not { } released ||
            GameAssets.GetImage(buttonName, 1) is not { } pressed)
        {
            GameLog.Warn($"'{buttonName}' is missing; the puzzle map stays closed.");

            return;
        }

        Sprite[] pieces = new Sprite[Puzzle.NumOfPuzzleTiles];

        for (int i = 0; i < pieces.Length; ++i)
        {
            pieces[i] = puzzleFile.Sprites[i];
        }

        GameKingdom kingdom = World.Current.GetKingdom(settings.Players.CurrentColor);

        PuzzleMapDialog dialog = new(
            _display,
            radarArea,
            new Point(GameBorder.BorderWidth, GameBorder.BorderWidth),
            picture,
            kingdom.GetPuzzleMaps(),
            pieces,
            radarFile.Sprites[0],
            new[] { released, pressed },
            MapSprites.GamePalette);

        // 参考把 alpha 从 250 起算，同一块图先整块画一次再淡出未揭开的块。
        dialog.RestartReveal();
        dialog.Show(events);
    }

    /// <summary>
    /// Opens the hero's spell book to pick a spell, <c>EventCastSpell()</c>'s call of
    /// <c>hero-&gt;OpenSpellBook( ADVN, true, false, {} )</c> (<c>interface_events.cpp:220-242</c>,
    /// <c>spell_book.cpp:132-397</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考把「有没有书」与「书里有没有能施放的法术」两句提示放在 <c>Hero::OpenSpellBook()</c> 里；
    /// <see cref="SpellBookWindow.TryOpen"/> 只回 <see langword="null"/>，所以这里按
    /// <see cref="Hero.HasSpellBook"/> 挑出参考的那一句再弹。
    /// </para>
    /// <para>
    /// 参考把翻到第几页记在英雄身上；端口的英雄没有这层状态，每次从第一页开始
    /// （<c>restorePreviousState: false</c>）。
    /// </para>
    /// </remarks>
    /// <param name="hero">The focused hero whose book is opened.</param>
    /// <param name="spell">The picked spell, or <see cref="SpellType.None"/> when nothing was cast.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns><see langword="true"/> when a spell was picked and is to be cast.</returns>
    public bool OpenSpellBook(Hero hero, out Spell spell, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(hero);

        spell = new Spell(SpellType.None);

        if (TryCreateSpellBookArtwork() is not { } artwork ||
            GetSmallFont() is not { } white || GetSmallGrayFont() is not { } gray)
        {
            return false;
        }

        SpellBookWindow? window = SpellBookWindow.TryOpen(hero, new SpellBook(), artwork,
            new SpellBookFonts(white, gray), SpellBookFilter.Adventure, canCastSpell: true,
            restorePreviousState: false, _display.Bounds.Size);

        if (window is null)
        {
            ShowMessage(string.Empty,
                hero.HasSpellBook ? SpellBookWindow.NoSpellToCastMessage : SpellBookWindow.NoMagicBookMessage,
                events);

            return false;
        }

        SpellType picked = window.Show(_display, events);

        if (picked == SpellType.None)
        {
            return false;
        }

        spell = new Spell(picked);

        return true;
    }

    /// <summary>
    /// Opens the file menu, <c>EventFileDialog()</c>'s <c>Dialog::FileOptions()</c>
    /// (<c>interface_events.cpp:292-295</c>, <c>dialog_file.cpp:42-159</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的六块大按钮在装素材时用 <c>makeSymmetricBackgroundSprites()</c> 现生成
    /// （<c>game_assets.cpp:1563-1578</c>）；端口没有生成物缓存，所以每次打开都现生成十二帧再交给
    /// <see cref="FileOptionsView.TryCreate"/>，取消键照旧取 <c>SYSTEM.ICN</c> 的 3/4 帧。
    /// </para>
    /// <para>
    /// 参考在这两处不返回模式：存档键先隐藏窗口再调 <c>EventSaveGame()</c>，快速存档键直接
    /// <c>Game::QuickSave()</c> 并在失败时弹一句，两者都回 <c>CANCEL</c>。这里照做，把两者的结果都算作
    /// <see cref="AdventureEventResult.Cancel"/>。
    /// </para>
    /// </remarks>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>The outcome of the pressed button.</returns>
    public AdventureEventResult ShowFileOptions(LocalEvent? events = null)
    {
        if (TryReadIcn(SystemAssetName) is not { } system)
        {
            return AdventureEventResult.Cancel;
        }

        List<Sprite> plates = ButtonFactory.MakeSymmetricBackgroundSprites(
            FileOptionsView.ButtonLabels, Settings.Get().IsEvilInterfaceEnabled, FileOptionsView.MinimumButtonWidth);

        if (FileOptionsView.TryCreate(plates, system.Sprites) is not { } view)
        {
            return AdventureEventResult.Cancel;
        }

        return FileOptionsDialog.Show(_display, view, events) switch
        {
            FileOptionsResult.NewGame => AdventureEventResult.NewGame,
            FileOptionsResult.LoadGame => AdventureEventResult.LoadGame,
            FileOptionsResult.Quit => AdventureEventResult.QuitGame,
            FileOptionsResult.SaveGame => SaveGame(events),
            FileOptionsResult.QuickSave => QuickSave(events),
            _ => AdventureEventResult.Cancel,
        };
    }

    /// <summary>Writes the quick save slot, the quick save branch of <c>dialog_file.cpp:108-114</c>.</summary>
    private AdventureEventResult QuickSave(LocalEvent? events)
    {
        if (!GameSave.QuickSave())
        {
            ShowMessage(string.Empty, "There was an issue during saving.", events);
        }

        return AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Opens the hero screen, <c>EventHeroesDialog()</c>'s <c>Heroes::OpenDialog( *hero, false, false )</c>
    /// (<c>interface_events.cpp:253-266</c>, <c>heroes_dialog.cpp:60-831</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的循环兑现五类请求：弹窗提示、快速信息、解雇确认、军队条的拆分/信息与上下一个英雄。这里能兑现前两类；
    /// 解雇缺 <c>Kingdom::RemoveHero</c> 的接线、军队条的输入（<see cref="ArmyBar"/> 自己也没接），切换英雄缺
    /// 英雄到王国的反向引用（<see cref="Hero"/> 不认识王国），所以 <c>canSwitchHeroes</c> 与 <c>canDismiss</c>
    /// 都传 <see langword="false"/>，把这三个按钮留在禁用状态。
    /// </para>
    /// <para>
    /// 招魂术的额外等级（参考的 <c>getNecromancyBonus()</c>）还没移植，次技能条拿到的是 0。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero to show.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    public void OpenHeroesDialog(Hero hero, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (HeroDialogArtwork.Load(hero, hero.GetColor(), Settings.Get().IsEvilInterfaceEnabled) is not { } artwork ||
            GetBodyFont() is not { } font || GetSmallFont() is not { } small)
        {
            return;
        }

        HeroDialog dialog = new(hero, artwork, new ArmyBarArtwork(), new ArtifactsBarArtwork(),
            new SkillBarSpriteSet(), font, small, canSwitchHeroes: false, canDismiss: false);

        while (dialog.Show(_display, events) == HeroDialogResult.None)
        {
            HeroDialogRequests requests = dialog.Requests;

            if (requests.IsEmpty)
            {
                break;
            }

            if (requests.Popup is { } popup)
            {
                ShowMessage(popup.Header, popup.Body, events);
            }

            if (requests.ShowQuickInfo)
            {
                ShowQuickInfo(hero, events);
            }

            requests.Clear();
        }
    }

    /// <summary>
    /// Opens the kingdom overview, <c>Kingdom::openOverviewDialog()</c> (<c>kingdom_overview.cpp:763-959</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的循环在点击里直接打开英雄屏、城堡屏、快速信息、收入与灯塔两个信息窗,以及军队条的交互;端口把这一切
    /// 收进 <see cref="KingdomOverviewWindow.Requests"/>,在这里兑现能兑现的部分:弹窗提示、英雄与城堡的快速信息、
    /// 英雄窗口。城堡窗口要 <see cref="CastleScreenView"/> 与 <see cref="CastleScreenArtwork"/> 的整套装配、军队条
    /// 还没有输入、收入与灯塔窗口要 <c>RESOURCE.ICN</c> 与 <c>OBJNMUL2.ICN</c> 的自定义元素,这几类留给后续;
    /// <c>NeedFadeIn</c> 与淡入淡出同样属于场景的绘制节奏。
    /// </para>
    /// <para>
    /// 当前玩家取 <c>Settings::CurrentColor()</c>,端口即 <see cref="Players.CurrentColor"/>
    /// (<see cref="Settings.Players"/>);世界取 <see cref="World.Current"/>,与 <see cref="AdventureMap.TryCreate"/>
    /// 用的是同一个单例。
    /// </para>
    /// </remarks>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    public void OpenKingdomOverview(LocalEvent? events = null)
    {
        if (KingdomOverviewArtwork.Load(_assets) is not { } artwork ||
            GetSmallFont() is not { } smallWhite || GetSmallYellowFont() is not { } smallYellow)
        {
            return;
        }

        GameKingdom kingdom = World.Current.Kingdoms.GetKingdom(Settings.Get().Players.CurrentColor);
        KingdomOverviewWindow window = new(kingdom, World.Current, artwork, smallWhite, smallYellow);

        while (!window.Show(_display, events))
        {
            KingdomOverviewRequests requests = window.Requests;

            if (requests.IsEmpty)
            {
                return;
            }

            if (requests.Popup is { } popup)
            {
                ShowMessage(popup.Header, popup.Body, events);
            }

            if (requests.HeroInfo is { } hero)
            {
                ShowQuickInfo(hero, events);
            }

            if (requests.CastleInfo is { } castle)
            {
                ShowQuickInfo(castle, events);
            }

            if (requests.HeroDialog is { } shownHero)
            {
                OpenHeroesDialog(shownHero, events);
            }

            requests.Clear();
        }
    }

    /// <summary>
    /// Opens the town screen, <c>Game::OpenCastleDialog( castle, true, true )</c>, the loop over
    /// <c>Castle::OpenDialog()</c> in <c>castle_dialog.cpp:220-859</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的 <see cref="CastleDialog"/> 把窗口拆成一份一份的请求交回调用方;这里的循环兑现弹窗提示,
    /// 其余请求(<c>Window</c> 的建造/招募/市场/魔法塔窗口、军队条)还没有兑现方,原因与总览那一段相同。
    /// 屏幕自己返回 <c>NextCastle</c>/<c>PreviousCastle</c> 时在同一王国的城镇里走一格再开,返回
    /// <c>Close</c> 或四个「下一个窗口」就结束——建造与魔法塔窗口未移植,所以那四个返回值也当作关闭。
    /// </para>
    /// <para>
    /// 城镇屏一次只显示一座城,所以每换一座城就重取一份 <see cref="CastleScreenArtwork.TryCreate"/>:
    /// <c>CREST</c> 的帧号是玩家色,邻城可能属于别的玩家。
    /// </para>
    /// </remarks>
    /// <param name="castle">The town to show.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> is <see langword="null"/>.</exception>
    public void OpenCastleDialog(CastleTown castle, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(castle);

        World world = World.Current;
        CastleTown? current = castle;

        while (current is not null)
        {
            CastleTown shown = current;

            if (CastleScreenArtwork.TryCreate(shown.Color) is not { } artwork ||
                GetSmallFont() is not { } smallFont)
            {
                return;
            }

            CastleDialog dialog = CreateCastleDialog(shown, artwork, smallFont, world);

            current = ShowCastleDialog(dialog, events) switch
            {
                CastleDialog.ReturnValue.NextCastle => GetAdjacentCastle(world, shown, 1),
                CastleDialog.ReturnValue.PreviousCastle => GetAdjacentCastle(world, shown, -1),
                _ => null,
            };
        }
    }

    /// <summary>
    /// Builds the town screen of one town, <c>Castle::OpenDialog()</c>'s prologue (<c>castle_dialog.cpp:220-330</c>).
    /// </summary>
    /// <param name="castle">The town to show.</param>
    /// <param name="artwork">The frames of the screen, already read for the town's color.</param>
    /// <param name="smallFont">The small font of the status bar.</param>
    /// <param name="world">The world the visiting hero is looked up in.</param>
    /// <returns>The dialog, ready for <see cref="CastleDialog.Show"/>.</returns>
    /// <remarks>
    /// 参考在开场里做三件事:找站在城里的英雄、取英雄的大头像与船长的大头像、把军队条的初始选择清掉。
    /// 最后一件由 <see cref="CastleScreenView.Show"/> 自己完成;大头像取不到就传空,屏幕会画空地。
    /// </remarks>
    private CastleDialog CreateCastleDialog(CastleTown castle, CastleScreenArtwork artwork,
        IGlyphSource smallFont, World world)
    {
        Hero? hero = world.GetHero(castle);

        return new CastleDialog(
            new CastleScreenView(artwork, smallFont),
            castle,
            hero,
            world,
            heroPortrait: hero is null ? null : GetHeroPortrait(hero),
            captainPortrait: castle.Captain.IsValid ? CaptainPortrait.TryCreateBig(_assets, castle.Captain) : null);
    }

    /// <summary>
    /// Runs one town screen and settles its requests, the body of <c>Castle::OpenDialog()</c>'s event loop.
    /// </summary>
    /// <param name="dialog">The screen of one town.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>Why the screen ended, as <see cref="CastleDialog.Show"/> reports it.</returns>
    /// <remarks>
    /// <see cref="CastleDialog.Show"/> 在请求非空时返回 <c>DoNothing</c>,所以这里要区分「引擎停了」与「有请求」:
    /// 前者请求为空,应当收尾;后者兑现完再让它继续跑同一份屏幕,和 <see cref="HeroDialog"/> 那一段一样。
    /// </remarks>
    private CastleDialog.ReturnValue ShowCastleDialog(CastleDialog dialog, LocalEvent? events)
    {
        while (true)
        {
            CastleDialog.ReturnValue result = dialog.Show(_display, events);

            if (result != CastleDialog.ReturnValue.DoNothing)
            {
                return result;
            }

            CastleDialogRequests requests = dialog.Requests;

            if (requests.IsEmpty)
            {
                return CastleDialog.ReturnValue.Close;
            }

            if (requests.Popup is { } popup)
            {
                ShowMessage(popup.Header, popup.Body, events);
            }

            requests.Clear();
        }
    }

    /// <summary>
    /// The town one step around the owner's ring, <c>getNextCastle()</c> of <c>castle_dialog.cpp</c>.
    /// </summary>
    /// <param name="world">The world whose kingdom is walked.</param>
    /// <param name="castle">The town the arrow was pressed on.</param>
    /// <param name="step">+1 for the next town, -1 for the previous one.</param>
    /// <returns>The neighbouring town, or <see langword="null"/> when it is the owner's only one.</returns>
    private static CastleTown? GetAdjacentCastle(World world, CastleTown castle, int step)
    {
        IReadOnlyList<CastleTown> towns = world.GetKingdom(castle.Color).Castles;

        if (towns.Count < 2)
        {
            return null;
        }

        for (int index = 0; index < towns.Count; ++index)
        {
            if (ReferenceEquals(towns[index], castle))
            {
                return towns[(index + step + towns.Count) % towns.Count];
            }
        }

        return null;
    }

    /// <summary>The hero's big portrait, <c>hero.getPortrait( PORT_BIG )</c> (<c>ICN::PORT0000</c>…).</summary>
    /// <param name="hero">The hero whose face is wanted.</param>
    /// <returns>The portrait, or <see langword="null"/> when the race has no portrait in the archives.</returns>
    private Sprite? GetHeroPortrait(Hero hero)
    {
        IcnType icn = HeroPortraitIcn.Get(hero.PortraitId);

        if (icn == IcnType.Unknown || TryReadIcn(IcnCatalog.GetFileName(icn)) is not { Count: > 0 } file)
        {
            return null;
        }

        return file.Sprites[0];
    }

    /// <summary>
    /// Evaluates the end-of-scenario conditions and shows what they ask for, the tail of
    /// <c>GameOver::Result::checkGameOver()</c> (<c>game_over.cpp:481-682</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在这个函数里依次做四件事：逐个通报出局的玩家、弹失败或胜利框、重置音频、放 <c>LOSE.SMK</c>/<c>WIN.SMK</c>。
    /// 端口只做前两件：判定与「该弹什么」由 <see cref="GameOverResult"/> 放进
    /// <see cref="GameOverResult.Requests"/>，这里把那些窗口画出来；音频与片子是调用方的事（请求里已有
    /// <see cref="GameOverRequests.LoseVideo"/>、<see cref="GameOverRequests.WinVideo"/> 与
    /// <see cref="GameOverRequests.VictoryMusic"/>）。
    /// </para>
    /// <para>
    /// 判定与展示的顺序照参考：先 <see cref="GameOverResult.CheckGameOver"/>，再通报出局者，最后才是自己的胜负框。
    /// 美术缺失时记一条警告并跳过对应的窗口，但返回值仍是判定结果——窗口画不出来不该改变游戏是否结束。
    /// </para>
    /// </remarks>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// What the game should switch to, the <see cref="AdventureEventResult"/> equivalent of the
    /// <see cref="GameOverMode"/> the verdict returned; <see cref="AdventureEventResult.Cancel"/> while the game goes on.
    /// </returns>
    public AdventureEventResult CheckGameOver(LocalEvent? events = null)
    {
        GameOverResult verdict = GameOverResult.Current;
        GameOverMode mode = verdict.CheckGameOver();

        ShowGameOverRequests(verdict.Requests, events);

        return ToAdventureEventResult(mode);
    }

    /// <summary>
    /// Turns the four <see cref="GameOverMode"/> values into the outcomes the adventure map reports.
    /// </summary>
    /// <remarks>
    /// <see cref="AdventureEventResult"/> 没有「回主菜单」这一项：参考的 <c>MAIN_MENU</c> 是失败之后的结果，
    /// 所以失败映射成 <see cref="AdventureEventResult.Lose"/>；战役关的 <c>COMPLETE_CAMPAIGN_SCENARIO</c> 是赢了这一关，
    /// 映射成 <see cref="AdventureEventResult.Win"/>。场景切换由 App 解释这三个值。
    /// </remarks>
    /// <param name="mode">The verdict of <see cref="GameOverResult.CheckGameOver"/>.</param>
    /// <returns>The outcome for the adventure screen.</returns>
    public static AdventureEventResult ToAdventureEventResult(GameOverMode mode) => mode switch
    {
        GameOverMode.MainMenu => AdventureEventResult.Lose,
        GameOverMode.HighScoresStandard => AdventureEventResult.HighScore,
        GameOverMode.CompleteCampaignScenario => AdventureEventResult.Win,
        _ => AdventureEventResult.Cancel,
    };

    /// <summary>
    /// Draws the windows a verdict asked for, <c>Game::DialogPlayers()</c> followed by <c>DialogLoss()</c> /
    /// <c>DialogWins()</c>.
    /// </summary>
    /// <param name="requests">What <see cref="GameOverResult.CheckGameOver"/> collected.</param>
    /// <param name="events">Event state to pull from.</param>
    private void ShowGameOverRequests(GameOverRequests requests, LocalEvent? events)
    {
        if (requests.IsEmpty)
        {
            return;
        }

        if (GetHeaderFont() is not { } headerFont || GetBodyFont() is not { } bodyFont ||
            GetButtons() is not { } buttons)
        {
            return;
        }

        if (requests.Vanquished.Count > 0)
        {
            if (GetSignArtwork() is { } sign)
            {
                foreach (PlayerColor color in requests.Vanquished)
                {
                    GameOverDialogs.ShowVanquished(_display, color, VanquishedTitle, VanquishedMessage,
                        sign, headerFont, bodyFont, buttons, events);
                }
            }
            else
            {
                GameLog.Warn(
                    $"The player signs of '{BrcrestAssetName}' are missing; the elimination notices stay closed.");
            }
        }

        // 参考的两个框都从 world.GetMapInfo() 取条件参数；端口从设置里取同一份文件头。
        if (Settings.Get().CurrentMapFileInfo is not { } info)
        {
            GameLog.Warn("The map information is missing; the victory and defeat windows stay closed.");

            return;
        }

        if (requests.LossNotice is uint loss)
        {
            GameOverDialogs.ShowDefeat(_display, loss, info, headerFont, bodyFont, buttons, events: events);
        }

        if (requests.WinsNotice is uint wins)
        {
            GameOverDialogs.ShowVictory(_display, wins, info, headerFont, bodyFont, buttons, events: events);
        }
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
    /// Asks for a name and writes the game, <c>Interface::AdventureMap::EventSaveGame()</c>
    /// (<c>interface_events.cpp:329-350</c>).
    /// </summary>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// Always <see cref="AdventureEventResult.Cancel"/>: the reference ends with <c>fheroes2::GameMode::CANCEL</c>
    /// whether the save worked, failed or was never asked for.
    /// </returns>
    public AdventureEventResult SaveGame(LocalEvent? events = null)
    {
        while (true)
        {
            string? fileName = SelectSaveFile(events);

            if (fileName is null)
            {
                return AdventureEventResult.Cancel;
            }

            // 参考：同名文件要先问一句，答 No 就退回去重新选名，而不是直接放弃。
            if (System.IO.File.Exists(fileName) &&
                !ShowYesNo(string.Empty, "Are you sure you want to overwrite the save with this name?", events))
            {
                continue;
            }

            if (GameSave.Save(fileName))
            {
                ShowMessage(string.Empty, "Game saved successfully.", events);
            }
            else
            {
                ShowMessage(string.Empty, "There was an issue during saving.", events);
            }

            return AdventureEventResult.Cancel;
        }
    }

    /// <summary>
    /// Shows the quick info of a tile, <c>Dialog::QuickInfo( const Maps::Tile &amp; )</c>
    /// (<c>dialog_quickinfo.cpp:914-937</c>).
    /// </summary>
    /// <param name="tile">Tile under the cursor.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考把 <c>getMainTile()</c> 的索引再交给 <c>getQuickInfoText( world.getTile( index ) )</c>：站在矿洞或城堡上的
    /// 英雄由它背后的那一格说话。没侦察过的瓦片连地面都不讲，只说一句 <c>"Uncharted Territory"</c>。
    /// </remarks>
    public void ShowQuickInfo(Tile tile, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(tile);

        LocalEvent input = events ?? LocalEvent.Current;

        if (GetQuickInfoFrame() is not { } frame || GetSmallFont() is not { } font)
        {
            return;
        }

        PlayerColor color = Settings.Get().Players.CurrentColor;
        World world = World.Current;

        string text;
        if (tile.IsFog(color))
        {
            text = QuickInfoText.UnchartedTerritory;
        }
        else
        {
            int mainIndex = Tile.GetIndexOfMainTile(tile, world);

            // getMainTile() 取不到时参考仍在原瓦片上取文字。
            Tile described = mainIndex < 0 ? tile : world.GetTile(mainIndex);

            text = QuickInfoText.ForTile(described, world, color, InterfaceFocus.GetFocusHeroes());
        }

        if (StandardPopupWindow.TryCreate(frame, font, text, input.CursorPosition, _display.Bounds) is { } popup)
        {
            Present(popup, input);
        }
    }

    /// <summary>
    /// Shows the quick info of a castle, <c>Dialog::QuickInfo( const Castle &amp; )</c>
    /// (<c>dialog_quickinfo.cpp:597-660</c>).
    /// </summary>
    /// <param name="castle">Castle under the cursor.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="castle"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 两处「看得见」决定面板画多少：自家城堡或水晶球照到的城堡给完整驻军（<c>isDetailedView</c>），
    /// 盗贼公会等级决定守卫数量是精确值还是模糊区间（<c>thievesGuildCount</c>）。
    /// </remarks>
    public void ShowQuickInfo(CastleTown castle, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(castle);

        LocalEvent input = events ?? LocalEvent.Current;

        if (GetCastleQuickInfoArtwork() is not { } artwork ||
            GetSmallFont() is not { } font ||
            GetMonsters() is not { } monsters ||
            GetIcons() is not { } icons)
        {
            return;
        }

        Settings settings = Settings.Get();
        World world = World.Current;
        PlayerColor color = settings.Players.CurrentColor;
        GameKingdom kingdom = world.GetKingdom(color);

        bool detailedView = Players.IsFriends(castle.Color, color) ||
            (world.Geometry is { } geometry && kingdom.IsTileVisibleFromCrystalBall(geometry, castle.TileIndex));

        uint thievesGuildCount = (uint)Math.Max(kingdom.CountThievesGuilds(), 0);

        if (QuickInfoDialog.CreateForCastle(castle).TryCreateCastleView(
                artwork,
                font,
                monsters,
                icons,
                settings.IsEvilInterfaceEnabled,
                detailedView,
                thievesGuildCount,
                input.CursorPosition,
                _display.Bounds) is { } view)
        {
            Present(view, input);
        }
    }

    /// <summary>
    /// Shows the quick info of a hero, <c>Dialog::QuickInfo( const HeroBase &amp; )</c>
    /// (<c>dialog_quickinfo.cpp:663-690</c>).
    /// </summary>
    /// <param name="hero">Hero under the cursor.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <c>isFullInfo</c> 的四个来源照参考：中立英雄、友方英雄、本回合有人用过鉴定术、水晶球照到的那一格。
    /// 第四条要求英雄在主地图上站得住（<see cref="Hero.MapIndex"/> 有效）。
    /// </remarks>
    public void ShowQuickInfo(Hero hero, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(hero);

        LocalEvent input = events ?? LocalEvent.Current;

        if (GetHeroQuickInfoArtwork() is not { } artwork ||
            GetSmallFont() is not { } font ||
            GetMonsters() is not { } monsters)
        {
            return;
        }

        World world = World.Current;
        PlayerColor color = Settings.Get().Players.CurrentColor;
        GameKingdom kingdom = world.GetKingdom(color);

        bool fullInfo = hero.Color == PlayerColor.None
            || hero.IsFriends(color)
            || kingdom.Modes.HasFlag(KingdomModes.IdentifyHero)
            || (hero.MapIndex >= 0 &&
                world.Geometry is { } geometry &&
                kingdom.IsTileVisibleFromCrystalBall(geometry, hero.MapIndex));

        if (QuickInfoDialog.CreateForHero(hero).TryCreateHeroView(
                artwork,
                font,
                monsters,
                GetPortrait(hero.Id),
                fullInfo,
                input.CursorPosition,
                _display.Bounds) is { } view)
        {
            Present(view, input);
        }
    }

    /// <summary>Hands a box to <see cref="PresentQuickInfo"/>, or shows it with the blocking loop.</summary>
    private void Present(IQuickInfoPanel panel, LocalEvent input)
    {
        if (PresentQuickInfo is { } present)
        {
            present(panel);

            return;
        }

        QuickInfoPopup.Show(_display, panel, input);
    }

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

    /// <summary>The small grey font, <c>Font::Get( FontSize::SMALL, GRAY_FONT )</c>.</summary>
    /// <returns>The font, or <see langword="null"/> when <c>FONT.ICN</c> is missing or broken.</returns>
    private IGlyphSource? GetSmallGrayFont()
    {
        if (!_smallGrayFontLoaded)
        {
            _smallGrayFontLoaded = true;
            _smallGrayFont = LoadFont(BitmapFontSize.Small, PaletteId.GrayFont, "small grey");
        }

        return _smallGrayFont;
    }

    /// <summary>The small yellow font, <c>Font::Get( FontSize::SMALL, YELLOW_FONT )</c>.</summary>
    /// <returns>The font, or <see langword="null"/> when <c>FONT.ICN</c> is missing or broken.</returns>
    private IGlyphSource? GetSmallYellowFont()
    {
        if (!_smallYellowFontLoaded)
        {
            _smallYellowFontLoaded = true;
            _smallYellowFont = LoadFont(BitmapFontSize.Small, PaletteId.YellowFont, "small yellow");
        }

        return _smallYellowFont;
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

    /// <summary>The frame of the tile popup, <c>QWIKINFO.ICN</c> frame 0, read once.</summary>
    /// <returns>The frame, or <see langword="null"/> when the archive is missing or empty.</returns>
    private Sprite? GetQuickInfoFrame()
    {
        if (_quickInfoFrameLoaded)
        {
            return _quickInfoFrame;
        }

        _quickInfoFrameLoaded = true;

        if (TryReadIcn(StandardPopupWindow.AssetName) is not { Count: > 0 } file)
        {
            GameLog.Warn(
                $"The quick info artwork is missing ('{StandardPopupWindow.AssetName}'); " +
                "the right button shows no popup.");

            return null;
        }

        _quickInfoFrame = file.Sprites[0];

        return _quickInfoFrame;
    }

    /// <summary>The <c>QWIKTOWN</c> frames of the castle panel, read once.</summary>
    /// <returns>The artwork, or <see langword="null"/> when it cannot be read.</returns>
    private CastleQuickInfoArtwork? GetCastleQuickInfoArtwork()
    {
        if (!_castleQuickInfoArtworkLoaded)
        {
            _castleQuickInfoArtworkLoaded = true;
            _castleQuickInfoArtwork = TryCreateCastleQuickInfoArtwork();
        }

        return _castleQuickInfoArtwork;
    }

    /// <summary>The <c>QWIKHERO</c> frames of the hero panel, read once.</summary>
    /// <returns>The artwork, or <see langword="null"/> when it cannot be read.</returns>
    private HeroQuickInfoArtwork? GetHeroQuickInfoArtwork()
    {
        if (!_heroQuickInfoArtworkLoaded)
        {
            _heroQuickInfoArtworkLoaded = true;

            try
            {
                _heroQuickInfoArtwork = HeroQuickInfoArtwork.TryCreate(_assets, Settings.Get().IsEvilInterfaceEnabled);
            }
            catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
            {
                GameLog.Warn($"The hero quick info artwork could not be read: {exception.Message}");
            }
        }

        return _heroQuickInfoArtwork;
    }

    /// <summary>The army portraits of both panels, <c>MONS32.ICN</c>, read once.</summary>
    /// <returns>The set, or <see langword="null"/> when it cannot be read.</returns>
    private IMiniMonsterSprites? GetMonsters()
    {
        if (!_monstersLoaded)
        {
            _monstersLoaded = true;
            _monsters = Mons32Sprites.TryCreate(_assets);
        }

        return _monsters;
    }

    /// <summary>The <c>LOCATORS</c> icons the castle panel draws, read once.</summary>
    /// <returns>The source, or <see langword="null"/> when it cannot be read.</returns>
    private IViewWorldSpriteSource? GetIcons()
    {
        if (!_iconsLoaded)
        {
            _iconsLoaded = true;
            _icons = new IcnViewWorldSpriteSource(_assets);
        }

        return _icons;
    }

    /// <summary>Reads one hero face, <c>ICN::MINIPORT</c> frame <paramref name="index"/>.</summary>
    /// <param name="index">The hero id, which is the frame of the archive.</param>
    /// <returns>The face, or <see langword="null"/> when the archive lacks that frame.</returns>
    private Sprite? GetPortrait(int index)
    {
        if (index < 0 || TryReadIcn(MiniportAssetName) is not { } file || index >= file.Count)
        {
            return null;
        }

        return file.Sprites[index];
    }

    /// <summary>The <c>BOOK</c> and <c>SPELLS</c> frames of the spell book, read each time it opens.</summary>
    /// <returns>The artwork, or <see langword="null"/> when either archive is missing or too short.</returns>
    private SpellBookArtwork? TryCreateSpellBookArtwork()
    {
        if (TryReadIcn(BookAssetName) is not { } book || book.Count <= StraightCornerFrame)
        {
            return null;
        }

        if (TryReadIcn(SpellsAssetName) is not { Count: > 0 } spells)
        {
            return null;
        }

        return new SpellBookArtwork
        {
            Page = book.Sprites[0],
            AdventureBookmark = book.Sprites[3],
            CombatBookmark = book.Sprites[4],
            CloseBookmark = book.Sprites[5],
            SpellPointsBookmark = book.Sprites[6],
            StraightCorner = book.Sprites[StraightCornerFrame],
            SpellIcons = spells.Sprites,
        };
    }

    /// <summary>The <c>BRCREST</c> frames of the elimination notice, read once.</summary>
    /// <returns>The signs, or <see langword="null"/> when the archive is missing or has too few frames.</returns>
    private IVanquishedSignArtworkSource? GetSignArtwork()
    {
        if (_signArtworkLoaded)
        {
            return _signArtwork;
        }

        _signArtworkLoaded = true;

        if (TryReadIcn(BrcrestAssetName) is not { } file || file.Count <= SignBorderFrame)
        {
            return null;
        }

        _signArtwork = new BrcrestSignArtwork(file);

        return _signArtwork;
    }

    private CastleQuickInfoArtwork? TryCreateCastleQuickInfoArtwork()
    {
        try
        {
            return CastleQuickInfoArtwork.TryCreate(_assets, Settings.Get().IsEvilInterfaceEnabled);
        }
        catch (Exception exception) when (exception is BinaryFormatException or ArgumentException)
        {
            GameLog.Warn($"The castle quick info artwork could not be read: {exception.Message}");

            return null;
        }
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

    /// <summary>
    /// The empty sign and the six crests of <c>BRCREST.ICN</c>, the <c>switch ( color )</c> of
    /// <c>game_startgame.cpp:256-282</c>.
    /// </summary>
    /// <remarks>
    /// 参考对六个颜色各写一条 <c>Blit</c>；这里靠 <see cref="PlayerColors.IndexOf"/>——帧序与 <c>PlayerColors.Ordered</c>
    /// 同为蓝、绿、红、黄、橙、紫，所以不必再抄一遍分支。
    /// </remarks>
    private sealed class BrcrestSignArtwork : IVanquishedSignArtworkSource
    {
        private readonly IcnSpriteFile _file;

        internal BrcrestSignArtwork(IcnSpriteFile file) => _file = file;

        /// <inheritdoc />
        public Sprite Border => _file.Sprites[SignBorderFrame];

        /// <inheritdoc />
        public Sprite Crest(PlayerColor color) => _file.Sprites[PlayerColors.IndexOf(color)];
    }
}
