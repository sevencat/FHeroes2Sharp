using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The <c>Dialog::inputString()</c> window as a window of the adventure map, the one the sphinx asks its riddle in
/// (<c>heroes_action.cpp:3563</c>).
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="RecruitWindow"/> 同一种形状：场景只转输入与帧，界面全在 <see cref="InputStringView"/> 里。关掉之后
/// <see cref="Result"/> 就是玩家敲下的那一行；按 CANCEL 或 ESC 关掉时它是 <see langword="null"/>。
/// </para>
/// <para>
/// 参考的循环里左键是「按下即算」，端口照抄：<see cref="SetLeftButton"/> 只在按下的那一帧去找按钮，
/// 抬起那帧只更新按下态。
/// </para>
/// </remarks>
public sealed class InputStringWindow
{
    private readonly InputStringView _view;
    private Point? _cursor;
    private bool _leftDown;

    private InputStringWindow(InputStringView view)
    {
        _view = view;
    }

    /// <summary>Raised when the player presses the virtual keyboard button.</summary>
    public event Action? VirtualKeyboardRequested
    {
        add => _view.VirtualKeyboardRequested += value;
        remove => _view.VirtualKeyboardRequested -= value;
    }

    /// <summary>The model this window answers into.</summary>
    public InputStringDialog Dialog => _view.Dialog;

    /// <summary>The text the player typed, or <see langword="null"/> when the window was cancelled.</summary>
    public string? Result => _view.IsClosed ? _view.Dialog.Result : null;

    /// <summary>Whether the window has closed, by OKAY or by CANCEL.</summary>
    public bool IsClosed => _view.IsClosed;

    /// <summary>Whether the window closed by OKAY rather than by CANCEL.</summary>
    public bool IsConfirmed => _view.IsConfirmed;

    /// <summary>The area the window occupies.</summary>
    public Rect Area => _view.Area;

    /// <summary>
    /// Area of the OKAY button; a multi line field is confirmed only by clicking it, since Enter is a newline there
    /// (<c>InputStringView.ProcessHotKey</c>'s guard).
    /// </summary>
    public Rect OkayArea => _view.OkayArea;

    /// <summary>
    /// Builds the window around <paramref name="dialog"/>.
    /// </summary>
    /// <param name="dialog">The texts and the limits.</param>
    /// <param name="titleFont">Face of the title, the yellow one of the reference.</param>
    /// <param name="bodyFont">Face of the question, the white one of the reference.</param>
    /// <param name="screen">Screen the frame is centred on; 640x480 when omitted.</param>
    /// <returns>The window, or <see langword="null"/> when its artwork is missing.</returns>
    /// <exception cref="ArgumentNullException">The dialog or a font is <see langword="null"/>.</exception>
    public static InputStringWindow? TryCreate(
        InputStringDialog dialog, IGlyphSource titleFont, IGlyphSource bodyFont, Size? screen = null) =>
        InputStringView.TryCreate(dialog, titleFont, bodyFont, screen) is { } view ? new InputStringWindow(view) : null;

    /// <summary>Records where the mouse is.</summary>
    /// <param name="point">Cursor position in logical pixels, or <see langword="null"/> when it left the window.</param>
    public void SetCursor(Point? point)
    {
        _cursor = point;
        _view.SetCursor(point);
    }

    /// <summary>Records a left button press or release and answers the press, the <c>MouseClickLeft</c> of the reference.</summary>
    /// <param name="down">Whether the button is now held.</param>
    /// <returns><see langword="true"/> when the press closed the window or moved the caret.</returns>
    public bool SetLeftButton(bool down)
    {
        bool pressed = down && !_leftDown;

        _leftDown = down;
        _view.SetLeftButton(down);

        return pressed && _cursor is { } point && _view.ClickLeft(point);
    }

    /// <summary>Types a key into the field.</summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifier">Modifiers held with it.</param>
    /// <returns><see langword="true"/> when the text or the caret changed.</returns>
    public bool PressKey(Key key, KeyModifier modifier) => _view.PressKey(key, modifier);

    /// <summary>Answers ESC or Enter, the two keys the reference reads out of the button row.</summary>
    /// <param name="hotKey">The hot key.</param>
    /// <returns><see langword="true"/> when the window closed.</returns>
    public bool PressHotKey(ButtonHotKey hotKey) => _view.ProcessHotKey(hotKey);

    /// <summary>Advances the caret blink.</summary>
    /// <returns><see langword="true"/> when the caret changed visibility.</returns>
    public bool Update() => _view.Update();

    /// <summary>Draws the window.</summary>
    /// <param name="target">Indexed frame buffer of the scene below.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target) => _view.Draw(target);
}
