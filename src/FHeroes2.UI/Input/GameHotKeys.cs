using System.Text;
using FHeroes2.Game.System;

namespace FHeroes2.UI.Input;

/// <summary>The groups of events the hot key settings dialog lists, <c>Game::HotKeyCategory</c>.</summary>
public enum HotKeyCategory
{
    /// <summary>The keys every dialog honours first: OK, cancel and the four arrows.</summary>
    Default,

    /// <summary>The keys that work on every screen.</summary>
    Global,

    /// <summary>The main menu.</summary>
    MainMenu,

    /// <summary>The campaign screens.</summary>
    Campaign,

    /// <summary>The adventure map.</summary>
    WorldMap,

    /// <summary>The battle field.</summary>
    Battle,

    /// <summary>The town screen.</summary>
    Town,

    /// <summary>The army and hero troop slots.</summary>
    Army,

    /// <summary>The map editor.</summary>
    Editor,
}

/// <summary>
/// Every key the game can be bound to, <c>Game::HotKeyEvent</c> (<c>src/fheroes2/game/game_hotkeys.h:30-186</c>).
/// </summary>
/// <remarks>
/// The order is the order of the reference enumeration because the table in <see cref="GameHotKeys"/> and the saved
/// configuration file both walk it: keep the two in step. The <c>WITH_DEBUG</c> entries of the reference (developer
/// mode, the AI control transfer and the editor's regeneration keys) are not part of the port and are skipped.
/// </remarks>
public enum HotKeyEvent
{
    /// <summary>Not an event; the first value of the reference enumeration.</summary>
    None = 0,

    DefaultOkay,
    DefaultCancel,
    DefaultLeft,
    DefaultRight,
    DefaultUp,
    DefaultDown,

    /// <summary>Delete in a text field; the reference files it under <see cref="HotKeyCategory.Global"/>.</summary>
    DefaultDelete,

    GlobalToggleFullScreen,
    GlobalToggleTextSupportMode,
    GlobalAppQuit,

    MainMenuNewGame,
    MainMenuLoadGame,
    MainMenuHighScores,
    MainMenuCredits,
    MainMenuStandard,
    MainMenuCampaign,
    MainMenuMultiPlayer,
    MainMenuSettings,
    MainMenuSelectMap,
    MainMenuMapSizeSmall,
    MainMenuMapSizeMedium,
    MainMenuMapSizeLarge,
    MainMenuMapSizeExtraLarge,
    MainMenuMapSizeAll,
    MainMenuHotSeat,
    MainMenuBattleOnly,
    MainMenuNewOriginalCampaign,
    MainMenuNewExpansionCampaign,

    EditorMainMenu,
    EditorNewMapMenu,
    EditorLoadMapMenu,
    EditorFromScratchMapMenu,
    EditorRandomMapMenu,
    EditorUndoLastAction,
    EditorRedoLastAction,
    EditorToGameMainMenu,
    EditorTogglePassability,
    EditorAutoPlaytest,

    CampaignRoland,
    CampaignArchibald,
    CampaignPriceOfLoyalty,
    CampaignVoyageHome,
    CampaignWizardsIsle,
    CampaignDescendants,
    CampaignSelectFirstBonus,
    CampaignSelectSecondBonus,
    CampaignSelectThirdBonus,
    CampaignViewIntro,
    CampaignSelectDifficulty,
    CampaignRestartScenario,

    WorldLeft,
    WorldRight,
    WorldUp,
    WorldDown,
    WorldUpLeft,
    WorldUpRight,
    WorldDownLeft,
    WorldDownRight,
    WorldSaveGame,
    WorldQuickSave,
    WorldNextHero,
    WorldQuickSelectHero,
    WorldMoveHeroPortraitUpModifier,
    WorldStartHeroMovement,
    WorldCastSpell,
    WorldSleepHero,
    WorldNextTown,
    WorldEndTurn,
    WorldFileOptions,
    WorldAdventureOptions,
    WorldPuzzleMap,
    WorldScenarioInformation,
    WorldDigArtifact,
    WorldViewWorld,
    WorldKingdomSummary,
    WorldDefaultAction,
    WorldOpenFocus,
    WorldSystemOptions,
    WorldScrollLeft,
    WorldScrollRight,
    WorldScrollUp,
    WorldScrollDown,
    WorldToggleControlPanel,
    WorldToggleRadar,
    WorldToggleButtons,
    WorldToggleStatus,
    WorldToggleIcons,

