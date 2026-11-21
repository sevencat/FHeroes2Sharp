namespace FHeroes2.Core.Imaging;

/// <summary>
/// An image with a screen position and an optional second byte layer, the equivalent of
/// <c>fheroes2::Sprite</c>. The <i>transform layer</i> is not a mask: for every pixel it
/// selects how that pixel is combined with the destination, which is how the original engine
/// renders transparency, shadows and light effects without per pixel alpha.
/// </summary>
/// <remarks>
/// The transform values are decoded straight from the ICN data (see
/// <c>FHeroes2.Assets.Icn.IcnSpriteDecoder</c>): <c>0</c> copies <see cref="Image"/>,
/// <c>1</c> leaves the destination untouched and every other value is an index into
/// <see cref="PaletteTransformTable"/>. A sprite whose transform layer is unused stores
/// <see langword="null"/> in <see cref="Transform"/>, mirroring
/// <c>fheroes2::Image::_disableTransformLayer()</c>.
/// </remarks>
public sealed class Sprite
{
    public Sprite(int width, int height, int offsetX = 0, int offsetY = 0)
        : this(new PixelBuffer(width, height), offsetX, offsetY, new byte[checked(width * height)])
    {
    }

    public Sprite(PixelBuffer image, int offsetX = 0, int offsetY = 0, byte[]? transform = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (transform is not null && transform.Length != image.Width * image.Height)
        {
            throw new ArgumentException(
                $"A transform layer needs {image.Width * image.Height} bytes but {transform.Length} were provided.",
                nameof(transform));
        }

        Image = image;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Transform = transform;
    }

    /// <summary>The indexed image layer.</summary>
    public PixelBuffer Image { get; }

    /// <summary>
    /// The transform layer, or <see langword="null"/> when every pixel is copied verbatim
    /// (the original engine calls this a "single layer" image).
    /// </summary>
    public byte[]? Transform { get; }

    /// <summary>Horizontal offset of the sprite anchor, as stored in the ICN header.</summary>
    public int OffsetX { get; }

    /// <summary>Vertical offset of the sprite anchor, as stored in the ICN header.</summary>
    public int OffsetY { get; }

    public int Width => Image.Width;

    public int Height => Image.Height;

    public bool HasTransformLayer => Transform is not null;

    /// <summary>
    /// Makes the sprite fully transparent: the image layer is zeroed and the transform layer is
    /// set to <c>1</c> (skip), exactly like <c>fheroes2::Image::reset()</c>.
    /// </summary>
    public void Reset()
    {
        Image.Fill(0);

        if (Transform is not null)
        {
            Array.Fill(Transform, (byte)1);
        }
    }

    public Sprite Clone() => new(Image.Clone(), OffsetX, OffsetY, Transform is null ? null : (byte[])Transform.Clone());

    /// <summary>
    /// Returns the sprite mirrored inside its own bounds, the <c>fheroes2::Flip</c> of the reference,
    /// which <c>Interface::GameArea::BlitOnTile( ..., flip )</c> applies on the fly.
    /// </summary>
    /// <remarks>
    /// The transform layer is mirrored with the image and must not be dropped: transparent pixels are
    /// stored as transform <c>1</c>, so a sprite that kept only its image layer would paint those
    /// pixels as palette index zero, i.e. as black.
    /// </remarks>
    public Sprite Flip(bool horizontal, bool vertical)
    {
        if (!horizontal && !vertical)
        {
            return this;
        }

        return new Sprite(
            ImageOps.Flip(Image, horizontal, vertical),
            OffsetX,
            OffsetY,
            Transform is null ? null : FlipLayer(Transform, Width, Height, horizontal, vertical));
    }

    /// <summary>
    /// Returns the sprite transposed into its own bounds, <c>fheroes2::Transpose</c> of the engine:
    /// the result is as wide as this sprite is tall, and <c>out(x, y) = in(y, x)</c>.
    /// </summary>
    /// <remarks>
    /// The reference uses it only to grow the four monster switch arrows of a recruit dialog out of
    /// the up and down arrows of <c>ICN::RECRUIT</c> (<c>game_assets.cpp:3022-3042</c>). The transform
    /// layer is transposed with the image for the same reason as in <see cref="Flip"/>.
    /// </remarks>
    public Sprite Transpose()
    {
        PixelBuffer image = new(Height, Width);

        // The result is as wide as this sprite is tall, so a source row becomes a result column.
        for (int row = 0; row < Height; ++row)
        {
            for (int column = 0; column < Width; ++column)
            {
                image[row, column] = Image[column, row];
            }
        }

        return new Sprite(image, OffsetX, OffsetY, Transform is null ? null : TransposeLayer(Transform, Width, Height));
    }

    private static byte[] TransposeLayer(byte[] source, int width, int height)
    {
        byte[] result = new byte[source.Length];

        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                result[(x * height) + y] = source[(y * width) + x];
            }
        }

        return result;
    }

    private static byte[] FlipLayer(byte[] source, int width, int height, bool horizontal, bool vertical)
    {
        byte[] result = new byte[source.Length];

        for (int y = 0; y < height; ++y)
        {
            int sourceY = vertical ? height - 1 - y : y;

            for (int x = 0; x < width; ++x)
            {
                int sourceX = horizontal ? width - 1 - x : x;
                result[(y * width) + x] = source[(sourceY * width) + sourceX];
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the same image without a transform layer, i.e. <c>fheroes2::Image::_disableTransformLayer()</c>.
    /// The transform array is shared on purpose: callers only drop the layer once decoding is done.
    /// </summary>
    public Sprite WithoutTransformLayer() => Transform is null ? this : new(Image, OffsetX, OffsetY);
}
