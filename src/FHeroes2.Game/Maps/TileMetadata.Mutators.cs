using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The writing half of the tile metadata and the object queries that need no removal, the port of
/// <c>maps_tiles_helper.cpp</c> beside <see cref="TileMetadata"/> (which holds the reading half).
/// </summary>
/// <remarks>
/// <para>
/// The reference names these as free functions in <c>namespace Maps</c>; the port keeps them as members
/// of <see cref="Tile"/> because every call site already has the tile, exactly like <see cref="TileMetadata"/>.
/// </para>
/// <para>
/// <b>Object type and the action bit.</b> The reference compares against <c>MP2::OBJ_MINE</c>, which already
/// carries <c>OBJ_ACTION_OBJECT_TYPE</c>; the port has both values in the enum (<c>Mine</c> and
/// <c>ActionMine</c>) and the tile may hold either while a map is being edited. Every comparison here
/// therefore goes through <see cref="GetNonActionObjectType"/>, which strips the bit when it is there and
/// leaves the type alone otherwise (ADR-133).
/// </para>
/// <para>
/// <b>These readers look through the hero.</b> 参考里它们比的都是 <c>tile.getMainObjectType( false )</c>
/// （<c>ignoreObjectUnderHero: false</c>）：读到的是英雄<b>脚下</b>那个物件。调用场合正是英雄刚踏上这一格、
/// 正要处理它，拿带英雄的类型去比只会读到 <c>OBJ_HERO</c>，判定全部落空。所以这些成员都收 <see cref="World"/>。
/// </para>
/// <para>
/// <b>The removal and the mine half is in <see cref="TileRemoval"/></b> (<c>restoreAbandonedMine</c>,
/// <c>removeMainObjectFromTile</c>, <c>removeObjectFromTileByType</c>, <c>removeObjectFromMapByUID</c>).
/// </para>
/// <para>
/// <b>Not here yet:</b> <c>placeObjectOnTile</c>, which needs the editor's <c>ObjectInfo</c> and the object
/// placement rules. The rest of the file's writers are elsewhere: <c>setInitialObjectInfo</c>,
/// <c>updateObjectInfoTile</c> and the three random placeholders in <c>Tile.InitialObjectInfo.cs</c> /
/// <c>TileWeekLife.cs</c>, the army half in <see cref="TileTroops"/> (<c>getTroopFromTile</c>,
/// <c>updateMonstersOnTile</c>, <c>setMonsterOnTile</c>).
/// </para>
/// </remarks>
public sealed partial class Tile
{
    // ---------------------------------------------------------------- mines

