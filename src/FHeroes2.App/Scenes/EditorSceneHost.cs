using System.Diagnostics.CodeAnalysis;
using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Spells;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// What the editor scene asks of the host: the frame pump its windows run on, and the windows themselves
/// built out of the archives.
/// </summary>
/// <remarks>
/// <para>
/// 参考的编辑器直接伸手去拿界面：<c>showStandardTextMessage</c>、<c>openMapSpecificationsDialog</c>、
/// <c>eventFileDialog</c> 都是阻塞调用（<c>editor_interface_panel.cpp:1461-1479</c>、<c>editor_interface.cpp:2068</c>）。
/// 端口一条帧循环，所以这些窗口借宿主的 <see cref="RunModal"/> 进内层循环，关掉之后外层接着跑——与
/// <see cref="AdventureHeroActionHost"/> 同一条缝。
/// </para>
/// <para>
/// <b>从哪来</b>：<see cref="RunModal"/> 由 <c>GameHost</c> 在造好编辑器场景之后接上（和
/// <c>AdventureMapScene.ExitEvent</c> 一样的位置）。没接上时提示只在日志里留一行，不弹窗。
/// </para>
/// <para>
/// 造窗口的方法留在这里而不是场景里：它们要的字体、按钮与页面美术都是这里攒下的两样（<see cref="AssetBundle"/>
/// 与 <see cref="GameFonts"/>）。文件菜单与系统菜单随后面几个子任务加进同样的位置。
/// </para>
/// </remarks>
internal sealed class EditorSceneHost
{
    /// <summary>Title of the box the host shows when a window reports a failure.</summary>
    public const string ErrorTitle = "Error";

    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;

    private SystemButtonArtwork? _buttons;
    private EditorListArtwork? _listArtwork;
    private EditorDailyEventSpecArtwork? _dailyEventArtwork;
    private ItemSelectionWindowArtwork? _itemSelectionArtwork;
    private IcnViewWorldSpriteSource? _sprites;
    private VirtualKeyboardArtwork? _keyboardArtwork;

    /// <summary>Collects what a window of the editor needs from the archives.</summary>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="fonts">The fonts of the windows; <see langword="null"/> when the data is missing.</param>
    public EditorSceneHost(AssetBundle? assets, GameFonts? fonts)
    {
        _assets = assets;
        _fonts = fonts;
    }

    /// <summary>
    /// Runs a modal window to its end, <c>GameHost.RunModal</c>; <see langword="null"/> while the host has not
    /// wired it up.
    /// </summary>
    public Action<IModalScreen>? RunModal { get; set; }

    /// <summary>Puts an OK-only box to the player and waits for it, <c>fheroes2::showStandardTextMessage( …, Dialog::OK )</c>.</summary>
    /// <param name="header">Title of the box; empty for none.</param>
    /// <param name="body">The message.</param>
    /// <returns>Whether the box was run; <see langword="false"/> when the fonts, the buttons or the frame pump are missing.</returns>
    public bool ShowMessage(string header, string body)
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn($"The box \"{body}\" has no frame pump (RunModal); it is skipped.");

