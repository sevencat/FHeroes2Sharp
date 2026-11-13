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
    /// Returns the same image without a transform layer, i.e. <c>fheroes2::Image::_disableTransformLayer()</c>.
    /// The transform array is shared on purpose: callers only drop the layer once decoding is done.
    /// </summary>
    public Sprite WithoutTransformLayer() => Transform is null ? this : new(Image, OffsetX, OffsetY);
}
