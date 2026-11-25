using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The battle settings window opened by the <c>SETTINGS</c> button, a port of
/// <c>openInterfaceBattleOptionDialog()</c> (<c>battle_interface_settings.cpp:196-330</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>布局</b>：<c>StandardWindow( 289, optionsStepY * 3 + 52 )</c>，两行三列外加第三行中间一个图标——
/// 第一行回合顺序 / 网格 / 伤害数字，第二行移动阴影 / 鼠标阴影 / 移动范围，第三行中间是血量条。
/// 每个图标由 <see cref="OptionItem.Draw"/> 连同标题与当前值一起画（<c>drawOption()</c>）。
/// </para>
/// <para>
/// <b>开关</b>：点击图标就地切换 <see cref="Settings"/> 的对应标志并把 <see cref="Changed"/> 置上，
/// <c>OK</c> 或 Esc 关闭。参考用 <c>emptyDialogRestorer</c> 只重画窗口区域，端口每帧整屏重画，
/// 所以场景只要下一帧按新设置画战场即可（网格开关在 <see cref="BattleFieldPainter"/> 里读）。
/// </para>
/// <para>
/// <b>偏差</b>：右击图标的说明框（<c>showStandardTextMessage</c>）没接；「移动范围」图标里的六角阴影与
/// 「血量条」图标里示意用的血量条也没画（后者依赖 <c>drawHitPointsBar</c>，见 <c>bd list</c> 的
/// <c>ukw.21</c>），两个图标先用背景 + 生物图 + 计数条拼出来。
/// </para>
/// </remarks>
public sealed class BattleSettingsWindow
{
    /// <summary>Width of the window, <c>StandardWindow( 289, … )</c>.</summary>
    public const int Width = 289;

    /// <summary>Height of the window, <c>optionsStepY * 3 + 52</c>.</summary>
    public const int Height = (OptionItem.OptionsStepY * 3) + 52;

    /// <summary>Offset of the <c>OK</c> button from the bottom of the active area, <c>{ 0, 5 }</c>.</summary>
    private const int OkOffsetY = 5;

    private readonly IViewWorldSpriteSource _sprites;
    private readonly StandardWindow _window;
    private readonly ButtonSprite? _okButton;
    private readonly bool _isTurnOrderInsideWindow;

    private readonly Rect _turnOrderArea;
    private readonly Rect _gridArea;
    private readonly Rect _damageInfoArea;
    private readonly Rect _shadowMovementArea;
    private readonly Rect _shadowCursorArea;
    private readonly Rect _movementArea;
    private readonly Rect _hitPointsBarArea;

