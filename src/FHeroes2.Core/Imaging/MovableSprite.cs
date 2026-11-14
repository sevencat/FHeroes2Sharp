namespace FHeroes2.Core.Imaging;

/// <summary>
/// A sprite that remembers where it goes and whether it is visible, the counterpart of
/// <c>fheroes2::MovableSprite</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_tool.h:48</c> class MovableSprite；实现
/// <c>src/fheroes2/gui/ui_tool.cpp:123</c>（构造 123 / 129 / 137，<c>operator=</c> 152，
/// <c>setPosition</c> 165，<c>show</c> 177）。
/// </para>
/// <para>
/// 差异一：参考继承 <c>fheroes2::Sprite</c>（<c>src/engine/image.h:128</c>，基类自带 <c>_x/_y</c>），
/// C# 的 <see cref="Sprite"/> 是 <c>sealed</c>，所以这里用组合——<see cref="Sprite"/> 是图像本身，
/// 本类型加位置与可见性。
/// </para>
/// <para>
/// 差异二：参考的 <c>show</c> 会立刻 <c>Blit</c> 到 <c>Display</c> 并让 <c>ImageRestorer</c> 记下背景，
/// <c>setPosition</c> 在可见时是「隐藏 → 移动 → 重新显示」。本项目没有立即呈现的 Display，场景每帧
/// 从干净缓冲重画，所以这里只改标志位与坐标，呈现交给下一帧。
/// </para>
/// </remarks>
public sealed class MovableSprite
{
    private Sprite? _sprite;

    /// <summary>Creates an empty, hidden sprite.</summary>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.cpp:123</c> MovableSprite::MovableSprite()。</remarks>
    public MovableSprite()
    {
    }

    /// <summary>Creates an empty image of the given size at the given position. It starts visible unless the area is empty.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="x">Left edge on the target.</param>
    /// <param name="y">Top edge on the target.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.cpp:129</c> MovableSprite( width, height, x, y )：<c>_isHidden( height == 0 &amp;&amp; width == 0 )</c>。</remarks>
    public MovableSprite(int width, int height, int x, int y)
    {
        _sprite = new Sprite(width, height, x, y);
        X = x;
        Y = y;
        IsHidden = width == 0 && height == 0;
    }

    /// <summary>Creates a sprite around <paramref name="sprite"/>. It starts visible, like the reference.</summary>
    /// <param name="sprite">The image to show.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.cpp:137</c> MovableSprite( const Sprite &amp; )：<c>_isHidden( false )</c>。</remarks>
    public MovableSprite(Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        _sprite = sprite;
        IsHidden = false;
    }

    /// <summary>The image itself, the reference's <c>Sprite</c> base; <see langword="null"/> until set.</summary>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.h:48</c> 的基类 <c>fheroes2::Sprite</c>（<c>src/engine/image.h:128</c>）。</remarks>
    public Sprite? Sprite => _sprite;

    /// <summary>Left edge of the sprite on the target, in pixels.</summary>
    /// <remarks>参考：<c>src/engine/image.h:166</c> Sprite::x()。</remarks>
    public int X { get; private set; }

    /// <summary>Top edge of the sprite on the target, in pixels.</summary>
    /// <remarks>参考：<c>src/engine/image.h:171</c> Sprite::y()。</remarks>
    public int Y { get; private set; }

    /// <summary>Whether drawing skips the sprite.</summary>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.h:80</c> isHidden()；默认值 <c>_isHidden{ true }</c> 在 100 行。</remarks>
    public bool IsHidden { get; private set; } = true;

    /// <summary>Moves the sprite.</summary>
    /// <param name="x">Left edge on the target.</param>
    /// <param name="y">Top edge on the target.</param>
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_tool.cpp:165</c> MovableSprite::setPosition（可见时 hide → 移动 → show）。
    /// 差异：没有立即呈现，只改坐标，见类注释。
    /// </remarks>
    public void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Replaces the image, the reference's <c>operator=( const Sprite &amp; )</c>. The position and
    /// the visibility flag are kept, so assigning a new frame does not move a checkbox.
    /// </summary>
    /// <param name="sprite">The new image.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.cpp:152</c> operator=( const Sprite &amp; )。</remarks>
    public void Set(Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        _sprite = sprite;
    }

    /// <summary>Makes the sprite visible.</summary>
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_tool.cpp:177</c> show()（隐藏时 <c>Blit</c> 到 Display）。
    /// 差异：只清标志位，呈现交给场景的下一帧。
    /// </remarks>
    public void Show() => IsHidden = false;

    /// <summary>Skips the sprite when drawing.</summary>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_tool.h:65</c> hide()（恢复背景并置隐藏）。差异：只置标志位。</remarks>
    public void Hide() => IsHidden = true;
}
