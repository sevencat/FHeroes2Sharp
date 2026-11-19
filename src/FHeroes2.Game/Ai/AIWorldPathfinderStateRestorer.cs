namespace FHeroes2.Game.Ai;

/// <summary>
/// Puts the two settings of <see cref="AIWorldPathfinder"/> back the way they were, the RAII guard of
/// <c>AI::AIWorldPathfinderStateRestorer</c> (<c>ai_planner_internals.h:32-56</c>).
/// </summary>
/// <remarks>
/// <para>
/// The hero planner tunes the pathfinder for one decision - how much stronger than a guard an army has to be, and how
/// much of its spell points it may spend - and the tuning must not leak into the next decision. The reference makes
/// the scope a C++ destructor; the port makes it <see cref="IDisposable"/>, so the same call sites become
/// <c>using</c> blocks.
/// </para>
/// <para>
/// <b>Already final.</b> Unlike the rest of the AI declarations (ADR-180) this class is complete, because its whole
/// body is the capture and the restore of two numbers: there is nothing left to decide later, and a guard that threw
/// would make every future call site untestable.
/// </para>
/// </remarks>
public sealed class AIWorldPathfinderStateRestorer : IDisposable
{
    private readonly AIWorldPathfinder _pathfinder;
    private readonly double _minimalArmyStrengthAdvantage;
    private readonly double _spellPointsReserveRatio;

    private bool _restored;

    /// <summary>
    /// Remembers the current settings of <paramref name="pathfinder"/>.
    /// </summary>
    /// <param name="pathfinder">The pathfinder whose settings are about to be changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pathfinder"/> is <see langword="null"/>.</exception>
    public AIWorldPathfinderStateRestorer(AIWorldPathfinder pathfinder)
    {
        ArgumentNullException.ThrowIfNull(pathfinder);

        _pathfinder = pathfinder;
        _minimalArmyStrengthAdvantage = pathfinder.MinimalArmyStrengthAdvantage;
        _spellPointsReserveRatio = pathfinder.SpellPointsReserveRatio;
    }

    /// <summary>
    /// Puts both settings back, the destructor of the reference.
    /// </summary>
    /// <remarks>
    /// Disposing twice leaves the settings alone the second time: the reference restores in its destructor, which
    /// runs once, and a <c>using</c> block plus an explicit call would otherwise capture-print the numbers twice.
    /// </remarks>
    public void Dispose()
    {
        if (_restored)
        {
            return;
        }

        _restored = true;

        _pathfinder.SetMinimalArmyStrengthAdvantage(_minimalArmyStrengthAdvantage);
        _pathfinder.SetSpellPointsReserveRatio(_spellPointsReserveRatio);
    }
}
