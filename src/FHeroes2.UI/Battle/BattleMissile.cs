using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Battle;

/// <summary>
/// A shot on its way: where it leaves the shooter, which way it bends and where its sprite is blitted,
/// <c>RedrawActionAttackPart1()</c> (<c>battle_interface.cpp:4120-4156</c>) and <c>RedrawMissileAnimation()</c>
/// (<c>:1062-1136</c>).
/// </summary>
/// <remarks>
/// 参考把「算路线」摊在 AttackPart1 里、「走路线」放在一个以时间片为粒度的循环里；端口把前者收成这里的
/// 纯几何，后者交给 <see cref="BattleFieldPainter"/> 一帧一步。
/// </remarks>
public sealed class BattleMissile
{
    /// <summary>Past this many degrees a shot counts as going up or down, <c>angle &gt;= 25</c> (<c>:4134-4140</c>).</summary>
    public const double DirectionThreshold = 25.0;

    /// <summary>The fewest sample points a path is cut into, <c>std::max( missile.width(), 25 )</c> (<c>:4131</c>).</summary>
    private const int ShortestPath = 25;

    /// <summary>How finely the lightning of a mage is cut, <c>getLinePoints( ..., 50 )</c> (<c>:4129</c>).</summary>
    private const int MagePathStep = 50;

    /// <summary>Where a shot is aimed and which way it ends up bending, the arithmetic before the route.</summary>
    /// <param name="Start">The point the projectile leaves from, already offset for the shooting direction.</param>
    /// <param name="Target">The point it flies at, the defender's centre.</param>
    /// <param name="Angle">The angle in degrees, <c>-180..180</c>, up being positive.</param>
    /// <param name="Direction">Which of the three shooting sequences the angle falls into.</param>
    public readonly record struct Aim(Point Start, Point Target, double Angle, MonsterAttackDirection Direction);

    private readonly Point[] _points;

    /// <summary>Lays a shot's route out, <c>RedrawMissileAnimation()</c> (<c>:1062-1136</c>).</summary>
    /// <param name="start">The point the projectile leaves from.</param>
    /// <param name="target">The point it flies at.</param>
    /// <param name="angle">The angle in degrees, from <see cref="GetAngle"/>.</param>
    /// <param name="monster">The shooter, which decides the mage and troll special cases.</param>
    /// <param name="width">Width of the projectile sprite.</param>
    /// <param name="height">Height of the projectile sprite.</param>
    /// <param name="spriteOffsetX">The projectile sprite's own x offset.</param>
    /// <param name="spriteOffsetY">The projectile sprite's own y offset.</param>
    public BattleMissile(
        Point start, Point target, double angle, MonsterType monster, int width, int height, int spriteOffsetX, int spriteOffsetY)
    {
        IsMage = monster is MonsterType.Mage or MonsterType.Archmage;
        IsMirrored = start.X > target.X;

        // 参考把终点往射手那一侧挪半个弹体，让箭尾落在目标中心；法师没有弹体，直接打到中心。
        Point end = IsMage
            ? target
            : new Point(
                target.X + (IsMirrored ? width / 2 : -(width / 2)),
                target.Y + (start.Y > target.Y ? height / 2 : -(height / 2)));

        _points = LinePoints.GetLinePoints(start, end, IsMage ? MagePathStep : Math.Max(width, ShortestPath));

        // 第一段是「弹体从光点冒出来」的那一格，除了法师与巨魔都跳过（:1080-1096）。
        bool skipFirst = !IsMage && monster != MonsterType.Troll && monster != MonsterType.WarTroll;

        if (skipFirst && _points.Length > 1)
        {
            _points = _points[1..];
        }

        BlitOffset = new Point(
            IsMirrored ? -width - spriteOffsetX : spriteOffsetX,
            angle > 0 ? -height - spriteOffsetY : spriteOffsetY);
    }

    /// <summary>Whether the shooter is a mage, whose shot is lightning drawn as a line rather than a sprite.</summary>
    public bool IsMage { get; }

