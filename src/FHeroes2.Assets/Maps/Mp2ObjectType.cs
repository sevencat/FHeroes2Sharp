namespace FHeroes2.Assets.Maps;

/// <summary>
/// The object type stored in the <c>mapObjectType</c> byte of an MP2 tile, the port of
/// <c>MP2::MapObjectType</c> (<c>mp2.h</c>). Values must not be renumbered: they are written by the
/// original map editor and several of them are reported by the reference implementation in save
/// games.
///
/// <para>
/// The layout is a bit field: objects below <see cref="ActionObjectType"/> are "non action" objects,
/// every action object has the value of its non-action counterpart plus
/// <see cref="ActionObjectType"/>. <see cref="Mp2ObjectTypes.IsActionObject"/> reproduces the
/// <c>objectType &amp; OBJ_ACTION_OBJECT_TYPE</c> test of the reference.
/// </para>
///
/// <para>
/// Entries from <see cref="ResurrectionObjectType"/> on belong to the Resurrection expansion and are
/// never present in an MP2 file.
/// </para>
/// </summary>
public enum Mp2ObjectType : ushort
{
    /// <summary>No object at all, <c>MP2::OBJ_NONE</c>.</summary>
    None = 0,

    AlchemistLab = 1,
    Sign = 2,
    Buoy = 3,
    Skeleton = 4,
    DaemonCave = 5,
    TreasureChest = 6,
    FaerieRing = 7,
    Campfire = 8,
    Fountain = 9,
    Gazebo = 10,
    GenieLamp = 11,
    Graveyard = 12,
    ArcherHouse = 13,
    GoblinHut = 14,
    DwarfCottage = 15,
    PeasantHut = 16,
    Stables = 17,
    AlchemistTower = 18,
    Event = 19,
    DragonCity = 20,
    Lighthouse = 21,
    WaterWheel = 22,
    Mine = 23,
    Monster = 24,
    Obelisk = 25,
    Oasis = 26,
    Resource = 27,

    /// <summary>Coast line; the only entry of the non-action range that is an action object in some cases.</summary>
    Coast = 28,

    Sawmill = 29,
    Oracle = 30,
    ShrineFirstCircle = 31,
    Shipwreck = 32,
    SeaChest = 33,
    DesertTent = 34,
    Castle = 35,
    StoneLiths = 36,
    WagonCamp = 37,
    HutOfMagi = 38,
    Whirlpool = 39,
    Windmill = 40,
    Artifact = 41,
    Mermaid = 42,
    Boat = 43,
    RandomUltimateArtifact = 44,
    RandomArtifact = 45,
    RandomResource = 46,
    RandomMonster = 47,
    RandomTown = 48,
    RandomCastle = 49,
    EyeOfMagi = 50,
    RandomMonsterWeak = 51,
    RandomMonsterMedium = 52,
    RandomMonsterStrong = 53,
    RandomMonsterVeryStrong = 54,

    /// <summary>Any kind of hero, including random ones.</summary>
    Hero = 55,

    NothingSpecial = 56,
    MossyRock = 57,
    WatchTower = 58,
    TreeHouse = 59,
    TreeCity = 60,
    Ruins = 61,
    Fort = 62,
    TradingPost = 63,
    AbandonedMine = 64,

    /// <summary>
    /// Originally the "Thatched Hut"; the reference rewrites every occurrence on an original map to
    /// <see cref="PeasantHut"/> / <see cref="ActionPeasantHut"/>.
    /// </summary>
    Sirens = 65,

