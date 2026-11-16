namespace FHeroes2.Assets.Maps;

/// <summary>
/// The classification predicates of <c>mp2.cpp</c> (<c>:505-813</c>) — everything but the two refresh
/// lists <see cref="IsWeekLife"/> and <see cref="IsBattleLife"/>, which live in
/// <c>Mp2ObjectType.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every list below is compared against the <b>action</b> values, because that is what the map stores and
/// what <c>Tile.GetMainObjectType()</c> answers. The Resurrection extension objects are the one
/// exception: their base values (<see cref="Mp2ObjectType.SwampyLake"/> and friends, 257-260) already
/// carry the action bit of 128, so the reference's <c>OBJ_BARREL</c> is the port's
/// <see cref="Mp2ObjectType.Barrel"/> and not <see cref="Mp2ObjectType.ActionBarrel"/>, the mechanical
/// twin the reference does not have.
/// </para>
/// <para>
/// All of them are plain switches on purpose: they are consulted many times per pathfinder run, and a
/// table lookup would hide the fact that the lists are the game data. Adding an object type means adding
/// it to the lists where the reference has it — not to all of them.
/// </para>
/// </remarks>
public static partial class Mp2ObjectTypes
{
    /// <summary>
    /// <c>MP2::isDayLife</c> (<c>mp2.cpp:505</c>): the objects restored every day. Only the magic well
    /// refills daily; everything else waits for a new week.
    /// </summary>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> for the magic well.</returns>
    /// <remarks>
    /// 参考比较的是 <c>OBJ_ACTION_MAGIC_WELL</c>（`mp2.cpp:507`），因为参考的内存里动作对象带着 +128 的位；
    /// 端口的瓦片存的是 MP2 文件的原始值（无动作位），所以这里与 <see cref="IsWeekLife"/> 一样比非动作值。
    /// 一个 `MP2::isDayLife` 的调用点在端口中一条也不差：`Visit.IsDayLife` 只把它当「这个对象一天寿命」用。
    /// </remarks>
    public static bool IsDayLife(Mp2ObjectType type) => type == Mp2ObjectType.MagicWell;

    /// <summary>
    /// <c>MP2::isInGameActionObject</c> (<c>mp2.cpp:635</c>): whether the object is an action object that
    /// the game knows how to visit.
    /// </summary>
    /// <remarks>
    /// Two conditions stack: the type must carry the action bit, and its non-action twin must have an
    /// action counterpart at all (<see cref="CanBeActionObject"/>). The second test is what keeps the
    /// terrain decorations — trees, mountains, lakes — from counting as visitable when a map editor has
    /// stored their action twin.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when a hero can visit the object.</returns>
    public static bool IsInGameActionObject(Mp2ObjectType type)
    {
        if (!IsActionObject(type))
        {
            return false;
        }

        // The event object is the one edge case inherited from the original game: it carries the action
        // bit but is not an object a hero triggers by walking onto it.
        if (type == Mp2ObjectType.ActionEvent)
        {
            return false;
        }

        return CanBeActionObject((Mp2ObjectType)((ushort)type & ~(ushort)Mp2ObjectType.ActionObjectType));
    }

    /// <summary>
    /// <c>MP2::isInGameActionObject( type, accessedFromWater )</c> (<c>mp2.cpp:590</c>): the same test,
    /// narrowed to what a hero on a boat can reach.
    /// </summary>
    /// <param name="type">Object type to test.</param>
    /// <param name="accessedFromWater">Whether the hero arrives on a boat.</param>
    /// <returns><see langword="true"/> when the object is visitable from that side.</returns>
    public static bool IsInGameActionObject(Mp2ObjectType type, bool accessedFromWater) =>
        accessedFromWater ? IsWaterActionObject(type) : IsInGameActionObject(type);

