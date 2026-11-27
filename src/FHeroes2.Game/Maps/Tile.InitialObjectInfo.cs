using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The metadata a map object carries, the port of <c>Maps::setInitialObjectInfo()</c>
/// (<c>maps_tiles_helper.cpp:1015-1640</c>) as the loader of a map calls it through
/// <c>World::updateTileMetadata()</c> (<c>world_loadmap.cpp:1492-1634</c>).
/// </summary>
/// <remarks>
/// <para>
/// An MP2 file has no metadata block: <c>quantity1/quantity2</c> hold the editor's own numbers and the
/// object type lives in one byte, so everything the objects keep (the spell of a shrine, the skill of a
/// witch's hut, the artifact in a skeleton, the income of a mine) has to be filled here from the sprite
/// the object was placed with, or rolled, after the parts of the tile are known.
/// </para>
/// <para>
/// The three random placeholders are resolved here, exactly when the reference resolves them: the tile
/// becomes a real pile, artifact or wandering stack (<c>updateRandomResource()</c>,
/// <c>updateRandomArtifact()</c>, <c>updateRandomMonster()</c>), the sprite is rewritten and the metadata is
/// then filled from the new sprite by a second pass of this method. An fh2 map stores the rolled object in
/// <c>Metadata[1]</c>, which is taken as is and cleared.
/// </para>
/// <para>
/// Not ported yet, the branches that need the editor's object placement (<c>setObjectOnTile()</c> and
/// <c>replaceObject()</c>): the boat sprite fix.
/// </para>
/// </remarks>
public sealed partial class Tile
{
    /// <summary>
    /// Gives the tile the metadata of the object that stands on it, <c>World::updateTileMetadata()</c>
    /// (<c>world_loadmap.cpp:1492</c>).
    /// </summary>
    /// <param name="world">The world being loaded, needed by the objects that ask for a game version.</param>
    /// <remarks>
    /// A hero is placed by <see cref="PlaceMapHero"/> instead: a hero carries no object metadata, only a
    /// tile, and the reference handles it in the same per-tile pass.
    /// </remarks>
    public void UpdateTileMetadata(World world)
    {
        switch (GetNonActionObjectType(world))
        {
            case Mp2ObjectType.Hero:
            case Mp2ObjectType.Boat:
            case Mp2ObjectType.Event:
            case Mp2ObjectType.RandomUltimateArtifact:
                break;

            default:
                SetInitialObjectInfo(world);
                break;
        }
    }