    StandingStones = 66,
    Idol = 67,
    TreeOfKnowledge = 68,
    WitchDoctorsHut = 69,
    Temple = 70,
    HillFort = 71,
    HalflingHole = 72,
    MercenaryCamp = 73,
    ShrineSecondCircle = 74,
    ShrineThirdCircle = 75,
    Pyramid = 76,
    CityOfDead = 77,
    Excavation = 78,
    Sphinx = 79,
    Wagon = 80,
    TarPit = 81,
    ArtesianSpring = 82,
    TrollBridge = 83,
    WateringHole = 84,
    WitchsHut = 85,
    Xanadu = 86,
    Cave = 87,
    LeanTo = 88,
    MagellansMaps = 89,
    Flotsam = 90,
    DerelictShip = 91,
    ShipwreckSurvivor = 92,
    Bottle = 93,
    MagicWell = 94,
    MagicGarden = 95,
    ObservationTower = 96,
    FreemansFoundry = 97,
    Reefs = 98,
    Trees = 99,
    Mountains = 100,
    Volcano = 101,
    Flowers = 102,
    Rock = 103,
    WaterLake = 104,
    Mandrake = 105,
    DeadTree = 106,
    Stump = 107,
    Crater = 108,
    Cactus = 109,
    Mound = 110,
    Dune = 111,
    LavaPool = 112,
    Shrub = 113,
    Arena = 114,
    BarrowMounds = 115,
    RandomArtifactTreasure = 116,
    RandomArtifactMinor = 117,
    RandomArtifactMajor = 118,
    Barrier = 119,
    TravellerTent = 120,
    ExpansionDwelling = 121,
    ExpansionObject = 122,
    Jail = 123,
    FireAltar = 124,
    AirAltar = 125,
    EarthAltar = 126,
    WaterAltar = 127,

    /// <summary>
    /// Offset that turns a non-action object into its action counterpart,
    /// <c>MP2::OBJ_ACTION_OBJECT_TYPE</c>. Never store this value itself on a map.
    /// </summary>
    ActionObjectType = 128,