    /// <summary>
    /// <c>MP2::isWaterActionObject</c> (<c>mp2.cpp:599</c>): the objects the hero on a boat can interact
    /// with.
    /// </summary>
    /// <remarks>
    /// The list is what the original editor allows to place on water, plus the heroes — who cannot be
    /// placed there but can board a boat. Anything else is treated as a land object, except that a
    /// non-action type still goes through <see cref="IsInGameActionObject(Mp2ObjectType)"/>: some editors
    /// place arbitrary objects on water and the game has to cope.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object can be visited from the water.</returns>
    public static bool IsWaterActionObject(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionArtifact => true,
        Mp2ObjectType.Barrel => true,
        Mp2ObjectType.ActionBottle => true,
        Mp2ObjectType.ActionBuoy => true,
        Mp2ObjectType.ActionCoast => true,
        Mp2ObjectType.ActionDerelictShip => true,
        Mp2ObjectType.ActionFlotsam => true,

        // Heroes cannot be placed on water by the original editor, but they can board a boat.
        Mp2ObjectType.ActionHero => true,
        Mp2ObjectType.ActionMagellansMaps => true,
        Mp2ObjectType.ActionMermaid => true,
        Mp2ObjectType.ActionSeaChest => true,
        Mp2ObjectType.ActionShipwreck => true,
        Mp2ObjectType.ActionShipwreckSurvivor => true,
        Mp2ObjectType.ActionSirens => true,
        Mp2ObjectType.ActionWhirlpool => true,

        Mp2ObjectType.ActionBoat => false,
        Mp2ObjectType.ActionCastle => false,

        _ => IsInGameActionObject(type),
    };

    /// <summary>
    /// <c>MP2::isRevisitAllowedForObject</c> (<c>mp2.cpp:578</c>): whether the object may be visited more
    /// than once.
    /// </summary>
    /// <remarks>
    /// A whirlpool is single use per hero and is excluded from the action objects; everything else
    /// follows the rule of <see cref="IsInGameActionObject(Mp2ObjectType, bool)"/>. Which is why an
    /// object may be an action object and still refuse a second visit — that is what the caller checks
    /// with this predicate before showing the "already visited" feedback.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <param name="accessedFromWater">Whether the hero arrives on a boat.</param>
    /// <returns><see langword="true"/> when the object can be visited again.</returns>
    public static bool IsRevisitAllowedForObject(Mp2ObjectType type, bool accessedFromWater) =>
        type != Mp2ObjectType.ActionWhirlpool && IsInGameActionObject(type, accessedFromWater);

    /// <summary>
    /// <c>MP2::isValuableResourceObject</c> (<c>mp2.cpp:669</c>): the objects that hand out a resource, an
    /// artifact or gold.
    /// </summary>
    /// <remarks>
    /// The explicit list is the objects whose reward lives in their metadata lane rather than in their
    /// type; the pickup objects (a pile of resources, a chest) are added by
    /// <see cref="IsPickupObject"/>. The AI uses this to decide whether a detour pays off.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object gives something valuable.</returns>
    public static bool IsValuableResourceObject(Mp2ObjectType type) => type switch
    {
        // Sorted alphabetically, as in the reference.
        Mp2ObjectType.Barrel => true,
        Mp2ObjectType.ActionCampfire => true,
        Mp2ObjectType.ActionDaemonCave => true,
        Mp2ObjectType.ActionDerelictShip => true,
        Mp2ObjectType.ActionFlotsam => true,
        Mp2ObjectType.ActionGraveyard => true,
        Mp2ObjectType.ActionLeanTo => true,
        Mp2ObjectType.ActionMagicGarden => true,
        Mp2ObjectType.ActionPyramid => true,
        Mp2ObjectType.ActionSeaChest => true,
        Mp2ObjectType.ActionShipwreck => true,
        Mp2ObjectType.ActionShipwreckSurvivor => true,
        Mp2ObjectType.ActionSkeleton => true,
        Mp2ObjectType.ActionWagon => true,
        Mp2ObjectType.ActionWaterWheel => true,
        Mp2ObjectType.ActionWindmill => true,
        _ => IsPickupObject(type),
    };

    /// <summary>
    /// <c>MP2::isCaptureObject</c> (<c>mp2.cpp:700</c>): the objects a player can take ownership of, i.e.
    /// the ones <c>World.CaptureObject</c> knows about.
    /// </summary>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object can be captured.</returns>
    public static bool IsCaptureObject(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionAlchemistLab => true,
        Mp2ObjectType.ActionCastle => true,
        Mp2ObjectType.ActionLighthouse => true,
        Mp2ObjectType.ActionMine => true,
        Mp2ObjectType.ActionSawmill => true,
        _ => false,
    };

