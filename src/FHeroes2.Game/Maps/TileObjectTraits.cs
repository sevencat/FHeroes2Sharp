using System.Diagnostics;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The tile level object classification helpers of <c>maps_tiles.cpp</c> plus the two <c>MP2::</c>
/// predicates that need the direction type.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the helpers in an anonymous namespace inside <c>maps_tiles.cpp</c>
/// (<c>isValidShadowSprite</c>, <c>isObjectPartShadow</c>, <c>isShortObject</c>,
/// <c>isDetachedObjectType</c>, <c>isCombinedObject</c>) and derives the last two from
/// <c>MP2::</c> (<c>isOffGameActionObject</c> is already <see cref="Mp2ObjectTypes.IsActionObject"/>,
/// <c>getActionObjectDirection</c> is here). They are gathered into one static class because they all
/// answer the same question: what kind of thing is this sprite on this tile.
/// </para>
/// <para>
/// <c>isValidShadowSprite</c> takes an <c>ICN::</c> id in the reference; the port switches on
/// <see cref="Mp2ObjectIcnType"/> directly, because <c>MP2::getIcnIdFromObjectIcnType</c> is a one to one
/// mapping for every type that reaches this table (<c>ICN::UNKNOWN</c> only comes back for the
/// Price of Loyalty archives when the expansion is missing, which is an asset question, not a map one).
/// </para>
/// </remarks>
public static class TileObjectTraits
{
    // The tables below are copied verbatim from isValidShadowSprite(), sorted as in the reference.
    private static readonly byte[] MountainsShadowIndexes = [0, 5, 11, 17, 21, 26, 32, 38, 42, 45, 49, 52, 55, 59, 62, 65, 68, 71, 74, 75, 79, 80];

    private static readonly byte[] DirtMountainsShadowIndexes = [0, 5, 11, 17, 21, 26, 32, 38, 42, 47, 53, 62, 68, 72, 75, 79, 82, 85, 89, 92, 95, 98, 101, 104, 105, 109, 110];

    private static readonly byte[] TreesShadowIndexes = [0, 3, 7, 10, 13, 17, 20, 23, 26, 29, 32, 34];

    private static readonly byte[] CrackShadowIndexes = [2, 9, 13, 15, 20, 23, 28, 33, 36, 39, 45, 48, 51, 54, 56, 73, 75, 79, 200, 201, 207, 237];

    private static readonly byte[] DirtShadowIndexes =
    [
        0, 1, 5, 6, 14, 47, 52, 59, 62, 65, 68, 70, 72, 75, 78, 81, 84, 87, 91, 94, 97, 100, 103, 111, 114, 117,
        126, 128, 136, 149, 150, 158, 161, 162, 163, 164, 165, 166, 167, 168, 177, 178, 179, 180, 181, 182, 183, 184, 193, 196, 200,
    ];

    private static readonly byte[] DesertShadowIndexes = [11, 13, 16, 19, 23, 25, 27, 29, 33, 35, 38, 41, 44, 47, 50, 52, 54, 55, 56, 57, 58, 59, 60, 71, 75, 77, 80, 86, 103, 115, 118];

    private static readonly byte[] Grass2ShadowIndexes =
    [
        5, 14, 19, 20, 28, 31, 32, 33, 34, 35, 36, 37, 38, 47, 48, 49, 50, 51, 52, 53, 54, 70, 71, 72, 73, 74, 75,
        76, 77, 78, 79, 80, 81, 82, 83, 91, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 121, 124, 128,
    ];

    private static readonly byte[] GrassShadowIndexes = [0, 4, 29, 32, 36, 39, 42, 44, 46, 48, 76, 82, 88, 92, 94, 98, 102, 105, 108, 111, 113, 120, 124, 128, 134, 138, 141, 143, 145, 147];

    private static readonly byte[] Multi2ShadowIndexes =
    [
        14, 17, 20, 24, 42, 43, 49, 50, 60, 71, 72, 113, 115, 118, 121, 123, 127, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147,
        148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 164, 180, 181, 182, 183, 184, 185, 186, 189, 199, 200, 202, 206,
    ];

    private static readonly byte[] MultiShadowIndexes =
    [
        1, 3, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 57,
        61, 67, 68, 75, 77, 79, 81, 83, 97, 98, 99, 100, 101, 102, 103, 105, 106, 107, 108, 109, 110, 113, 115, 121, 122, 124, 125, 126, 127, 128, 129, 130,
    ];

