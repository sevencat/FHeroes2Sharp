namespace FHeroes2.Game.Battle;

/// <summary>
/// Which of a castle's three shooting positions a tower is, <c>battle_tower.h:36-41</c>.
/// </summary>
/// <remarks>
/// The order is the reference's and is observable through <see cref="BattleCommand.Tower(int, int)"/>, which carries
/// the type as a plain number. The left and right turrets stand on the board's archer-tower cells (19 and 85) and can
/// be knocked down; the center tower is the keep itself.
/// </remarks>
public enum BattleTowerType
{
    Left,
    Center,
    Right
}
