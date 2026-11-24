using System.Text;

using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.System;

/// <summary>
/// The machine-wide preferences as they live in <c>fheroes2.cfg</c>, <c>Settings::Read()</c>,
/// <c>Settings::Save()</c> and <c>Settings::getOptionsString()</c> (<c>settings.cpp:145-583</c>).
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="Settings.Persistence"/> 那份（存档里跟着一局游戏走的五个字段）是两回事：配置文件描述这台机器，
/// 音量、速度、各种开关与界面位置都在这里，读档不会覆盖它。
/// </para>
/// <para>
/// <b>谁拼路径</b>：参考的 <c>Save()</c> 自己把文件名拼到 <c>GetConfigDirectory( "fheroes2" )</c> 下面，端口没有那个
/// 函数（<c>dir.cpp</c> 未移植），所以 <see cref="Save"/> 收完整路径，位置由宿主决定，与 <c>GameHotKeys</c> 的热键
/// 文件一致。
/// </para>
/// <para>
/// <b>三个键不在这里</b>，有对应状态才写、才读：<c>videomode</c> 与 <c>game window position</c> 属于
/// <c>screen.cpp</c> 的分辨率与窗口（端口由 UI 层持有），<c>debug</c> 属于未移植的 <c>logging.cpp</c>。它们通过
/// <see cref="Save"/> / <see cref="GetOptionsString"/> 的可选参数写出去；<see cref="Read"/> 读到也跳过，不留半截状态。
/// </para>
/// </remarks>
public sealed partial class Settings
{
    /// <summary>
    /// Reads a configuration file, <c>Settings::Read()</c> (<c>settings.cpp:145-387</c>).
    /// </summary>
    /// <remarks>
    /// 每个键都照参考的默认值起手：<c>music</c> 缺省是 <c>external</c>，<c>battle turn order</c> 认不出就是
    /// <c>off</c>，<c>interface type</c> 认不出就是 <c>dynamic</c>，其余认不出的值落到 0 或 false。文件读不到时
    /// 返回 <see langword="false"/> 且一个字段都不动——参考在 <c>config.Load()</c> 失败时也直接返回。
    /// </remarks>
    /// <param name="filePath">The file to read; the caller builds the path.</param>
    /// <returns><see langword="true"/> when the file was read.</returns>
    public bool Read(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        TinyConfig config = new();

        if (!config.LoadFile(filePath))
        {
            return false;
        }

        // debug: 参考写进日志层的等级，端口没有日志层（logging.cpp 未移植）。

        string text = config.StrParams("lang");

        if (text.Length > 0)
        {
            SetGameLanguage(text);
        }

        // 参考先把音乐源设成 external，再按字符串改；认不出的值保持 external。
        SetMusicType(config.StrParams("music") switch
        {
            "original" => MusicSource.MidiOriginal,
            "expansion" => MusicSource.MidiExpansion,
            _ => MusicSource.External,
        });

        if (config.Exists("sound volume"))
        {
            SetSoundVolume(config.IntParams("sound volume"));
        }

        if (config.Exists("music volume"))
        {
            SetMusicVolume(config.IntParams("music volume"));
        }

        if (config.Exists("ai speed"))
        {
            SetAiMoveSpeed(config.IntParams("ai speed"));
        }

        if (config.Exists("heroes speed"))
        {
            SetHeroesMoveSpeed(config.IntParams("heroes speed"));
        }

        // scroll speed 没有 Exists 检查：缺键时参考拿到 0，端口一样落到 None。
        SetScrollSpeed(config.IntParams("scroll speed"));

        if (config.Exists("map smooth scrolling"))
        {
            SetMapSmoothScrolling(config.StrParams("map smooth scrolling") != "off");
        }

        if (config.Exists("battle speed"))
        {
            SetBattleSpeed(config.IntParams("battle speed"));
        }

        if (config.Exists("battle grid"))
        {
            SetBattleGrid(config.StrParams("battle grid") == "on");
        }

        if (config.Exists("battle shadow movement"))
        {
            SetBattleMovementShaded(config.StrParams("battle shadow movement") == "on");
        }

        if (config.Exists("battle shadow cursor"))
        {
            SetBattleMouseShaded(config.StrParams("battle shadow cursor") == "on");
        }

        if (config.Exists("highlight movement area"))
        {
            SetBattleMovementAreaHighlight(config.StrParams("highlight movement area") == "on");
        }

        if (config.Exists("battle show damage"))
        {
            SetBattleShowDamage(config.StrParams("battle show damage") == "on");
        }

        if (config.Exists("battle show hit points bar"))
        {
            SetBattleShowHitPointsBar(config.StrParams("battle show hit points bar") == "on");
        }

        if (config.Exists("auto resolve battles"))
        {
            SetBattleAutoResolve(config.StrParams("auto resolve battles") == "on");
        }

        if (config.Exists("auto spell casting"))
        {
            SetBattleAutoSpellcast(config.StrParams("auto spell casting") == "on");
        }

        if (config.Exists("battle turn order"))
        {
            // "on" 是旧版本的写法，参考把它当成 top（settings.cpp:246-262）。
            BattleTurnOrder = config.StrParams("battle turn order") switch
            {
                "on" or "top" => BattleTurnOrderState.Top,
                "bottom" => BattleTurnOrderState.Bottom,
                _ => BattleTurnOrderState.Off,
            };
        }

        // 旧版本只在邪恶界面上留了一个开关，还是读它优先（settings.cpp:264-283）。
        if (config.Exists("use evil interface"))
        {
            InterfaceType = config.StrParams("use evil interface") == "on" ? InterfaceType.Evil : InterfaceType.Good;
        }
        else if (config.Exists("interface type"))
        {
            InterfaceType = config.StrParams("interface type") switch
            {
                "good" => InterfaceType.Good,
                "evil" => InterfaceType.Evil,
                _ => InterfaceType.Dynamic,
            };
        }

        if (config.Exists("army estimation view type"))
        {
            SetNumericArmyEstimationView(config.StrParams("army estimation view type") == "numeric");
        }

        if (config.Exists("hide interface"))
        {
            SetHideInterface(config.StrParams("hide interface") == "on");
        }

        if (config.Exists("radar window position"))
        {
            PosRadar = config.PointParams("radar window position", new Point(-1, -1));
        }

        if (config.Exists("buttons window position"))
        {
            PosButtons = config.PointParams("buttons window position", new Point(-1, -1));
        }

        if (config.Exists("icons window position"))
        {
            PosIcons = config.PointParams("icons window position", new Point(-1, -1));
        }

        if (config.Exists("status window position"))
        {
            PosStatus = config.PointParams("status window position", new Point(-1, -1));
        }

        if (config.Exists("fullscreen"))
        {
            SetFullScreen(config.StrParams("fullscreen") == "on");
        }

        if (config.Exists("controller pointer speed"))
        {
            ControllerPointerSpeed = Math.Clamp(config.IntParams("controller pointer speed"), 0, 100);
        }

        // 只有明确写了 off 才清掉「第一次运行」——缺键保持未配置，参考如此（settings.cpp:307-310）。
        if (config.Exists("first time game run") && config.StrParams("first time game run") == "off")
        {
            ResetFirstGameRun();
        }

        if (config.Exists("v-sync"))
        {
            SetVSync(config.StrParams("v-sync") == "on");
        }

        if (config.Exists("text support mode"))
        {
            SetTextSupportMode(config.StrParams("text support mode") == "on");
        }

        if (config.Exists("monochrome cursor"))
        {
            SetMonochromeCursor(config.StrParams("monochrome cursor") == "on");
        }

        if (config.Exists("3d audio"))
        {
            Set3DAudio(config.StrParams("3d audio") == "on");
        }

        if (config.Exists("system info"))
        {
            SetSystemInfo(config.StrParams("system info") == "on");
        }

        if (config.Exists("auto save at the beginning of the turn"))
        {
            SetAutoSaveAtBeginningOfTurn(
                config.StrParams("auto save at the beginning of the turn") == "on");
        }

        if (config.Exists("cursor soft rendering"))
        {
            SetCursorSoftwareEmulation(config.StrParams("cursor soft rendering") == "on");
        }

        if (config.Exists("screen scaling type"))
        {
            SetScreenScalingTypeNearest(config.StrParams("screen scaling type") == "nearest");
        }

        if (config.Exists("editor animation"))
        {
            SetEditorAnimation(config.StrParams("editor animation") == "on");
        }

        if (config.Exists("editor passability"))
        {
            SetEditorPassability(config.StrParams("editor passability") == "on");
        }

        if (config.Exists("save file sorting"))
        {
            SaveFileSortType = config.StrParams("save file sorting") == "date"
                ? SaveFileSortingMethod.Timestamp
                : SaveFileSortingMethod.Filename;
        }

        return true;
    }