    ActionAlchemistLab = AlchemistLab + ActionObjectType,
    ActionSign = Sign + ActionObjectType,
    ActionBuoy = Buoy + ActionObjectType,
    ActionSkeleton = Skeleton + ActionObjectType,
    ActionDaemonCave = DaemonCave + ActionObjectType,
    ActionTreasureChest = TreasureChest + ActionObjectType,
    ActionFaerieRing = FaerieRing + ActionObjectType,
    ActionCampfire = Campfire + ActionObjectType,
    ActionFountain = Fountain + ActionObjectType,
    ActionGazebo = Gazebo + ActionObjectType,
    ActionGenieLamp = GenieLamp + ActionObjectType,
    ActionGraveyard = Graveyard + ActionObjectType,
    ActionArcherHouse = ArcherHouse + ActionObjectType,
    ActionGoblinHut = GoblinHut + ActionObjectType,
    ActionDwarfCottage = DwarfCottage + ActionObjectType,
    ActionPeasantHut = PeasantHut + ActionObjectType,
    ActionStables = Stables + ActionObjectType,
    ActionAlchemistTower = AlchemistTower + ActionObjectType,
    ActionEvent = Event + ActionObjectType,
    ActionDragonCity = DragonCity + ActionObjectType,
    ActionLighthouse = Lighthouse + ActionObjectType,
    ActionWaterWheel = WaterWheel + ActionObjectType,
    ActionMine = Mine + ActionObjectType,
    ActionMonster = Monster + ActionObjectType,
    ActionObelisk = Obelisk + ActionObjectType,
    ActionOasis = Oasis + ActionObjectType,
    ActionResource = Resource + ActionObjectType,
    ActionCoast = Coast + ActionObjectType,
    ActionSawmill = Sawmill + ActionObjectType,
    ActionOracle = Oracle + ActionObjectType,
    ActionShrineFirstCircle = ShrineFirstCircle + ActionObjectType,
    ActionShipwreck = Shipwreck + ActionObjectType,
    ActionSeaChest = SeaChest + ActionObjectType,
    ActionDesertTent = DesertTent + ActionObjectType,
    ActionCastle = Castle + ActionObjectType,
    ActionStoneLiths = StoneLiths + ActionObjectType,
    ActionWagonCamp = WagonCamp + ActionObjectType,
    ActionHutOfMagi = HutOfMagi + ActionObjectType,
    ActionWhirlpool = Whirlpool + ActionObjectType,
    ActionWindmill = Windmill + ActionObjectType,
    ActionArtifact = Artifact + ActionObjectType,
    ActionMermaid = Mermaid + ActionObjectType,
    ActionBoat = Boat + ActionObjectType,
    ActionRandomUltimateArtifact = RandomUltimateArtifact + ActionObjectType,
    ActionRandomArtifact = RandomArtifact + ActionObjectType,
    ActionRandomResource = RandomResource + ActionObjectType,
    ActionRandomMonster = RandomMonster + ActionObjectType,
    ActionRandomTown = RandomTown + ActionObjectType,
    ActionRandomCastle = RandomCastle + ActionObjectType,
    ActionEyeOfMagi = EyeOfMagi + ActionObjectType,
    ActionRandomMonsterWeak = RandomMonsterWeak + ActionObjectType,
    ActionRandomMonsterMedium = RandomMonsterMedium + ActionObjectType,
    ActionRandomMonsterStrong = RandomMonsterStrong + ActionObjectType,
    ActionRandomMonsterVeryStrong = RandomMonsterVeryStrong + ActionObjectType,
    ActionHero = Hero + ActionObjectType,
    ActionNothingSpecial = NothingSpecial + ActionObjectType,
    ActionMossyRock = MossyRock + ActionObjectType,
    ActionWatchTower = WatchTower + ActionObjectType,
    ActionTreeHouse = TreeHouse + ActionObjectType,
    ActionTreeCity = TreeCity + ActionObjectType,
    ActionRuins = Ruins + ActionObjectType,
    ActionFort = Fort + ActionObjectType,
    ActionTradingPost = TradingPost + ActionObjectType,
    ActionAbandonedMine = AbandonedMine + ActionObjectType,
    ActionSirens = Sirens + ActionObjectType,
    ActionStandingStones = StandingStones + ActionObjectType,
    ActionIdol = Idol + ActionObjectType,
    ActionTreeOfKnowledge = TreeOfKnowledge + ActionObjectType,
    ActionWitchDoctorsHut = WitchDoctorsHut + ActionObjectType,
    ActionTemple = Temple + ActionObjectType,
    ActionHillFort = HillFort + ActionObjectType,
    ActionHalflingHole = HalflingHole + ActionObjectType,
    ActionMercenaryCamp = MercenaryCamp + ActionObjectType,
    ActionShrineSecondCircle = ShrineSecondCircle + ActionObjectType,
    ActionShrineThirdCircle = ShrineThirdCircle + ActionObjectType,
    ActionPyramid = Pyramid + ActionObjectType,
    ActionCityOfDead = CityOfDead + ActionObjectType,
    ActionExcavation = Excavation + ActionObjectType,
    ActionSphinx = Sphinx + ActionObjectType,
    ActionWagon = Wagon + ActionObjectType,
    ActionTarPit = TarPit + ActionObjectType,
    ActionArtesianSpring = ArtesianSpring + ActionObjectType,
    ActionTrollBridge = TrollBridge + ActionObjectType,
    ActionWateringHole = WateringHole + ActionObjectType,
    ActionWitchsHut = WitchsHut + ActionObjectType,
    ActionXanadu = Xanadu + ActionObjectType,
    ActionCave = Cave + ActionObjectType,
    ActionLeanTo = LeanTo + ActionObjectType,
    ActionMagellansMaps = MagellansMaps + ActionObjectType,
    ActionFlotsam = Flotsam + ActionObjectType,
    ActionDerelictShip = DerelictShip + ActionObjectType,
    ActionShipwreckSurvivor = ShipwreckSurvivor + ActionObjectType,
    ActionBottle = Bottle + ActionObjectType,
    ActionMagicWell = MagicWell + ActionObjectType,
    ActionMagicGarden = MagicGarden + ActionObjectType,
    ActionObservationTower = ObservationTower + ActionObjectType,
    ActionFreemansFoundry = FreemansFoundry + ActionObjectType,
    ActionReefs = Reefs + ActionObjectType,
    ActionTrees = Trees + ActionObjectType,
    ActionMountains = Mountains + ActionObjectType,
    ActionVolcano = Volcano + ActionObjectType,
    ActionFlowers = Flowers + ActionObjectType,
    ActionRock = Rock + ActionObjectType,
    ActionWaterLake = WaterLake + ActionObjectType,
    ActionMandrake = Mandrake + ActionObjectType,
    ActionDeadTree = DeadTree + ActionObjectType,
    ActionStump = Stump + ActionObjectType,
    ActionCrater = Crater + ActionObjectType,
    ActionCactus = Cactus + ActionObjectType,
    ActionMound = Mound + ActionObjectType,
    ActionDune = Dune + ActionObjectType,
    ActionLavaPool = LavaPool + ActionObjectType,
    ActionShrub = Shrub + ActionObjectType,
    ActionArena = Arena + ActionObjectType,
    ActionBarrowMounds = BarrowMounds + ActionObjectType,
    ActionRandomArtifactTreasure = RandomArtifactTreasure + ActionObjectType,
    ActionRandomArtifactMinor = RandomArtifactMinor + ActionObjectType,
    ActionRandomArtifactMajor = RandomArtifactMajor + ActionObjectType,
    ActionBarrier = Barrier + ActionObjectType,
    ActionTravellerTent = TravellerTent + ActionObjectType,
    ActionExpansionDwelling = ExpansionDwelling + ActionObjectType,
    ActionExpansionObject = ExpansionObject + ActionObjectType,
    ActionJail = Jail + ActionObjectType,
    ActionFireAltar = FireAltar + ActionObjectType,
    ActionAirAltar = AirAltar + ActionObjectType,
    ActionEarthAltar = EarthAltar + ActionObjectType,
    ActionWaterAltar = WaterAltar + ActionObjectType,

