namespace FHeroes2.Game.Heroes;

/// <summary>
/// The four things <c>heroes_move.cpp</c> calls that the port does not have yet, handed to the hero instead of
/// being linked in.
/// </summary>
/// <remarks>
/// <para>
/// <c>_moveStep()</c> interleaves three jobs: advance the sprite, move the hero between tiles, and tell the
/// rest of the game that he arrived. The first two are pure bookkeeping and are ported; the third is
/// <c>Scout()</c> (fog), <c>ScoutRadar()</c> (minimap), <c>Action()</c> (the object under the hero) and
/// <c>ActionNewPosition()</c> (monster guards, artifact pickup, AI planner), and every one of them needs a
/// subsystem that is still missing — <c>MapFog</c>, the minimap renderer, the object action table, the AI.
/// </para>
/// <para>
/// Rather than leave the calls out and have a hero who walks through the fog as if it were already clear, the
/// hero asks its handler. The default handler does nothing, which keeps the movement state machine honest and
/// testable: a test can walk a hero across the map and assert the tile, the path and the movement points
/// without a fog layer, and when the fog lands it is implemented in a handler rather than in the middle of
/// the animation code.
/// </para>
/// </remarks>
public interface IHeroMovementHandler
{
    /// <summary>
    /// Clears the fog around a tile, <c>Heroes::Scout( int32_t )</c> (<c>heroes.cpp:1749</c>).
    /// </summary>
    /// <param name="hero">The hero who scouts.</param>
    /// <param name="tileIndex">Tile whose surroundings get revealed.</param>
    void Scout(Hero hero, int tileIndex);

    /// <summary>
    /// Runs the action of the object under the hero, <c>Heroes::Action( int32_t )</c>.
    /// </summary>
    /// <param name="hero">The hero acting.</param>
    /// <param name="tileIndex">Tile of the object.</param>
    void Action(Hero hero, int tileIndex);

    /// <summary>
    /// Pays for the tile the hero just stepped on — monsters, buttons, pickups — then optionally centres the
    /// view on him, <c>Heroes::ActionNewPosition( bool )</c> (<c>heroes.cpp:2019</c>).
    /// </summary>
    /// <param name="hero">The hero who moved.</param>
    void ActionNewPosition(Hero hero);

    /// <summary>
    /// Redraws the minimap around the hero, <c>Heroes::ScoutRadar()</c>.
    /// </summary>
    /// <param name="hero">The hero who moved.</param>
    void ScoutRadar(Hero hero);
}

/// <summary>
/// The handler a hero starts with: does nothing. See <see cref="IHeroMovementHandler"/> for why a hero keeps
/// walking even though nobody clears the fog.
/// </summary>
public sealed class NullHeroMovementHandler : IHeroMovementHandler
{
    /// <summary>The shared instance; the handler is stateless.</summary>
    public static NullHeroMovementHandler Instance { get; } = new();

    /// <inheritdoc />
    public void Scout(Hero hero, int tileIndex)
    {
    }

    /// <inheritdoc />
    public void Action(Hero hero, int tileIndex)
    {
    }

    /// <inheritdoc />
    public void ActionNewPosition(Hero hero)
    {
    }

    /// <inheritdoc />
    public void ScoutRadar(Hero hero)
    {
    }
}
