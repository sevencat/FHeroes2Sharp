using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// One key of the virtual keyboard, a port of <c>KeyboardButton</c> (<c>ui_keyboard.cpp:379-433</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的按键除了按钮本身，还带三样东西：键上的文字（同时也是键盘输入的字符）、对应的硬件按键
/// （用来让物理键盘也按得动这个键）、以及按下时执行的动作。另有两项状态：右键帮助的标题与正文，
/// 和「反色渲染」开关——小写布局里那个 <c>|</c> 键画的是大写字母表，按下时反而要显示成未按下，
/// 才不会和真正按下的键混在一起。
/// </para>
/// <para>
/// 差异：参考在构造时先把阴影渲染进一张 <c>buttonShadow</c> 图片，绘制时只做一次 blit；
/// 端口每帧整屏重画，改用 <see cref="ButtonBase.DrawShadow"/> 现画，像素结果相同，少了那张缓存图。
/// </para>
/// </remarks>
public sealed class VirtualKeyboardButton
{
    /// <summary>
    /// Builds the key of <paramref name="text"/>, the reference's four-argument constructor
    /// (<c>ui_keyboard.cpp:383-399</c>).
    /// </summary>
    /// <param name="text">Label of the key, also the character it types.</param>
    /// <param name="key">Hardware key that presses the same key, <see cref="Key.None"/> for none.</param>
    /// <param name="buttonSize">Area of the label; the plate is grown around it.</param>
    /// <param name="isEvilInterface">Whether the gray plate is used.</param>
    /// <param name="buttonBackground">
    /// Texture the transparent corners of the plate are filled with, <c>STONEBAK</c> or <c>STONEBAK_EVIL</c>.
    /// </param>
    /// <param name="action">What the key does; <see langword="null"/> for a key without an action.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="text"/> or <paramref name="buttonBackground"/> is <see langword="null"/>.
    /// </exception>
    public VirtualKeyboardButton(
        string text,
        Key key,
        Size buttonSize,
        bool isEvilInterface,
        Sprite buttonBackground,
        Func<VirtualKeyboardRenderer, KeyboardDialogAction>? action)
        : this(
            text,
            key,
            ButtonFactory.MakeButtonSprites(text, buttonSize, isEvilInterface, buttonBackground),
            action)
    {
    }

    /// <summary>Wraps ready-made button sprites; used by tests and by the okay button.</summary>
    /// <param name="text">Label of the key.</param>
    /// <param name="key">Hardware key that presses the same key.</param>
    /// <param name="sprites">Released and pressed artwork.</param>
    /// <param name="action">What the key does.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public VirtualKeyboardButton(
        string text,
        Key key,
        (Sprite Released, Sprite Pressed) sprites,
        Func<VirtualKeyboardRenderer, KeyboardDialogAction>? action)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        Key = key;
        Sprite = new ButtonSprite(sprites.Released, sprites.Pressed);
        Action = action;
    }

    /// <summary>Label of the key, <c>text</c> of the reference.</summary>
    public string Text { get; }

    /// <summary>Hardware key which does the same thing, <c>key</c> of the reference.</summary>
    public Key Key { get; }

    /// <summary>The button artwork; it also carries the hit box, <c>button</c> of the reference.</summary>
    public ButtonSprite Sprite { get; }

    /// <summary>The action of the key, <c>action</c> of the reference.</summary>
    public Func<VirtualKeyboardRenderer, KeyboardDialogAction>? Action { get; }

    /// <summary>Title of the right click help, <c>customTitle</c>.</summary>
    public string? PopupTitle { get; init; }

    /// <summary>Body of the right click help, <c>customDescription</c>.</summary>
    public string? PopupBody { get; init; }

    /// <summary>Whether the pressed look is inverted, <c>isInvertedRenderingLogic</c>.</summary>
    public bool IsInvertedRenderingLogic { get; init; }

    /// <summary>Moves the key and its hit box, <c>setPosition()</c> of the reference.</summary>
    /// <param name="x">Left edge of the key.</param>
    /// <param name="y">Top edge of the key.</param>
    public void SetPosition(int x, int y) => Sprite.SetPosition(x, y);

    /// <summary>Shows the key in the pressed or released look.</summary>
    /// <param name="pressed">Whether the pressed look is wanted.</param>
    public void SetPressed(bool pressed)
    {
        if (pressed)
        {
            Sprite.Press();
        }
        else
        {
            Sprite.Release();
        }
    }

    /// <summary>Draws the key with its drop shadow.</summary>
    /// <param name="output">Frame buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考先画按键再贴阴影（<c>ui_keyboard.cpp:974-976</c>），端口反过来把阴影压在按键下面，重叠的那几个像素
    /// 因此是按键本身而不是阴影，看上去更自然。隐藏的按键连阴影都不画，和参考的「<c>draw()</c> 返回
    /// <c>false</c> 就跳过阴影」一致。
    /// </remarks>
    public void Draw(PixelBuffer output)
    {
        if (!Sprite.IsVisible)
        {
            return;
        }

        Sprite.DrawShadow(output);
        Sprite.Draw(output);
    }
}
