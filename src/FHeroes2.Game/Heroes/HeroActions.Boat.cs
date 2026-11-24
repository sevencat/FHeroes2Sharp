using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Whether the hero got on a boat, <c>ActionToBoat()</c> (<c>heroes_action.cpp:668</c>).
/// </summary>
public enum BoatOutcome
{
    /// <summary>The hero already has a ship of his own; nothing happens (<c>heroes_action.cpp:672</c>).</summary>
    AlreadyShipMaster,

    /// <summary>The hero boarded, <c>heroes_action.cpp:674-715</c>.</summary>
    Boarded,
}

/// <summary>
/// Whether the hero got off, <c>ActionToCoast()</c> (<c>heroes_action.cpp:718</c>).
/// </summary>
public enum CoastOutcome
{
    /// <summary>The hero walks on foot already; nothing happens (<c>heroes_action.cpp:722</c>).</summary>
    NotAShipMaster,

    /// <summary>The hero stepped ashore and the boat was left where he was, <c>heroes_action.cpp:724-749</c>.</summary>
    Disembarked,
}

/// <summary>
/// What boarding came to.
/// </summary>
/// <param name="Outcome">Whether the hero boarded.</param>
/// <param name="Facing">The direction the boat points, which the hero copies.</param>
/// <param name="PreviousOwner">The color the boat belonged to before it was taken, <see cref="PlayerColor.None"/> for none.</param>
public readonly record struct BoatResult(BoatOutcome Outcome, Direction Facing, PlayerColor PreviousOwner);

/// <summary>
/// What landing came to.
/// </summary>
/// <param name="Outcome">Whether the hero stepped ashore.</param>
/// <param name="BoatTileIndex">The tile the empty boat was left on, <c>-1</c> when the hero did not land.</param>
/// <param name="BoatFacing">The direction the left boat points.</param>
public readonly record struct CoastResult(CoastOutcome Outcome, int BoatTileIndex, Direction BoatFacing);

public sealed partial class HeroActions
{
    /// <summary>
    /// Boards a boat standing on the tile the hero walked onto, <c>ActionToBoat()</c> (<c>heroes_action.cpp:668</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:668-716</c>。顺序里有一步容易写错：<b>英雄先站上去（<c>Move2Dest</c>），再把船擦掉</b>。
    /// 站上去那一步会把「船」记进 <see cref="Hero.ObjectTypeUnderHero"/>，所以擦完船之后必须显式改记船底下的东西；
    /// 反过来写（先擦后站）看着也能得到同样的结果，但一旦以后 <c>SetHero</c> 的寄存时机变了，就会静默地记错。
    /// </para>
    /// <para>
    /// 上船<b>不记访问</b>：船是工具不是遭遇，来回上下多少次都行。船的原主人颜色在英雄踩上去的同时清掉——
    /// 从此这艘船是自己的了。
    /// </para>
    /// <para>
    /// <c>hero.FadeIn()</c> 由界面层的过场负责（参考在这里同步淡出淡入，端口把画面交给 <see cref="IHeroActionHost"/>，
    /// 数据侧只把 alpha 拉回不透明）。<c>hero.setLastGroundRegion()</c> 记下「上船前踩在哪块陆地」——参考里没人读它
    /// （AI 与存档各自存一份），端口照抄，只为对齐状态。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the boat stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>Whether the hero boarded and which way the boat points.</returns>
    public BoatResult ActionToBoat(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_hero.IsShipMaster)
        {
            Debug.Assert(false, "A hero who already owns a ship boarded a second boat.");

            return new BoatResult(BoatOutcome.AlreadyShipMaster, _hero.Direction, PlayerColor.None);
        }

        Tile destination = world.GetTile(tileIndex);
        Direction facing = destination.GetBoatDirection();
        PlayerColor previousOwner = destination.BoatOwnerColor;

        _hero.SetLastGroundRegion(world.GetTile(_hero.MapIndex).Region);

        _hero.ShowPath(false);
        _hero.Scout(world, tileIndex);
        _hero.MoveToDest(tileIndex, world);
        _hero.ResetMovePoints();
        _hero.Path.Reset();
        _hero.SetAlphaValue(255);

        // 船不会转，所以是英雄迁就船的朝向。
        _hero.Direction = facing;

        // 先擦船再重算类型，然后把「船底下的东西」记进英雄身上、把瓦片改记成英雄。
        // MoveToDest 里 SetHero 记下的是船本身，所以这两行必须落在它之后（参考 heroes_action.cpp:700-708 同序）。
        destination.ResetMainObjectPart();
        destination.UpdateObjectType(world);
        _hero.SetObjectTypeUnderHero(destination.GetMainObjectType(world, true));
        destination.SetMainObjectType(Mp2ObjectType.ActionHero);
        world.ResetPathfinder();

        _hero.SetShipMaster(true);
        _hero.ShowPath(true);

        destination.ResetBoatOwnerColor();

        return new BoatResult(BoatOutcome.Boarded, facing, previousOwner);
    }

    /// <summary>
    /// Steps ashore from a boat, <c>ActionToCoast()</c> (<c>heroes_action.cpp:718</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:718-750</c>。船<b>留在下水的地方</b>，朝向是「从船指向岸边」——所以以后回来上船，
    /// 船头还是冲着陆地，跟原版一样。
    /// </para>
    /// <para>
    /// 落脚之后还要 <c>ActionNewPosition()</c>：上岸这一步本身已经站在新瓦片上了，可那块瓦片上也许正躺着资源或宝箱，
    /// 得再走一遍动作分派把它们捡起来（参考 <c>heroes_action.cpp:749</c>）。这是本文件里唯一一次「动作里再套动作」，
    /// 不会递归：ActionNewPosition 走的是落地后的分派，海岸只在 <see cref="IsShipMaster"/> 时成立，此刻已经是假的。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The land tile to step onto.</param>
    /// <param name="world">The world.</param>
    /// <returns>Whether the hero landed and where the boat was left.</returns>
    public CoastResult ActionToCoast(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_hero.IsShipMaster)
        {
            Debug.Assert(false, "A hero on foot disembarked.");

            return new CoastResult(CoastOutcome.NotAShipMaster, -1, Direction.Unknown);
        }

        int fromIndex = _hero.MapIndex;
        Tile from = world.GetTile(fromIndex);

        _hero.ShowPath(false);
        _hero.Scout(world, tileIndex);
        _hero.MoveToDest(tileIndex, world);
        _hero.ResetMovePoints();
        _hero.Path.Reset();

        Direction boatFacing = world.Geometry!.Value.GetDirection(fromIndex, tileIndex);
        from.SetBoat(world, boatFacing, _hero.Color);

        _hero.SetShipMaster(false);

        _hero.ShowPath(true);
        _hero.MovementHandler.ActionNewPosition(_hero, true);

        return new CoastResult(CoastOutcome.Disembarked, fromIndex, boatFacing);
    }
}