    /// <summary>Whether the shot flies to the left, i.e. its sprite is mirrored.</summary>
    public bool IsMirrored { get; }

    /// <summary>Where the projectile sprite goes relative to the point it stands on.</summary>
    public Point BlitOffset { get; }

    /// <summary>The sample points the shot passes, one per frame.</summary>
    public IReadOnlyList<Point> Points => _points;

    /// <summary>
    /// The angle between two points in degrees, <c>GetAngle()</c> (<c>math_tools.cpp:20-33</c>): positive up,
    /// normalised so that a shot to the left still reads <c>-180..180</c>.
    /// </summary>
    /// <param name="start">Where the line starts.</param>
    /// <param name="target">Where it ends.</param>
    /// <returns>The angle in degrees.</returns>
    public static double GetAngle(Point start, Point target)
    {
        int dx = target.X - start.X;
        int dy = target.Y - start.Y;

        double angle = Math.Atan2(-dy, dx) * 180.0 / Math.PI;

        if (dx < 0)
        {
            angle = dy <= 0 ? 180.0 - angle : -angle - 180.0;
        }

        return angle;
    }

    /// <summary>Which shooting sequence an angle falls into, <c>:4134-4140</c>.</summary>
    /// <param name="angle">The angle in degrees, up being positive.</param>
    /// <returns>The direction, one of the three the creature's shots are cut into.</returns>
    public static MonsterAttackDirection GetDirection(double angle)
    {
        if (angle >= DirectionThreshold)
        {
            return MonsterAttackDirection.Top;
        }

        return angle <= -DirectionThreshold ? MonsterAttackDirection.Bottom : MonsterAttackDirection.Front;
    }

    /// <summary>
    /// Where a shot leaves the shooter, <c>RedrawActionAttackPart1()</c> (<c>battle_interface.cpp:4120-4146</c>):
    /// the middle of the cell, thrown back a quarter for a reflected wide creature, then moved along the
    /// shooting direction's projectile offset.
    /// </summary>
    /// <param name="attacker">The shooter.</param>
    /// <param name="targetPosition">The point aimed at, <see cref="BattleTroopSprite.GetCenterPoint"/>.</param>
    /// <param name="attackerSprite">The shooter's current frame, whose offsets place it on the cell.</param>
    /// <param name="animation">The shooter's animation, which knows the projectile offsets.</param>
    /// <param name="isSameRow">Whether both stand on one row, in which case the shot is flat.</param>
    /// <returns>Where the shot starts, what it aims at, its angle and the direction it falls into.</returns>
    public static Aim AimShot(
        BattleUnit attacker, Point targetPosition, Sprite attackerSprite, AnimationReference animation, bool isSameRow)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(attackerSprite);
        ArgumentNullException.ThrowIfNull(animation);

        Rect area = attacker.Position.GetRect();

        Point attackerPosition = BattleTroopSprite.GetPosition(attacker, attackerSprite);

        // 宽单位镜像时从后半截的中心出手，其余从格子中心（:4122）。
        int rearCenterX = attacker.IsWide && attacker.IsReflect ? area.Width * 3 / 4 : BattleCell.WidthPx / 2;

        Point shooter = new(area.X + rearCenterX, attackerPosition.Y - attackerSprite.OffsetY);

        // 角度先按正面偏移量一次，同排则强制平射（:4126-4128）。
        Point frontOffset = animation.GetProjectileOffset((int)MonsterAttackDirection.Front);
        double angle = isSameRow ? 0 : GetAngle(shooter + frontOffset, targetPosition);

        MonsterAttackDirection direction = GetDirection(angle);
        Point offset = animation.GetProjectileOffset((int)direction);

        Point start = new(shooter.X + (attacker.IsReflect ? -offset.X : offset.X), shooter.Y + offset.Y);

        return new Aim(start, targetPosition, angle, direction);
    }
}