    private static readonly byte[] SnowShadowIndexes =
    [
        21, 25, 29, 31, 33, 36, 40, 48, 54, 59, 63, 67, 70, 73, 76, 79, 101, 104, 105, 106, 107,
        108, 109, 110, 111, 120, 121, 122, 123, 124, 125, 126, 127, 137, 140, 142, 144, 148, 193, 203, 207,
    ];

    private static readonly byte[] SwampShadowIndexes =
    [
        2, 3, 14, 15, 16, 17, 18, 19, 20, 21, 31, 43, 44, 45, 46, 47, 48, 49, 66,
        83, 125, 127, 130, 132, 136, 141, 163, 170, 175, 178, 195, 197, 202, 204, 207, 211, 215,
    ];

    private static readonly byte[] WaterShadowIndexes =
    [
        12, 13, 14, 15, 16, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41,
        42, 43, 44, 52, 55, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 166, 167,
        168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 184, 188, 189, 190, 191, 192, 193, 194, 240,
    ];

    private static readonly byte[] Location1ShadowIndexes = [1, 2, 32, 33, 34, 35, 36, 37, 38, 39, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 72, 78, 79, 83, 84, 112, 116, 120, 124, 125, 129, 133];

    private static readonly byte[] Location2ShadowIndexes = [2, 7, 10, 11, 12, 13, 14, 15, 16, 17, 18, 47, 48, 49, 50, 51, 52, 53, 54, 55, 83, 84, 85, 86, 87, 88, 89, 90, 91];

