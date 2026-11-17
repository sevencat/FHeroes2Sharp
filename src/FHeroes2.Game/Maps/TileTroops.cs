using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The monster stack standing on a tile, the port of <c>Maps::getTroopFromTile()</c> (<c>maps_tiles_helper.cpp:763</c>),
/// <c>Maps::updateMonstersOnTile()</c> (<c>:1697</c>) and <c>Maps::setMonsterOnTile()</c> (<c>:1727</c>).
/// </summary>
/// <remarks>
/// <para>
/// The three functions belong together because they all answer "what is guarding this tile, and how does it
/// grow": a tile either holds a wandering stack in its metadata, or it is a captured object — a mine, a
/// dwelling, a dragon city — whose stack lives in the <c>CapturedObjects</c> table of the world.
/// </para>
/// <para>
/// The reference takes a <c>const Tile &amp;</c>; here the functions are instance methods, which is the shape
/// every other tile helper of the port has.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>
    /// The stack on the tile, <c>Maps::getTroopFromTile()</c> (<c>maps_tiles_helper.cpp:763</c>).
    /// </summary>
    /// <remarks>
    /// A captured object answers with the guards stored in the world's table — that is where a mine's goblins
    /// or a dragon city's dragons are, while <see cref="GetMonster"/> and <see cref="GetMonsterCount"/> would
    /// answer the neutral monster of a wandering stack only. Everything else builds a stack from the two
    /// metadata lanes.
    /// </remarks>
    /// <param name="world">World that holds the captured objects.</param>
    /// <returns>The stack of the tile; an empty one when the tile holds no guards.</returns>
    public Troop GetTroop(World world) =>
        Mp2ObjectTypes.IsCaptureObject(MainObjectType)
            ? world.GetCapturedObject(Index).Guardians
            : new Troop(GetMonster(world), GetMonsterCount());

    /// <summary>
    /// Grows a wandering stack, <c>Maps::updateMonstersOnTile()</c> (<c>maps_tiles_helper.cpp:1697</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The weekly growth of a wandering stack is a seventh of its size plus one: the remainder of the division
    /// is the chance of that extra creature (<c>Rand::Get( 1, 7 ) &lt;= count % 7</c>), which is what makes a
    /// stack of seven grow by one every time and a stack of six grow by one in six weeks out of seven.
    /// </para>
    /// <para>
    /// The growth is capped at <paramref name="monsterLimit"/>, and nothing happens for a tile that is not a
    /// wandering monster, for a stack of zero creatures (a broken save) and for a stack that already reached
    /// the limit — all three are early returns in the reference as well, not assertions.
    /// </para>
    /// </remarks>
    /// <param name="monsterLimit">The largest stack the map allows a wandering monster to grow to.</param>
    public void UpdateMonsters(uint monsterLimit)
    {
        if (Mp2ObjectTypes.NonActionType(MainObjectType) != Mp2ObjectType.Monster)
        {
            return;
        }

        uint beforeTroopCount = GetMonsterCount();

        if (beforeTroopCount == 0 || beforeTroopCount >= monsterLimit)
        {
            return;
        }

        uint bonusUnit = Rand.Get(1, 7) <= (beforeTroopCount % 7) ? 1u : 0u;
        uint troopGrowth = Math.Min((beforeTroopCount / 7) + bonusUnit, monsterLimit - beforeTroopCount);

        SetMonsterCount(beforeTroopCount + troopGrowth);
    }

    /// <summary>
    /// Places a wandering stack on the tile, <c>Maps::setMonsterOnTile()</c> (<c>maps_tiles_helper.cpp:1727</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tile becomes a monster object with a <c>MONS32</c> sprite, the sprite index being the monster id
    /// minus one. An existing non-monster sprite is pushed down to the ground level parts instead of being
    /// thrown away, and only a tile that already shows a monster keeps its UID — a new object needs a new one.
    /// </para>
    /// <para>
    /// <b>The join condition is decided here</b>. Ghosts and elementals never join a hero, so they are marked
    /// <c>SKIP</c>. A stack with a count the map designer wrote down is "hostile" and only joins for money —
    /// and so is every stack during the week of its own monster, which is the reference's way of making the
    /// weekly stacks cost something. Everything else joins for free with a twenty percent chance
    /// (<c>Rand.Get( 1, 10 ) &lt; 3</c>).
    /// </para>
    /// <para>
    /// A <paramref name="count"/> of zero means "roll one", not "an empty stack": the reference treats zero as
    /// the marker of a map that did not specify a count.
    /// </para>
    /// </remarks>
    /// <param name="world">World that hands out object UIDs and knows the week.</param>
    /// <param name="monster">The creature of the stack.</param>
    /// <param name="count">Number of creatures, or zero to roll <see cref="MonsterStatistics.GetRandomCount"/>.</param>
    public void SetMonster(World world, MonsterType monster, uint count)
    {
        // The reference stores `MP2::OBJ_MONSTER`, which carries the action bit
        // (maps_tiles_helper.cpp:1727). The port keeps the file's convention on the tile (ADR-075), so a wandering
        // stack is ActionMonster — the same value a map file gives it; Monster is only the stripped form the
        // classification predicates compare against.
        SetMainObjectType(Mp2ObjectType.ActionMonster);

        ObjectPart mainObjectPart = MainObjectPart;

        if (mainObjectPart.IcnType != Mp2ObjectIcnType.Mons32)
        {
            if (mainObjectPart.IcnType != Mp2ObjectIcnType.Unknown)
            {
                // If there is another object sprite here (a shadow for example) push it down to the add-ons.
                PushGroundObjectPart(mainObjectPart);
            }

            mainObjectPart = new ObjectPart
            {
                Uid = world.GetNewObjectUid(),
                IcnType = Mp2ObjectIcnType.Mons32,
                LayerType = ObjectLayerType.Object
            };
        }

        uint spriteIndex = MonsterSpriteIndex(monster);

        Debug.Assert(spriteIndex <= byte.MaxValue, "A monster sprite index has to fit into the byte of a part.");

        mainObjectPart.IcnIndex = (byte)spriteIndex;
        SetMainObjectPart(mainObjectPart);

        bool setDefinedCount = count > 0;

        SetMonsterCount(setDefinedCount ? count : MonsterStatistics.GetRandomCount(monster));

        if (monster == MonsterType.Ghost || MonsterAbilities.IsElemental(monster))
        {
            // Ghosts and elementals never join a hero's army.
            SetMonsterJoinCondition(world, MonsterJoinConditions.Skip);
        }
        else if (setDefinedCount ||
                 (world.WeekType.Type == WeekName.Monsters && world.WeekType.Monster == monster))
        {
            // A stack with a count from the map is hostile, and so is the stack of the week's monster.
            SetMonsterJoinCondition(world, MonsterJoinConditions.Money);
        }
        else
        {
            // Twenty percent chance to join for free.
            SetMonsterJoinCondition(
                world, Rand.Get(1, 10) < 3 ? MonsterJoinConditions.Free : MonsterJoinConditions.Money);
        }
    }

    /// <summary>
    /// The sprite of a monster inside <c>MONS32.ICN</c>, <c>Monster::GetSpriteIndex()</c>
    /// (<c>monster.h:301-304</c>).
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The id minus one, or zero for the unknown monster.</returns>
    public static uint MonsterSpriteIndex(MonsterType monster) =>
        monster > MonsterType.Unknown ? (uint)(monster - 1) : 0;
}