    /// <summary>
    /// Fills the metadata lanes of the object of the tile, <c>Maps::setInitialObjectInfo()</c>
    /// (<c>maps_tiles_helper.cpp:1015</c>).
    /// </summary>
    /// <param name="world">The world being loaded.</param>
    /// <remarks>
    /// Every object that is not named here ends up with cleared lanes, which is what the reference's
    /// <c>default:</c> branch does — the numbers an MP2 file leaves there are not metadata.
    /// </remarks>
    public void SetInitialObjectInfo(World world)
    {
        // 参考取 Game::getVersion()（全局设置里的当前地图），端口改取这份地图自己的版本：
        // 载入时 World.LoadMap 已经把 MapInfo 设成 map.Info，两者在游戏里相等，而读地图头不会被
        // 另一个线程换地图带偏（并行测试曾因此偶发把随机神器读成 Unknown）。
        MapVersion version = world.MapInfo?.Version ?? MapVersion.SuccessionWars;

        switch (GetNonActionObjectType(world))
        {
            case Mp2ObjectType.WitchsHut:
                if (Metadata[0] != 0)
                {
                    // The skill has been set externally.
                    break;
                }

                Metadata[0] = (uint)SecondarySkills.WitchsHutSkills[Rand.Get(0, SecondarySkills.WitchsHutSkills.Count - 1)];
                break;

            case Mp2ObjectType.ShrineFirstCircle:
                if (Metadata[0] == 0)
                {
                    SetSpellOnTile(world, Spell.RandomSpell(1).Type);
                }

                break;

            case Mp2ObjectType.ShrineSecondCircle:
                if (Metadata[0] == 0)
                {
                    SetSpellOnTile(world, Spell.RandomSpell(2).Type);
                }

                break;

            case Mp2ObjectType.ShrineThirdCircle:
                if (Metadata[0] == 0)
                {
                    SetSpellOnTile(world, Spell.RandomSpell(3).Type);
                }

                break;

            case Mp2ObjectType.Skeleton:
                // 80%: empty, 20%: a treasure artifact.
                Metadata[0] = PickWeighted((0, 80), (1, 20)) != 0
                    ? (uint)ArtifactRandom.Next(ArtifactLevel.AllNormal, version)
                    : (uint)ArtifactType.Unknown;
                break;

            case Mp2ObjectType.Wagon:
                {
                    ResetObjectMetadata();

                    // 20%: empty, 10%: a treasure or a minor artifact, 50%: some resource.
                    switch (PickWeighted((0, 20), (1, 10), (2, 50)))
                    {
                        case 1:
                            Metadata[0] = (uint)ArtifactRandom.Next(Rand.Get(0, 1) != 0 ? ArtifactLevel.Treasure : ArtifactLevel.Minor, version);
                            break;

                        case 2:
                            Metadata[1] = (uint)Resource.Rand(false);
                            Metadata[2] = (uint)Rand.Get(2, 5);
                            break;

                        default:
                            break;
                    }

                    break;
                }

            case Mp2ObjectType.Artifact:
                {
                    uint artifactSpriteIndex = (uint)ArtifactType.Unknown;

                    if (MainObjectPart.IcnType == Mp2ObjectIcnType.ObjnArti)
                    {
                        artifactSpriteIndex = (uint)MainObjectPart.IcnIndex;
                    }
                    else
                    {
                        // On some hacked original maps the artifact sits on the ground layer.
                        foreach (ObjectPart part in GroundObjectParts)
                        {
                            if (part.IcnType == Mp2ObjectIcnType.ObjnArti)
                            {
                                artifactSpriteIndex = (uint)part.IcnIndex;
                                break;
                            }
                        }
                    }

                    // 参考传 Settings::isPriceOfLoyaltySupported()（装没装扩展）；端口没有设置层，改由地图版本推断：
                    // Price of Loyalty 与 Resurrection 的地图只可能来自扩展。传 false 会把扩展神器的索引
                    // （172..205）当成没有神器——随机神器在 PoL 地图上恰好抽到扩展神器时就这样丢掉了。
                    ArtifactType artifact = ArtifactMapSprites.FromMapIndex(artifactSpriteIndex, version,
                        version != MapVersion.SuccessionWars);
                    if (artifact == ArtifactType.Unknown)
                    {
                        // The sprite names no artifact the port knows: leave the tile without one.
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 0;
                        Metadata[2] = 0;
                        break;
                    }

                    if (artifact == ArtifactType.SpellScroll)
                    {
                        // The MP2 file keeps the spell id one lower than the game does, and an editor that
                        // wrote nothing leaves a value that has to be pinned to the spell range.
                        int spell = Math.Clamp((int)Metadata[0] + 1, (int)SpellType.Fireball, (int)SpellType.SetWGuardian);

                        Metadata[1] = (uint)spell;
                        Metadata[2] = (uint)ArtifactCaptureCondition.ContainsSpell;
                    }
                    else
                    {
                        // 70% of the guarded artifacts ask for nothing; the rest ask for one of the 13
                        // conditions of ArtifactCaptureCondition.
                        uint condition = Rand.Get(1, 10) < 4 ? (uint)Rand.Get(1, 13) : 0;

                        Metadata[2] = condition;

                        if (condition == (uint)ArtifactCaptureCondition.Pay2500GoldAnd3Resources ||
                            condition == (uint)ArtifactCaptureCondition.Pay3000GoldAnd5Resources)
                        {
                            // The reference stores an icon index, not a map sprite index, and adds one.
                            Metadata[1] = (uint)(ResourceIconIndex.Of(Resource.Rand(false)) + 1);
                        }
                    }

                    Metadata[0] = (uint)artifact;
                    break;
                }

            case Mp2ObjectType.Resource:
                {
                    int resourceType = Resource.Unknown;

                    if (MainObjectPart.IcnType == Mp2ObjectIcnType.ObjnRsrc)
                    {
                        // The pile frame already decodes to a resource flag: it must not go through the icon
                        // table again, which would read the flag as a RESOURCE.ICN frame.
                        resourceType = ResourceIconIndex.ResourceOfObjectFrame((uint)MainObjectPart.IcnIndex);
                    }
                    else
                    {
                        foreach (ObjectPart part in GroundObjectParts)
                        {
                            if (part.IcnType == Mp2ObjectIcnType.ObjnRsrc)
                            {
                                resourceType = ResourceIconIndex.ResourceOfObjectFrame((uint)part.IcnIndex);

                                // The map maker put the resource under another object: the tile has to show
                                // the action object instead of the decoration.
                                UpdateObjectType(world);
                                break;
                            }
                        }
                    }

                    if ((Metadata[0] & (uint)Resource.All) != 0 && Metadata[1] > 0)
                    {
                        // The resource was set externally.
                        break;
                    }

                    uint count;
                    switch (resourceType)
                    {
                        case Resource.Gold:
                            count = (uint)(100 * Rand.Get(5, 10));
                            break;

                        case Resource.Wood:
                        case Resource.Ore:
                            count = (uint)Rand.Get(5, 10);
                            break;

                        case Resource.Mercury:
                        case Resource.Sulfur:
                        case Resource.Crystal:
                        case Resource.Gems:
                            count = (uint)Rand.Get(3, 6);
                            break;

                        default:
                            // Some maps have broken resource piles; the reference turns them into empty wood.
                            resourceType = Resource.Wood;
                            count = 0;
                            break;
                    }

                    SetResourceOnTile(resourceType, count);
                    break;
                }

            case Mp2ObjectType.Campfire:
                // 4-6 of a random resource.
                SetResourceOnTile(Resource.Rand(false), (uint)Rand.Get(4, 6));
                break;

            case Mp2ObjectType.LeanTo:
                // 1-4 of a random resource.
                SetResourceOnTile(Resource.Rand(false), (uint)Rand.Get(1, 4));
                break;

            case Mp2ObjectType.Flotsam:
                switch (Rand.Get(1, 4))
                {
                    case 1:
                        Metadata[0] = 10;
                        Metadata[1] = 500;
                        break;

                    case 2:
                        Metadata[0] = 5;
                        Metadata[1] = 200;
                        break;

                    case 3:
                        Metadata[0] = 5;
                        break;

                    default:
                        break;
                }

                break;

            case Mp2ObjectType.ShipwreckSurvivor:
                // 55%: a treasure artifact, 30%: a minor one, 15%: a major one.
                switch (PickWeighted((1, 55), (2, 30), (3, 15)))
                {
                    case 1:
                        Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.Treasure, version);
                        break;

                    case 2:
                        Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.Minor, version);
                        break;

                    default:
                        Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.Major, version);
                        break;
                }

                break;

            case Mp2ObjectType.SeaChest:
                // 20%: empty, 70%: 1500 gold, 10%: 1000 gold and a treasure artifact.
                switch (PickWeighted((0, 20), (1, 70), (2, 10)))
                {
                    case 1:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 1500;
                        break;

                    case 2:
                        Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.Treasure, version);
                        Metadata[1] = 1000;
                        break;

                    default:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 0;
                        break;
                }

                break;

            case Mp2ObjectType.TreasureChest:
                if (IsWater)
                {
                    // A treasure chest on water is a bug of the original maps ("Alteris 2" has one). The
                    // reference replaces the object with a sea chest; the port cannot place objects yet, so
                    // it only turns the chest into one of the same tile, which keeps the reward reachable.
                    RemoveObjectByType(world, Mp2ObjectType.TreasureChest);
                    SetMainObjectType(Mp2ObjectType.SeaChest);

                    SetInitialObjectInfo(world);
                    return;
                }

                // 31%: 2000 gold, 32%: 1500, 32%: 1000, 5%: a treasure artifact.
                switch (PickWeighted((1, 31), (2, 32), (3, 32), (4, 5)))
                {
                    case 1:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 2000;
                        break;

                    case 2:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 1500;
                        break;

                    case 3:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 1000;
                        break;

                    default:
                        Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.Treasure, version);
                        Metadata[1] = 0;
                        break;
                }

                break;

            case Mp2ObjectType.DerelictShip:
                SetResourceOnTile(Resource.Gold, 5000);
                break;

            case Mp2ObjectType.Shipwreck:
                // 40%: 10 ghosts and 1000 gold, 30%: 15 and 2000, 20%: 25 and 5000, 10%: 50 and 2000
                // plus an artifact.
                Metadata[2] = (uint)PickWeighted((1, 40), (2, 30), (3, 20), (4, 10));

                switch ((ShipwreckCaptureCondition)Metadata[2])
                {
                    case ShipwreckCaptureCondition.Fight10GhostsAndGet1000Gold:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 1000;
                        break;

                    case ShipwreckCaptureCondition.Fight15GhostsAndGet2000Gold:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 2000;
                        break;

                    case ShipwreckCaptureCondition.Fight25GhostsAndGet5000Gold:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 5000;
                        break;

                    default:
                        Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.AllNormal, version);
                        Metadata[1] = 2000;
                        break;
                }

                break;

            case Mp2ObjectType.Graveyard:
                Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.AllNormal, version);
                Metadata[1] = 1000;
                break;

            case Mp2ObjectType.Pyramid:
                if (Metadata[0] == 0)
                {
                    // A random spell of the fifth circle.
                    SetSpellOnTile(world, Spell.RandomSpell(5).Type);
                }

                break;

            case Mp2ObjectType.DaemonCave:
                // 1000 experience, 1000 and 2500 gold, 1000 and an artifact, or pay 2500.
                Metadata[2] = (uint)Rand.Get(1, 4);

                switch ((DaemonCaveCaptureBonus)Metadata[2])
                {
                    case DaemonCaveCaptureBonus.Get1000Experience:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 0;
                        break;

                    case DaemonCaveCaptureBonus.Get1000ExperienceAnd2500Gold:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 2500;
                        break;

                    case DaemonCaveCaptureBonus.Get1000ExperienceAndArtifact:
                        Metadata[0] = (uint)ArtifactRandom.Next(ArtifactLevel.AllNormal, version);
                        Metadata[1] = 0;
                        break;

                    default:
                        Metadata[0] = (uint)ArtifactType.Unknown;
                        Metadata[1] = 2500;
                        break;
                }

                break;

            case Mp2ObjectType.TreeOfKnowledge:
                // 10 gems, 2000 gold or a free level.
                switch (Rand.Get(1, 3))
                {
                    case 1:
                        SetResourceOnTile(Resource.Gems, 10);
                        break;

                    case 2:
                        SetResourceOnTile(Resource.Gold, 2000);
                        break;

                    default:
                        break;
                }

                break;

            case Mp2ObjectType.Barrier:
                SetBarrierColorOnTile(world, GetColorFromBarrierSprite(MainObjectPart.IcnType, (byte)MainObjectPart.IcnIndex));
                break;

            case Mp2ObjectType.TravellerTent:
                SetBarrierColorOnTile(world, GetColorFromTravellerTentSprite(MainObjectPart.IcnType, (byte)MainObjectPart.IcnIndex));
                break;

            case Mp2ObjectType.AlchemistLab:
                // One mercury a day.
                SetIncomeMetadata(Resource.Mercury, 1);
                break;

            case Mp2ObjectType.Sawmill:
                // Two wood a day.
                SetIncomeMetadata(Resource.Wood, 2);
                break;

            case Mp2ObjectType.Mine:
                {
                    // A mine's main part is the overlay cart that names its resource; a map that puts the
                    // overlay on the ground layer is fixed up by the unique id of the object.
                    if (MainObjectPart.IcnType != Mp2ObjectIcnType.ExtraOvr)
                    {
                        for (int index = 0; index < GroundObjectParts.Count; ++index)
                        {
                            ObjectPart part = GroundObjectParts[index];
                            if (part.IcnType == Mp2ObjectIcnType.ExtraOvr && part.Uid == MainObjectPart.Uid)
                            {
                                // The reference swaps the part into the main slot.
                                ObjectPart mainPart = MainObjectPart;
                                SetMainObjectPart(part);
                                GroundObjectParts[index] = mainPart;
                                break;
                            }
                        }
                    }

                    if (MainObjectPart.IcnType != Mp2ObjectIcnType.ExtraOvr)
                    {
                        // No cart: a hacked mine, or one of the mountain tiles of a multi-tile mine. The
                        // reference clears the type, and the passability pass may derive it back from the
                        // mountain sprite, so the metadata stays empty — nothing pays an income here.
                        SetMainObjectType(Mp2ObjectType.None);
                        break;
                    }

                    switch (MainObjectPart.IcnIndex)
                    {
                        case 0:
                            SetIncomeMetadata(Resource.Ore, 2);
                            break;

                        case 1:
                            SetIncomeMetadata(Resource.Sulfur, 1);
                            break;

                        case 2:
                            SetIncomeMetadata(Resource.Crystal, 1);
                            break;

                        case 3:
                            SetIncomeMetadata(Resource.Gems, 1);
                            break;

                        case 4:
                            SetIncomeMetadata(Resource.Gold, 1000);
                            break;

                        default:
                            SetMainObjectType(Mp2ObjectType.None);
                            break;
                    }

                    break;
                }

            case Mp2ObjectType.WaterWheel:
                // 1-3 of a random resource.
                SetResourceOnTile(Resource.Rand(false), (uint)Rand.Get(1, 3));
                break;

            case Mp2ObjectType.Windmill:
                // 2-5 of a random resource.
                SetResourceOnTile(Resource.Rand(false), (uint)Rand.Get(2, 5));
                break;

            case Mp2ObjectType.MagicGarden:
                // The fairy ring grants one random resource, refreshed every week.
                UpdateObjectInfoTile(world);
                break;

            case Mp2ObjectType.GenieLamp:
                // A random number of genies guard the lamp.
                SetMonsterCount(world, (uint)Rand.Get(2, 4));
                break;

            case Mp2ObjectType.RandomArtifact:
            case Mp2ObjectType.RandomArtifactTreasure:
            case Mp2ObjectType.RandomArtifactMinor:
            case Mp2ObjectType.RandomArtifactMajor:
                // 随机神器在载入时抽定，抽完就是一件普通神器，再走一遍本方法按神器初始化。
                UpdateRandomArtifact(world);
                SetInitialObjectInfo(world);
                return;

            case Mp2ObjectType.RandomResource:
                UpdateRandomResource(world);
                SetInitialObjectInfo(world);
                return;

            case Mp2ObjectType.RandomMonster:
            case Mp2ObjectType.RandomMonsterWeak:
            case Mp2ObjectType.RandomMonsterMedium:
            case Mp2ObjectType.RandomMonsterStrong:
            case Mp2ObjectType.RandomMonsterVeryStrong:
                UpdateRandomMonster(world);
                SetInitialObjectInfo(world);
                return;

            case Mp2ObjectType.Monster:
                // The count comes from the file, the monster itself from the sprite.
                SetMonster(world, (MonsterType)(MainObjectPart.IcnIndex + 1), Metadata[0]);
                break;

            case Mp2ObjectType.AirAltar:
            case Mp2ObjectType.WaterAltar:
            case Mp2ObjectType.EarthAltar:
            case Mp2ObjectType.FireAltar:
            case Mp2ObjectType.ArcherHouse:
            case Mp2ObjectType.BarrowMounds:
            case Mp2ObjectType.Cave:
            case Mp2ObjectType.CityOfDead:
            case Mp2ObjectType.DesertTent:
            case Mp2ObjectType.DragonCity:
            case Mp2ObjectType.DwarfCottage:
            case Mp2ObjectType.Excavation:
            case Mp2ObjectType.GoblinHut:
            case Mp2ObjectType.HalflingHole:
            case Mp2ObjectType.PeasantHut:
            case Mp2ObjectType.Ruins:
            case Mp2ObjectType.TreeCity:
            case Mp2ObjectType.TreeHouse:
            case Mp2ObjectType.TrollBridge:
            case Mp2ObjectType.WagonCamp:
            case Mp2ObjectType.WatchTower:
                // A dwelling keeps a number of creatures waiting, the reference rolls it from the type.
                UpdateDwellingPopulation(world, true);
                break;

            default:
                ResetObjectMetadata();
                break;
        }
    }

    /// <summary>
    /// Picks one of the weighted entries the way <c>Rand::Queue</c> does, <c>rand.cpp</c>: a roll in
    /// <c>[1, sum]</c> walks the weights and the entry that owns the roll answers.
    /// </summary>
    private static int PickWeighted(params (int Value, int Weight)[] entries)
    {
        int total = 0;
        foreach ((int value, int weight) in entries)
        {
            total += weight;
        }

        int roll = Rand.Get(1, total);
        foreach ((int value, int weight) in entries)
        {
            roll -= weight;
            if (roll <= 0)
            {
                return value;
            }
        }

        return entries[^1].Value;
    }

    /// <summary>
    /// Turns a random-resource object into a real pile of something, <c>updateRandomResource()</c>
    /// (<c>maps_tiles_helper.cpp:61-81</c>).
    /// </summary>
    /// <remarks>
    /// 资源堆的精灵帧完全由资源种类决定（<c>Resource::GetIndexSprite()</c>）。影子那一帧比本体小一号、挂在
    /// <b>左边</b>那一格上，而且和本体共用同一个 UID——所以影子得跟着一起改，否则留下的是「新资源堆 +
    /// 旧资源堆的影子」。
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    private void UpdateRandomResource(World world)
    {
        // 参考写的是 OBJ_RESOURCE，本身就带动作位，所以这里是带位的那个成员。
        SetMainObjectType(Mp2ObjectType.ActionResource);

        byte resourceSprite = (byte)ResourceIconIndex.ObjectFrameOf(Resource.Rand());

        uint resourceUid = GetObjectIdByObjectIcnType(Mp2ObjectIcnType.ObjnRsrc);

        if (resourceUid == 0)
        {
            resourceUid = MainObjectPart.Uid;
        }

        UpdateTileObjectIcnIndex(this, resourceUid, resourceSprite);

        // 影子是左边那一格上的同一件物件（同一个 UID），帧号比本体小一号。
        MapGeometry geometry = world.Geometry!.Value;

        if (!geometry.IsValidDirection(Index, Direction.Left))
        {
            return;
        }

        Debug.Assert(resourceSprite > 0, "A resource pile always has a shadow frame before it.");

        Tile shadowTile = world.GetTile(geometry.GetDirectionIndex(Index, Direction.Left));

        UpdateTileObjectIcnIndex(shadowTile, resourceUid, (byte)(resourceSprite - 1));
    }

    /// <summary>
    /// Turns a random-artifact object into a real artifact, <c>updateRandomArtifact()</c>
    /// (<c>maps_tiles_helper.cpp:83-142</c>).
    /// </summary>
    /// <remarks>
    /// 神器种类不写进元数据：只把精灵帧换成「那件神器」，随后 <see cref="SetInitialObjectInfo"/> 会按普通神器
    /// 把种类从帧号反推回 <c>Metadata[0]</c>（参考 <c>setInitialObjectInfo()</c> 的 <c>OBJ_ARTIFACT</c> 分支）。
    /// fh2 地图例外：种类本来就写在 <c>Metadata[1]</c>，那就用它，然后清掉。
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    private void UpdateRandomArtifact(World world)
    {
        ArtifactType artifact;

        if (Metadata[1] != (uint)ArtifactType.Unknown)
        {
            // This metadata is only set for fh2 map format.
            artifact = (ArtifactType)Metadata[1];
            Metadata[1] = 0;
        }
        else
        {
            ArtifactLevel artifactLevel = GetNonActionObjectType(world) switch
            {
                Mp2ObjectType.RandomArtifactTreasure => ArtifactLevel.Treasure,
                Mp2ObjectType.RandomArtifactMinor => ArtifactLevel.Minor,
                Mp2ObjectType.RandomArtifactMajor => ArtifactLevel.Major,
                Mp2ObjectType.RandomArtifact => ArtifactLevel.AllNormal,
                _ => throw new NotSupportedException($"A random artifact type is missing here: {MainObjectType}."),
            };

            // 同上：用这份地图的版本，不读全局的当前地图。
            artifact = ArtifactRandom.Next(artifactLevel,
                world.MapInfo?.Version ?? MapVersion.SuccessionWars);
        }

        if (artifact == ArtifactType.Unknown)
        {
            ResetObjectMetadata();
            UpdateObjectType(world);

            return;
        }

        SetMainObjectType(Mp2ObjectType.ActionArtifact);

        uint artifactUid = GetObjectIdByObjectIcnType(Mp2ObjectIcnType.ObjnArti);

        if (artifactUid == 0)
        {
            artifactUid = MainObjectPart.Uid;
        }

        // OBJNARTI 里每件神器占两帧：本体和影子；原件的神器 id 从 0 开始，所以要减一。
        byte artifactSprite = (byte)(((int)artifact - 1) * 2 + 1);

        UpdateTileObjectIcnIndex(this, artifactUid, artifactSprite);

        MapGeometry geometry = world.Geometry!.Value;

        if (!geometry.IsValidDirection(Index, Direction.Left))
        {
            return;
        }

        Tile shadowTile = world.GetTile(geometry.GetDirectionIndex(Index, Direction.Left));

        UpdateTileObjectIcnIndex(shadowTile, artifactUid, (byte)(artifactSprite - 1));
    }

    /// <summary>
    /// Turns a random-monster object into a real wandering stack, <c>updateRandomMonster()</c>
    /// (<c>maps_tiles_helper.cpp:144-193</c>).
    /// </summary>
    /// <remarks>
    /// 只换主部件的帧（MONS32 从农民开始，帧号 = 种类 - 1），种类也从帧号反推；数量留给下一步按等级抽。
    /// fh2 地图例外：种类写在 <c>Metadata[1]</c>，用它并清掉。
    /// </remarks>
    /// <param name="world">The world the tile belongs to.</param>
    private void UpdateRandomMonster(World world)
    {
        MonsterType monster;

        if (Metadata[1] != (uint)MonsterType.Unknown)
        {
            // This metadata is only set for fh2 map format.
            monster = (MonsterType)Metadata[1];
            Metadata[1] = 0;
        }
        else
        {
            MonsterLevelType monsterLevel = GetNonActionObjectType(world) switch
            {
                Mp2ObjectType.RandomMonsterWeak => MonsterLevelType.Level1,
                Mp2ObjectType.RandomMonsterMedium => MonsterLevelType.Level2,
                Mp2ObjectType.RandomMonsterStrong => MonsterLevelType.Level3,
                Mp2ObjectType.RandomMonsterVeryStrong => MonsterLevelType.Level4,
                Mp2ObjectType.RandomMonster => MonsterLevelType.Any,
                _ => throw new NotSupportedException($"A random monster type is missing here: {MainObjectType}."),
            };

            monster = MonsterLevels.GetRandom(monsterLevel);
        }

        if (monster == MonsterType.Unknown)
        {
            ResetObjectMetadata();
            UpdateObjectType(world);

            return;
        }

        SetMainObjectType(Mp2ObjectType.ActionMonster);

        UpdateTileObjectIcnIndex(this, MainObjectPart.Uid, (byte)((int)monster - 1));
    }
}
