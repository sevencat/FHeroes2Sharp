namespace FHeroes2.Game.Editor;

/// <summary>
/// The three modes of the detail instrument, <c>DetailBrushType</c> of
/// <c>editor/editor_interface_panel.h:269-278</c>.
/// </summary>
/// <remarks>
/// 三个模式的数字是参考的：面板上的三个按钮按数字排，右手边的图标与它选中的模式不能各自重排。
/// </remarks>
public enum EditorDetailMode
{
    /// <summary>Left click opens the properties of the object under the pointer.</summary>
    Editing = 0,

    /// <summary>Left drag takes the object of one tile and drops it on another.</summary>
    Moving = 1,

    /// <summary>Left drag leaves the object where it is and places a copy of it.</summary>
    Copying = 2,
}

/// <summary>
/// The names and the descriptions of <see cref="EditorDetailMode"/>, the labels the reference shows in
/// the tooltips of the three buttons of the detail page (<c>editor_interface_panel.cpp:868-877</c> and
/// <c>1272-1280</c>).
/// </summary>
public static class EditorDetailModes
{
    private static readonly EditorDetailMode[] Modes =
    [
        EditorDetailMode.Editing,
        EditorDetailMode.Moving,
        EditorDetailMode.Copying,
    ];

    /// <summary>The three modes, in the order of the artwork of the page.</summary>
    public static IReadOnlyList<EditorDetailMode> All => Modes;

    /// <summary>
    /// The short name of a mode, the title of the message the reference shows on a right click of its
    /// button (<c>editorDetailMode|Edit</c> and the two next to it).
    /// </summary>
    /// <param name="mode">The mode to name.</param>
    /// <returns>The name of the mode, or <c>"Unknown detail mode"</c> for a value that is not one.</returns>
    public static string Name(EditorDetailMode mode) => mode switch
    {
        EditorDetailMode.Editing => "Edit",
        EditorDetailMode.Moving => "Move",
        EditorDetailMode.Copying => "Copy",
        _ => "Unknown detail mode",
    };

    /// <summary>The description the reference shows under the name of a mode.</summary>
    /// <param name="mode">The mode to describe.</param>
    /// <returns>The description, empty for a value that is not a mode.</returns>
    public static string Description(EditorDetailMode mode) => mode switch
    {
        EditorDetailMode.Editing => "Edit objects on the Adventure Map.",
        EditorDetailMode.Moving => "Move objects on the Adventure Map.",
        EditorDetailMode.Copying => "Copy objects on the Adventure Map.",
        _ => string.Empty,
    };

    /// <summary>Position of <paramref name="mode"/> in <see cref="All"/>, or <c>-1</c>.</summary>
    /// <param name="mode">The mode to look up.</param>
    /// <returns>Index of the mode, <c>-1</c> when it is not one.</returns>
    public static int IndexOf(EditorDetailMode mode) => Array.IndexOf(Modes, mode);
}
