namespace FHeroes2.Client.Desktop;

/// <summary>The screen the client starts with.</summary>
public enum SceneKind
{
    /// <summary>The M2 main menu (<c>HEROES.ICN</c> with the <c>BTNSHNGL.ICN</c> signs).</summary>
    Menu = 0,

    /// <summary>The M0 diagnostics screen: palette strip, moving marker and font samples.</summary>
    Diagnostics = 1,
}
