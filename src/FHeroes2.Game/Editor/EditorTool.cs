namespace FHeroes2.Game.Editor;

/// <summary>
/// What the left button of the editor does, the instrument of the reference's
/// <c>Editor::Interface</c> panel.
/// </summary>
///
/// <remarks>
/// The reference groups its instruments into a panel of buttons; this port keeps the three that the
/// map level needs and selects them with keys, because the panel artwork is not ported yet. The
/// terrain tool is the default so that the number keys keep working the way they did before objects
/// existed.
/// </remarks>
public enum EditorTool
{
    /// <summary>The ground brush: a click replaces the terrain of the tile, <c>1</c> to <c>9</c>.</summary>
    Terrain = 0,

    /// <summary>Places the selected object, <c>O</c> cycles the objects.</summary>
    Object = 1,

    /// <summary>Clears the object of a tile and leaves its ground alone, <c>X</c>.</summary>
    Erase = 2,
}