    /// <summary>
    /// Boundary between the original game's objects and the Resurrection ones,
    /// <c>MP2::OBJ_RESURRECTION_OBJECT_TYPE</c>. Never store this value itself on a map.
    /// </summary>
    ResurrectionObjectType = 256,

    SwampyLake = 257,
    FrozenLake = 258,
    BlackCat = 259,
    Barrel = 260,

    ActionSwampyLake = SwampyLake + ActionObjectType,
    ActionFrozenLake = FrozenLake + ActionObjectType,
    ActionBlackCat = BlackCat + ActionObjectType,
    ActionBarrel = Barrel + ActionObjectType,
}

/// <summary>
/// Helpers around <see cref="Mp2ObjectType"/>, ported from the predicates at the end of
/// <c>mp2.h</c> that the map loader needs.
/// </summary>
public static partial class Mp2ObjectTypes
{
    /// <summary>
    /// <c>MP2::isOffGameActionObject</c>: whether the type carries the action bit. Note that this is a
    /// plain bit test, so it answers <see langword="true"/> for the Resurrection range as well —
    /// exactly like the reference.
    /// </summary>
    public static bool IsActionObject(Mp2ObjectType type) =>
        ((ushort)type & (ushort)Mp2ObjectType.ActionObjectType) == (ushort)Mp2ObjectType.ActionObjectType;

    /// <summary>
    /// <c>MP2::getBaseActionObjectType</c>: the action variant of <paramref name="type"/>, or the value
    /// itself when there is no action counterpart.
    /// </summary>
    /// <remarks>
    /// The action variant does not exist for every object: the reference keeps a list of the types that
    /// never carry the action bit (<c>isObjectCanBeAction</c>, <c>mp2.cpp:38</c> — the terrain-like
    /// decorations such as trees, mountains and lakes), and the caller is expected to compare the result
    /// with the input to find out whether a counterpart exists. Returning <c>type | 128</c> for those
    /// would make <see cref="IsActionObject"/> answer <see langword="true"/> for a tree, which
    /// <c>Tile.UpdatePassability</c> reads as "the tile below is a tall action object".
    /// </remarks>
    /// <param name="type">Object type to correct.</param>
    /// <returns>The action variant, or <paramref name="type"/> when there is none.</returns>
    public static Mp2ObjectType BaseActionType(Mp2ObjectType type)
    {
        if (IsActionObject(type))
        {
            return type;
        }

        if (!CanBeActionObject(type))
        {
            return type;
        }

        return (Mp2ObjectType)((ushort)type | (ushort)Mp2ObjectType.ActionObjectType);
    }