    BattleRetreat,
    BattleSurrender,
    BattleToggleAutoCombat,
    BattleQuickCombat,
    BattleOptions,
    BattleSkip,
    BattleCastSpell,
    BattleToggleTurnOrderDisplay,

    TownDwellingLevel1,
    TownDwellingLevel2,
    TownDwellingLevel3,
    TownDwellingLevel4,
    TownDwellingLevel5,
    TownDwellingLevel6,
    TownWell,
    TownMarketplace,
    TownMageGuild,
    TownShipyard,
    TownThievesGuild,
    TownTavern,
    TownConstruction,
    TownWellBuyAll,
    TownMergeTroopsWithHero,
    TownMergeTroopsWithGarrison,

    ArmySplitStackByHalf,
    ArmySplitStackByOne,
    ArmyJoinStacks,
    ArmyUpgradeTroop,
    ArmyDismiss,
    ArmySwap,

    /// <summary>One past the last event; the length of the tables.</summary>
    NoEvent,
}

/// <summary>
/// The hot key table, the port of <c>src/fheroes2/game/game_hotkeys.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps one global table in the <c>game</c> module and saves it to <c>fheroes2.key</c>. The port has
/// to put it where <see cref="Key"/> lives, i.e. the input layer: the action tables of the game and of every dialog
/// query it, and the hot key settings dialog rewrites it. The file itself is read and written by the host (the port
/// has no config directory of its own); <see cref="HotKeySave"/> and <see cref="HotKeyLoad"/> turn the table into
/// text and back, which keeps the round trip testable.
/// </para>
/// <para>
/// The reference resets the table to the defaults on every load (<c>initializeHotKeyEvents()</c>) so a stale file can
/// only ever change single keys; <see cref="Reset"/> does the same and <see cref="HotKeyLoad"/> calls it first.
/// </para>
/// </remarks>
public static class GameHotKeys
{
    /// <summary>Puts every key back to its default, the reference's <c>initializeHotKeyEvents()</c>.</summary>
    public static void Reset()
    {
        for (int index = 1; index < (int)HotKeyEvent.NoEvent; ++index)
        {
            Info[index] = Defaults[index];
        }
    }

    /// <summary>
    /// Whether the key bound to <paramref name="eventId"/> was pressed this frame,
    /// <c>Game::HotKeyPressEvent()</c> (<c>game_hotkeys.cpp:386-394</c>).
    /// </summary>
    /// <remarks>
    /// 参考按键时就顺手关掉快速卷屏（<c>setFastScrollStatus( false )</c>），那是冒险地图界面的副作用，留给调用方；
    /// 这里只回答「按了没有」。
    /// </remarks>
    public static bool HotKeyPressEvent(HotKeyEvent eventId) => IsPressed(KeyOf(eventId));

    /// <summary>
    /// Whether the key bound to <paramref name="eventId"/> is being held,
    /// <c>Game::HotKeyHoldEvent()</c> (<c>game_hotkeys.cpp:396-400</c>).
    /// </summary>
    public static bool HotKeyHoldEvent(HotKeyEvent eventId) => IsHeld(KeyOf(eventId));

    /// <summary>The key bound to an event, <c>Game::getHotKeyForEvent()</c>.</summary>
    public static Key GetHotKeyForEvent(HotKeyEvent eventId) => KeyOf(eventId);

    /// <summary>Binds a key to an event, <c>Game::setHotKeyForEvent()</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a real event.</exception>
    public static void SetHotKeyForEvent(HotKeyEvent eventId, Key key)
    {
        if (eventId is <= HotKeyEvent.None or >= HotKeyEvent.NoEvent)
        {
            throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "Not a real hot key event.");
        }

