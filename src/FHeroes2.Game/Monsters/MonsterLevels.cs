using FHeroes2.Core.Random;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// <c>Monster::LevelType</c> of <c>src/fheroes2/monster/monster.h:44</c>: the bucket a creature is drawn from
/// when the map asks for "a random monster of level N".
/// </summary>
/// <remarks>
/// <see cref="Any"/> is zero and the four levels are one to four, because the reference indexes its bucket
/// array with <c>static_cast&lt;int&gt;( type ) - 1</c>. Only <c>LEVEL_ANY</c> may be zero, which is why
/// <see cref="MonsterLevels.GetRandomUnitLevel"/> never returns it for a real creature.
/// </remarks>
public enum MonsterLevelType
{
    /// <summary><c>LEVEL_ANY</c>: any real creature, the bucket is not used.</summary>
    Any = 0,

    /// <summary><c>LEVEL_1</c>.</summary>
    Level1 = 1,

    /// <summary><c>LEVEL_2</c>.</summary>
    Level2 = 2,

    /// <summary><c>LEVEL_3</c>.</summary>
    Level3 = 3,

    /// <summary><c>LEVEL_4</c>.</summary>
    Level4 = 4,
}

/// <summary>
/// The random monster drawing of <c>src/fheroes2/monster/monster.cpp:576-689</c>: which creature belongs to
/// which bucket and how a bucket is rolled.
/// </summary>
/// <remarks>
/// <para>
/// The buckets are not the creature's own level — a level six dragon sits in bucket four, a level one peasant
/// in bucket one — they are the tiers the editor's "random monster" object offers, and the reference lists
/// every member of each one by hand. Both tables are copied one for one.
/// </para>
/// <para>
/// <c>RANDOM_MONSTER</c> itself rolls a level every time it is asked, which is why the port keeps
/// <see cref="GetRandomUnitLevel"/> separate from the stats: the answer is not a property of the row.
/// </para>
/// </remarks>
public static class MonsterLevels
{
    /// <summary>The deepest bucket, <c>LEVEL_4</c>.</summary>
    private const MonsterLevelType DeepestLevel = MonsterLevelType.Level4;

    /// <summary>
    /// The bucket of a creature, <c>Monster::GetRandomUnitLevel()</c> (<c>monster.cpp:591-689</c>).
    /// </summary>
    /// <remarks>
    /// The five random markers answer with their own level, which is what keeps them out of the buckets
    /// below: <see cref="GetRandom"/> walks the real creatures only. Everything else — the unknown monster
    /// and a marker that has no level — answers <see cref="MonsterLevelType.Any"/>.
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <returns>The bucket, or <see cref="MonsterLevelType.Any"/> for a non-creature.</returns>
    public static MonsterLevelType GetRandomUnitLevel(MonsterType monster) => monster switch
    {
        MonsterType.Peasant or MonsterType.Archer or MonsterType.Goblin or MonsterType.Orc
            or MonsterType.Sprite or MonsterType.Centaur or MonsterType.Halfling or MonsterType.Skeleton
            or MonsterType.Zombie or MonsterType.Rogue or MonsterType.RandomMonsterLevel1 => MonsterLevelType.Level1,

        MonsterType.Ranger or MonsterType.Pikeman or MonsterType.VeteranPikeman or MonsterType.OrcChief
            or MonsterType.Wolf or MonsterType.Dwarf or MonsterType.BattleDwarf or MonsterType.Elf
            or MonsterType.GrandElf or MonsterType.Gargoyle or MonsterType.Boar or MonsterType.IronGolem
            or MonsterType.MutantZombie or MonsterType.Mummy or MonsterType.Nomad
            or MonsterType.RandomMonsterLevel2 => MonsterLevelType.Level2,

        MonsterType.Swordsman or MonsterType.MasterSwordsman or MonsterType.Cavalry or MonsterType.Champion
            or MonsterType.Ogre or MonsterType.OgreLord or MonsterType.Troll or MonsterType.WarTroll
            or MonsterType.Druid or MonsterType.GreaterDruid or MonsterType.Griffin or MonsterType.Minotaur
            or MonsterType.MinotaurKing or MonsterType.SteelGolem or MonsterType.Roc or MonsterType.Mage
            or MonsterType.Archmage or MonsterType.RoyalMummy or MonsterType.Vampire
            or MonsterType.VampireLord or MonsterType.Lich or MonsterType.Ghost or MonsterType.Medusa
            or MonsterType.EarthElement or MonsterType.AirElement or MonsterType.FireElement
            or MonsterType.WaterElement or MonsterType.RandomMonsterLevel3 => MonsterLevelType.Level3,

        MonsterType.Paladin or MonsterType.Crusader or MonsterType.Cyclops or MonsterType.Unicorn
            or MonsterType.Phoenix or MonsterType.Hydra or MonsterType.GreenDragon or MonsterType.RedDragon
            or MonsterType.BlackDragon or MonsterType.Giant or MonsterType.Titan or MonsterType.PowerLich
            or MonsterType.BoneDragon or MonsterType.Genie
            or MonsterType.RandomMonsterLevel4 => MonsterLevelType.Level4,

        // The generic marker rolls a bucket; the reference does the same inside the getter.
        MonsterType.RandomMonster => (MonsterLevelType)(1 + Rand.Get(0, 3)),

        _ => MonsterLevelType.Any,
    };

    /// <summary>
    /// A random creature of a bucket, <c>Monster::Rand( LevelType )</c> (<c>monster.cpp:576-589</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MonsterLevelType.Any"/> does not touch the buckets at all: it is a uniform draw over the
    /// real creatures, <c>Rand::Get( PEASANT, WATER_ELEMENT )</c>, which is every id the marker range does
    /// not cover.
    /// </para>
    /// <para>
    /// The buckets are built once, in creature-id order, exactly like the reference's static vector, so the
    /// sequence of rolls is the same as the reference's for a given seed.
    /// </para>
    /// </remarks>
    /// <param name="level">The bucket, or <see cref="MonsterLevelType.Any"/>.</param>
    /// <returns>The creature drawn.</returns>
    public static MonsterType GetRandom(MonsterLevelType level)
    {
        if (level == MonsterLevelType.Any)
        {
            return (MonsterType)Rand.Get((int)MonsterType.Peasant, (int)MonsterType.WaterElement);
        }

        if (level < MonsterLevelType.Level1 || level > DeepestLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Not a monster level.");
        }

        MonsterType[] bucket = Buckets.Value[(int)level - 1];

        return bucket[Rand.Get(0, bucket.Length - 1)];
    }

    /// <summary>The four buckets, built on first use like the reference's static vectors.</summary>
    private static readonly Lazy<MonsterType[][]> Buckets = new(BuildBuckets);

    private static MonsterType[][] BuildBuckets()
    {
        List<MonsterType>[] buckets = [[], [], [], []];

        for (int id = (int)MonsterType.Peasant; id <= (int)MonsterType.WaterElement; ++id)
        {
            var monster = (MonsterType)id;
            MonsterLevelType level = GetRandomUnitLevel(monster);

            if (level > MonsterLevelType.Any && level <= DeepestLevel)
            {
                buckets[(int)level - 1].Add(monster);
            }
        }

        return [.. buckets.Select(bucket => bucket.ToArray())];
    }
}
