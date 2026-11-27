using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The object popup of the map editor on screen, the two calls of
/// <c>EditorInterface::mouseCursorAreaPressRight()</c> of the reference
/// (<c>src/fheroes2/editor/editor_interface.cpp:3080-3083</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference presses a tile with the right button and calls <c>Editor::showPopupWindow()</c> for the text,
/// then <c>Interface::displayStandardPopupWindow()</c> for the frame
/// (<c>src/fheroes2/gui/ui_map_interface.cpp:56-81</c>). Only the second call is a window: it blits the
/// <c>QWIKINFO.ICN</c> frame next to the cursor, draws the text into it and blocks until the button is released.
/// The port has no blocking loop, so <see cref="StandardPopupWindow"/> keeps the layout and the drawing, the
/// caller keeps the lifetime, and this class glues the text of <see cref="EditorObjectPopup"/> to it.
/// </para>
/// <para>
/// <b>Not ported</b>: <c>CursorRestorer</c> and <c>ImageRestorer</c>. They save and restore the pixels under the
/// window because the reference only repaints that rectangle; the port redraws the whole scene every frame.
/// </para>
/// </remarks>
public static class EditorObjectPopupView
{
    /// <summary>
    /// Builds the popup of a tile, <c>Editor::showPopupWindow()</c> followed by
    /// <c>displayStandardPopupWindow()</c>.
    /// </summary>
    /// <param name="map">Map to read the tile from.</param>
    /// <param name="tile">Tile the right button pressed, in map tiles.</param>
    /// <param name="metadata">The per-object metadata tables of the map.</param>
    /// <param name="frame">The <c>QWIKINFO.ICN</c> frame.</param>
    /// <param name="font">Font of the text, <c>FontType::smallWhite()</c> in the reference.</param>
    /// <param name="cursor">Pointer in interface pixels, i.e. the same coordinates as <paramref name="interfaceArea"/>.</param>
    /// <param name="interfaceArea">Area the popup has to stay inside, <see cref="InterfaceAreaOf"/>.</param>
    /// <returns>The popup, or <see langword="null"/> when the tile is outside the map or the frame cannot fit.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="map"/>, <paramref name="metadata"/>, <paramref name="frame"/>, <paramref name="font"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static StandardPopupWindow? TryCreate(
        Mp2Map map,
        Point tile,
        MapObjectMetadataStore metadata,
        Sprite frame,
        IGlyphSource font,
        Point cursor,
        Rect interfaceArea)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(metadata);

        // 参考的 showPopupWindow() 只认地图内的格号，别的格一概不弹；端口在同一处拦下，省得
        // Mp2Map.Tile() 抛越界。
        if (tile.X < 0 || tile.X >= map.Width || tile.Y < 0 || tile.Y >= map.Height)
        {
            return null;
        }

        return StandardPopupWindow.TryCreate(
            frame, font, EditorObjectPopup.Describe(map, tile, metadata), cursor, interfaceArea);
    }

    /// <summary>
    /// The area the popup is clamped to, <c>getGameArea().GetROI()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// 参考把鼠标的绝对坐标交给 <c>getPopupWindowPosition()</c>（<c>ui_map_interface.cpp:39-54</c>），只用界面区的
    /// <b>宽高</b>夹边，所以那个区从屏幕原点算起：地图区从 16 像素的边框内开始，宽高要算到边框的外侧。
    /// 端口的视口已经是边框内的矩形（左上角在 (16,16)），于是把它的右/下缘当宽高，得到一个从原点起、
    /// 与视口同右下角的矩形。直接传视口会多减一次边框，弹窗会往左上偏 16 像素。
    /// </remarks>
    /// <param name="viewport">Rectangle of the map, border excluded.</param>
    /// <returns>Area of the map interface, from the display origin.</returns>
    public static Rect InterfaceAreaOf(Rect viewport) => new(0, 0, viewport.Right, viewport.Bottom);
}
