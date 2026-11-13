namespace FHeroes2.Game;

/// <summary>
/// Entry point of the game layer: owns world state, turn processing, AI and save games.
///
/// <para>
/// Milestone M0 only contains the engine subset (Core, Assets, Rendering); this type exists so
/// the assembly and the dependency graph (<c>Core ← Assets ← Rendering ← UI ← Game</c>) are
/// validated from day one. Gameplay systems are added from M3 on.
/// </para>
/// </summary>
public static class GameLayer
{
    /// <summary>Name of the layer, used by the diagnostics overlay and the docs.</summary>
    public const string Name = "FHeroes2.Game";
}
