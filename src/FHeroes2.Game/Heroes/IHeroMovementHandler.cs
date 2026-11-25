namespace FHeroes2.Game.Heroes;

/// <summary>
/// The four things <c>heroes_move.cpp</c> calls after the hero arrives on a tile, handed to the hero's handler
/// instead of being linked in.
/// </summary>
/// <remarks>
/// <para>
/// <c>_moveStep()</c> interleaves three jobs: advance the sprite, move the hero between tiles, and tell the rest
/// of the game that he arrived. The first two are pure bookkeeping and are ported; the third is <c>Scout()</c>
/// (fog), <c>ScoutRadar()</c> (minimap), <c>Action()</c> (the object under the hero) and
/// <c>ActionNewPosition()</c> (monster guards, artifact pickup, AI planner). Which of them does real work depends
/// on who drives the hero: <see cref="FHeroes2.Game.Ai.AiHeroMovementHandler"/> does all four for a computer
/// player, <see cref="HumanHeroMovementHandler"/> does them for a human one (the questions go through
/// <see cref="World.HeroActionHost"/>), and <see cref="FogHeroMovementHandler"/> is the fog-only stand-in a test
/// can install when it wants a hero to walk without any of the other three.
/// </para>
/// <para>
/// The default handler does nothing, which keeps the movement state machine honest and testable: a test can walk
/// a hero across the map and assert the tile, the path and the movement points without a fog layer or an opponent.
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
    /// <param name="allowMonsterAttack">
    /// Whether the guards of the tile may pull the hero into a fight. The reference passes
    /// <see langword="false"/> for the three teleports (dimension door, both teleporters), because a hero who
    /// materialises next to a wandering monster did not walk into it (<c>heroes.cpp:2021</c>).
    /// </param>
    void ActionNewPosition(Hero hero, bool allowMonsterAttack);

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
    public void ActionNewPosition(Hero hero, bool allowMonsterAttack)
    {
    }

    /// <inheritdoc />
    public void ScoutRadar(Hero hero)
    {
    }
}

/// <summary>
/// The handler a hero gets once the world has fog: walking uncovers it, <c>Heroes::Scout()</c>
/// (<c>heroes.cpp:1749</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这是「只开图」的处理器：另外三件事都留空。装了界面宿主的人类英雄装的是
/// <see cref="HumanHeroMovementHandler"/>（四件事齐备，要问玩家的点交给 <see cref="World.HeroActionHost"/>），
/// 电脑英雄装的是 <see cref="FHeroes2.Game.Ai.AiHeroMovementHandler"/>。没有宿主的运行（命令行、测试）里人类
/// 英雄仍退回这一个：没有玩家在点，自动触发对象既没画面也没提示。
/// </para>
/// <para>
/// 与 <see cref="NullHeroMovementHandler"/> 不同，它<b>持有世界</b>：迷雾是世界的状态，
/// <see cref="Hero.Scout"/> 需要它才能改。
/// </para>
/// </remarks>
/// <param name="world">The world whose fog the hero uncovers.</param>
public sealed class FogHeroMovementHandler(World world) : IHeroMovementHandler
{
    /// <summary>
    /// Uncovers the fog around a tile for the hero's color, <c>Heroes::Scout( int32_t )</c>.
    /// </summary>
    /// <param name="hero">The hero who scouts.</param>
    /// <param name="tileIndex">Tile whose surroundings get revealed.</param>
    public void Scout(Hero hero, int tileIndex) => hero.Scout(world, tileIndex);

    /// <inheritdoc />
    public void Action(Hero hero, int tileIndex)
    {
    }

    /// <inheritdoc />
    public void ActionNewPosition(Hero hero, bool allowMonsterAttack)
    {
    }

    /// <inheritdoc />
    public void ScoutRadar(Hero hero)
    {
    }
}
