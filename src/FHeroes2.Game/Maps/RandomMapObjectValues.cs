using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Maps;

/// <summary>
/// What an object is worth in gold, <c>objectGoldValue</c> and <c>getObjectGoldValue()</c>
/// (<c>map_random_generator_helper.cpp:79-302</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考用金币给所有宝物与能力点估价：基准是「一千五百金币约等于一千经验」、「一个基本属性点值两千金币」，
/// 宝物按属性点算（低级 1 点、次级 2 点、高级 4 点，再加稀有度溢价）。随机地图用它决定一个区域能放多少宝物、
/// 以及守卫该多强。
/// </para>
/// <para>
/// 表里只有会影响地图平衡的那一批物件；表外的物件估价为 <c>0</c>。参考在这里断言，端口改成一条警告：一个没列进表的
/// 装饰物不该把生成器带走。
/// </para>
/// </remarks>
public static class RandomMapObjectValues
{
    /// <summary>The floor of a treasure's value, <c>minimalTreasureValue</c>.</summary>
    public const int MinimumTreasureValue = 650;

    /// <summary>The ceiling of one treasure group, <c>maximumTreasureGroupValue</c>.</summary>
    public const int MaximumTreasureGroupValue = 14000;

    /// <summary>Every object the generator values, <c>objectGoldValue</c>.</summary>
    private static readonly Dictionary<Mp2ObjectType, int> GoldValues = new()
    {
        // Mines.
        [Mp2ObjectType.AbandonedMine] = 7500,
        [Mp2ObjectType.AlchemistLab] = 3500,
        [Mp2ObjectType.Mine] = 3500,
        [Mp2ObjectType.Sawmill] = 1000,

        // Pickups.
        [Mp2ObjectType.Campfire] = 1000,
        [Mp2ObjectType.GenieLamp] = 7000,
        [Mp2ObjectType.RandomArtifactMajor] = 10000,
        [Mp2ObjectType.RandomArtifactMinor] = 5000,
        [Mp2ObjectType.RandomArtifactTreasure] = 2000,
        [Mp2ObjectType.RandomResource] = MinimumTreasureValue,
        [Mp2ObjectType.Resource] = MinimumTreasureValue,
        [Mp2ObjectType.TreasureChest] = 1500,

        // Power-ups.
        [Mp2ObjectType.Fort] = 3000,
        [Mp2ObjectType.Gazebo] = 2000,
        [Mp2ObjectType.MercenaryCamp] = 3000,
        [Mp2ObjectType.ShrineFirstCircle] = 1000,
        [Mp2ObjectType.ShrineSecondCircle] = 2500,
        [Mp2ObjectType.ShrineThirdCircle] = 4000,
        [Mp2ObjectType.StandingStones] = 3000,
        [Mp2ObjectType.TreeOfKnowledge] = 5000,
        [Mp2ObjectType.WitchDoctorsHut] = 3000,
        [Mp2ObjectType.Xanadu] = 10000,

        // Dwellings.
        [Mp2ObjectType.AirAltar] = 5000,
        [Mp2ObjectType.ArcherHouse] = 2000,
        [Mp2ObjectType.Cave] = 1500,
        [Mp2ObjectType.DesertTent] = 3000,
        [Mp2ObjectType.DwarfCottage] = 2000,
        [Mp2ObjectType.EarthAltar] = 5000,
        [Mp2ObjectType.Excavation] = 1500,
        [Mp2ObjectType.FireAltar] = 5000,
        [Mp2ObjectType.GoblinHut] = 1500,
        [Mp2ObjectType.HalflingHole] = 1500,
        [Mp2ObjectType.PeasantHut] = MinimumTreasureValue,
        [Mp2ObjectType.Ruins] = 5000,
        [Mp2ObjectType.TreeCity] = 1500,
        [Mp2ObjectType.TreeHouse] = 1500,
        [Mp2ObjectType.WagonCamp] = 2000,
        [Mp2ObjectType.WatchTower] = 2000,
        [Mp2ObjectType.WaterAltar] = 5000,
    };

    /// <summary>The gold value of an object, <c>getObjectGoldValue()</c>.</summary>
    /// <param name="objectType">The object.</param>
    /// <returns>Its value; <c>0</c> for an object the table does not price.</returns>
    /// <remarks>
    /// 端口的 <see cref="Mp2ObjectType"/> 把「动作位」拆成两个成员（<c>TreasureChest</c> 与
    /// <c>ActionTreasureChest</c>），而物件目录里存的是带动作位的那个；表按不带动作位的写法建，
    /// 查之前先 <see cref="Mp2ObjectTypes.NonActionType"/> 归一，两个成员都查得到。
    /// </remarks>
    public static int GoldValue(Mp2ObjectType objectType)
    {
        if (GoldValues.TryGetValue(Mp2ObjectTypes.NonActionType(objectType), out int value))
        {
            return value;
        }

        GameLog.Warn($"The object {objectType} has no gold value in the random map generator's table; it counts as 0.");

        return 0;
    }

    /// <summary>Whether the table prices an object, without the assertion of <see cref="GoldValue"/>.</summary>
    /// <param name="objectType">The object.</param>
    /// <returns><see langword="true"/> when the object is in the table.</returns>
    public static bool HasGoldValue(Mp2ObjectType objectType) =>
        GoldValues.ContainsKey(Mp2ObjectTypes.NonActionType(objectType));
}

/// <summary>
/// The guard stack of a protected object, <c>Maps::Random_Generator::MonsterSelection</c>
/// (<c>map_random_generator_info.h:314-318</c>).
/// </summary>
/// <param name="MonsterId">
/// The creature placed first: either a concrete one or one of the four random monster ids the editor uses,
/// <c>Monster::RANDOM_MONSTER</c> and its four levels.
/// </param>
/// <param name="AllowedMonsters">
/// The creatures a random stack may turn into; empty when <paramref name="MonsterId"/> is concrete.
/// </param>
public readonly record struct RandomMapMonsterSelection(
    MonsterType MonsterId,
    IReadOnlyList<MonsterType> AllowedMonsters)
{
    /// <summary>No guard at all, the empty <c>MonsterSelection{}</c> of the reference.</summary>
    public static RandomMapMonsterSelection None { get; } = new(MonsterType.Unknown, []);

    /// <summary>Whether the selection puts anything on the map.</summary>
    public bool IsEmpty => MonsterId == MonsterType.Unknown;
}