    /// <summary>
    /// Builds the window, or returns <see langword="null"/> when the icons or the OK button are missing.
    /// </summary>
    /// <param name="sprites">Where the <c>CSPANEL</c>, <c>MONS32</c> and <c>TEXTBAR</c> frames come from.</param>
    /// <param name="okReleased">Released sprite of the OK button, <c>SYSTEM.ICN</c> frame 1 in this data set.</param>
    /// <param name="okPressed">Pressed sprite of the OK button, <c>SYSTEM.ICN</c> frame 2.</param>
    /// <param name="screen">Screen the window is centered on.</param>
    /// <param name="turnOrderInsideWindow">
    /// Whether the turn order bar is drawn inside the battlefield window; the reference then skips the
    /// «Bottom» state (<c>battle_interface_settings.cpp:245-250</c>).
    /// </param>
    /// <returns>The window, or <see langword="null"/> without artwork.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sprites"/>, <paramref name="okReleased"/> or <paramref name="okPressed"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static BattleSettingsWindow? TryCreate(IViewWorldSpriteSource sprites, Sprite okReleased,
        Sprite okPressed, Size screen, bool turnOrderInsideWindow)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(okReleased);
        ArgumentNullException.ThrowIfNull(okPressed);

        if (sprites.GetSprite(IcnType.Cspanel, 3) is null)
        {
            return null;
        }

        return new BattleSettingsWindow(sprites, screen, turnOrderInsideWindow, okReleased, okPressed);
    }

    /// <summary>Places the window and its seven options.</summary>
    /// <param name="sprites">Where the artwork comes from.</param>
    /// <param name="screen">Screen the window is centered on.</param>
    /// <param name="turnOrderInsideWindow">Whether the turn order bar lives inside the battlefield window.</param>
    /// <param name="okReleased">Released sprite of the OK button.</param>
    /// <param name="okPressed">Pressed sprite of the OK button.</param>
    private BattleSettingsWindow(IViewWorldSpriteSource sprites, Size screen, bool turnOrderInsideWindow,
        Sprite okReleased, Sprite okPressed)
    {
        _sprites = sprites;
        _isTurnOrderInsideWindow = turnOrderInsideWindow;
        _window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);

        Rect active = _window.ActiveArea;

        _turnOrderArea = OptionArea(active, OptionItem.ThreeOptionsOffsetX, OptionItem.OptionsOffsetY);
        _gridArea = OptionArea(active, OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
            OptionItem.OptionsOffsetY);
        _damageInfoArea = OptionArea(active, OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2),
            OptionItem.OptionsOffsetY);
        _shadowMovementArea = OptionArea(active, OptionItem.ThreeOptionsOffsetX,
            OptionItem.OptionsOffsetY + OptionItem.OptionsStepY);
        _shadowCursorArea = OptionArea(active, OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
            OptionItem.OptionsOffsetY + OptionItem.OptionsStepY);
        _movementArea = OptionArea(active, OptionItem.ThreeOptionsOffsetX + (OptionItem.ThreeOptionsStepX * 2),
            OptionItem.OptionsOffsetY + OptionItem.OptionsStepY);
        _hitPointsBarArea = OptionArea(active, OptionItem.ThreeOptionsOffsetX + OptionItem.ThreeOptionsStepX,
            OptionItem.OptionsOffsetY + (OptionItem.OptionsStepY * 2));

        _okButton = new ButtonSprite(okReleased, okPressed);

        Point okPosition = _window.RenderPosition(new Point(0, OkOffsetY),
            new Size(okReleased.Width, okReleased.Height), WindowPadding.BottomCenter);
        _okButton.SetPosition(okPosition.X, okPosition.Y);
    }

    /// <summary>The window rectangle, <c>StandardWindow::windowArea()</c>.</summary>
    public Rect Frame => _window.WindowArea;

    /// <summary>The area the options are laid out in, <c>background.activeArea()</c>.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>Whether an option changed this session, so the battlefield has to be painted again.</summary>
    public bool Changed { get; private set; }

    /// <summary>Paints the window, its seven options and the OK button.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        DrawTurnOrder(target);
        DrawGrid(target);
        DrawDamageInfo(target);
        DrawShadowMovement(target);
        DrawShadowCursor(target);
        DrawMovementArea(target);
        DrawHitPointsBar(target);

        _okButton?.Draw(target);
    }
    /// <summary>Draws the turn order option, <c>drawTurnOrder()</c> (<c>battle_interface_settings.cpp:53-86</c>).</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawTurnOrder(PixelBuffer target)
    {
        BattleTurnOrderState state = Settings.Get().BattleTurnOrder;

        string value = state switch
        {
            BattleTurnOrderState.Off => "Off",
            BattleTurnOrderState.Top => "Top",
            _ => _isTurnOrderInsideWindow ? "Top" : "Bottom",
        };

        DrawOption(target, _turnOrderArea, state == BattleTurnOrderState.Off ? 3 : 4, "Turn Order", value);
    }

    /// <summary>Draws the grid option, <c>drawGrid()</c> (<c>battle_interface_settings.cpp:88-95</c>).</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawGrid(PixelBuffer target)
    {
        bool enabled = Settings.Get().IsBattleShowGrid;

        DrawOption(target, _gridArea, enabled ? 9 : 8, "Grid", enabled ? "On" : "Off");
    }

    /// <summary>Draws the damage info option, <c>drawDamageInfo()</c> (<c>battle_interface_settings.cpp:97-105</c>).</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawDamageInfo(PixelBuffer target)
    {
        bool enabled = Settings.Get().IsBattleShowDamage;

        DrawOption(target, _damageInfoArea, enabled ? 4 : 3, "Damage Info", enabled ? "On" : "Off");
    }

    /// <summary>Draws the movement shadow option, <c>drawShadowMovement()</c> (<c>battle_interface_settings.cpp:107-114</c>).</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawShadowMovement(PixelBuffer target)
    {
        bool enabled = Settings.Get().IsBattleShowMoveShadow;

        DrawOption(target, _shadowMovementArea, enabled ? 11 : 10, "Shadow Movement", enabled ? "On" : "Off");
    }

    /// <summary>Draws the cursor shadow option, <c>drawShadowCursor()</c> (<c>battle_interface_settings.cpp:116-123</c>).</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawShadowCursor(PixelBuffer target)
    {
        bool enabled = Settings.Get().IsBattleShowMouseShadow;

        DrawOption(target, _shadowCursorArea, enabled ? 13 : 12, "Shadow Cursor", enabled ? "On" : "Off");
    }

    /// <summary>Draws one option of the <c>CSPANEL</c> icons.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    /// <param name="area">Area of the icon.</param>
    /// <param name="frame">Frame of <c>CSPANEL</c>.</param>
    /// <param name="title">Text above the icon.</param>
    /// <param name="value">Text under the icon.</param>
    private void DrawOption(PixelBuffer target, Rect area, int frame, string title, string value)
    {
        if (_sprites.GetSprite(IcnType.Cspanel, frame) is { } icon)
        {
            OptionItem.Draw(target, area, icon, title, value, OptionItem.ThreeElementsRowWidth);
        }
    }


    /// <summary>Draws the movement area option, <c>drawMovementArea()</c> (<c>battle_interface_settings.cpp:139-161</c>).</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawMovementArea(PixelBuffer target)
    {
        bool enabled = Settings.Get().IsBattleMovementAreaHighlight;

        if (ComposeMovementAreaIcon(enabled) is { } icon)
        {
            OptionItem.Draw(target, _movementArea, icon, "Movement Area", enabled ? "On" : "Off",
                OptionItem.ThreeElementsRowWidth);
        }
    }

    /// <summary>Draws the hit points bar option, <c>drawHitPointsBarOption()</c> (<c>battle_interface_settings.cpp:163-193</c>).</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawHitPointsBar(PixelBuffer target)
    {
        bool enabled = Settings.Get().IsBattleShowHitPointsBar;

        if (ComposeHitPointsIcon() is { } icon)
        {
            OptionItem.Draw(target, _hitPointsBarArea, icon, "Hit Points", enabled ? "On" : "Off",
                OptionItem.ThreeElementsRowWidth);
        }
    }

    /// <summary>
    /// Builds the movement area icon: an empty option background with a Hydra and, while the option is on,
    /// the adventure cursor over it.
    /// </summary>
    /// <param name="enabled">Whether the option is on.</param>
    /// <returns>The icon, or <see langword="null"/> when the background is missing.</returns>
    private Sprite? ComposeMovementAreaIcon(bool enabled)
    {
        PixelBuffer image = ComposeOptionBackground();

        if (_sprites.GetSprite(IcnType.Mons32, 34) is { } creature)
        {
            BlitCentered(image, creature);
        }

        if (enabled && _sprites.GetSprite(IcnType.Advmco, 0) is { } cursor)
        {
            BlitCentered(image, cursor, 10, 15);
        }

        return new Sprite(image);
    }

    /// <summary>
    /// Builds the hit points bar icon: an empty option background with a creature and the troop count bar
    /// under it.
    /// </summary>
    /// <returns>The icon, or <see langword="null"/> when the background is missing.</returns>
    /// <remarks>
    /// 参考还会在计数条上画一条示意用的血量条（<c>drawHitPointsBar</c>），那是 <c>bd list</c> 的 <c>ukw.21</c>；
    /// 这里只画计数条。
    /// </remarks>
    private Sprite? ComposeHitPointsIcon()
    {
        PixelBuffer image = ComposeOptionBackground();

        if (_sprites.GetSprite(IcnType.Mons32, 34) is { } creature)
        {
            BlitCentered(image, creature, 0, -6);
        }

        if (_sprites.GetSprite(IcnType.Textbar, 10) is { } countBar)
        {
            BlitCentered(image, countBar, 0, (image.Height - countBar.Image.Height) / 2);
        }

        return new Sprite(image);
    }

    /// <summary>Copies <c>EMPTY_OPTION_ICON_BACKGROUND</c> into a new buffer the size of the option icon.</summary>
    /// <returns>The buffer, or <see langword="null"/> when the artwork is missing.</returns>
    /// <summary>Copies <c>EMPTY_OPTION_ICON_BACKGROUND</c> into a new buffer, the background of a composed icon.</summary>
    /// <returns>The buffer; an empty one when the artwork is missing.</returns>
    /// <remarks>
    /// 这套数据里没有 <c>EMPTY_OPTION_ICON_BACKGROUND</c>（新版资源），退回一块同尺寸的空底板——
    /// 两次合成因此是「黑底上的生物图」，与参考的石头底板不同，其余布局一致。
    /// </remarks>
    private PixelBuffer ComposeOptionBackground()
    {
        if (_sprites.GetSprite(IcnType.EmptyOptionIconBackground, 0) is not { } background)
        {
            return new PixelBuffer(OptionItem.OptionIconSize, OptionItem.OptionIconSize);
        }

        PixelBuffer image = new(background.Image.Width, background.Image.Height);
        ImageOps.Blit(image, Point.Zero, background.Image, background.Image.Bounds);

        return image;
    }

    /// <summary>Blits a sprite centered in <paramref name="target"/>, optionally shifted.</summary>
    /// <param name="target">Buffer to draw into.</param>
    /// <param name="sprite">The sprite.</param>
    /// <param name="offsetX">Shift from the centred position.</param>
    /// <param name="offsetY">Shift from the centred position.</param>
    private static void BlitCentered(PixelBuffer target, Sprite sprite, int offsetX = 0, int offsetY = 0)
    {
        int x = ((target.Width - sprite.Image.Width) / 2) + offsetX;
        int y = ((target.Height - sprite.Image.Height) / 2) + offsetY;

        ImageOps.Blit(target, new Point(x, y), sprite.Image, sprite.Image.Bounds);
    }

    /// <summary>Consumes one frame of input.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns><see langword="true"/> when the window has to close.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _okButton?.UpdatePressState(events);

        Settings settings = Settings.Get();

        if (events.MouseClickLeft(_turnOrderArea))
        {
            settings.SwitchToNextBattleTurnOrderState();

            if (_isTurnOrderInsideWindow && settings.BattleTurnOrder == BattleTurnOrderState.Bottom)
            {
                // 战场窗口里画不了「底部」，与参考一样再跳一格（battle_interface_settings.cpp:245-250）。
                settings.SwitchToNextBattleTurnOrderState();
            }

            Changed = true;
        }
        else if (events.MouseClickLeft(_gridArea))
        {
            settings.SetBattleGrid(!settings.IsBattleShowGrid);
            Changed = true;
        }
        else if (events.MouseClickLeft(_damageInfoArea))
        {
            settings.SetBattleShowDamage(!settings.IsBattleShowDamage);
            Changed = true;
        }
        else if (events.MouseClickLeft(_shadowMovementArea))
        {
            settings.SetBattleMovementShaded(!settings.IsBattleShowMoveShadow);
            Changed = true;
        }
        else if (events.MouseClickLeft(_shadowCursorArea))
        {
            settings.SetBattleMouseShaded(!settings.IsBattleShowMouseShadow);
            Changed = true;
        }
        else if (events.MouseClickLeft(_movementArea))
        {
            settings.SetBattleMovementAreaHighlight(!settings.IsBattleMovementAreaHighlight);
            Changed = true;
        }
        else if (events.MouseClickLeft(_hitPointsBarArea))
        {
            settings.SetBattleShowHitPointsBar(!settings.IsBattleShowHitPointsBar);
            Changed = true;
        }

        if (_okButton is not null && events.MouseClickLeft(_okButton.Area))
        {
            return true;
        }

        return GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel);
    }

    /// <summary>The area of one option: its top-left corner inside the active area plus the icon size.</summary>
    /// <param name="active">Active area of the window.</param>
    /// <param name="offsetX">Horizontal offset, <c>threeOptionsOffsetX + step * column</c>.</param>
    /// <param name="offsetY">Vertical offset, <c>optionsOffsetY + step * row</c>.</param>
    /// <returns>The icon's rectangle.</returns>
    public static Rect OptionArea(Rect active, int offsetX, int offsetY) =>
        new(active.X + offsetX, active.Y + offsetY, OptionItem.OptionIconSize, OptionItem.OptionIconSize);
}
