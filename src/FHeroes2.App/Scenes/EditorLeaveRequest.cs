namespace FHeroes2.App.Scenes;

/// <summary>
/// How the editor asks to be left, the <c>fheroes2::GameMode</c> the reference's editor loop returns
/// (<c>editor_interface.cpp:1393-1401</c>, <c>:2121-2153</c>).
/// </summary>
/// <remarks>
/// 参考的 <c>EditorInterface::startEdit()</c> 是个返回 <c>GameMode</c> 的循环，所以「退出」「回主菜单」「新图」
/// 「载图」都是同一个返回值；端口让场景把这个值交给 <c>GameHost</c>，由它换场景或关客户端。
/// </remarks>
public enum EditorLeaveRequest
{
    /// <summary>Stay in the editor, the <c>GameMode::CANCEL</c> of the reference.</summary>
    None,

    /// <summary>Return to the game's main menu, <c>GameMode::MAIN_MENU</c>.</summary>
    MainMenu,

    /// <summary>Quit the application, <c>GameMode::QUIT_GAME</c>.</summary>
    Quit,

    /// <summary>
    /// Start a new map, <c>GameMode::EDITOR_NEW_MAP</c>: the reference goes back to the editor's main menu, whose
    /// new map entry runs (<c>editor_interface.cpp:2059-2067</c>).
    /// </summary>
    NewMap,

    /// <summary>
    /// Load another map, <c>GameMode::EDITOR_LOAD_MAP</c>: the editor's main menu picks the file
    /// (<c>editor_interface.cpp:2050-2057</c>).
    /// </summary>
    LoadMap,

    /// <summary>
    /// Leave the editor and play the map, <c>GameMode::NEW_STANDARD</c>
    /// (<c>editor_interface.cpp:2131-2142</c>).
    /// </summary>
    StartMap,
}
