using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Cursor;

/// <summary>
/// Which cursor the game shows and where its hotspot sits, the port of <c>Cursor</c>
/// (<c>src/fheroes2/gui/cursor.h:37-221</c>, <c>cursor.cpp:31-237</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps three pieces of state: the current theme, the offset between the mouse position and the top
/// left corner of the cursor image, and whether the monochrome themes are enabled in the settings. Everything else —
/// which image set a theme belongs to, the sprite index, the per theme offsets, the distance variants — is pure
/// geometry and lives in <see cref="CursorLayout"/>; resolving the sprite lives in <see cref="CursorImages"/>.
/// </para>
/// <para>
/// <see cref="Current"/> is the composition root's single instance, the counterpart of <c>Cursor::Get()</c>. It is
/// <see langword="null"/> until the host creates it: the platform pointer (<see cref="ICursorRenderer"/>) has no
/// implementation in the port yet.
/// </para>
/// <para>
/// 偏差：<c>SetThemes</c> 在取不到图时只记主题、不换图。参考的 <c>Assets::getImage</c> 保证有图（缺图返回 errorImage
/// 占位），端口没有占位图，且距离光标的两套图还没合成（见 <see cref="CursorImages"/>）；因此此时的 <see cref="Offset"/>、
/// 指针位置与「是否夹在屏幕内」都维持上一次的值。参考从 <c>LocalEvent::Get()</c> 读鼠标位置，端口从
/// <see cref="ICursorRenderer.Position"/> 读，免得模型依赖输入状态。
/// </para>
/// </remarks>
public sealed class CursorState
{
    private readonly ICursorRenderer _renderer;

    private CursorTheme _theme = CursorTheme.None;
    private Point _offset;
    private bool _monochromeThemes;

    /// <summary>Creates a cursor state that drives <paramref name="renderer"/>.</summary>
    /// <param name="renderer">The platform pointer, <c>fheroes2::cursor()</c>.</param>
    public CursorState(ICursorRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        _renderer = renderer;
    }

    /// <summary>The instance the whole game shares, <c>Cursor::Get()</c>. Set by the composition root.</summary>
    public static CursorState? Current { get; set; }

    /// <summary>The theme currently shown, <c>Themes()</c>.</summary>
    public CursorTheme Theme => _theme;

    /// <summary>Whether the pointer is drawn, <c>fheroes2::cursor().isVisible()</c>.</summary>
    public bool IsVisible => _renderer.IsVisible;

    /// <summary>Shows or hides the pointer, <c>fheroes2::cursor().show()</c>.</summary>
    /// <param name="visible">Whether the pointer should be drawn.</param>
    public void Show(bool visible) => _renderer.Show(visible);

    /// <summary>
    /// The offset between the mouse position and the top left corner of the cursor image, <c>_offset</c>.
    /// </summary>
    public Point Offset => _offset;

    /// <summary>
    /// Whether the monochrome cursor sets are used, <c>setMonochromeCursor()</c>. The reference says to set this from
    /// the settings screen only: the current theme is not re-rendered by the setter.
    /// </summary>
    public bool MonochromeThemes
    {
        get => _monochromeThemes;
        set => _monochromeThemes = value;
    }

    /// <summary>
    /// Switches the cursor to <paramref name="theme"/>, <c>Cursor::SetThemes()</c> (<c>cursor.cpp:37-71</c>).
    /// </summary>
    /// <param name="theme">The theme to show.</param>
    /// <param name="force">Whether to re-render even when the theme is already the current one.</param>
    public void SetThemes(CursorTheme theme, bool force = false)
    {
        if (_theme == theme && !force)
        {
            return;
        }

        _theme = theme;

        if (CursorImages.GetFrame(CursorLayout.ImageSet(theme, _monochromeThemes), CursorLayout.ImageIndex(theme))
            is not { } sprite)
        {
            // The reference cannot get here: its asset layer always answers with an image. See the class remarks.
            return;
        }

        _offset = CursorLayout.Offset(theme, CursorLayout.AnchorOffset(sprite));

        _renderer.Update(sprite, -_offset.X, -_offset.Y);

        // Force the scroll cursors to stay in the screen boundaries.
        _renderer.KeepInScreenArea(theme is >= CursorTheme.ScrollTop and <= CursorTheme.ScrollTopLeft);

        // Apply the new offset to the current pointer position.
        Move(_renderer.Position.X, _renderer.Position.Y);
    }

    /// <summary>
    /// Shows an arbitrary image as the pointer, <c>Cursor::setCustomImage()</c> (<c>cursor.cpp:73-86</c>).
    /// </summary>
    /// <param name="image">The image to draw, normally with its anchor at the hotspot.</param>
    /// <param name="offset">Offset between the mouse position and the top left corner of the image.</param>
    public void SetCustomImage(Sprite image, Point offset)
    {
        ArgumentNullException.ThrowIfNull(image);

        _theme = CursorTheme.None;

        _renderer.Update(image, -offset.X, -offset.Y);
        _renderer.KeepInScreenArea(false);

        // Immediately apply the new mouse offset.
        _offset = offset;

        Move(_renderer.Position.X, _renderer.Position.Y);
    }

    /// <summary>
    /// Moves a software emulated pointer to the mouse position, <c>Cursor::updateCursorPosition()</c>
    /// (<c>cursor.cpp:88-98</c>).
    /// </summary>
    /// <param name="x">Mouse position, x.</param>
    /// <param name="y">Mouse position, y.</param>
    /// <returns>
    /// The area that has to be re-rendered, which is the single pixel under the pointer while it is visible, or an
    /// empty rectangle when the host draws the pointer itself.
    /// </returns>
    /// <remarks>
    /// 参考是静态函数、从全局取光标；端口是实例方法，因为光标状态本来就是实例。返回值语义照抄：只有软件光标且当前可见
    /// 时才是 1x1，其余情况为空矩形。
    /// </remarks>
    public Rect UpdateCursorPosition(int x, int y)
    {
        if (!_renderer.IsSoftwareEmulation)
        {
            return Rect.Empty;
        }

        Move(x, y);

        return _renderer.IsVisible ? new Rect(x, y, 1, 1) : Rect.Empty;
    }

    /// <summary>
    /// Switches to the pointer used during video playback, <c>Cursor::setVideoPlaybackCursor()</c>
    /// (<c>cursor.cpp:174-179</c>). Only meaningful for a software emulated pointer.
    /// </summary>
    public void SetVideoPlaybackCursor()
    {
        if (_renderer.IsSoftwareEmulation)
        {
            SetThemes(CursorTheme.PointerVideo);
        }
    }

    /// <summary>
    /// Re-renders the current theme, <c>Cursor::Refresh()</c> (<c>cursor.cpp:181-184</c>). Used when the sprite the
    /// cursor was built from has been replaced.
    /// </summary>
    public void Refresh() => SetThemes(_theme, true);

    private void Move(int x, int y) => _renderer.SetPosition(x + _offset.X, y + _offset.Y);
}
