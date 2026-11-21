using FHeroes2.Core.Geometry;

namespace FHeroes2.Core.Imaging;

/// <summary>
/// Saves a rectangle of an image and puts it back later, a port of <c>fheroes2::ImageRestorer</c>
/// (<c>src/engine/image.h:184-231</c>, <c>src/engine/image.cpp:665-773</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps a copy of the covered pixels and writes them back in <see cref="Restore"/> - and in
/// the destructor, unless <see cref="Restore"/> already ran. Dialogs use it to draw a window inside a
/// window and then pretend it was never there: <c>Dialog::FrameBorder</c> owns one bound to the whole
/// display (<c>dialog_frameborder.cpp:34-63</c>), and <c>Interface::BorderWindow</c> hands it the frame
/// rectangle.
/// </para>
/// <para>
/// <b>The rectangle is clipped, not validated.</b> A negative origin moves the rectangle inwards and
/// shrinks it; a rectangle that reaches past the far edge is trimmed; one that lies fully outside
/// collapses to an empty rectangle at the origin. The reference does this in <c>_updateRoi()</c> before
/// every copy, so a restorer that was never given a valid rectangle simply copies nothing.
/// </para>
/// <para>
/// The port has no transform layer inside <see cref="PixelBuffer"/>, so the reference's
/// «make a single-layer copy when the image is single-layer» step has no counterpart: the copy is always
/// single-layer here.
/// </para>
/// </remarks>
public sealed class ImageRestorer : IDisposable
{
    private readonly PixelBuffer _image;
    private PixelBuffer _copy;
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private bool _isRestored;

    /// <summary>
    /// Saves the whole image, <c>ImageRestorer( Image &amp; )</c>.
    /// </summary>
    /// <param name="image">Image to remember.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    public ImageRestorer(PixelBuffer image)
        : this(image, 0, 0, image?.Width ?? 0, image?.Height ?? 0)
    {
    }

    /// <summary>
    /// Saves a rectangle of the image, <c>ImageRestorer( Image &amp;, x, y, width, height )</c>.
    /// </summary>
    /// <param name="image">Image to remember.</param>
    /// <param name="x">Left edge of the rectangle, clipped to the image.</param>
    /// <param name="y">Top edge of the rectangle, clipped to the image.</param>
    /// <param name="width">Width of the rectangle, clipped to the image.</param>
    /// <param name="height">Height of the rectangle, clipped to the image.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    public ImageRestorer(PixelBuffer image, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);

        _image = image;
        _x = x;
        _y = y;
        _width = width;
        _height = height;

        UpdateRoi();

        _copy = new PixelBuffer(_width, _height);
        Save();
    }

    /// <summary>Left edge of the remembered rectangle.</summary>
    public int X => _x;

    /// <summary>Top edge of the remembered rectangle.</summary>
    public int Y => _y;

    /// <summary>Width of the remembered rectangle.</summary>
    public int Width => _width;

    /// <summary>Height of the remembered rectangle.</summary>
    public int Height => _height;

    /// <summary>The remembered rectangle, <c>rect()</c>.</summary>
    public Rect Rect => new(_x, _y, _width, _height);

    /// <summary>
    /// Moves the saved rectangle and copies the new pixels, <c>update()</c>.
    /// </summary>
    /// <remarks>
    /// The old pixels stay forgotten: the saved copy is overwritten, so the pixels covered before the call
    /// are never restored. That is what the reference does, and <c>FrameBorder::SetPosition</c> relies on
    /// it - it restores first, then updates.
    /// </remarks>
    public void Update(int x, int y, int width, int height)
    {
        _isRestored = false;

        _x = x;
        _y = y;
        _width = width;
        _height = height;

        UpdateRoi();

        _copy = new PixelBuffer(_width, _height);
        Save();
    }

    /// <summary>
    /// Writes the saved pixels back, <c>restore()</c>.
    /// </summary>
    /// <remarks>
    /// Calling it twice is harmless - the same pixels are copied again - but the destructor will not call
    /// it a third time.
    /// </remarks>
    public void Restore()
    {
        _isRestored = true;

        if (_width <= 0 || _height <= 0)
        {
            return;
        }

        ImageOps.Blit(_image, new Point(_x, _y), _copy, new Rect(0, 0, _width, _height));
    }

    /// <summary>Restores the pixels if nothing did it yet, the destructor of the reference.</summary>
    public void Dispose()
    {
        if (!_isRestored)
        {
            Restore();
        }
    }

    private void Save()
    {
        if (_width <= 0 || _height <= 0)
        {
            return;
        }

        ImageOps.Blit(_copy, new Point(0, 0), _image, new Rect(_x, _y, _width, _height));
    }

    /// <summary>
    /// Clips the rectangle to the image, <c>_updateRoi()</c> (<c>image.cpp:720-773</c>).
    /// </summary>
    private void UpdateRoi()
    {
        if (_width < 0)
        {
            _width = 0;
        }

        if (_height < 0)
        {
            _height = 0;
        }

        if (_x < 0)
        {
            int offset = -_x;
            _x = 0;
            _width = _width < offset ? 0 : _width - offset;
        }

        if (_y < 0)
        {
            int offset = -_y;
            _y = 0;
            _height = _height < offset ? 0 : _height - offset;
        }

        if ((_x >= _image.Width) || (_y >= _image.Height))
        {
            _x = 0;
            _y = 0;
            _width = 0;
            _height = 0;

            return;
        }

        if (_x + _width > _image.Width)
        {
            int offsetX = _x + _width - _image.Width;

            if (offsetX >= _width)
            {
                _x = 0;
                _y = 0;
                _width = 0;
                _height = 0;

                return;
            }

            _width -= offsetX;
        }

        if (_y + _height > _image.Height)
        {
            int offsetY = _y + _height - _image.Height;

            if (offsetY >= _height)
            {
                _x = 0;
                _y = 0;
                _width = 0;
                _height = 0;

                return;
            }

            _height -= offsetY;
        }
    }
}
