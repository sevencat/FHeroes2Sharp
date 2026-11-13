namespace FHeroes2.UI;

/// <summary>
/// Entry point of the user interface layer: dialog stack, widgets and screen transitions.
///
/// <para>
/// The widgets live in <c>FHeroes2.UI.Widgets</c> (<c>Button</c> is the first one, used by the main
/// menu). The type below marks the layer boundary so that the dependency direction
/// (<c>FHeroes2.UI → FHeroes2.Game/Rendering/Core</c>) cannot be violated accidentally while the
/// engine is being ported.
/// </para>
/// </summary>
public static class UiLayer
{
    /// <summary>Name of the layer, used by the diagnostics overlay and the docs.</summary>
    public const string Name = "FHeroes2.UI";
}
