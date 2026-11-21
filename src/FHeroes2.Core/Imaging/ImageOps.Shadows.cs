using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// The two shadow helpers of <c>engine/image.cpp</c>: a darkened copy of a sprite, and the copy with the
/// sprite drawn back on top of it.
/// </summary>
/// <remarks>
/// <para>
/// 中世纪风格的界面不用模糊阴影，而是「把轮廓按调色板表压暗一档」：<see cref="MakeShadow"/> 造一张底色为空、
/// 变换层按原图剪影填表的图，<see cref="AddShadow"/> 再把原图贴回去，于是看起来像原图在 (x, y) 偏移处投影。
/// </para>
/// <para>
/// 两张图都是**双层**的（像素 + 变换层）：剪影不是画出来的颜色，而是「这些位置用了 2 号压暗表」。这与
/// <see cref="ApplyTransform"/>、<see cref="AddGradientShadow"/> 是同一套约定。单层输入当整块不透明处理，
/// 参考也是这么分支的。
/// </para>
/// </remarks>
public static partial class ImageOps
{
    /// <summary>
    /// The silhouette of a sprite, <c>fheroes2::makeShadow()</c> (<c>engine/image.cpp:2665-2706</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 阴影只往左下长：偏移的 x 必须非正、y 必须非负，否则答一张空图（参考直接 <c>return {}</c>）。
    /// 输出图因此更宽更高：宽 <c>width - offset.x</c>、高 <c>height + offset.y</c>，锚点跟着左移。
    /// </para>
    /// <para>
    /// 剪影的判定是「原图变换层为 <c>0</c>」——变换层 <c>1</c> 表示透明，其余值表示另一次压暗，
    /// 都不算可见像素。
    /// </para>
    /// </remarks>
    /// <param name="input">Sprite to cast a shadow of.</param>
    /// <param name="shadowOffset">Direction and length of the shadow; left and down only.</param>
    /// <param name="transformId">Palette table used for the silhouette.</param>
    /// <returns>The silhouette with its own anchor, empty when the offset points the wrong way.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public static Sprite MakeShadow(Sprite input, Point shadowOffset, byte transformId)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Image.Width == 0 || input.Image.Height == 0 || shadowOffset.X > 0 || shadowOffset.Y < 0)
        {
            return new Sprite(new PixelBuffer(0, 0));
        }

        int width = input.Image.Width;
        int height = input.Image.Height;

        PixelBuffer shadowImage = new(width - shadowOffset.X, height + shadowOffset.Y);
        Sprite shadow = new(shadowImage, input.OffsetX + shadowOffset.X, input.OffsetY,
            new byte[shadowImage.Width * shadowImage.Height]);

        if (input.Transform is not { } inputTransform)
        {
            // 单层图整块不透明：直接在下移后的整块区域填表。
            FillTransform(shadow, new Rect(0, shadowOffset.Y, width, height), transformId);

            return shadow;
        }

        int outputStride = shadow.Image.Stride;
        ReadOnlySpan<byte> source = inputTransform;
        Span<byte> destination = shadow.Transform;

        for (int y = 0; y < height; ++y)
        {
            ReadOnlySpan<byte> sourceRow = source.Slice(y * width, width);
            Span<byte> destinationRow = destination.Slice(((y + shadowOffset.Y) * outputStride), width);

            for (int x = 0; x < width; ++x)
            {
                if (sourceRow[x] == 0)
                {
                    destinationRow[x] = transformId;
                }
            }
        }

        return shadow;
    }

    /// <summary>
    /// A sprite drawn over its own shadow, <c>fheroes2::addShadow()</c> (<c>engine/image.cpp:949-959</c>).
    /// </summary>
    /// <param name="input">Sprite to cast a shadow of; returned unchanged when the offset points the wrong way.</param>
    /// <param name="shadowOffset">Direction and length of the shadow; left and down only.</param>
    /// <param name="transformId">Palette table used for the silhouette.</param>
    /// <returns>A new sprite with the shadow underneath.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public static Sprite AddShadow(Sprite input, Point shadowOffset, byte transformId)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Image.Width == 0 || input.Image.Height == 0 || shadowOffset.X > 0 || shadowOffset.Y < 0)
        {
            return input;
        }

        Sprite output = MakeShadow(input, shadowOffset, transformId);

        // 剪影图比原图宽出 -offset.x 像素，把原图右移同样的量正好落回原位。
        BlitSprite(output, new Point(-shadowOffset.X, 0), input);

        return output;
    }
}