    /// <summary>
    /// <c>isObjectCanBeAction</c> of the reference (<c>mp2.cpp:38</c>): <see langword="false"/> for the
    /// objects that have no action counterpart. The list is the reference's — the decorations
    /// <c>MP2::OBJ_COAST</c> through <c>MP2::OBJ_FROZEN_LAKE</c> — and must not be "simplified" to
    /// "everything can be an action object": <c>getBaseActionObjectType</c> is what tells an action
    /// object apart from its non-action twin when the map only stores the non-action value.
    /// </summary>
    /// <param name="type">Object type to test.</param>
    /// <returns><c>true</c> when the type has an action variant.</returns>
    public static bool CanBeActionObject(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.None => false,
        Mp2ObjectType.Coast => false,
        Mp2ObjectType.NothingSpecial => false,
        Mp2ObjectType.MossyRock => false,
        Mp2ObjectType.TarPit => false,
        Mp2ObjectType.Reefs => false,
        Mp2ObjectType.Trees => false,
        Mp2ObjectType.Mountains => false,
        Mp2ObjectType.Volcano => false,
        Mp2ObjectType.Flowers => false,
        Mp2ObjectType.Rock => false,
        Mp2ObjectType.WaterLake => false,
        Mp2ObjectType.Mandrake => false,
        Mp2ObjectType.DeadTree => false,
        Mp2ObjectType.Stump => false,
        Mp2ObjectType.Crater => false,
        Mp2ObjectType.Cactus => false,
        Mp2ObjectType.Mound => false,
        Mp2ObjectType.Dune => false,
        Mp2ObjectType.LavaPool => false,
        Mp2ObjectType.Shrub => false,
        Mp2ObjectType.SwampyLake => false,
        Mp2ObjectType.FrozenLake => false,
        _ => true,
    };

    /// <summary>
    /// <c>MP2::isWeekLife</c> (<c>mp2.cpp:511</c>): the objects that are refreshed at the beginning of
    /// every week — the weekly production buildings, the monster dwellings and (for the AI) a hero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list is the reference's and is compared against the <b>non-action</b> values, because the
    /// reference calls it with <c>tile.getMainObjectType( false )</c>. Passing an <c>Action*</c> value
    /// therefore answers <see langword="false"/> — that is the reference behaviour, not an oversight of
    /// the port.
    /// </para>
    /// <para>
    /// The hero case looks odd in a list of "life" objects: it is there for the AI, which uses the
    /// predicate to decide when a hero's movable points are refilled (<c>ai_planner_hero.cpp:823</c>).
    /// </para>
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object is refreshed weekly.</returns>
    public static bool IsWeekLife(Mp2ObjectType type) => type switch
    {
        // Weekly production.
        Mp2ObjectType.ArtesianSpring => true,
        Mp2ObjectType.MagicGarden => true,
        Mp2ObjectType.Stables => true,
        Mp2ObjectType.WaterWheel => true,
        Mp2ObjectType.Windmill => true,

        // Monster dwellings.
        Mp2ObjectType.AirAltar => true,
        Mp2ObjectType.ArcherHouse => true,
        Mp2ObjectType.BarrowMounds => true,
        Mp2ObjectType.Cave => true,
        Mp2ObjectType.CityOfDead => true,
        Mp2ObjectType.DesertTent => true,
        Mp2ObjectType.DragonCity => true,
        Mp2ObjectType.DwarfCottage => true,
        Mp2ObjectType.EarthAltar => true,
        Mp2ObjectType.Excavation => true,
        Mp2ObjectType.FireAltar => true,
        Mp2ObjectType.GoblinHut => true,
        Mp2ObjectType.HalflingHole => true,
        Mp2ObjectType.PeasantHut => true,
        Mp2ObjectType.Ruins => true,
        Mp2ObjectType.TreeCity => true,
        Mp2ObjectType.TreeHouse => true,
        Mp2ObjectType.TrollBridge => true,
        Mp2ObjectType.WagonCamp => true,
        Mp2ObjectType.WatchTower => true,
        Mp2ObjectType.WaterAltar => true,

        // For the AI: a hero's movement is refilled with the same weekly rhythm.
        Mp2ObjectType.Hero => true,
        _ => false,
    };