            return false;
        }

        if (TryCreateMessageBox(header, body) is not { } box)
        {
            return false;
        }

        runModal(box);

        return true;
    }

    /// <summary>
    /// Runs the map specifications window to its end and writes the result back,
    /// <c>openMapSpecificationsDialog()</c> of the reference.
    /// </summary>
    /// <param name="session">The edit session the accepted changes are committed to.</param>
    /// <param name="gamePalette">Palette the condition panels tint their town and hero rows with.</param>
    /// <remarks>
    /// 关窗后的两步照参考：确定就把 <c>MapSpecsState</c> 写回地图（<c>commitMapChange</c>），失败弹错误；取消就什么
    /// 都不写。规格窗自己有内层窗（消息框、文本输入、后面的传闻/事件/语言），它们由 <see cref="EditorMapSpecsModal"/>
    /// 自己摞起来——不能在这里递归 <see cref="RunModal"/>，那会在已经开着窗口的时候再压一层。
    /// </remarks>
    public void RunMapSpecs(MapEditSession session, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The map specifications window has no frame pump (RunModal); it is skipped.");

            return;
        }

        bool evil = Settings.Get().IsEvilInterfaceEnabled;

        if (MapSpecsPageArtwork.TryCreate(evil) is not { } artwork)
        {
            GameLog.Warn("The map specifications window needs the page artwork, and it is missing.");

            return;
        }

        var window = EditorMapSpecsWindow.Create(artwork, new MapSpecsState(session.Map), _fonts?.White, gamePalette);

        var modal = new EditorMapSpecsModal(window, new EditorMapSpecsInput(window), session, this);

        runModal(modal);

        if (modal.Error is { } error)
        {
            ShowMessage(ErrorTitle, error);
        }
    }

    /// <summary>
    /// Runs one turn of the editor's options page, <c>openEditorOptionsDialog()</c> of the reference.
    /// </summary>
    /// <returns>
    /// The action the page was left with; <see cref="EditorOptionsAction.Exit"/> when the author closed it or when the
    /// page could not be built at all (nothing else can be done with a missing window).
    /// </returns>
    /// <remarks>
    /// 端口只跑一轮：参考的 <c>openEditorOptionsDialog()</c> 是个阻塞循环，动作不等于 <c>Configuration</c> 就
    /// <c>return action</c>；「按动作改设置、再重开一层」在 <c>openEditorSettings()</c> 里
    /// （<c>editor_options.cpp:235-378</c>），那是场景的活——通行性叠层与界面换皮都要重画地图。
    /// </remarks>
    public EditorOptionsAction RunEditorOptionsDialog()
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The editor options page has no frame pump (RunModal); it is skipped.");

            return EditorOptionsAction.Exit;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The editor options page needs the OK button, and it is missing.");

            return EditorOptionsAction.Exit;
        }

        if (EditorOptionsArtwork.TryCreate(buttons) is not { } artwork)
        {
            GameLog.Warn("The editor options page needs its artwork, and it is missing.");

            return EditorOptionsAction.Exit;
        }

        if (EditorOptionsDialog.TryCreate(ScreenBuffer.DefaultSize, artwork) is not { } dialog)
        {
            GameLog.Warn("The editor options page is incomplete, and it is skipped.");

            return EditorOptionsAction.Exit;
        }

        var modal = new EditorOptionsModal(dialog, this);

        runModal(modal);

        // 模态没拿到动作就结束只可能是被外部收掉了，当关窗处理。
        return modal.Action;
    }

    /// <summary>
    /// Runs the editor's file menu to its end, <c>eventFileDialog()</c> of the reference.
    /// </summary>
    /// <returns>
    /// The button the menu was left with; <see cref="EditorFileResult.Cancel"/> when it could not be built at all.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 七块牌子不是归档里的帧：参考在载入时用 <c>makeSymmetricBackgroundSprites</c> 把 NEW MAP 这些字样压进
    /// 底图（<c>game_assets.cpp:1579-1593</c>），端口没有生成物缓存，所以这里现做——这一步要字体，字体没加载就
    /// 干脆别开窗，否则 <c>Text</c> 会在测量时炸。
    /// </para>
    /// <para>
    /// 取消键照参考取 <c>BUTTON_SMALL_CANCEL</c> 的 0/1 帧（<c>editor_interface.cpp:2090</c>），好坏两套主题各一张，
    /// 由 <see cref="Settings.IsEvilInterfaceEnabled"/> 决定。
    /// </para>
    /// </remarks>
    public EditorFileResult RunFileDialog()
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The editor file menu has no frame pump (RunModal); it is skipped.");

            return EditorFileResult.Cancel;
        }

        if (_fonts is null)
        {
            GameLog.Warn("The editor file menu needs the fonts, and they are missing.");

            return EditorFileResult.Cancel;
        }

        bool evil = Settings.Get().IsEvilInterfaceEnabled;

        string cancelName = GameAssets.GetFileName(
            evil ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood);

        if (GameAssets.GetImage(cancelName, 0) is not { } cancelReleased ||
            GameAssets.GetImage(cancelName, 1) is not { } cancelPressed)
        {
            GameLog.Warn($"The editor file menu needs \"{cancelName}\", and it is missing.");

            return EditorFileResult.Cancel;
        }

        List<Sprite> plates = ButtonFactory.MakeSymmetricBackgroundSprites(
            EditorFileDialog.ButtonLabels, evil, EditorFileDialog.MinimumButtonWidth);

        if (EditorFileDialog.TryCreate(ScreenBuffer.DefaultSize, plates, [cancelReleased, cancelPressed]) is not { } dialog)
        {
            GameLog.Warn("The editor file menu is incomplete, and it is skipped.");

            return EditorFileResult.Cancel;
        }

        var modal = new EditorFileModal(dialog, this);

        runModal(modal);

        return modal.Result == EditorFileResult.None ? EditorFileResult.Cancel : modal.Result;
    }

    /// <summary>
    /// Runs the spell selection window to its end, <c>openSpellSelectionWindow()</c> of the reference, and reopens
    /// it when the author asked for another level.
    /// </summary>
    /// <param name="title">The line above the scrolls; the callers pass the object or building name with a colon.</param>
    /// <param name="spellLevel">The level to start on, 1 to 5.</param>
    /// <param name="selectedSpells">The spells the caller holds; an empty list means every spell.</param>
    /// <param name="isMultiLevelSelectionEnabled">Whether the five level buttons are shown.</param>
    /// <param name="minimumEnabledSpells">How many spells have to stay enabled for OKAY to work.</param>
    /// <param name="pickDisabledSpells">Whether the caller wants the disabled spells instead of the enabled ones.</param>
    /// <returns>How the window was left and, when accepted, the spells of the last level shown.</returns>
    /// <remarks>
    /// 换等级的重开循环在参考里分两层：窗口自己 <c>break</c>（<c>editor_spell_selection.cpp:324-326</c>），调用方的
    /// <c>while</c> 拿着改过的 <c>spellLevel</c> 再来一次（<c>editor_castle_details_window.cpp:515</c>）。端口把两层
    /// 合成这个方法里的一个循环——调用方只看结果，不用自己拼 <c>while</c>。换等级时列表不算数，重开用的是调用方
    /// 原来那份（参考在 <c>break</c> 之后跳过了写回）。
    /// </remarks>
    public EditorSpellSelectionDialogResult RunSpellSelection(string title, int spellLevel,
        IReadOnlyList<SpellType> selectedSpells, bool isMultiLevelSelectionEnabled = false,
        int minimumEnabledSpells = 1, bool pickDisabledSpells = false)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(selectedSpells);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The spell selection window has no frame pump (RunModal); it is skipped.");

            return new EditorSpellSelectionDialogResult(false, spellLevel, []);
        }

        if (spellLevel is < 1 or > 5)
        {
            // 参考在这里 assert，调用方给错了等级就没有窗口可开。
            GameLog.Warn($"The spell selection window needs a level between 1 and 5, not {spellLevel}.");

            return new EditorSpellSelectionDialogResult(false, spellLevel, []);
        }

        if (_fonts?.Yellow is not { } titleFont || _fonts.White is not { } spellFont)
        {
            GameLog.Warn("The spell selection window needs the game fonts, and they are missing.");

            return new EditorSpellSelectionDialogResult(false, spellLevel, []);
        }

        if (EditorSpellSelectionArtwork.TryCreate(Settings.Get().IsEvilInterfaceEnabled) is not { } artwork)
        {
            GameLog.Warn("The spell selection window needs its artwork, and it is missing.");

            return new EditorSpellSelectionDialogResult(false, spellLevel, []);
        }

        int level = spellLevel;

        while (true)
        {
            var window = EditorSpellSelectionWindow.Create(artwork, title, level, selectedSpells,
                isMultiLevelSelectionEnabled, minimumEnabledSpells, pickDisabledSpells, titleFont, spellFont);

            var modal = new EditorSpellSelectionModal(window, new EditorSpellSelectionInput(window), this);

            runModal(modal);

            // 换了等级就按新的等级重开一层；等级没变却说要换，说明状态机与窗口对不上，当取消收场。
            if (modal.Outcome == EditorSpellSelectionOutcome.LevelChanged && modal.Level != level)
            {
                level = modal.Level;

                continue;
            }

            return new EditorSpellSelectionDialogResult(modal.Outcome == EditorSpellSelectionOutcome.Accepted, level,
                modal.Spells);
        }
    }

    /// <summary>Builds a message box ready to be run; <see langword="null"/> when the fonts or the buttons are missing.</summary>
    /// <param name="header">Title of the box; empty for none.</param>
    /// <param name="body">The message.</param>
    /// <returns>The box, or <see langword="null"/> with the reason in the log.</returns>
    public MessageBoxModal? TryCreateMessageBox(string header, string body)
    {
        if (_fonts is null)
        {
            GameLog.Warn($"The box \"{body}\" needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is null)
        {
            GameLog.Warn($"The box \"{body}\" needs the standard buttons, and they are missing.");

            return null;
        }

        return MessageBoxModal.Create(header, body, _fonts.Yellow, _fonts.White, DialogButton.Ok, _buttons);
    }

    /// <summary>Builds the input window a request of the specifications page asks for, <c>Dialog::inputString</c>.</summary>
    /// <param name="dialog">The dialog the page state machine filled in.</param>
    /// <returns>The window, or <see langword="null"/> when the fonts or the input frame are missing.</returns>
    public InputStringModal? TryCreateInput(InputStringDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (_fonts is null)
        {
            GameLog.Warn("The input window needs the game fonts, and they are missing.");

            return null;
        }

        if (InputStringWindow.TryCreate(dialog, _fonts.Yellow, _fonts.White, ScreenBuffer.DefaultSize) is not { } window)
        {
            GameLog.Warn("The input window needs the input frame, and it is missing.");

            return null;
        }

        return new InputStringModal(window);
    }

    /// <summary>
    /// Builds the number pad <c>openVirtualNumpad()</c> puts on the screen (<c>ui_keyboard.cpp:1295-1320</c>).
    /// </summary>
    /// <param name="value">The number the field shows now.</param>
    /// <param name="minimum">Smallest number the caller accepts; a negative one makes the pad signed.</param>
    /// <param name="maximum">Largest number the caller accepts.</param>
    /// <returns>The pad, or <see langword="null"/> with the reason in the log when the artwork is missing.</returns>
    /// <remarks>范围检查不在这里：参考是关窗时才 <c>std::stoi</c> 再比，越界要回到键盘上重打，端口照这个顺序。</remarks>
    public VirtualKeyboardView? TryCreateNumpad(int value, int minimum, int maximum)
    {
        bool isEvil = Settings.Get().IsEvilInterfaceEnabled;

        if (_keyboardArtwork is null)
        {
            string okayName = isEvil
                ? VirtualKeyboardArtwork.EvilOkayButtonAssetName
                : VirtualKeyboardArtwork.OkayButtonAssetName;
            Sprite? background = GameAssets.GetImage(isEvil
                ? VirtualKeyboardArtwork.EvilButtonBackgroundAssetName
                : VirtualKeyboardArtwork.ButtonBackgroundAssetName, 0);
            Sprite? request = GameAssets.GetImage(VirtualKeyboardArtwork.RequestBackgroundAssetName, 0);
            Sprite? okayReleased = GameAssets.GetImage(okayName, 0);
            Sprite? okayPressed = GameAssets.GetImage(okayName, 1);

            _keyboardArtwork = background is null || request is null || okayReleased is null || okayPressed is null
                ? null
                : VirtualKeyboardArtwork.TryCreate(background, request, [okayReleased, okayPressed]);
        }

        if (_keyboardArtwork is null)
        {
            GameLog.Warn("The number pad needs the keyboard artwork, and it is missing.");

            return null;
        }

        VirtualKeyboardView? pad = VirtualKeyboardView.TryCreateNumpad(_keyboardArtwork, ScreenBuffer.DefaultSize, value,
            minimum, maximum, isEvil);

        if (pad is null)
        {
            GameLog.Warn($"The number pad for {value} needs the game fonts, and they are missing.");
        }

        return pad;
    }

    /// <summary>
    /// Builds the box that describes one spell, <c>SpellDialogElement::showPopup()</c> (<c>ui_dialog.cpp:543-546</c>).
    /// </summary>
    /// <param name="spell">The spell the author right-clicked.</param>
    /// <returns>The box, or <see langword="null"/> with the reason in the log when the data is missing.</returns>
    public MessageBoxModal? TryCreateSpellInfo(SpellType spell)
    {
        if (_fonts is null)
        {
            GameLog.Warn("The spell description needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is null)
        {
            GameLog.Warn("The spell description needs the standard buttons, and they are missing.");

            return null;
        }

        // 图标是 ICN::SPELLS 的一帧，按 Spell::IndexSprite() 取（editor_spell_selection.cpp:117 同一个下标）。
        var described = new Spell(spell);
        Sprite? icon = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Spells), (int)described.IndexSprite());

        if (icon is null)
        {
            GameLog.Warn($"The spell description needs the icon of {spell}, and the archive lacks it.");

            return null;
        }

        return new MessageBoxModal(SpellDescriptionDialog.Create(described, icon, _fonts.Yellow, _fonts.White,
            _fonts.SmallWhite, _buttons));
    }

    /// <summary>The list artwork the rumors, the events and the language windows are drawn with.</summary>
    /// <returns>The artwork, or <see langword="null"/> with the reason in the log when the archives lack it.</returns>
    public EditorListArtwork? TryCreateListArtwork()
    {
        _listArtwork ??= EditorListArtwork.TryCreate(Settings.Get().IsEvilInterfaceEnabled);

        if (_listArtwork is null)
        {
            GameLog.Warn("The editor list windows need their artwork, and it is missing.");
        }

        return _listArtwork;
    }

    /// <summary>The artwork of the daily event spec window, <c>editDailyEvent()</c>'s frames.</summary>
    /// <returns>The artwork, or <see langword="null"/> with the reason in the log when the archives lack it.</returns>
    public EditorDailyEventSpecArtwork? TryCreateDailyEventArtwork()
    {
        _dailyEventArtwork ??= EditorDailyEventSpecArtwork.TryCreate(Settings.Get().IsEvilInterfaceEnabled);

        if (_dailyEventArtwork is null)
        {
            GameLog.Warn("The daily event window needs its artwork, and it is missing.");
        }

        return _dailyEventArtwork;
    }

    /// <summary>
    /// The artwork of the candidate list windows, <c>ItemSelectionWindow::ItemSelectionWindow()</c>
    /// (<c>dialog_selectitems.cpp:909-957</c>): the scroll bar, the groove and the two small buttons.
    /// </summary>
    /// <returns>The artwork, or <see langword="null"/> with the reason in the log when the archives lack it.</returns>
    public ItemSelectionWindowArtwork? TryCreateItemSelectionArtwork()
    {
        if (_assets is null)
        {
            GameLog.Warn("The candidate list windows need their artwork, and the archives are missing.");

            return null;
        }

        _sprites ??= new IcnViewWorldSpriteSource(_assets);
        _itemSelectionArtwork ??= ItemSelectionWindowArtwork.TryCreate(_sprites, Settings.Get().IsEvilInterfaceEnabled);

        if (_itemSelectionArtwork is null)
        {
            GameLog.Warn("The candidate list windows need their artwork, and it is missing.");
        }

        return _itemSelectionArtwork;
    }

    /// <summary>Hands the fonts of the editor windows out; the list windows take a yellow header and a white body.</summary>
    /// <param name="fonts">The fonts, when the data is installed.</param>
    /// <returns>Whether the fonts are there.</returns>
    public bool TryGetFonts([NotNullWhen(true)] out GameFonts? fonts)
    {
        fonts = _fonts;

        return _fonts is not null;
    }
}

/// <summary>
/// How the spell selection window was left, <c>openSpellSelectionWindow()</c>'s <c>bool</c> plus the level it
/// ended on.
/// </summary>
/// <param name="Accepted">Whether the author accepted the selection, <c>true</c> of the reference's return value.</param>
/// <param name="SpellLevel">The level of the last window shown; 1 to 5, or the level the caller passed in.</param>
/// <param name="Spells">
/// The spells to keep; empty both when the window was cancelled and when every spell of the level was enabled (the
/// game reads an empty list as "the default", <c>editor_spell_selection.cpp:426-429</c>).
/// </param>
public readonly record struct EditorSpellSelectionDialogResult(bool Accepted, int SpellLevel,
    IReadOnlyList<SpellType> Spells);
