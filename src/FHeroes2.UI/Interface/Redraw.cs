namespace FHeroes2.UI.Interface;

/// <summary>
/// Bit mask of the interface parts that have to be repainted, <c>Interface::Redraw</c>
/// (<c>src/fheroes2/gui/interface_base.h:33-59</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference uses an unnamed <c>enum : uint32_t</c> and ORs raw <c>uint32_t</c> values through
/// <c>setRedraw()</c>; <see cref="FlagsAttribute"/> keeps the same bit arithmetic with a readable name in the
/// debugger.
/// </para>
/// <para>
/// <see cref="RadarCursor"/> is the cheap half of <see cref="Radar"/>: only the cursor rectangle is blitted over
/// the already generated radar image. <see cref="Icons"/> is an alias of <see cref="Heroes"/> |
/// <see cref="Castles"/>, and <see cref="Passabilities"/> only exists for the map editor.
/// </para>
/// </remarks>
[Flags]
public enum Redraw : uint
{
    /// <summary>Nothing to repaint, <c>0</c>.</summary>
    None = 0,

    /// <summary>Paint the cursor over the previously generated radar image, <c>REDRAW_RADAR_CURSOR</c>.</summary>
    RadarCursor = 0x01,

    /// <summary>Generate the radar image, fully or in the queued ROI, then paint the cursor, <c>REDRAW_RADAR</c>.</summary>
    Radar = 0x02,

    /// <summary>Repaint the stone border of the adventure screen, <c>REDRAW_BORDER</c>.</summary>
    Border = 0x04,

    /// <summary>Repaint the map viewport, <c>REDRAW_GAMEAREA</c>.</summary>
    GameArea = 0x08,

    /// <summary>Repaint the status panel, <c>REDRAW_STATUS</c>.</summary>
    Status = 0x10,

    /// <summary>Repaint the hero part of the icons panel, <c>REDRAW_HEROES</c>.</summary>
    Heroes = 0x20,

    /// <summary>Repaint the castle part of the icons panel, <c>REDRAW_CASTLES</c>.</summary>
    Castles = 0x40,

    /// <summary>Repaint the buttons panel, <c>REDRAW_BUTTONS</c>.</summary>
    Buttons = 0x80,

    /// <summary>Both halves of the icons panel, <c>REDRAW_ICONS</c>.</summary>
    Icons = Heroes | Castles,

    /// <summary>The editor tool panel, <c>REDRAW_PANEL</c>.</summary>
    Panel = 0x100,

    /// <summary>Everything of the game interface, <c>REDRAW_ALL</c>.</summary>
    All = 0x1FF,

    /// <summary>Passability overlay of the map editor, <c>REDRAW_PASSABILITIES</c>.</summary>
    Passabilities = 0x200,
}