    /// <summary>
    /// The spell that guards a mine, <c>getMineSpellIdFromTile()</c> (<c>maps_tiles_helper.cpp:339</c>).
    /// </summary>
    public SpellType GetMineSpellId(World world)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.Mine)
        {
            Debug.Assert(false, $"A mine spell was asked from a tile that holds {MainObjectType}.");

            return SpellType.None;
        }

        return (SpellType)_metadata[0];
    }

    /// <summary>
    /// Sets the guarding spell of a mine, <c>setMineSpellOnTile()</c> (<c>maps_tiles_helper.cpp:350</c>).
    /// </summary>
    public void SetMineSpell(World world, SpellType spellId)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.Mine)
        {
            Debug.Assert(false, $"A mine spell was set on a tile that holds {MainObjectType}.");

            return;
        }

        if (spellId < SpellType.None)
        {
            Debug.Assert(false, $"A mine spell was set to an invalid id {(int)spellId}.");

            return;
        }

        _metadata[0] = (uint)spellId;
    }

    /// <summary>
    /// Removes the guarding spell of a mine, <c>removeMineSpellFromTile()</c>
    /// (<c>maps_tiles_helper.cpp:367</c>). The reference resets nothing else, so a mine that was already
    /// captured keeps its flag here as well.
    /// </summary>
    public void RemoveMineSpell(World world)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.Mine)
        {
            Debug.Assert(false, $"A mine spell was removed from a tile that holds {MainObjectType}.");

            return;
        }

        _metadata[0] = (uint)SpellType.None;
    }

    // ------------------------------------------------------- spells and shrines

    /// <summary>
    /// Writes the spell of a shrine, a pyramid or a spell scroll, <c>setSpellOnTile()</c>
    /// (<c>maps_tiles_helper.cpp:405</c>).
    /// </summary>
    public void SetSpellOnTile(World world, SpellType spellId)
    {
        switch (GetNonActionObjectType(world))
        {
            case Mp2ObjectType.ShrineFirstCircle:
            case Mp2ObjectType.ShrineSecondCircle:
            case Mp2ObjectType.ShrineThirdCircle:
            case Mp2ObjectType.Pyramid:
                _metadata[0] = (uint)spellId;
                break;

            case Mp2ObjectType.Artifact:
                // Only the spell scroll artifact holds a spell, and it keeps it in the second word.
                Debug.Assert(GetArtifactFromTile(world) == ArtifactType.SpellScroll);
                _metadata[1] = (uint)spellId;
                break;

            default:
                Debug.Assert(false, $"A spell was set on a tile that holds {MainObjectType}.");
                break;
        }
    }

    // ------------------------------------------------------- monster join condition

    /// <summary>
    /// Writes the join condition of wandering monsters, <c>setMonsterOnTileJoinCondition()</c>
    /// (<c>maps_tiles_helper.cpp:426</c>).
    /// </summary>
    /// <remarks>
    /// The reference calls <c>getMainObjectType()</c> here — the variant that does <b>not</b> see through the
    /// hero — so the port needs the world and refuses a tile whose hero hides the monsters.
    /// </remarks>
    public void SetMonsterJoinCondition(World world, int condition)
    {
        if (Mp2ObjectTypes.NonActionType(GetMainObjectType(world)) == Mp2ObjectType.Monster)
        {
            _metadata[2] = unchecked((uint)condition);
            return;
        }

        Debug.Assert(false, $"A join condition was set on a tile that holds {GetMainObjectType(world)}.");
    }

    /// <summary>
    /// Whether monsters on the tile refuse to join, <c>isMonsterOnTileJoinConditionSkip()</c>
    /// (<c>maps_tiles_helper.cpp:437</c>).
    /// </summary>
    public bool IsMonsterJoinConditionSkip(World world) => ReadJoinCondition(world) == MonsterJoinConditions.Skip;

    /// <summary>
    /// Whether monsters on the tile join for free, <c>isMonsterOnTileJoinConditionFree()</c>
    /// (<c>maps_tiles_helper.cpp:448</c>).
    /// </summary>
    public bool IsMonsterJoinConditionFree(World world) => ReadJoinCondition(world) == MonsterJoinConditions.Free;

    private int? ReadJoinCondition(World world)
    {
        if (Mp2ObjectTypes.NonActionType(GetMainObjectType(world)) != Mp2ObjectType.Monster)
        {
            Debug.Assert(false, $"A join condition was read from a tile that holds {GetMainObjectType(world)}.");

            return null;
        }

        // The reference stores -1 in a uint32_t word, so the round trip has to be unchecked.
        return unchecked((int)_metadata[2]);
    }

    // ------------------------------------------------------------- colours of sprites

    /// <summary>
    /// The colour index encoded by the barrier sprite, <c>getColorFromBarrierSprite()</c>
    /// (<c>maps_tiles_helper.cpp:459</c>). Returns 1…8 for the eight colours and 0 for anything else.
    /// </summary>
    public static int GetColorFromBarrierSprite(Mp2ObjectIcnType icnType, byte icnIndex)
    {
        // The colour is really stored in the metadata; while the map is being built we only have the sprite.
        if (icnType == Mp2ObjectIcnType.XLoc3 && icnIndex is >= 60 and <= 102)
        {
            // 60, 66, 72, 78, 84, 90, 96, 102
            return ((icnIndex - 60) / 6) + 1;
        }

        Debug.Assert(false, $"A barrier colour was asked from sprite {icnIndex} of {icnType}.");

        return 0;
    }

    /// <summary>
    /// The colour index encoded by the traveller tent sprite, <c>getColorFromTravellerTentSprite()</c>
    /// (<c>maps_tiles_helper.cpp:473</c>).
    /// </summary>
    public static int GetColorFromTravellerTentSprite(Mp2ObjectIcnType icnType, byte icnIndex)
    {
        if (icnType == Mp2ObjectIcnType.XLoc3 && icnIndex is >= 110 and <= 138)
        {
            // 110, 114, 118, 122, 126, 130, 134, 138
            return ((icnIndex - 110) / 4) + 1;
        }

        Debug.Assert(false, $"A traveller tent colour was asked from sprite {icnIndex} of {icnType}.");

        return 0;
    }

    // ------------------------------------------------------- artifact conditions

    /// <summary>
    /// The secondary skill a guarded artifact asks for, <c>getArtifactSecondarySkillRequirement()</c>
    /// (<c>maps_tiles_helper.cpp:601</c>). Returns <see langword="null"/> when the artifact asks for no skill.
    /// </summary>
    public SecondarySkillLevel? GetArtifactSecondarySkillRequirement(World world)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.Artifact)
        {
            Debug.Assert(false, $"A skill requirement was asked from a tile that holds {MainObjectType}.");

            return null;
        }

        return GetArtifactCaptureCondition(world) switch
        {
            ArtifactCaptureCondition.HaveWisdomSkill => new SecondarySkillLevel(SecondarySkill.Wisdom, SkillLevel.Basic),
            ArtifactCaptureCondition.HaveLeadershipSkill => new SecondarySkillLevel(SecondarySkill.Leadership, SkillLevel.Basic),
            _ => null,
        };
    }

    /// <summary>
    /// The resources a guarded artifact asks for, <c>getArtifactResourceRequirement()</c>
    /// (<c>maps_tiles_helper.cpp:634</c>). Returns an empty purse when the artifact asks for nothing.
    /// </summary>
    public Funds GetArtifactResourceRequirement(World world)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.Artifact)
        {
            Debug.Assert(false, $"A resource requirement was asked from a tile that holds {MainObjectType}.");

            return new Funds();
        }

        switch (GetArtifactCaptureCondition(world))
        {
            case ArtifactCaptureCondition.Pay2000Gold:
                return new Funds(Resource.Gold, 2000u);

            case ArtifactCaptureCondition.Pay2500GoldAnd3Resources:
                return new Funds(Resource.Gold, 2500u) + new Funds(GetGuardResourceType(), 3u);

            case ArtifactCaptureCondition.Pay3000GoldAnd5Resources:
                return new Funds(Resource.Gold, 3000u) + new Funds(GetGuardResourceType(), 5u);

            default:
                return new Funds();
        }
    }

    /// <summary>
    /// The resource a guarded artifact asks for, <c>Resource::getResourceTypeFromIconIndex( metadata[1] - 1 )</c>
    /// (<c>resource.cpp:443</c>). The reference subtracts one from the stored icon index and asserts on
    /// anything above six; the port falls back to wood instead of reading out of the table.
    /// </summary>
    private int GetGuardResourceType()
    {
        uint iconIndex = _metadata[1] == 0 ? 0 : _metadata[1] - 1;
        if (iconIndex > 6)
        {
            Debug.Assert(false, $"An artifact guard asks for resource icon {iconIndex}.");
            iconIndex = 0;
        }

        return iconIndex switch
        {
            0 => Resource.Wood,
            1 => Resource.Mercury,
            2 => Resource.Ore,
            3 => Resource.Sulfur,
            4 => Resource.Crystal,
            5 => Resource.Gems,
            _ => Resource.Gold,
        };
    }

    // ------------------------------------------------------- daemon cave and shipwreck

    /// <summary>
    /// What a visited Daemon Cave hands out, <c>getDaemonCaveBonusType()</c>
    /// (<c>maps_tiles_helper.cpp:658</c>).
    /// </summary>
    public DaemonCaveCaptureBonus GetDaemonCaveBonusType(World world)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.DaemonCave)
        {
            Debug.Assert(false, $"A daemon cave bonus was asked from a tile that holds {MainObjectType}.");

            return DaemonCaveCaptureBonus.Empty;
        }

        return (DaemonCaveCaptureBonus)_metadata[2];
    }

    /// <summary>
    /// The gold a visited Daemon Cave asks for, <c>getDaemonPaymentCondition()</c>
    /// (<c>maps_tiles_helper.cpp:669</c>). Only the <see cref="DaemonCaveCaptureBonus.Pay2500Gold"/> bonus
    /// has a price; every other bonus returns an empty purse.
    /// </summary>
    public Funds GetDaemonPaymentCondition(World world)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.DaemonCave)
        {
            Debug.Assert(false, $"A daemon cave payment was asked from a tile that holds {MainObjectType}.");

            return new Funds();
        }

        if ((DaemonCaveCaptureBonus)_metadata[2] != DaemonCaveCaptureBonus.Pay2500Gold)
        {
            return new Funds();
        }

        return new Funds(Resource.Gold, _metadata[1]);
    }

    /// <summary>
    /// What a visited Shipwreck hands out, <c>getShipwreckCaptureCondition()</c>
    /// (<c>maps_tiles_helper.cpp:686</c>).
    /// </summary>
    public ShipwreckCaptureCondition GetShipwreckCaptureCondition(World world)
    {
        if (GetNonActionObjectType(world) != Mp2ObjectType.Shipwreck)
        {
            Debug.Assert(false, $"A shipwreck condition was asked from a tile that holds {MainObjectType}.");

            return ShipwreckCaptureCondition.Empty;
        }

        return (ShipwreckCaptureCondition)_metadata[2];
    }

    // ------------------------------------------------------------- monster counts

    /// <summary>
    /// The number of creatures a dwelling, a wandering stack or an abandoned mine holds,
    /// <c>getMonsterCountFromTile()</c> (<c>maps_tiles_helper.cpp:856</c>).
    /// </summary>
    public uint GetMonsterCount(World world)
    {
        if (!IsMonsterCountHolder(GetNonActionObjectType(world)))
        {
            Debug.Assert(false, $"A monster count was asked from a tile that holds {MainObjectType}.");

            return 0;
        }

        return _metadata[0];
    }

    /// <summary>
    /// Writes the number of creatures, <c>setMonsterCountOnTile()</c> (<c>maps_tiles_helper.cpp:893</c>).
    /// </summary>
    public void SetMonsterCount(World world, uint count)
    {
        if (!IsMonsterCountHolder(GetNonActionObjectType(world)))
        {
            Debug.Assert(false, $"A monster count was set on a tile that holds {MainObjectType}.");

            return;
        }

        _metadata[0] = count;
    }

    private static bool IsMonsterCountHolder(Mp2ObjectType objectType) =>
        objectType is Mp2ObjectType.AbandonedMine
            or Mp2ObjectType.AirAltar
            or Mp2ObjectType.ArcherHouse
            or Mp2ObjectType.BarrowMounds
            or Mp2ObjectType.Cave
            or Mp2ObjectType.CityOfDead
            or Mp2ObjectType.DesertTent
            or Mp2ObjectType.DragonCity
            or Mp2ObjectType.DwarfCottage
            or Mp2ObjectType.EarthAltar
            or Mp2ObjectType.Excavation
            or Mp2ObjectType.FireAltar
            or Mp2ObjectType.GenieLamp
            or Mp2ObjectType.GoblinHut
            or Mp2ObjectType.HalflingHole
            or Mp2ObjectType.Monster
            or Mp2ObjectType.PeasantHut
            or Mp2ObjectType.Ruins
            or Mp2ObjectType.TreeCity
            or Mp2ObjectType.TreeHouse
            or Mp2ObjectType.TrollBridge
            or Mp2ObjectType.WagonCamp
            or Mp2ObjectType.WatchTower
            or Mp2ObjectType.WaterAltar;

    // ------------------------------------------------------------ creature and colour

    /// <summary>
    /// The creature a dwelling or a wandering stack is built of, <c>getMonsterFromTile()</c>
    /// (<c>maps_tiles_helper.cpp:508</c>). A capturable object answers with the guardians it was given.
    /// </summary>
    public MonsterType GetMonster(World world)
    {
        switch (GetNonActionObjectType(world))
        {
            case Mp2ObjectType.WatchTower:
                return MonsterType.Orc;
            case Mp2ObjectType.Excavation:
                return MonsterType.Skeleton;
            case Mp2ObjectType.Cave:
                return MonsterType.Centaur;
            case Mp2ObjectType.TreeHouse:
                return MonsterType.Sprite;
            case Mp2ObjectType.ArcherHouse:
                return MonsterType.Archer;
            case Mp2ObjectType.GoblinHut:
                return MonsterType.Goblin;
            case Mp2ObjectType.DwarfCottage:
                return MonsterType.Dwarf;
            case Mp2ObjectType.HalflingHole:
                return MonsterType.Halfling;
            case Mp2ObjectType.PeasantHut:
                return MonsterType.Peasant;
            case Mp2ObjectType.Ruins:
                return MonsterType.Medusa;
            case Mp2ObjectType.TreeCity:
                return MonsterType.Sprite;
            case Mp2ObjectType.WagonCamp:
                return MonsterType.Rogue;
            case Mp2ObjectType.DesertTent:
                return MonsterType.Nomad;
            case Mp2ObjectType.TrollBridge:
                return MonsterType.Troll;
            case Mp2ObjectType.DragonCity:
                return MonsterType.RedDragon;
            case Mp2ObjectType.CityOfDead:
                return MonsterType.PowerLich;
            case Mp2ObjectType.GenieLamp:
                return MonsterType.Genie;
            case Mp2ObjectType.AbandonedMine:
                return MonsterType.Ghost;
            case Mp2ObjectType.WaterAltar:
                return MonsterType.WaterElement;
            case Mp2ObjectType.AirAltar:
                return MonsterType.AirElement;
            case Mp2ObjectType.FireAltar:
                return MonsterType.FireElement;
            case Mp2ObjectType.EarthAltar:
                return MonsterType.EarthElement;
            case Mp2ObjectType.BarrowMounds:
                return MonsterType.Ghost;
            case Mp2ObjectType.Monster:
                // The sprite index of a wandering stack is the creature id minus one.
                return (MonsterType)(MainObjectPart.IcnIndex + 1);
            default:
                break;
        }

        // The reference passes the action-carrying type here, not the stripped one.
        if (Mp2ObjectTypes.IsCaptureObject(MainObjectType))
        {
            return world.GetCapturedObject(Index).Guardians.Monster;
        }

        return MonsterType.Unknown;
    }

    /// <summary>The colour the object was captured by, <c>getColorFromTile()</c> (<c>maps_tiles_helper.cpp:769</c>).</summary>
    public PlayerColor GetColor(World world) => world.ColorCapturedObject(Index);

    /// <summary>Marks the object capturable, <c>setColorOnTile()</c> (<c>maps_tiles_helper.cpp:788</c>).</summary>
    public void SetColor(World world, PlayerColor color) => world.CaptureObject(Index, color);

    // ------------------------------------------------------------ dwelling population

    /// <summary>
    /// Refills the population of a dwelling, <c>updateDwellingPopulationOnTile()</c>
    /// (<c>maps_tiles_helper.cpp:929</c>). <paramref name="isFirstLoad"/> is <see langword="true"/> for the
    /// initial map load, when the reference rolls the full weekly population instead of the growth.
    /// </summary>
    public void UpdateDwellingPopulation(World world, bool isFirstLoad)
    {
        uint count = isFirstLoad ? 0u : GetMonsterCount(world);

        switch (GetNonActionObjectType(world))
        {
            case Mp2ObjectType.HalflingHole:
                count += (uint)(isFirstLoad ? Rand.Get(20, 40) : Rand.Get(5, 10));
                break;
            case Mp2ObjectType.PeasantHut:
                count += (uint)(isFirstLoad ? Rand.Get(20, 50) : Rand.Get(5, 10));
                break;
            case Mp2ObjectType.Excavation:
            case Mp2ObjectType.TreeHouse:
                count += (uint)(isFirstLoad ? Rand.Get(10, 25) : Rand.Get(4, 8));
                break;
            case Mp2ObjectType.Cave:
                count += (uint)(isFirstLoad ? Rand.Get(10, 20) : Rand.Get(3, 6));
                break;
            case Mp2ObjectType.GoblinHut:
                count += (uint)(isFirstLoad ? Rand.Get(15, 40) : Rand.Get(3, 6));
                break;
            case Mp2ObjectType.TreeCity:
                count += (uint)(isFirstLoad ? Rand.Get(20, 40) : Rand.Get(10, 20));
                break;
            case Mp2ObjectType.WatchTower:
                count += (uint)(isFirstLoad ? Rand.Get(7, 10) : Rand.Get(2, 4));
                break;
            case Mp2ObjectType.ArcherHouse:
                count += (uint)(isFirstLoad ? Rand.Get(10, 25) : Rand.Get(2, 4));
                break;
            case Mp2ObjectType.DwarfCottage:
                count += (uint)(isFirstLoad ? Rand.Get(10, 20) : Rand.Get(3, 6));
                break;
            case Mp2ObjectType.WagonCamp:
                count += (uint)(isFirstLoad ? Rand.Get(30, 50) : Rand.Get(3, 6));
                break;
            case Mp2ObjectType.DesertTent:
                count += (uint)(isFirstLoad ? Rand.Get(10, 20) : Rand.Get(1, 3));
                break;
            case Mp2ObjectType.Ruins:
                count += (uint)(isFirstLoad ? Rand.Get(3, 5) : Rand.Get(1, 3));
                break;
            case Mp2ObjectType.WaterAltar:
            case Mp2ObjectType.AirAltar:
            case Mp2ObjectType.FireAltar:
            case Mp2ObjectType.EarthAltar:
            case Mp2ObjectType.BarrowMounds:
                // The altars and the barrow mounds roll their small garrison on every load.
                count += (uint)Rand.Get(2, 5);
                break;
            case Mp2ObjectType.TrollBridge:
            case Mp2ObjectType.CityOfDead:
                if (isFirstLoad)
                {
                    count = (uint)Rand.Get(4, 6);
                }
                else if (GetColor(world) != PlayerColor.None)
                {
                    // A captured bridge or city of the dead grows by 1…3 creatures a week.
                    count += (uint)Rand.Get(1, 3);
                }

                break;
            case Mp2ObjectType.DragonCity:
                if (isFirstLoad)
                {
                    count = 2;
                }
                else if (GetColor(world) != PlayerColor.None)
                {
                    // A captured dragon city grows by one dragon a week.
                    ++count;
                }

                break;
            default:
                // The reference asserts and then still writes the count it read back, which changes nothing.
                Debug.Assert(false, $"A dwelling population was updated on a tile that holds {MainObjectType}.");
                break;
        }

        Debug.Assert(count > 0);
        SetMonsterCount(world, count);
    }

    // ------------------------------------------------------------ empty ground

    /// <summary>
    /// Whether nothing actionable is left on the tile, <c>isClearGround()</c>
    /// (<c>maps_tiles_helper.cpp:2063</c>).
    /// </summary>
    /// <remarks>
    /// This is the variant that sees through the hero — a hero standing on an empty tile still leaves the
    /// ground clear, while a boat does not.
    /// </remarks>
    public bool IsClearGround(World world)
    {
        Mp2ObjectType objectType = GetMainObjectType(world);

        switch (Mp2ObjectTypes.NonActionType(objectType))
        {
            case Mp2ObjectType.None:
            case Mp2ObjectType.Coast:
                return true;
            case Mp2ObjectType.Boat:
                return false;
            default:
                break;
        }

        // An object that draws nothing, or whose part is a shadow or plain terrain, is ground too.
        if (MainObjectPart.IcnType == Mp2ObjectIcnType.Unknown
            || MainObjectPart.LayerType == ObjectLayerType.Shadow
            || MainObjectPart.LayerType == ObjectLayerType.Terrain)
        {
            return !Mp2ObjectTypes.IsInGameActionObject(objectType, IsWater);
        }

        return false;
    }
}
