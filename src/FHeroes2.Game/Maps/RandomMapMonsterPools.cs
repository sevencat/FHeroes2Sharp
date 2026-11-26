using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Maps;

/// <summary>
/// How strong the guards of an object have to be, <c>getMonstersByValue()</c>
/// (<c>map_random_generator_helper.cpp:304-363</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考按守卫目标的「保护价值」查一张阶梯表：价值越高，可用兵种越强、随机兵种的档位也越高。玩家的怪物强度选择会先把
/// 保护价值挪一挪再查表——<c>DEADLY</c> 加 2500、<c>STRONG</c> 加 1500、<c>WEAK</c> 减 1500。
/// </para>
/// <para>
/// 前几档还给出具体的「可转成的兵种」名单（随机兵种在这几名里挑），价值不高时只给一个随机档位（一到四级），名单留空
/// 表示按档位随机。低于 2000 一律不设守卫。
/// </para>
/// </remarks>
public static class RandomMapMonsterPools
{
    /// <summary>The guard of an object worth <paramref name="protectedObjectValue"/>, <c>getMonstersByValue()</c>.</summary>
    /// <param name="strength">The player's monster strength setting.</param>
    /// <param name="protectedObjectValue">What the guarded object is worth, <see cref="RandomMapObjectValues.GoldValue"/>.</param>
    /// <returns>The stack to place, <see cref="RandomMapMonsterSelection.None"/> for a very cheap object.</returns>
    public static RandomMapMonsterSelection ByValue(RandomMapMonsterStrength strength, int protectedObjectValue)
    {
        int value = strength switch
        {
            RandomMapMonsterStrength.Deadly => protectedObjectValue + 2500,
            RandomMapMonsterStrength.Strong => protectedObjectValue + 1500,
            RandomMapMonsterStrength.Weak => protectedObjectValue - 1500,
            _ => protectedObjectValue,
        };

        if (value >= 13000)
        {
            return new RandomMapMonsterSelection(
                MonsterType.RandomMonsterLevel4,
                [
                    MonsterType.Giant,
                    MonsterType.Phoenix,
                    MonsterType.BoneDragon,
                    MonsterType.GreenDragon,
                    MonsterType.RedDragon,
                    MonsterType.Titan,
                    MonsterType.BlackDragon,
                ]);
        }

        if (value >= 11500)
        {
            return new RandomMapMonsterSelection(
                MonsterType.RandomMonsterLevel4,
                [
                    MonsterType.Archmage,
                    MonsterType.PowerLich,
                    MonsterType.Paladin,
                    MonsterType.Crusader,
                    MonsterType.Cyclops,
                    MonsterType.Genie,
                    MonsterType.Giant,
                    MonsterType.Phoenix,
                    MonsterType.BoneDragon,
                ]);
        }

        if (value >= 10000)
        {
            return new RandomMapMonsterSelection(
                MonsterType.RandomMonster,
                [
                    MonsterType.Lich,
                    MonsterType.WarTroll,
                    MonsterType.Mage,
                    MonsterType.Unicorn,
                    MonsterType.Hydra,
                    MonsterType.VampireLord,
                    MonsterType.Archmage,
                    MonsterType.PowerLich,
                    MonsterType.Ghost,
                    MonsterType.Paladin,
                    MonsterType.Crusader,
                    MonsterType.Cyclops,
                    MonsterType.Genie,
                ]);
        }

        if (value >= 8500)
        {
            return new RandomMapMonsterSelection(
                MonsterType.RandomMonsterLevel3,
                [
                    MonsterType.GreaterDruid,
                    MonsterType.OgreLord,
                    MonsterType.Roc,
                    MonsterType.Cavalry,
                    MonsterType.Vampire,
                    MonsterType.Medusa,
                    MonsterType.MinotaurKing,
                    MonsterType.Troll,
                    MonsterType.Champion,
                    MonsterType.Lich,
                    MonsterType.WarTroll,
                    MonsterType.Mage,
                    MonsterType.VampireLord,
                    MonsterType.Archmage,
                    MonsterType.Ghost,
                ]);
        }

        if (value >= 7000)
        {
            return new RandomMapMonsterSelection(
                MonsterType.RandomMonsterLevel3,
                [
                    MonsterType.Griffin,
                    MonsterType.MasterSwordsman,
                    MonsterType.EarthElement,
                    MonsterType.AirElement,
                    MonsterType.WaterElement,
                    MonsterType.FireElement,
                    MonsterType.Druid,
                    MonsterType.GreaterDruid,
                    MonsterType.Minotaur,
                    MonsterType.OgreLord,
                    MonsterType.Roc,
                    MonsterType.Cavalry,
                    MonsterType.Vampire,
                    MonsterType.Medusa,
                    MonsterType.MinotaurKing,
                ]);
        }

        if (value >= 5500)
        {
            return new RandomMapMonsterSelection(
                MonsterType.RandomMonster,
                [
                    MonsterType.VeteranPikeman,
                    MonsterType.Mummy,
                    MonsterType.Nomad,
                    MonsterType.IronGolem,
                    MonsterType.Elf,
                    MonsterType.RoyalMummy,
                    MonsterType.Wolf,
                    MonsterType.GrandElf,
                    MonsterType.Swordsman,
                    MonsterType.Ogre,
                    MonsterType.SteelGolem,
                    MonsterType.Griffin,
                    MonsterType.MasterSwordsman,
                    MonsterType.EarthElement,
                    MonsterType.AirElement,
                    MonsterType.WaterElement,
                    MonsterType.FireElement,
                ]);
        }

        if (value >= 3500)
        {
            return new RandomMapMonsterSelection(MonsterType.RandomMonsterLevel2, []);
        }

        if (value >= 2000)
        {
            return new RandomMapMonsterSelection(MonsterType.RandomMonsterLevel1, []);
        }

        return RandomMapMonsterSelection.None;
    }
}
