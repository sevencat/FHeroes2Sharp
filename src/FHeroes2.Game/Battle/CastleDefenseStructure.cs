namespace FHeroes2.Game.Battle;

/// <summary>
/// The parts of a castle a catapult can aim at, <c>battle.h:135-148</c>.
/// </summary>
/// <remarks>
/// The values are the reference's own and are used as <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>
/// keys holding the state of each target during a siege, so they must not be renumbered. The two bridge towers
/// (<see cref="TopBridgeTower"/>, <see cref="BottomBridgeTower"/>) are not offered by
/// <see cref="BattleCatapult.GetAllowedTargets"/> — the reference only draws them when the bridge falls.
/// </remarks>
public enum CastleDefenseStructure
{
    None = 0,
    Wall1 = 1,
    Wall2 = 2,
    Wall3 = 3,
    Wall4 = 4,
    Tower1 = 5,
    Tower2 = 6,
    Bridge = 7,
    CentralTower = 8,
    TopBridgeTower = 9,
    BottomBridgeTower = 10
}

/// <summary>
/// What is hitting a castle structure, <c>Battle::SiegeWeaponType</c> (<c>battle_arena.h:68-72</c>).
/// </summary>
/// <remarks>
/// 同一堵墙对两种武器的「剩余耐久」读数不同：投石车的目标清单里，中央塔只有「站/倒」一位；地震法术面对的是
/// 墙体格上的格数（<c>board[...].GetObject()</c>），于是同一个 <see cref="CastleDefenseStructure"/> 要按武器
/// 换一套口径。参考因此把这个参数一路传进 <c>getCastleDefenseStructureCondition()</c>。
/// </remarks>
public enum SiegeWeaponType
{
    /// <summary>The attacking hero's catapult, <c>SiegeWeaponType::Catapult</c>.</summary>
    Catapult,

    /// <summary>The Earthquake spell, <c>SiegeWeaponType::EarthquakeSpell</c>.</summary>
    EarthquakeSpell
}