    private static readonly byte[] Location3ShadowIndexes =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 41, 42, 43, 44, 45, 46, 47,
        48, 49, 59, 65, 71, 77, 83, 89, 95, 101, 108, 109, 112, 113, 116, 117, 120, 121, 124, 125, 128, 129, 132, 133, 136, 137,
    ];

    private static readonly byte[] LavaShadowIndexes = [45, 49, 79, 80, 81, 82, 109, 113, 116];

    private static readonly byte[] Lava2ShadowIndexes =
    [
        7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 29, 34, 38, 39, 43, 44, 45, 46,
        47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 72, 77, 78,
    ];

    private static readonly byte[] Lava3ShadowIndexes =
    [
        1, 2, 3, 4, 16, 17, 18, 19, 31, 32, 33, 34, 38, 46, 47, 48, 49, 50, 57, 58, 59, 61, 62, 63, 64, 76, 77,
        91, 92, 93, 106, 107, 108, 109, 110, 111, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133,
        134, 136, 137, 138, 139, 142, 143, 144, 145, 146, 147, 148, 149, 166, 167, 168, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186,
        187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213,
        214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233, 234, 235, 236, 237, 238, 239, 243,
    ];

    /// <summary>
    /// Whether a sprite is a shadow, <c>isValidShadowSprite()</c> (<c>maps_tiles.cpp:57</c>). Shadows do
    /// not block movement and are drawn on the shadow layer.
    /// </summary>
    /// <param name="objectIcnType">ICN of the sprite; the place where the reference takes the ICN id.</param>
    /// <param name="icnIndex">Sprite index inside the ICN.</param>
    /// <returns><c>true</c> when the sprite is a shadow.</returns>
    public static bool IsValidShadowSprite(Mp2ObjectIcnType objectIcnType, byte icnIndex) => objectIcnType switch
    {
        // No objects on the tile at all.
        Mp2ObjectIcnType.Unknown => false,
        Mp2ObjectIcnType.MtnDsrt or Mp2ObjectIcnType.MtnGras or Mp2ObjectIcnType.MtnLava or Mp2ObjectIcnType.MtnMult or Mp2ObjectIcnType.MtnSnow or Mp2ObjectIcnType.MtnSwmp => In(MountainsShadowIndexes, icnIndex),
        Mp2ObjectIcnType.MtnCrck or Mp2ObjectIcnType.MtnDirt => In(DirtMountainsShadowIndexes, icnIndex),
        Mp2ObjectIcnType.TreDeci or Mp2ObjectIcnType.TreVil or Mp2ObjectIcnType.TreFall or Mp2ObjectIcnType.TreFir or Mp2ObjectIcnType.TreJngl or Mp2ObjectIcnType.TreSnow => In(TreesShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnCrck => In(CrackShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnDirt => In(DirtShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnDsrt => In(DesertShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnGra2 => In(Grass2ShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnGras => In(GrassShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnMul2 => In(Multi2ShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnMult => In(MultiShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnSnow => In(SnowShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnSwmp => In(SwampShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnWat2 => icnIndex == 1,
        Mp2ObjectIcnType.ObjnWatr => In(WaterShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnArti or Mp2ObjectIcnType.ObjnRsrc => icnIndex % 2 == 0,
        Mp2ObjectIcnType.ObjnTwrd => icnIndex > 31,
        Mp2ObjectIcnType.XLoc1 => In(Location1ShadowIndexes, icnIndex),
        Mp2ObjectIcnType.XLoc2 => In(Location2ShadowIndexes, icnIndex),
        Mp2ObjectIcnType.XLoc3 => In(Location3ShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnLava => In(LavaShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnLav2 => In(Lava2ShadowIndexes, icnIndex),
        Mp2ObjectIcnType.ObjnLav3 => In(Lava3ShadowIndexes, icnIndex),

        // Every sprite in these ICNs is a shadow.
        Mp2ObjectIcnType.ObjnTwsh => true,

        // These ICNs never contain shadows.
        Mp2ObjectIcnType.Boat32
            or Mp2ObjectIcnType.ExtraOvr
            or Mp2ObjectIcnType.Flag32
            or Mp2ObjectIcnType.MiniHero
            or Mp2ObjectIcnType.Mons32
            or Mp2ObjectIcnType.ObjnTown
            or Mp2ObjectIcnType.ObjnTwba
            or Mp2ObjectIcnType.ObjnXtra
            or Mp2ObjectIcnType.Road
            or Mp2ObjectIcnType.Stream => false,
        _ => false,
    };

    /// <summary>
    /// Whether an object part is a shadow, <c>isObjectPartShadow()</c>.
    /// </summary>
    /// <param name="part">Part to test.</param>
    /// <returns><c>true</c> when the part carries a shadow sprite.</returns>
    public static bool IsObjectPartShadow(ObjectPart part) =>
        part.IcnType != Mp2ObjectIcnType.Unknown && IsValidShadowSprite(part.IcnType, part.IcnIndex);

    /// <summary>
    /// Whether an object looks like it has no sprites above its own tile, <c>isShortObject()</c>
    /// (<c>maps_tiles.cpp:223</c>). Such objects may allow middle row moves although they touch the
    /// bottom of their tile.
    /// </summary>
    /// <param name="objectType">Object type to test.</param>
    /// <returns><c>true</c> for the hardcoded short objects.</returns>
    public static bool IsShortObject(Mp2ObjectType objectType) => objectType switch
    {
        // The reference lists the action object and, where the map format can hold it, its non action
        // twin as well: the object tables store the action value, but the editor sometimes writes the
        // plain one.
        Mp2ObjectType.ActionHalflingHole
            or Mp2ObjectType.HalflingHole
            or Mp2ObjectType.ActionLeanTo
            or Mp2ObjectType.WaterLake
            or Mp2ObjectType.ActionTarPit
            or Mp2ObjectType.ActionMercenaryCamp
            or Mp2ObjectType.MercenaryCamp
            or Mp2ObjectType.ActionStandingStones
            or Mp2ObjectType.ActionShrineFirstCircle
            or Mp2ObjectType.ActionShrineSecondCircle
            or Mp2ObjectType.ActionShrineThirdCircle
            or Mp2ObjectType.ActionMagicGarden
            or Mp2ObjectType.ActionRuins
            or Mp2ObjectType.Ruins
            or Mp2ObjectType.ActionSign
            or Mp2ObjectType.ActionIdol
            or Mp2ObjectType.ActionStoneLiths
            or Mp2ObjectType.StoneLiths
            or Mp2ObjectType.ActionWagon
            or Mp2ObjectType.ActionWagonCamp
            or Mp2ObjectType.WagonCamp
            or Mp2ObjectType.ActionGoblinHut
            or Mp2ObjectType.ActionFaerieRing
            or Mp2ObjectType.FaerieRing
            or Mp2ObjectType.ActionBarrier
            or Mp2ObjectType.ActionMagicWell
            or Mp2ObjectType.NothingSpecial => true,
        _ => false,
    };

    /// <summary>
    /// Whether an object ignores the objects below it, <c>isDetachedObjectType()</c>
    /// (<c>maps_tiles.cpp:265</c>).
    /// </summary>
    /// <param name="objectType">Object type to test.</param>
    /// <returns><c>true</c> for the detached object types.</returns>
    public static bool IsDetachedObjectType(Mp2ObjectType objectType) => objectType switch
    {
        Mp2ObjectType.ActionCastle
            or Mp2ObjectType.ActionWagonCamp
            or Mp2ObjectType.ActionFaerieRing
            or Mp2ObjectType.ActionMine
            or Mp2ObjectType.ActionSawmill
            or Mp2ObjectType.ActionWaterAltar
            or Mp2ObjectType.ActionAirAltar
            or Mp2ObjectType.ActionFireAltar
            or Mp2ObjectType.ActionEarthAltar => true,
        _ => false,
    };

    /// <summary>
    /// Whether an object still allows the moves of another object of the same kind,
    /// <c>isCombinedObject()</c> (<c>maps_tiles.cpp:286</c>): trees allow the row above and below until
    /// the next tree starts.
    /// </summary>
    /// <param name="objectType">Object type to test.</param>
    /// <returns><c>true</c> for trees and craters.</returns>
    public static bool IsCombinedObject(Mp2ObjectType objectType) =>
        objectType is Mp2ObjectType.Trees or Mp2ObjectType.Crater;

    /// <summary>
    /// Whether a sprite is a reef, <c>isValidReefsSprite()</c>: reefs are only impassable in their own
    /// sprites, not in the whole <c>X_LOC2</c> archive.
    /// </summary>
    /// <param name="objectIcnType">ICN of the sprite.</param>
    /// <param name="icnIndex">Sprite index inside the ICN.</param>
    /// <returns><c>true</c> when the sprite is a reef.</returns>
    public static bool IsValidReefsSprite(Mp2ObjectIcnType objectIcnType, byte icnIndex) =>
        objectIcnType == Mp2ObjectIcnType.XLoc2 && icnIndex is >= 111 and <= 135;

    /// <summary>
    /// The directions a hero can enter an action object from, <c>MP2::getActionObjectDirection()</c>
    /// (<c>mp2.cpp:815</c>). Every action object is either fully passable (pickups, events, monsters) or
    /// reachable from the bottom only.
    /// </summary>
    /// <remarks>
    /// The reference asserts and returns <c>DIRECTION_UNKNOWN</c> for anything else in release builds.
    /// The port does the same instead of throwing: an unknown value here means the object tables and
    /// this switch are out of sync, and an impassable tile is a much better failure than a crash in the
    /// middle of a map load.
    /// </remarks>
    /// <param name="objectType">Object type of an action object.</param>
    /// <returns>The passable directions; <see cref="Direction.Unknown"/> for an unexpected type.</returns>
    public static Direction GetActionObjectDirection(Mp2ObjectType objectType)
    {
        switch (objectType)
        {
            case Mp2ObjectType.ActionArtifact:
            case Mp2ObjectType.ActionBarrel:
            case Mp2ObjectType.ActionBarrier:
            case Mp2ObjectType.ActionBlackCat:
            case Mp2ObjectType.ActionBoat:
            case Mp2ObjectType.ActionBottle:
            case Mp2ObjectType.ActionBuoy:
            case Mp2ObjectType.ActionCampfire:
            case Mp2ObjectType.ActionCoast:
            case Mp2ObjectType.ActionEvent:
            case Mp2ObjectType.ActionFlotsam:
            case Mp2ObjectType.ActionGenieLamp:
            case Mp2ObjectType.ActionHero:
            case Mp2ObjectType.ActionJail:
            case Mp2ObjectType.ActionMonster:
            case Mp2ObjectType.ActionRandomArtifact:
            case Mp2ObjectType.ActionRandomArtifactMajor:
            case Mp2ObjectType.ActionRandomArtifactMinor:
            case Mp2ObjectType.ActionRandomArtifactTreasure:
            case Mp2ObjectType.ActionRandomCastle:
            case Mp2ObjectType.ActionRandomMonster:
            case Mp2ObjectType.ActionRandomMonsterMedium:
            case Mp2ObjectType.ActionRandomMonsterStrong:
            case Mp2ObjectType.ActionRandomMonsterVeryStrong:
            case Mp2ObjectType.ActionRandomMonsterWeak:
            case Mp2ObjectType.ActionRandomResource:
            case Mp2ObjectType.ActionRandomTown:
            case Mp2ObjectType.ActionRandomUltimateArtifact:
            case Mp2ObjectType.ActionResource:
            case Mp2ObjectType.ActionSeaChest:
            case Mp2ObjectType.ActionShipwreckSurvivor:
            case Mp2ObjectType.ActionSkeleton:
            case Mp2ObjectType.ActionTreasureChest:
            case Mp2ObjectType.ActionWhirlpool:
                return Directions.All;

            case Mp2ObjectType.ActionAbandonedMine:
            case Mp2ObjectType.ActionAirAltar:
            case Mp2ObjectType.ActionAlchemistLab:
            case Mp2ObjectType.ActionAlchemistTower:
            case Mp2ObjectType.ActionArcherHouse:
            case Mp2ObjectType.ActionArena:
            case Mp2ObjectType.ActionArtesianSpring:
            case Mp2ObjectType.ActionBarrowMounds:
            case Mp2ObjectType.ActionCastle:
            case Mp2ObjectType.ActionCave:
            case Mp2ObjectType.ActionCityOfDead:
            case Mp2ObjectType.ActionDaemonCave:
            case Mp2ObjectType.ActionDerelictShip:
            case Mp2ObjectType.ActionDesertTent:
            case Mp2ObjectType.ActionDragonCity:
            case Mp2ObjectType.ActionDwarfCottage:
            case Mp2ObjectType.ActionEarthAltar:
            case Mp2ObjectType.ActionExcavation:
            case Mp2ObjectType.ActionEyeOfMagi:
            case Mp2ObjectType.ActionFaerieRing:
            case Mp2ObjectType.ActionFireAltar:
            case Mp2ObjectType.ActionFort:
            case Mp2ObjectType.ActionFountain:
            case Mp2ObjectType.ActionFreemansFoundry:
            case Mp2ObjectType.ActionGazebo:
            case Mp2ObjectType.ActionGoblinHut:
            case Mp2ObjectType.ActionGraveyard:
            case Mp2ObjectType.ActionHalflingHole:
            case Mp2ObjectType.ActionHillFort:
            case Mp2ObjectType.ActionHutOfMagi:
            case Mp2ObjectType.ActionIdol:
            case Mp2ObjectType.ActionLeanTo:
            case Mp2ObjectType.ActionLighthouse:
            case Mp2ObjectType.ActionMagellansMaps:
            case Mp2ObjectType.ActionMagicGarden:
            case Mp2ObjectType.ActionMagicWell:
            case Mp2ObjectType.ActionMercenaryCamp:
            case Mp2ObjectType.ActionMermaid:
            case Mp2ObjectType.ActionMine:
            case Mp2ObjectType.ActionOasis:
            case Mp2ObjectType.ActionObelisk:
            case Mp2ObjectType.ActionObservationTower:
            case Mp2ObjectType.ActionOracle:
            case Mp2ObjectType.ActionPeasantHut:
            case Mp2ObjectType.ActionPyramid:
            case Mp2ObjectType.ActionRuins:
            case Mp2ObjectType.ActionSawmill:
            case Mp2ObjectType.ActionShipwreck:
            case Mp2ObjectType.ActionShrineFirstCircle:
            case Mp2ObjectType.ActionShrineSecondCircle:
            case Mp2ObjectType.ActionShrineThirdCircle:
            case Mp2ObjectType.ActionSign:
            case Mp2ObjectType.ActionSirens:
            case Mp2ObjectType.ActionSphinx:
            case Mp2ObjectType.ActionStables:
            case Mp2ObjectType.ActionStandingStones:
            case Mp2ObjectType.ActionStoneLiths:
            case Mp2ObjectType.ActionTemple:
            case Mp2ObjectType.ActionTradingPost:
            case Mp2ObjectType.ActionTravellerTent:
            case Mp2ObjectType.ActionTreeCity:
            case Mp2ObjectType.ActionTreeHouse:
            case Mp2ObjectType.ActionTreeOfKnowledge:
            case Mp2ObjectType.ActionTrollBridge:
            case Mp2ObjectType.ActionWagon:
            case Mp2ObjectType.ActionWagonCamp:
            case Mp2ObjectType.ActionWatchTower:
            case Mp2ObjectType.ActionWaterAltar:
            case Mp2ObjectType.ActionWaterWheel:
            case Mp2ObjectType.ActionWateringHole:
            case Mp2ObjectType.ActionWindmill:
            case Mp2ObjectType.ActionWitchDoctorsHut:
            case Mp2ObjectType.ActionWitchsHut:
            case Mp2ObjectType.ActionXanadu:
                return Directions.CenterRow | Directions.BottomRow;

            default:
                // Did you add a new action object? Please add its passability!
                Debug.Assert(false, $"{objectType} is not a known action object.");

                return Direction.Unknown;
        }
    }

    /// <summary>
    /// Membership test that keeps the tables readable; the arrays hold sprite indexes.
    /// </summary>
    /// <param name="indexes">Table from the reference.</param>
    /// <param name="icnIndex">Sprite index to look for.</param>
    /// <returns><c>true</c> when the table contains the index.</returns>
    private static bool In(byte[] indexes, byte icnIndex) => Array.IndexOf(indexes, icnIndex) >= 0;
}
