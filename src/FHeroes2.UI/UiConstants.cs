namespace FHeroes2.UI;

/// <summary>
/// The shared pixel metrics of the interface, <c>gui/ui_constants.h:23-39</c>.
/// </summary>
/// <remarks>
/// <para>
/// These five numbers used to live as literals in every class that needed them (the border width in
/// <see cref="Interface.GameBorder"/> and <see cref="Dialogs.FrameBox"/>, the radar width in
/// <see cref="Interface.Radar"/>, the tile size in <see cref="Dialogs.LighthouseInfo"/> and so on). The reference keeps
/// them in one header precisely so that the layout tables of <c>ui_window.cpp</c> and <c>interface_*.cpp</c> cannot
/// drift apart; this type is that header.
/// </para>
/// <para>
/// The owning classes keep their own public names (<c>GameBorder.BorderWidth</c> etc.) as aliases, so that call sites
/// read in the vocabulary of their module; what changes is that the numbers now have a single source.
/// </para>
/// </remarks>
public static class UiConstants
{
    /// <summary>Width of the rendered window border, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int BorderWidthPx = 16;

    /// <summary>Width of the rendered radar, <c>fheroes2::radarWidthPx</c>.</summary>
    public const int RadarWidthPx = 144;

    /// <summary>Width of the rendered dialog shadow, <c>fheroes2::shadowWidthPx</c>.</summary>
    public const int ShadowWidthPx = 16;

    /// <summary>The "standard" width of a dialog box, <c>fheroes2::boxAreaWidthPx</c>.</summary>
    public const int BoxAreaWidthPx = 244;

    /// <summary>Width of a rendered map tile in pixels, <c>fheroes2::tileWidthPx</c>.</summary>
    public const int TileWidthPx = 32;
}