    /// <summary>
    /// <c>MP2::isBattleLife</c> (<c>mp2.cpp:551</c>): the objects that give a temporary luck or morale
    /// bonus which lasts until the next battle.
    /// </summary>
    /// <remarks>
    /// Like <see cref="IsWeekLife"/> this is compared against the non-action values, and
    /// <c>MP2::OBJ_BLACK_CAT</c> is in the list because it changes both morale and luck.
    /// </remarks>
    /// <param name="type">Object type to test.</param>
    /// <returns><see langword="true"/> when the object's bonus is consumed by a battle.</returns>
    public static bool IsBattleLife(Mp2ObjectType type) => type switch
    {
        // Luck modifiers.
        Mp2ObjectType.FaerieRing => true,
        Mp2ObjectType.Fountain => true,
        Mp2ObjectType.Idol => true,
        Mp2ObjectType.Pyramid => true,

        // Morale modifiers.
        Mp2ObjectType.Buoy => true,
        Mp2ObjectType.DerelictShip => true,
        Mp2ObjectType.Graveyard => true,
        Mp2ObjectType.Mermaid => true,
        Mp2ObjectType.Oasis => true,
        Mp2ObjectType.Shipwreck => true,
        Mp2ObjectType.Temple => true,
        Mp2ObjectType.WateringHole => true,

        // Both.
        Mp2ObjectType.BlackCat => true,
        _ => false,
    };

    /// <summary>
    /// <c>MP2::doesObjectNeedExtendedMetadata</c>: these objects store a UID in <c>quantity1</c> /
    /// <c>quantity2</c> that points at an entry of the trailing information blocks.
    /// </summary>
    public static bool NeedsExtendedMetadata(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ActionBottle => true,
        Mp2ObjectType.ActionCastle => true,
        Mp2ObjectType.ActionEvent => true,
        Mp2ObjectType.ActionHero => true,
        Mp2ObjectType.ActionJail => true,
        Mp2ObjectType.ActionRandomCastle => true,
        Mp2ObjectType.ActionRandomTown => true,
        Mp2ObjectType.ActionSign => true,
        Mp2ObjectType.ActionSphinx => true,
        _ => false,
    };

    /// <summary>
    /// <c>MP2::doesObjectContainMetadata</c>: every object whose <c>quantity</c> bits mean something.
    /// The list is used to warn about maps that store metadata on objects which cannot carry it.
    /// </summary>
    public static bool ContainsMetadata(Mp2ObjectType type)
    {
        if (NeedsExtendedMetadata(type))
        {
            return true;
        }

        return type switch
        {
            // Only spell scrolls need the spell id here.
            Mp2ObjectType.ActionArtifact => true,

            // The colour of the object is stored but never used.
            Mp2ObjectType.ActionBarrier => true,
            Mp2ObjectType.ActionTravellerTent => true,

            // The expansion identifies its objects through these bytes.
            Mp2ObjectType.ActionExpansionDwelling => true,
            Mp2ObjectType.ActionExpansionObject => true,

            // The number of monsters.
            Mp2ObjectType.ActionMonster => true,
            Mp2ObjectType.ActionRandomMonster => true,
            Mp2ObjectType.ActionRandomMonsterWeak => true,
            Mp2ObjectType.ActionRandomMonsterMedium => true,
            Mp2ObjectType.ActionRandomMonsterStrong => true,
            Mp2ObjectType.ActionRandomMonsterVeryStrong => true,

            // The radius of the ultimate artifact, placed by the engine later on.
            Mp2ObjectType.ActionRandomUltimateArtifact => true,
            _ => false,
        };
    }
}
