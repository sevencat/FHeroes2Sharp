namespace FHeroes2.Assets.Maps;

/// <summary>
/// <c>MP2::StringObject</c>: the name the game shows for a kind of object, the table behind the
/// "object under the pointer" line of the reference and the header of the editor information box.
/// The reference writes it as one <c>switch</c> over the type with the action bit masked off, so every
/// kind is named once and <c>ActionArtifact</c> answers with the name of <c>Artifact</c>.
/// </summary>
/// <remarks>
/// A handful of entries are counted in the reference (<c>_n( "Lighthouse", "Lighthouses", count )</c>
/// for the lighthouse, the water wheel, the windmill and the magic garden); this port keeps the
/// singular, since what it names is always one object. Types the table does not know keep the name of
/// the enum member, so a map that carries an unexpected byte is still described instead of asserting
/// like the reference does.
/// </remarks>
public static class Mp2ObjectName
{
    /// <summary>
    /// The name of <paramref name="type"/>, the string <c>MP2::StringObject</c> returns for it.
    /// </summary>
    /// <param name="type">Type of the object, action bit included or not.</param>
    /// <returns>The English name of the kind of object.</returns>
    public static string Of(Mp2ObjectType type)
    {
        Mp2ObjectType plain = (Mp2ObjectType)((ushort)type & ~(ushort)Mp2ObjectType.ActionObjectType);

        return plain switch
        {
            Mp2ObjectType.None => "No object",

            // The objects of the original game, in the order of MP2::MapObjectType.
            Mp2ObjectType.AlchemistLab => "Alchemist Lab",
            Mp2ObjectType.Sign => "Sign",
            Mp2ObjectType.Buoy => "Buoy",
            Mp2ObjectType.Skeleton => "Skeleton",
            Mp2ObjectType.DaemonCave => "Daemon Cave",
            Mp2ObjectType.TreasureChest => "Treasure Chest",
            Mp2ObjectType.FaerieRing => "Faerie Ring",
            Mp2ObjectType.Campfire => "Campfire",
            Mp2ObjectType.Fountain => "Fountain",
            Mp2ObjectType.Gazebo => "Gazebo",
            Mp2ObjectType.GenieLamp => "Genie Lamp",
            Mp2ObjectType.Graveyard => "Graveyard",
            Mp2ObjectType.ArcherHouse => "Archer's House",
            Mp2ObjectType.GoblinHut => "Goblin Hut",
            Mp2ObjectType.DwarfCottage => "Dwarf Cottage",
            Mp2ObjectType.PeasantHut => "Peasant Hut",
            Mp2ObjectType.Stables => "Stables",
            Mp2ObjectType.AlchemistTower => "Alchemist's Tower",
            Mp2ObjectType.Event => "Event",
            Mp2ObjectType.DragonCity => "Dragon City",
            Mp2ObjectType.Lighthouse => "Lighthouse",
            Mp2ObjectType.WaterWheel => "Water Wheel",
            Mp2ObjectType.Mine => "Mine",
            Mp2ObjectType.Monster => "Monster",
            Mp2ObjectType.Obelisk => "Obelisk",
            Mp2ObjectType.Oasis => "Oasis",
            Mp2ObjectType.Resource => "Resource",

            // The beach is the one entry of the non action range the game treats as an object.
            Mp2ObjectType.Coast => "Beach",

            Mp2ObjectType.Sawmill => "Sawmill",
            Mp2ObjectType.Oracle => "Oracle",
            Mp2ObjectType.ShrineFirstCircle => "Shrine of the First Circle",
            Mp2ObjectType.Shipwreck => "Shipwreck",
            Mp2ObjectType.SeaChest => "Sea Chest",
            Mp2ObjectType.DesertTent => "Desert Tent",
            Mp2ObjectType.Castle => "Castle",
            Mp2ObjectType.StoneLiths => "Stone Liths",
            Mp2ObjectType.WagonCamp => "Wagon Camp",
            Mp2ObjectType.HutOfMagi => "Hut of the Magi",
            Mp2ObjectType.Whirlpool => "Whirlpool",
            Mp2ObjectType.Windmill => "Windmill",
            Mp2ObjectType.Artifact => "Artifact",
            Mp2ObjectType.Mermaid => "Mermaid",
            Mp2ObjectType.Boat => "Boat",
            Mp2ObjectType.RandomUltimateArtifact => "Random Ultimate Artifact",
            Mp2ObjectType.RandomArtifact => "Random Artifact",
            Mp2ObjectType.RandomResource => "Random Resource",
            Mp2ObjectType.RandomMonster => "Random Monster",
            Mp2ObjectType.RandomTown => "Random Town",
            Mp2ObjectType.RandomCastle => "Random Castle",
            Mp2ObjectType.EyeOfMagi => "Eye of the Magi",
            Mp2ObjectType.RandomMonsterWeak => "Random Monster - weak",
            Mp2ObjectType.RandomMonsterMedium => "Random Monster - medium",
            Mp2ObjectType.RandomMonsterStrong => "Random Monster - strong",
            Mp2ObjectType.RandomMonsterVeryStrong => "Random Monster - very strong",
            Mp2ObjectType.Hero => "Hero",
            Mp2ObjectType.NothingSpecial => "Nothing Special",
            Mp2ObjectType.MossyRock => "Mossy Rock",
            Mp2ObjectType.WatchTower => "Watch Tower",
            Mp2ObjectType.TreeHouse => "Tree House",
            Mp2ObjectType.TreeCity => "Tree City",
            Mp2ObjectType.Ruins => "Ruins",
            Mp2ObjectType.Fort => "Fort",
            Mp2ObjectType.TradingPost => "Trading Post",
            Mp2ObjectType.AbandonedMine => "Abandoned Mine",
            Mp2ObjectType.Sirens => "Sirens",
            Mp2ObjectType.StandingStones => "Standing Stones",
            Mp2ObjectType.Idol => "Idol",
            Mp2ObjectType.TreeOfKnowledge => "Tree of Knowledge",
            Mp2ObjectType.WitchDoctorsHut => "Witch Doctor's Hut",
            Mp2ObjectType.Temple => "Temple",
            Mp2ObjectType.HillFort => "Hill Fort",
            Mp2ObjectType.HalflingHole => "Halfling Hole",
            Mp2ObjectType.MercenaryCamp => "Mercenary Camp",
            Mp2ObjectType.ShrineSecondCircle => "Shrine of the Second Circle",
            Mp2ObjectType.ShrineThirdCircle => "Shrine of the Third Circle",
            Mp2ObjectType.Pyramid => "Pyramid",
            Mp2ObjectType.CityOfDead => "City of the Dead",
            Mp2ObjectType.Excavation => "Excavation",
            Mp2ObjectType.Sphinx => "Sphinx",
            Mp2ObjectType.Wagon => "Wagon",
            Mp2ObjectType.TarPit => "Tar Pit",
            Mp2ObjectType.ArtesianSpring => "Artesian Spring",
            Mp2ObjectType.TrollBridge => "Troll Bridge",
            Mp2ObjectType.WateringHole => "Watering Hole",
            Mp2ObjectType.WitchsHut => "Witch's Hut",
            Mp2ObjectType.Xanadu => "Xanadu",
            Mp2ObjectType.Cave => "Cave",
            Mp2ObjectType.LeanTo => "Lean-To",
            Mp2ObjectType.MagellansMaps => "Magellan's Maps",
            Mp2ObjectType.Flotsam => "Flotsam",
            Mp2ObjectType.DerelictShip => "Derelict Ship",
            Mp2ObjectType.ShipwreckSurvivor => "Shipwreck Survivor",
            Mp2ObjectType.Bottle => "Bottle",
            Mp2ObjectType.MagicWell => "Magic Well",
            Mp2ObjectType.MagicGarden => "Magic Garden",
            Mp2ObjectType.ObservationTower => "Observation Tower",
            Mp2ObjectType.FreemansFoundry => "Freeman's Foundry",

            // Terrain decorations, which the map stores as objects of their own.
            Mp2ObjectType.Reefs => "Reefs",
            Mp2ObjectType.Trees => "Trees",
            Mp2ObjectType.Mountains => "Mountains",
            Mp2ObjectType.Volcano => "Volcano",
            Mp2ObjectType.Flowers => "Flowers",
            Mp2ObjectType.Rock => "Rock",
            Mp2ObjectType.WaterLake => "Water Lake",
            Mp2ObjectType.Mandrake => "Mandrake",
            Mp2ObjectType.DeadTree => "Dead Tree",
            Mp2ObjectType.Stump => "Stump",
            Mp2ObjectType.Crater => "Crater",
            Mp2ObjectType.Cactus => "Cactus",
            Mp2ObjectType.Mound => "Mound",
            Mp2ObjectType.Dune => "Dune",
            Mp2ObjectType.LavaPool => "Lava Pool",
            Mp2ObjectType.Shrub => "Shrub",

            Mp2ObjectType.Arena => "Arena",
            Mp2ObjectType.BarrowMounds => "Barrow Mounds",
            Mp2ObjectType.RandomArtifactTreasure => "Random Artifact - Treasure",
            Mp2ObjectType.RandomArtifactMinor => "Random Artifact - Minor",
            Mp2ObjectType.RandomArtifactMajor => "Random Artifact - Major",
            Mp2ObjectType.Barrier => "Barrier",
            Mp2ObjectType.TravellerTent => "Traveller's Tent",
            Mp2ObjectType.ExpansionDwelling => "Expansion Dwelling",
            Mp2ObjectType.ExpansionObject => "Expansion Object",
            Mp2ObjectType.Jail => "Jail",
            Mp2ObjectType.FireAltar => "Fire Summoning Altar",
            Mp2ObjectType.AirAltar => "Air Summoning Altar",
            Mp2ObjectType.EarthAltar => "Earth Summoning Altar",
            Mp2ObjectType.WaterAltar => "Water Summoning Altar",

            // The objects The Price of Loyalty added, above OBJ_RESURRECTION_OBJECT_TYPE.
            Mp2ObjectType.SwampyLake => "Swampy Lake",
            Mp2ObjectType.FrozenLake => "Frozen Lake",
            Mp2ObjectType.BlackCat => "Black Cat",
            Mp2ObjectType.Barrel => "Barrel",

            // Not in the reference: it asserts on an unknown type, a map is allowed to be wrong.
            _ => type.ToString(),
        };
    }
}