    /// <summary>Writes a configuration file, <c>Settings::Save()</c> (<c>settings.cpp:388-403</c>).</summary>
    /// <param name="filePath">The file to write; the caller builds the path.</param>
    /// <param name="resolution">The in-game and window sizes, or <see langword="null"/> to leave <c>videomode</c> out.</param>
    /// <param name="windowPosition">
    /// The position of the window, or <see langword="null"/> to leave <c>game window position</c> out.
    /// </param>
    /// <returns><see langword="false"/> when the file cannot be written, like the reference.</returns>
    public bool Save(string filePath, ResolutionInfo? resolution = null, Point? windowPosition = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        try
        {
            File.WriteAllText(filePath, GetOptionsString(resolution, windowPosition));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              ArgumentException or NotSupportedException)
        {
            return false;
        }

        return true;
    }

    /// <summary>The text of a configuration file, <c>Settings::getOptionsString()</c>.</summary>
    /// <remarks>
    /// 顺序、键名与注释都照参考，好让两份文件可以并排 diff；<c>videomode</c>、<c>game window position</c> 与
    /// <c>debug</c> 见类注释里的说明，前两个只在调用方给出值时才写。
    /// </remarks>
    /// <param name="resolution">The in-game and window sizes, or <see langword="null"/> to leave <c>videomode</c> out.</param>
    /// <param name="windowPosition">
    /// The position of the window, or <see langword="null"/> to leave <c>game window position</c> out.
    /// </param>
    /// <returns>The text of the file.</returns>
    public string GetOptionsString(ResolutionInfo? resolution = null, Point? windowPosition = null)
    {
        StringBuilder text = new();

        text.Append("# fheroes2 configuration file (saved by version ").Append(EngineVersion.Version)
            .Append(")\n\n")
            .Append("# !!! WARNING !!!\n")
            .Append("# Only modify this file if you are absolutely sure of what you are doing!\n")
            .Append("# !!! WARNING !!!\n");

        if (resolution is { } video)
        {
            text.Append("\n# Resolution: in-game width x height : on-screen width x height\n")
                .Append($"videomode = {video.GameWidth}x{video.GameHeight}:{video.ScreenWidth}x{video.ScreenHeight}\n");
        }

        if (windowPosition is { } position)
        {
            text.Append("\n# Position of the game's window\n")
                .Append($"game window position = [ {position.X}, {position.Y} ]\n");
        }

        text.Append("\n# Music type: original, expansion, external\n")
            .Append($"music = {MusicType switch { MusicSource.MidiOriginal => "original", MusicSource.MidiExpansion => "expansion", _ => "external" }}\n")
            .Append("\n# Sound volume: 0 - 10\n")
            .Append($"sound volume = {SoundVolume}\n")
            .Append("\n# Music volume: 0 - 10\n")
            .Append($"music volume = {MusicVolume}\n")
            .Append("\n# Toggle fullscreen mode: on/off\n")
            .Append($"fullscreen = {OnOff(IsFullScreen)}\n")
            .Append("\n# Hero movement speed: 1 - 10\n")
            .Append($"heroes speed = {HeroesMoveSpeed}\n")
            .Append("\n# AI movement speed: 0 - 10\n")
            .Append($"ai speed = {AiMoveSpeed}\n")
            .Append("\n# Battle animation speed: 1 - 10\n")
            .Append($"battle speed = {BattleSpeed}\n")
            .Append("\n# Adventure Map scrolling speed: 0 - 4. 0 means no scrolling\n")
            .Append($"scroll speed = {(int)ScrollSpeed}\n")
            .Append("\n# Smooth scrolling when panning the Adventure Map by dragging: on/off\n")
            .Append($"map smooth scrolling = {OnOff(IsMapSmoothScrollingEnabled)}\n")
            .Append("\n# Toggle battle grid: on/off\n")
            .Append($"battle grid = {OnOff(IsBattleShowGrid)}\n")
            .Append("\n# Show battle shadow movement: on/off\n")
            .Append($"battle shadow movement = {OnOff(IsBattleShowMoveShadow)}\n")
            .Append("\n# Show battle shadow cursor: on/off\n")
            .Append($"battle shadow cursor = {OnOff(IsBattleShowMouseShadow)}\n")
            .Append("\n# Highlight battle movement area: on/off\n")
            .Append($"highlight movement area = {OnOff(IsBattleMovementAreaHighlight)}\n")
            .Append("\n# Show battle damage information: on/off\n")
            .Append($"battle show damage = {OnOff(IsBattleShowDamage)}\n")
            .Append("\n# Show hit points bar: on/off\n")
            .Append($"battle show hit points bar = {OnOff(IsBattleShowHitPointsBar)}\n")
            .Append("\n# Enable auto resolve battles: on/off\n")
            .Append($"auto resolve battles = {OnOff(IsBattleAutoResolve)}\n")
            .Append("\n# Enable auto combat spell casting: on/off\n")
            .Append($"auto spell casting = {OnOff(IsBattleAutoSpellcast)}\n")
            .Append("\n# Show turn order during battle: on/off\n")
            .Append($"battle turn order = {BattleTurnOrder switch { BattleTurnOrderState.Top => "top", BattleTurnOrderState.Bottom => "bottom", _ => "off" }}\n")
            .Append("\n# Interface type: good/evil/dynamic\n")
            .Append($"interface type = {InterfaceType switch { InterfaceType.Good => "good", InterfaceType.Evil => "evil", _ => "dynamic" }}\n")
            .Append("\n# Hide interface elements on the adventure map: on/off\n")
            .Append($"hide interface = {OnOff(IsHideInterface)}\n")
            .Append("\n# Position of the radar window on the adventure map when interface elements are hidden\n")
            .Append($"radar window position = [ {PosRadar.X}, {PosRadar.Y} ]\n")
            .Append("\n# Position of the buttons window on the adventure map when interface elements are hidden\n")
            .Append($"buttons window position = [ {PosButtons.X}, {PosButtons.Y} ]\n")
            .Append("\n# Position of the icons window on the adventure map when interface elements are hidden\n")
            .Append($"icons window position = [ {PosIcons.X}, {PosIcons.Y} ]\n")
            .Append("\n# Position of the status window on the adventure map when interface elements are hidden\n")
            .Append($"status window position = [ {PosStatus.X}, {PosStatus.Y} ]\n")
            .Append("\n# Game language (an empty value means English)\n")
            .Append($"lang = {GameLanguage}\n")
            .Append("\n# Controller pointer speed: 0 - 100\n")
            .Append($"controller pointer speed = {ControllerPointerSpeed}\n")
            .Append("\n# First time game run (show additional hints): on/off\n")
            .Append($"first time game run = {OnOff(IsFirstGameRun)}\n")
            .Append("\n# Enable V-Sync (Vertical Synchronization) for rendering\n")
            .Append($"v-sync = {OnOff(IsVSyncEnabled)}\n")
            .Append("\n# Enable text support mode that outputs extra information in console window: on/off\n")
            .Append($"text support mode = {OnOff(IsTextSupportMode)}\n")
            .Append("\n# Toggle monochrome (black and white) cursors in the game: on/off\n")
            .Append($"monochrome cursor = {OnOff(IsMonochromeCursor)}\n")
            .Append("\n# Enable 3D audio for objects on Adventure Map: on/off\n")
            .Append($"3d audio = {OnOff(Is3DAudioEnabled)}\n")
            .Append("\n# Display system information: on/off\n")
            .Append($"system info = {OnOff(IsSystemInfo)}\n")
            .Append("\n# Perform auto save at the beginning of the turn instead of the end of the turn: on/off\n")
            .Append($"auto save at the beginning of the turn = {OnOff(IsAutoSaveAtBeginningOfTurnEnabled)}\n")
            .Append("\n# Enable cursor software rendering: on/off\n")
            .Append($"cursor soft rendering = {OnOff(IsCursorSoftwareEmulation)}\n")
            .Append("\n# Screen scaling type: nearest or linear\n")
            .Append($"screen scaling type = {(IsScreenScalingTypeNearest ? "nearest" : "linear")}\n")
            .Append("\n# Show object animation in the Editor: on/off\n")
            .Append($"editor animation = {OnOff(IsEditorAnimationEnabled)}\n")
            .Append("\n# Display object passability in the Editor: on/off\n")
            .Append($"editor passability = {OnOff(IsEditorPassabilityEnabled)}\n")
            .Append("\n# Save files sorting method: name/date\n")
            .Append($"save file sorting = {(SaveFileSortType == SaveFileSortingMethod.Timestamp ? "date" : "name")}\n")
            .Append("\n# Show army size estimates: in 'canonical' (few, several, lots, ...) or 'numeric' (1-4, 5-9, 10-19, ...) way\n")
            .Append($"army estimation view type = {(IsNumericArmyEstimationView ? "numeric" : "canonical")}\n");

        return text.ToString();
    }

    private static string OnOff(bool value) => value ? "on" : "off";
}
