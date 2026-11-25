using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.UI.Battle;

/// <summary>Which of the three buttons under the battlefield is meant, <c>Interface::_buttonAuto</c> and friends.</summary>
public enum BattleStatusButton
{
    /// <summary>No button; the pointer is somewhere else.</summary>
    None,

    /// <summary>The <c>AUTO</c> button, <c>TEXTBAR</c> frames 4 and 5.</summary>
    Auto,

    /// <summary>The <c>SETTINGS</c> button, <c>TEXTBAR</c> frames 6 and 7.</summary>
    Settings,

    /// <summary>The <c>SKIP</c> button, <c>TEXTBAR</c> frames 0 and 1.</summary>
    Skip,
}

/// <summary>
/// The three buttons of the status bar — auto combat, settings and skip — with their hit boxes and their
/// pressed look, the <c>_buttonAuto</c>/<c>_buttonSettings</c>/<c>_buttonSkip</c> of
/// <c>Battle::Interface</c> (<c>battle_interface.cpp:1467-1490</c>, <c>:1678-1680</c>, <c>:3222-3224</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>摆位</b>：设置按钮贴着屏幕左下角，自动战斗按钮压在它上面，跳过按钮贴右下角——参考的
/// <c>_buttonSettings.setPosition( area.x, area.y + area.height - settingsRect.height )</c>、
/// <c>_buttonAuto.setPosition( area.x, satusOffsetY )</c> 与
/// <c>_buttonSkip.setPosition( area.x + area.width - width, area.y + area.height - height )</c>。
/// 三块图都从 <c>TEXTBAR</c> 取，按钮自己的锚点（<see cref="Sprite.OffsetX"/>）要加进去，位置才和画出来的像素一致。
/// </para>
/// <para>
/// <b>按下态</b>：参考每帧用 <c>le.isMouseLeftButtonPressedAndHeldInArea()</c> 决定画哪一帧
/// （<c>battle_interface.cpp:3222-3224</c>），所以这一层只提供「画哪个状态」，按键判断留在场景里。
/// </para>
/// </remarks>
public sealed class BattleStatusButtons
{
    /// <summary>Frame of the released auto combat button, <c>TEXTBAR</c> 4.</summary>
    public const int AutoReleasedFrame = 4;

    /// <summary>Frame of the pressed auto combat button, <c>TEXTBAR</c> 5.</summary>
    public const int AutoPressedFrame = 5;

    /// <summary>Frame of the released settings button, <c>TEXTBAR</c> 6.</summary>
    public const int SettingsReleasedFrame = 6;

    /// <summary>Frame of the pressed settings button, <c>TEXTBAR</c> 7.</summary>
    public const int SettingsPressedFrame = 7;

    /// <summary>Frame of the released skip button, <c>TEXTBAR</c> 0.</summary>
    public const int SkipReleasedFrame = 0;

    /// <summary>Frame of the pressed skip button, <c>TEXTBAR</c> 1.</summary>
    public const int SkipPressedFrame = 1;

    /// <summary>Width of the auto button when the archive is missing.</summary>
    public const int FallbackAutoWidth = 49;

    /// <summary>Height of the auto button when the archive is missing.</summary>
    public const int FallbackAutoHeight = 18;

    /// <summary>Width of the settings button when the archive is missing.</summary>
    public const int FallbackSettingsWidth = 49;

    /// <summary>Height of the settings button when the archive is missing.</summary>
    public const int FallbackSettingsHeight = 19;

    /// <summary>Width of the skip button when the archive is missing.</summary>
    public const int FallbackSkipWidth = 51;

    /// <summary>Height of the skip button when the archive is missing.</summary>
    public const int FallbackSkipHeight = 37;

    private readonly IViewWorldSpriteSource _sprites;
    private readonly Point _autoPosition;
    private readonly Point _settingsPosition;
    private readonly Point _skipPosition;
    private readonly Rect _autoArea;
    private readonly Rect _settingsArea;
    private readonly Rect _skipArea;

    /// <summary>Lays the three buttons out against the bottom of the screen.</summary>
    /// <param name="sprites">Where the <c>TEXTBAR</c> frames come from.</param>
    /// <param name="screenWidth">Width of the screen, <c>area.width</c> of the reference.</param>
    /// <param name="screenHeight">Height of the screen, <c>area.height</c> of the reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sprites"/> is <see langword="null"/>.</exception>
    public BattleStatusButtons(IViewWorldSpriteSource sprites, int screenWidth, int screenHeight)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        _sprites = sprites;

        Sprite? auto = sprites.GetSprite(IcnType.Textbar, AutoReleasedFrame);
        Sprite? settings = sprites.GetSprite(IcnType.Textbar, SettingsReleasedFrame);
        Sprite? skip = sprites.GetSprite(IcnType.Textbar, SkipReleasedFrame);

        int autoHeight = auto?.Image.Height ?? FallbackAutoHeight;
        int settingsHeight = settings?.Image.Height ?? FallbackSettingsHeight;
        int skipWidth = skip?.Image.Width ?? FallbackSkipWidth;
        int skipHeight = skip?.Image.Height ?? FallbackSkipHeight;