    /// <summary>
    /// <c>MP2::isPickupObject</c> (<c>mp2.cpp:717</c>): the objects that vanish from the map once taken.
    /// </summary>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object is picked up.</returns>
    public static bool IsPickupObject(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionArtifact => true,
        Mp2ObjectType.Barrel => true,
        Mp2ObjectType.ActionBottle => true,
        Mp2ObjectType.ActionCampfire => true,
        Mp2ObjectType.ActionFlotsam => true,
        Mp2ObjectType.ActionGenieLamp => true,
        Mp2ObjectType.ActionResource => true,
        Mp2ObjectType.ActionSeaChest => true,
        Mp2ObjectType.ActionShipwreckSurvivor => true,
        Mp2ObjectType.ActionTreasureChest => true,
        _ => false,
    };

    /// <summary>
    /// <c>MP2::isArtifactObject</c> (<c>mp2.cpp:739</c>): the objects that keep an artifact id in their
    /// metadata lane — the exact list <c>Tile.GetArtifactFromTile</c> switches on.
    /// </summary>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object may hold an artifact.</returns>
    public static bool IsArtifactObject(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionArtifact => true,
        Mp2ObjectType.ActionDaemonCave => true,
        Mp2ObjectType.ActionGraveyard => true,
        Mp2ObjectType.ActionSeaChest => true,
        Mp2ObjectType.ActionShipwreck => true,
        Mp2ObjectType.ActionShipwreckSurvivor => true,
        Mp2ObjectType.ActionSkeleton => true,
        Mp2ObjectType.ActionTreasureChest => true,
        Mp2ObjectType.ActionWagon => true,
        _ => false,
    };

    /// <summary>
    /// <c>MP2::isBattleMandatoryifObjectIsProtected</c> (<c>mp2.cpp:760</c>): whether a fight started by
    /// the object's guards cannot be refused.
    /// </summary>
    /// <remarks>
    /// An artifact guarded by rogues is the case the comment in the reference names; a capture object is
    /// added because its garrison has to be beaten before the flag changes. The name keeps the reference's
    /// missing space — it is quoted in several comments there.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the battle must be fought.</returns>
    public static bool IsBattleMandatoryIfObjectIsProtected(Mp2ObjectType type) =>
        type switch
        {
            Mp2ObjectType.ActionArtifact => true,
            Mp2ObjectType.ActionMonster => true,
            _ => IsCaptureObject(type),
        };

    /// <summary>
    /// <c>MP2::isSafeForFogDiscoveryObject</c> (<c>mp2.cpp:775</c>): whether the AI may walk onto the
    /// object while unveiling the fog.
    /// </summary>
    /// <remarks>
    /// Stone liths and whirlpools are wanted because they open access to new tiles, signs are harmless,
    /// and every other action object is avoided: it may be guarded or may cost resources the AI would
    /// rather spend elsewhere.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when visiting the object for fog discovery is safe.</returns>
    public static bool IsSafeForFogDiscoveryObject(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionStoneLiths => true,
        Mp2ObjectType.ActionWhirlpool => true,
        Mp2ObjectType.ActionSign => true,
        _ => !IsInGameActionObject(type),
    };

    /// <summary>
    /// <c>MP2::isNeedStayFront</c> (<c>mp2.cpp:793</c>): whether the hero has to stand on the tile in
    /// front of the object to interact with it.
    /// </summary>
    /// <remarks>
    /// This is the predicate behind <c>Maps::isActionObject</c> checks during movement: a hero cannot
    /// walk onto a monster, a boat or a barrier from any side — he has to stand next to it. Pickup objects
    /// are included because their reward is collected on approach, not by standing on them.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object needs the tile in front of it.</returns>
    public static bool IsNeedStayFront(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionBarrier => true,
        Mp2ObjectType.ActionBoat => true,
        Mp2ObjectType.ActionBuoy => true,
        Mp2ObjectType.ActionHero => true,
        Mp2ObjectType.ActionJail => true,
        Mp2ObjectType.ActionMermaid => true,
        Mp2ObjectType.ActionMonster => true,
        Mp2ObjectType.ActionShipwreck => true,
        Mp2ObjectType.ActionSirens => true,
        Mp2ObjectType.ActionSkeleton => true,
        _ => IsPickupObject(type),
    };
}