        Info[(int)eventId] = Info[(int)eventId] with { Key = key };
    }

    /// <summary>
    /// The upper-case name of the key bound to an event, <c>Game::getHotKeyNameByEventId()</c>
    /// (<c>game_hotkeys.cpp:412-415</c>).
    /// </summary>
    public static string GetHotKeyNameByEventId(HotKeyEvent eventId) => KeyNames.Get(KeyOf(eventId));

    /// <summary>
    /// The untranslated name of an event, <c>Game::getHotKeyEventNameByEventId()</c>; the same text is the key in the
    /// configuration file and the label of the settings dialog.
    /// </summary>
    public static string GetHotKeyEventNameByEventId(HotKeyEvent eventId) => Info[(int)eventId].Name;

    /// <summary>
    /// Every event with its category, in enumeration order, <c>Game::getAllHotKeyEvents()</c>
    /// (<c>game_hotkeys.cpp:422-438</c>).
    /// </summary>
    /// <remarks>参考在没有 Price of Loyalty 数据时隐藏编辑器分组；端口照做。</remarks>
    public static IReadOnlyList<(HotKeyEvent Event, HotKeyCategory Category)> GetAllHotKeyEvents()
    {
        bool hideEditor = !Settings.Get().IsPriceOfLoyaltySupported;
        List<(HotKeyEvent, HotKeyCategory)> events = new((int)HotKeyEvent.NoEvent - 1);

        for (int index = 1; index < (int)HotKeyEvent.NoEvent; ++index)
        {
            HotKeyEventInfo info = Info[index];
            if (hideEditor && info.Category == HotKeyCategory.Editor)
            {
                continue;
            }

            events.Add(((HotKeyEvent)index, info.Category));
        }

        return events;
    }

    /// <summary>
    /// The display name of a category, <c>Game::getHotKeyCategoryName()</c> (<c>game_hotkeys.cpp:569-597</c>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a real category.</exception>
    public static string GetHotKeyCategoryName(HotKeyCategory category) => category switch
    {
        HotKeyCategory.Default => "Default Actions",
        HotKeyCategory.Global => "Global Actions",
        HotKeyCategory.MainMenu => "Main Menu",
        HotKeyCategory.Campaign => "Campaign",
        HotKeyCategory.WorldMap => "World Map",
        HotKeyCategory.Battle => "Battle Screen",
        HotKeyCategory.Town => "Town Screen",
        HotKeyCategory.Army => "Army Actions",
        HotKeyCategory.Editor => "Editor",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown hot key category."),
    };

    /// <summary>
    /// The content of the hot key file, <c>getHotKeyFileContent()</c> (<c>game_hotkeys.cpp:351-383</c>).
    /// </summary>
    /// <returns>One <c>event name = KEY NAME</c> line per event, grouped by category comments.</returns>
    public static string HotKeySave()
    {
        StringBuilder text = new();
        text.AppendLine("# fheroes2 hotkey file (saved by the C# port)");
        text.AppendLine();

        HotKeyCategory currentCategory = Info[1].Category;
        text.Append("# ").Append(GetHotKeyCategoryName(currentCategory)).AppendLine(":");

        for (int index = 1; index < (int)HotKeyEvent.NoEvent; ++index)
        {
            HotKeyEventInfo info = Info[index];
            if (info.Category != currentCategory)
            {
                currentCategory = info.Category;
                text.AppendLine();
                text.Append("# ").Append(GetHotKeyCategoryName(currentCategory)).AppendLine(":");
            }

            text.Append(info.Name).Append(" = ").Append(KeyNames.Get(info.Key)).AppendLine();
        }

        return text.ToString();
    }

    /// <summary>
    /// Replaces the table with the defaults and then applies the keys of a hot key file,
    /// <c>Game::HotKeysLoad()</c> (<c>game_hotkeys.cpp:440-478</c>).
    /// </summary>
    /// <param name="content">The file content, as written by <see cref="HotKeySave"/>; <see langword="null"/> or empty
    /// keeps the defaults.</param>
    /// <remarks>
    /// 参考用 <c>TinyConfig( '=', '#' )</c> 读文件；端口没有那个类，解析就放在这里：<c>#</c> 起头是注释，每行
    /// <c>名字 = 键名</c>，不认识的键与键名都跳过（与参考一样只覆盖认得的项）。文件的位置与写入仍归宿主。
    /// </remarks>
    public static void HotKeyLoad(string? content)
    {
        Reset();

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        Dictionary<string, Key> byName = new((int)Key.LastKey, StringComparer.OrdinalIgnoreCase);
        for (int keyValue = (int)Key.None; keyValue < (int)Key.LastKey; ++keyValue)
        {
            byName.TryAdd(KeyNames.Get((Key)keyValue), (Key)keyValue);
        }

        Dictionary<string, int> eventByName = new((int)HotKeyEvent.NoEvent, StringComparer.Ordinal);
        for (int index = 1; index < (int)HotKeyEvent.NoEvent; ++index)
        {
            eventByName[Info[index].Name] = index;
        }

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            string name = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (!eventByName.TryGetValue(name, out int eventIndex) || !byName.TryGetValue(value, out Key key))
            {
                continue;
            }

            Info[eventIndex] = Info[eventIndex] with { Key = key };
        }
    }

    /// <summary>
    /// Applies the two global toggles, <c>Game::globalKeyDownEvent()</c> (<c>game_hotkeys.cpp:495-567</c>).
    /// </summary>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifier">The modifiers held at the same time.</param>
    /// <returns><see langword="true"/> when the key was one of the global toggles.</returns>
    /// <remarks>
    /// 带 Alt 或 Ctrl 的按法不算（参考如此，免得在输入框里打 F4 就切全屏）。参考改完设置立刻 <c>conf.Save()</c>；
    /// 端口把持久化留给宿主，这里只改 <see cref="Settings"/>，返回 <see langword="true"/> 让调用方自己决定存不存。
    /// <c>WITH_DEBUG</c> 的两个分支（开发者模式、把控制权交给 AI）不在端口里。
    /// 另外参考的 <c>conf.setFullScreen()</c> 会顺手切引擎窗口，端口的 <see cref="Settings.SetFullScreen"/> 只翻标志位，
    /// 真正切窗口要调用方拿到 <see cref="IDisplayControl"/> 后自己做（见 <c>GraphicsSettingsDialog.Apply</c>）。
    /// </remarks>
    public static bool GlobalKeyDownEvent(Key key, KeyModifier modifier)
    {
        if ((modifier & (KeyModifier.Alt | KeyModifier.Ctrl)) != KeyModifier.None)
        {
            return false;
        }

        Settings settings = Settings.Get();

        if (key == KeyOf(HotKeyEvent.GlobalToggleFullScreen))
        {
            settings.SetFullScreen(!settings.IsFullScreen);
            return true;
        }

        if (key == KeyOf(HotKeyEvent.GlobalToggleTextSupportMode))
        {
            settings.SetTextSupportMode(!settings.IsTextSupportMode);
            return true;
        }

        return false;
    }

    private static Key KeyOf(HotKeyEvent eventId) =>
        eventId is >= HotKeyEvent.None and < HotKeyEvent.NoEvent
            ? Info[(int)eventId].Key
            : throw new ArgumentOutOfRangeException(nameof(eventId), eventId, "Not a hot key event.");

    private static bool IsPressed(Key key)
    {
        LocalEvent events = LocalEvent.Current;
        return events.IsAnyKeyPressed && events.PressedKeyValue == key;
    }

    private static bool IsHeld(Key key)
    {
        LocalEvent events = LocalEvent.Current;
        return events.IsKeyBeingHold && events.PressedKeyValue == key;
    }

    private readonly record struct HotKeyEventInfo(HotKeyCategory Category, string Name, Key Key);

    /// <summary>The table itself; index zero is <see cref="HotKeyEvent.None"/> and never used.</summary>
    private static readonly HotKeyEventInfo[] Info = Build();

    /// <summary>A copy of the table as it ships, restored by <see cref="Reset"/>.</summary>
    private static readonly HotKeyEventInfo[] Defaults = Build();

    private static HotKeyEventInfo[] Build()
    {
        HotKeyEventInfo[] table = new HotKeyEventInfo[(int)HotKeyEvent.NoEvent];

        static void Add(HotKeyEventInfo[] table, HotKeyEvent id, HotKeyCategory category, string name, Key key) =>
            table[(int)id] = new HotKeyEventInfo(category, name, key);

        Add(table, HotKeyEvent.DefaultOkay, HotKeyCategory.Default, "default okay event", Key.Enter);
        Add(table, HotKeyEvent.DefaultCancel, HotKeyCategory.Default, "default cancel event", Key.Escape);
        Add(table, HotKeyEvent.DefaultLeft, HotKeyCategory.Default, "default left", Key.Left);
        Add(table, HotKeyEvent.DefaultRight, HotKeyCategory.Default, "default right", Key.Right);
        Add(table, HotKeyEvent.DefaultUp, HotKeyCategory.Default, "default up", Key.Up);
        Add(table, HotKeyEvent.DefaultDown, HotKeyCategory.Default, "default down", Key.Down);
        Add(table, HotKeyEvent.DefaultDelete, HotKeyCategory.Global, "default delete", Key.Delete);
        Add(table, HotKeyEvent.GlobalToggleFullScreen, HotKeyCategory.Global, "toggle fullscreen", Key.F4);
        Add(table, HotKeyEvent.GlobalToggleTextSupportMode, HotKeyCategory.Global, "toggle text support mode", Key.F10);
        Add(table, HotKeyEvent.GlobalAppQuit, HotKeyCategory.Global, "quit", Key.Q);

        Add(table, HotKeyEvent.MainMenuNewGame, HotKeyCategory.MainMenu, "new game", Key.N);
        Add(table, HotKeyEvent.MainMenuLoadGame, HotKeyCategory.MainMenu, "load game", Key.L);
        Add(table, HotKeyEvent.MainMenuHighScores, HotKeyCategory.MainMenu, "high scores", Key.H);
        Add(table, HotKeyEvent.MainMenuCredits, HotKeyCategory.MainMenu, "credits", Key.C);
        Add(table, HotKeyEvent.MainMenuStandard, HotKeyCategory.MainMenu, "standard game", Key.S);
        Add(table, HotKeyEvent.MainMenuCampaign, HotKeyCategory.MainMenu, "campaign game", Key.C);
        Add(table, HotKeyEvent.MainMenuMultiPlayer, HotKeyCategory.MainMenu, "multi-player game", Key.M);
        Add(table, HotKeyEvent.MainMenuSettings, HotKeyCategory.MainMenu, "settings", Key.T);
        Add(table, HotKeyEvent.MainMenuSelectMap, HotKeyCategory.MainMenu, "select map", Key.S);
        Add(table, HotKeyEvent.MainMenuMapSizeSmall, HotKeyCategory.MainMenu, "select small map size", Key.S);
        Add(table, HotKeyEvent.MainMenuMapSizeMedium, HotKeyCategory.MainMenu, "select medium map size", Key.M);
        Add(table, HotKeyEvent.MainMenuMapSizeLarge, HotKeyCategory.MainMenu, "select large map size", Key.L);
        Add(table, HotKeyEvent.MainMenuMapSizeExtraLarge, HotKeyCategory.MainMenu, "select extra large map size", Key.X);
        Add(table, HotKeyEvent.MainMenuMapSizeAll, HotKeyCategory.MainMenu, "select all map sizes", Key.A);
        Add(table, HotKeyEvent.MainMenuHotSeat, HotKeyCategory.MainMenu, "hot seat game", Key.H);
        Add(table, HotKeyEvent.MainMenuBattleOnly, HotKeyCategory.MainMenu, "battle only game", Key.B);
        Add(table, HotKeyEvent.MainMenuNewOriginalCampaign, HotKeyCategory.MainMenu, "choose the original campaign", Key.O);
        Add(table, HotKeyEvent.MainMenuNewExpansionCampaign, HotKeyCategory.MainMenu, "choose the expansion campaign", Key.E);

        Add(table, HotKeyEvent.EditorMainMenu, HotKeyCategory.Editor, "map editor main menu", Key.E);
        Add(table, HotKeyEvent.EditorNewMapMenu, HotKeyCategory.Editor, "new map menu", Key.N);
        Add(table, HotKeyEvent.EditorLoadMapMenu, HotKeyCategory.Editor, "load map menu", Key.L);
        Add(table, HotKeyEvent.EditorFromScratchMapMenu, HotKeyCategory.Editor, "new map from scratch", Key.S);
        Add(table, HotKeyEvent.EditorRandomMapMenu, HotKeyCategory.Editor, "new random map", Key.R);
        Add(table, HotKeyEvent.EditorUndoLastAction, HotKeyCategory.Editor, "undo last action", Key.U);
        Add(table, HotKeyEvent.EditorRedoLastAction, HotKeyCategory.Editor, "redo last action", Key.R);
        Add(table, HotKeyEvent.EditorToGameMainMenu, HotKeyCategory.Editor, "open game main menu", Key.M);
        Add(table, HotKeyEvent.EditorTogglePassability, HotKeyCategory.Editor, "toggle passability", Key.P);

        // 参考把「自动试玩」放在 WORLD_MAP 分类里（game_hotkeys.cpp:173-174），照抄。
        Add(table, HotKeyEvent.EditorAutoPlaytest, HotKeyCategory.WorldMap, "auto playtest", Key.A);

        Add(table, HotKeyEvent.CampaignRoland, HotKeyCategory.Campaign, "roland campaign", Key.Digit1);
        Add(table, HotKeyEvent.CampaignArchibald, HotKeyCategory.Campaign, "archibald campaign", Key.Digit2);
        Add(table, HotKeyEvent.CampaignPriceOfLoyalty, HotKeyCategory.Campaign, "price of loyalty campaign", Key.Digit1);
        Add(table, HotKeyEvent.CampaignVoyageHome, HotKeyCategory.Campaign, "voyage home campaign", Key.Digit2);
        Add(table, HotKeyEvent.CampaignWizardsIsle, HotKeyCategory.Campaign, "wizard's isle campaign", Key.Digit3);
        Add(table, HotKeyEvent.CampaignDescendants, HotKeyCategory.Campaign, "descendants campaign", Key.Digit4);
        Add(table, HotKeyEvent.CampaignSelectFirstBonus, HotKeyCategory.Campaign, "select first campaign bonus", Key.Digit1);
        Add(table, HotKeyEvent.CampaignSelectSecondBonus, HotKeyCategory.Campaign, "select second campaign bonus", Key.Digit2);
        Add(table, HotKeyEvent.CampaignSelectThirdBonus, HotKeyCategory.Campaign, "select third campaign bonus", Key.Digit3);
        Add(table, HotKeyEvent.CampaignViewIntro, HotKeyCategory.Campaign, "view campaign intro", Key.V);
        Add(table, HotKeyEvent.CampaignSelectDifficulty, HotKeyCategory.Campaign, "select campaign difficulty", Key.D);
        Add(table, HotKeyEvent.CampaignRestartScenario, HotKeyCategory.Campaign, "restart campaign scenario", Key.R);

        Add(table, HotKeyEvent.WorldLeft, HotKeyCategory.WorldMap, "world map left", Key.KeyPad4);
        Add(table, HotKeyEvent.WorldRight, HotKeyCategory.WorldMap, "world map right", Key.KeyPad6);
        Add(table, HotKeyEvent.WorldUp, HotKeyCategory.WorldMap, "world map up", Key.KeyPad8);
        Add(table, HotKeyEvent.WorldDown, HotKeyCategory.WorldMap, "world map down", Key.KeyPad2);
        Add(table, HotKeyEvent.WorldUpLeft, HotKeyCategory.WorldMap, "world map up left", Key.KeyPad7);
        Add(table, HotKeyEvent.WorldUpRight, HotKeyCategory.WorldMap, "world map up right", Key.KeyPad9);
        Add(table, HotKeyEvent.WorldDownLeft, HotKeyCategory.WorldMap, "world map down left", Key.KeyPad1);
        Add(table, HotKeyEvent.WorldDownRight, HotKeyCategory.WorldMap, "world map down right", Key.KeyPad3);
        Add(table, HotKeyEvent.WorldSaveGame, HotKeyCategory.WorldMap, "save game", Key.S);
        Add(table, HotKeyEvent.WorldQuickSave, HotKeyCategory.WorldMap, "quick save", Key.F2);
        Add(table, HotKeyEvent.WorldNextHero, HotKeyCategory.WorldMap, "next hero", Key.H);
        Add(table, HotKeyEvent.WorldQuickSelectHero, HotKeyCategory.WorldMap, "change to hero under cursor", Key.LeftShift);
        Add(table, HotKeyEvent.WorldMoveHeroPortraitUpModifier, HotKeyCategory.WorldMap, "hold and left click to move hero portrait to top of list", Key.LeftShift);
        Add(table, HotKeyEvent.WorldStartHeroMovement, HotKeyCategory.WorldMap, "start hero movement", Key.M);
        Add(table, HotKeyEvent.WorldCastSpell, HotKeyCategory.WorldMap, "cast adventure spell", Key.C);
        Add(table, HotKeyEvent.WorldSleepHero, HotKeyCategory.WorldMap, "put hero to sleep", Key.Z);
        Add(table, HotKeyEvent.WorldNextTown, HotKeyCategory.WorldMap, "next town", Key.T);
        Add(table, HotKeyEvent.WorldEndTurn, HotKeyCategory.WorldMap, "end turn", Key.E);
        Add(table, HotKeyEvent.WorldFileOptions, HotKeyCategory.WorldMap, "file options", Key.F);
        Add(table, HotKeyEvent.WorldAdventureOptions, HotKeyCategory.WorldMap, "adventure options", Key.A);
        Add(table, HotKeyEvent.WorldPuzzleMap, HotKeyCategory.WorldMap, "puzzle map", Key.P);
        Add(table, HotKeyEvent.WorldScenarioInformation, HotKeyCategory.WorldMap, "scenario information", Key.I);
        Add(table, HotKeyEvent.WorldDigArtifact, HotKeyCategory.WorldMap, "dig for artifact", Key.D);
        Add(table, HotKeyEvent.WorldViewWorld, HotKeyCategory.WorldMap, "view world", Key.V);
        Add(table, HotKeyEvent.WorldKingdomSummary, HotKeyCategory.WorldMap, "kingdom summary", Key.K);
        Add(table, HotKeyEvent.WorldDefaultAction, HotKeyCategory.WorldMap, "default action", Key.Space);
        Add(table, HotKeyEvent.WorldOpenFocus, HotKeyCategory.WorldMap, "open focus", Key.Enter);
        Add(table, HotKeyEvent.WorldSystemOptions, HotKeyCategory.WorldMap, "system options", Key.O);
        Add(table, HotKeyEvent.WorldScrollLeft, HotKeyCategory.WorldMap, "scroll left", Key.Left);
        Add(table, HotKeyEvent.WorldScrollRight, HotKeyCategory.WorldMap, "scroll right", Key.Right);
        Add(table, HotKeyEvent.WorldScrollUp, HotKeyCategory.WorldMap, "scroll up", Key.Up);
        Add(table, HotKeyEvent.WorldScrollDown, HotKeyCategory.WorldMap, "scroll down", Key.Down);
        Add(table, HotKeyEvent.WorldToggleControlPanel, HotKeyCategory.WorldMap, "toggle control panel", Key.Digit1);
        Add(table, HotKeyEvent.WorldToggleRadar, HotKeyCategory.WorldMap, "toggle radar", Key.Digit2);
        Add(table, HotKeyEvent.WorldToggleButtons, HotKeyCategory.WorldMap, "toggle buttons", Key.Digit3);
        Add(table, HotKeyEvent.WorldToggleStatus, HotKeyCategory.WorldMap, "toggle status", Key.Digit4);
        Add(table, HotKeyEvent.WorldToggleIcons, HotKeyCategory.WorldMap, "toggle icons", Key.Digit5);

        Add(table, HotKeyEvent.BattleRetreat, HotKeyCategory.Battle, "retreat from battle", Key.R);
        Add(table, HotKeyEvent.BattleSurrender, HotKeyCategory.Battle, "surrender during battle", Key.S);
        Add(table, HotKeyEvent.BattleToggleAutoCombat, HotKeyCategory.Battle, "toggle auto combat mode", Key.A);
        Add(table, HotKeyEvent.BattleQuickCombat, HotKeyCategory.Battle, "quick combat", Key.Q);
        Add(table, HotKeyEvent.BattleOptions, HotKeyCategory.Battle, "battle options", Key.O);
        Add(table, HotKeyEvent.BattleSkip, HotKeyCategory.Battle, "skip turn in battle", Key.Space);
        Add(table, HotKeyEvent.BattleCastSpell, HotKeyCategory.Battle, "cast battle spell", Key.C);
        Add(table, HotKeyEvent.BattleToggleTurnOrderDisplay, HotKeyCategory.Battle, "toggle display of battle turn order", Key.T);

        Add(table, HotKeyEvent.TownDwellingLevel1, HotKeyCategory.Town, "dwelling level 1", Key.Digit1);
        Add(table, HotKeyEvent.TownDwellingLevel2, HotKeyCategory.Town, "dwelling level 2", Key.Digit2);
        Add(table, HotKeyEvent.TownDwellingLevel3, HotKeyCategory.Town, "dwelling level 3", Key.Digit3);
        Add(table, HotKeyEvent.TownDwellingLevel4, HotKeyCategory.Town, "dwelling level 4", Key.Digit4);
        Add(table, HotKeyEvent.TownDwellingLevel5, HotKeyCategory.Town, "dwelling level 5", Key.Digit5);
        Add(table, HotKeyEvent.TownDwellingLevel6, HotKeyCategory.Town, "dwelling level 6", Key.Digit6);
        Add(table, HotKeyEvent.TownWell, HotKeyCategory.Town, "well", Key.W);
        Add(table, HotKeyEvent.TownMarketplace, HotKeyCategory.Town, "marketplace", Key.M);
        Add(table, HotKeyEvent.TownMageGuild, HotKeyCategory.Town, "mage guild", Key.S);
        Add(table, HotKeyEvent.TownShipyard, HotKeyCategory.Town, "shipyard", Key.N);
        Add(table, HotKeyEvent.TownThievesGuild, HotKeyCategory.Town, "thieves guild", Key.T);
        Add(table, HotKeyEvent.TownTavern, HotKeyCategory.Town, "tavern", Key.R);
        Add(table, HotKeyEvent.TownConstruction, HotKeyCategory.Town, "construction screen", Key.B);
        Add(table, HotKeyEvent.TownWellBuyAll, HotKeyCategory.Town, "buy all monsters in well", Key.M);
        Add(table, HotKeyEvent.TownMergeTroopsWithHero, HotKeyCategory.Town, "merge troops with hero", Key.Down);
        Add(table, HotKeyEvent.TownMergeTroopsWithGarrison, HotKeyCategory.Town, "merge troops with garrison", Key.Up);

        Add(table, HotKeyEvent.ArmySplitStackByHalf, HotKeyCategory.Army, "split stack by half", Key.LeftShift);
        Add(table, HotKeyEvent.ArmySplitStackByOne, HotKeyCategory.Army, "split stack by one", Key.LeftControl);
        Add(table, HotKeyEvent.ArmyJoinStacks, HotKeyCategory.Army, "join stacks", Key.LeftAlt);
        Add(table, HotKeyEvent.ArmyUpgradeTroop, HotKeyCategory.Army, "upgrade troop", Key.U);
        Add(table, HotKeyEvent.ArmyDismiss, HotKeyCategory.Army, "dismiss hero or troop", Key.D);
        Add(table, HotKeyEvent.ArmySwap, HotKeyCategory.Army, "exchange all troops", Key.X);

        return table;
    }
}