        _settingsPosition = new Point(0, screenHeight - settingsHeight);
        _autoPosition = new Point(0, _settingsPosition.Y - autoHeight);
        _skipPosition = new Point(screenWidth - skipWidth, screenHeight - skipHeight);

        _autoArea = AreaOf(auto, _autoPosition, FallbackAutoWidth, FallbackAutoHeight);
        _settingsArea = AreaOf(settings, _settingsPosition, FallbackSettingsWidth, FallbackSettingsHeight);
        _skipArea = AreaOf(skip, _skipPosition, FallbackSkipWidth, FallbackSkipHeight);
    }

    /// <summary>The auto combat button, <c>_buttonAuto.area()</c>.</summary>
    public Rect AutoArea => _autoArea;

    /// <summary>The settings button, <c>_buttonSettings.area()</c>.</summary>
    public Rect SettingsArea => _settingsArea;

    /// <summary>The skip button, <c>_buttonSkip.area()</c>.</summary>
    public Rect SkipArea => _skipArea;

    /// <summary>The hit box of one button; a default rectangle for <see cref="BattleStatusButton.None"/>.</summary>
    /// <param name="button">The button asked about.</param>
    /// <returns>Its area, or an empty rectangle.</returns>
    public Rect AreaOf(BattleStatusButton button) => button switch
    {
        BattleStatusButton.Auto => _autoArea,
        BattleStatusButton.Settings => _settingsArea,
        BattleStatusButton.Skip => _skipArea,
        _ => default,
    };


    /// <summary>The button under <paramref name="cursor"/>, or <see cref="BattleStatusButton.None"/>.</summary>
    /// <param name="cursor">Cursor position in logical pixels.</param>
    /// <returns>The button, or none.</returns>
    /// <remarks>
    /// 参考按 <c>isMouseCursorPosInArea( _buttonAuto.area() )</c> 的顺序逐个问
    /// （<c>battle_interface.cpp:3317-3361</c>）；三块互不重叠，所以顺序不影响结果。
    /// </remarks>
    public BattleStatusButton HitTest(Point cursor)
    {
        if (AreaOf(BattleStatusButton.Auto).Contains(cursor))
        {
            return BattleStatusButton.Auto;
        }

        if (AreaOf(BattleStatusButton.Settings).Contains(cursor))
        {
            return BattleStatusButton.Settings;
        }

        return AreaOf(BattleStatusButton.Skip).Contains(cursor) ? BattleStatusButton.Skip : BattleStatusButton.None;
    }

    /// <summary>Draws the three buttons, the released or the pressed frame per button.</summary>
    /// <param name="output">Frame buffer of the battle screen.</param>
    /// <param name="pressed">The button that is held down, or <see cref="BattleStatusButton.None"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer output, BattleStatusButton pressed)
    {
        ArgumentNullException.ThrowIfNull(output);

        DrawButton(output, AutoReleasedFrame, AutoPressedFrame, _autoPosition, pressed == BattleStatusButton.Auto);
        DrawButton(output, SettingsReleasedFrame, SettingsPressedFrame, _settingsPosition,
            pressed == BattleStatusButton.Settings);
        DrawButton(output, SkipReleasedFrame, SkipPressedFrame, _skipPosition, pressed == BattleStatusButton.Skip);
    }

    /// <summary>Blits one frame of <c>TEXTBAR</c> with the frame's own offsets, the reference's <c>Button::draw()</c>.</summary>
    /// <param name="output">Frame buffer of the battle screen.</param>
    /// <param name="releasedFrame">Frame of the released state.</param>
    /// <param name="pressedFrame">Frame of the pressed state.</param>
    /// <param name="position">Where the button's anchor goes.</param>
    /// <param name="isPressed">Whether the pressed frame is wanted.</param>
    private void DrawButton(PixelBuffer output, int releasedFrame, int pressedFrame, Point position, bool isPressed)
    {
        if (_sprites.GetSprite(IcnType.Textbar, isPressed ? pressedFrame : releasedFrame) is not { } sprite)
        {
            return;
        }

        ImageOps.BlitSprite(output, new Point(position.X + sprite.OffsetX, position.Y + sprite.OffsetY), sprite);
    }

    /// <summary>The hit box of a button: its sprite bounds shifted by the anchor and the sprite's own offset.</summary>
    /// <param name="sprite">Artwork of the button, or <see langword="null"/> when it is missing.</param>
    /// <param name="position">Where the button's anchor goes.</param>
    /// <param name="fallbackWidth">Width used when the artwork is missing.</param>
    /// <param name="fallbackHeight">Height used when the artwork is missing.</param>
    /// <returns>The area the button covers.</returns>
    private static Rect AreaOf(Sprite? sprite, Point position, int fallbackWidth, int fallbackHeight) =>
        sprite is null
            ? new Rect(position.X, position.Y, fallbackWidth, fallbackHeight)
            : new Rect(position.X + sprite.OffsetX, position.Y + sprite.OffsetY, sprite.Image.Width,
                sprite.Image.Height);
}

