using System;

using FHeroes2.Assets.Maps;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

using ArmyTroops = FHeroes2.Game.Army.Army;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The text of the right button quick info, <c>getQuickInfoText()</c> of
/// <c>src/fheroes2/dialog/dialog_quickinfo.cpp:455-595</c>, and the per family helpers it calls
/// (<c>:135-443</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference reads three global pieces of state: the colour of the player whose turn it is
/// (<c>Settings::Get().CurrentColor()</c>), the world, and the hero the interface focuses
/// (<c>Interface::GetFocusHeroes()</c>). They are parameters here.
/// </para>
/// <para>
/// <see cref="ForTile(Tile)"/> answers without a world at all, keeping the tile only branches for callers
/// and tests that have none; the full table is the four argument overload.
/// </para>
/// </remarks>
public static class QuickInfoText
{
    /// <summary>Name the reference gives a tile the player has not charted yet.</summary>
    public const string UnchartedTerritory = "Uncharted Territory";

    /// <summary>Name the reference gives a road tile.</summary>
    public const string Road = "Road";

    /// <summary>
    /// The quick info of a tile, <c>getQuickInfoText( tile )</c>.
    /// </summary>
    /// <param name="tile">Tile under the cursor.</param>
    /// <param name="world">World the tile belongs to.</param>
    /// <param name="viewer">Colour of the player whose turn it is, <c>Settings::Get().CurrentColor()</c>.</param>
    /// <param name="focusHero">Hero the interface focuses, or <see langword="null"/> when none is selected.</param>
    /// <returns>The text to show in the popup.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="tile"/> or <paramref name="world"/> is <see langword="null"/>.
    /// </exception>
    public static string ForTile(Tile tile, World world, PlayerColor viewer, Hero? focusHero = null)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(world);

        Mp2ObjectType objectType = ObjectTypeOf(tile, world);

        // 参考：有守军的可占领对象和中立矿洞一律先讲守军。
        if (objectType == Mp2ObjectType.AbandonedMine || tile.IsCaptureObjectProtected(world))
        {
            return DescribeGuarded(tile, world, viewer == tile.GetColor(world));
        }

        GameKingdom kingdom = world.GetKingdom(viewer);

        return objectType switch
        {
            Mp2ObjectType.Monster => DescribeMonster(tile, world, kingdom),

            Mp2ObjectType.Coast or Mp2ObjectType.Event or Mp2ObjectType.None => DescribeGround(tile, world, focusHero),

            Mp2ObjectType.ArtesianSpring
                or Mp2ObjectType.DaemonCave
                or Mp2ObjectType.DerelictShip
                or Mp2ObjectType.Graveyard
                or Mp2ObjectType.LeanTo
                or Mp2ObjectType.MagicGarden
                or Mp2ObjectType.Obelisk
                or Mp2ObjectType.Pyramid
                or Mp2ObjectType.Shipwreck
                or Mp2ObjectType.Skeleton
                or Mp2ObjectType.Sphinx
                or Mp2ObjectType.Wagon
                or Mp2ObjectType.WaterWheel
                or Mp2ObjectType.Windmill => DescribeObjectVisit(objectType, kingdom.IsVisited(tile)),

            Mp2ObjectType.MagellansMaps => DescribeObjectVisit(objectType, kingdom.IsVisited(objectType)),

            Mp2ObjectType.Resource => DescribeResourcePile(tile, world),

            Mp2ObjectType.Mine => DescribeMine(tile, world, viewer == tile.GetColor(world)),

            Mp2ObjectType.AlchemistLab or Mp2ObjectType.Sawmill =>
                DescribeIncomeObject(tile, world, viewer == tile.GetColor(world)),

            // 可加入、可招募、先打后招的住处，参考合成一支。
            Mp2ObjectType.ArcherHouse or Mp2ObjectType.Cave or Mp2ObjectType.DwarfCottage
                or Mp2ObjectType.Excavation or Mp2ObjectType.GoblinHut or Mp2ObjectType.HalflingHole
                or Mp2ObjectType.PeasantHut or Mp2ObjectType.TreeHouse or Mp2ObjectType.WatchTower
                or Mp2ObjectType.DesertTent or Mp2ObjectType.Ruins or Mp2ObjectType.TreeCity
                or Mp2ObjectType.WagonCamp or Mp2ObjectType.AirAltar or Mp2ObjectType.BarrowMounds
                or Mp2ObjectType.CityOfDead or Mp2ObjectType.DragonCity or Mp2ObjectType.EarthAltar
                or Mp2ObjectType.FireAltar or Mp2ObjectType.TrollBridge or Mp2ObjectType.WaterAltar
                => DescribeDwelling(tile, world, kingdom.IsVisited(tile)),

            Mp2ObjectType.Fort or Mp2ObjectType.Gazebo or Mp2ObjectType.MercenaryCamp
                or Mp2ObjectType.StandingStones or Mp2ObjectType.WitchDoctorsHut or Mp2ObjectType.Xanadu
                => DescribeLocalVisitTile(tile, objectType, focusHero),

            Mp2ObjectType.Arena or Mp2ObjectType.BlackCat or Mp2ObjectType.Buoy
                or Mp2ObjectType.FaerieRing or Mp2ObjectType.Fountain or Mp2ObjectType.Idol
                or Mp2ObjectType.MagicWell or Mp2ObjectType.Mermaid or Mp2ObjectType.Oasis
                or Mp2ObjectType.Sirens or Mp2ObjectType.Stables or Mp2ObjectType.Temple
                or Mp2ObjectType.WateringHole => DescribeLocalVisitObject(objectType, focusHero),

            Mp2ObjectType.ShrineFirstCircle or Mp2ObjectType.ShrineSecondCircle
                or Mp2ObjectType.ShrineThirdCircle =>
                DescribeShrine(tile, world, objectType, kingdom.IsVisited(tile), focusHero),

            Mp2ObjectType.WitchsHut =>
                DescribeWitchHut(tile, world, objectType, kingdom.IsVisited(tile), focusHero),

            Mp2ObjectType.Barrier => DescribeBarrier(tile, world, kingdom),

            Mp2ObjectType.TravellerTent => DescribeTent(tile, world, kingdom),

            Mp2ObjectType.TreeOfKnowledge =>
                DescribeTreeOfKnowledge(tile, world, objectType, kingdom.IsVisited(tile), focusHero),

            // 参考：这几样没有额外文字。
            _ => Mp2ObjectName.Of(objectType),
        };
    }

    /// <summary>
    /// The quick info of a tile for callers that have no world at hand.
    /// </summary>
    /// <remarks>
    /// Only the branches that read nothing but the tile itself survive: the surface of bare ground, the
    /// resource of a pile (named by its sprite, as this method always did) and the object name of
    /// <c>MP2::StringObject()</c>. Garrisons, ownership and the digging advice need the world.
    /// </remarks>
    /// <param name="tile">Tile under the cursor.</param>
    /// <returns>The text to show in the popup.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static string ForTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        // The reference calls getMainObjectType( false ), which keeps the object a hero stands on.
        Mp2ObjectType objectType = tile.MainObjectType;

        return objectType switch
        {
            Mp2ObjectType.Coast or Mp2ObjectType.Event or Mp2ObjectType.None => DescribeGround(tile),
            Mp2ObjectType.Resource => DescribeResourcePile(tile),
            _ => Mp2ObjectName.Of(objectType),
        };
    }

    /// <summary>
    /// The first line of <c>showGroundInfo()</c>: the coast, the road or the ground.
    /// </summary>
    /// <param name="tile">Tile to describe.</param>
    /// <returns>The name of the surface.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static string DescribeGround(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        if (tile.MainObjectType == Mp2ObjectType.Coast)
        {
            return Mp2ObjectName.Of(Mp2ObjectType.Coast);
        }

        return tile.IsRoad ? Road : tile.Ground.ToString();
    }

    /// <summary>
    /// The <c>OBJ_RESOURCE</c> branch for callers with no world: the sprite of the pile names the resource.
    /// </summary>
    /// <param name="tile">Tile of the pile.</param>
    /// <returns>The resource name, capitalized the way <c>Resource::String()</c> prints it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static string DescribeResourcePile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        Mp2ResourceType resource = Mp2ObjectData.ResourceFromSpriteIndex(tile.MainObjectPart.IcnIndex);
        string name = Mp2ObjectData.Name(resource);

        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// The income a mine adds to the daily funds, <c>getMineIncomeString()</c>
    /// (<c>dialog_quickinfo.cpp:135-152</c>).
    /// </summary>
    /// <remarks>
    /// The reference writes a sign and then the number, so a negative value would come out as <c>(--5)</c>;
    /// mine income is positive and this prints the sign once.
    /// </remarks>
    /// <param name="resource">Resource the object yields.</param>
    /// <returns><c> (+500)</c> style suffix, empty when the object yields nothing of that resource.</returns>
    public static string MineIncome(int resource)
    {
        int value = ProfitConditions.FromMine(resource).Get(resource);

        if (value == 0)
        {
            return string.Empty;
        }

        return value > 0 ? $" (+{value})" : $" ({value})";
    }

    /// <summary>The object type of a tile as <c>getMainObjectType( false )</c> reports it, action bit removed.</summary>
    private static Mp2ObjectType ObjectTypeOf(Tile tile, World world) =>
        Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(world, false));

    /// <summary>
    /// <c>showGroundInfo()</c> (<c>dialog_quickinfo.cpp:333-373</c>): the surface, whether digging is worth a
    /// try and how much the ground slows a hero down.
    /// </summary>
    private static string DescribeGround(Tile tile, World world, Hero? focusHero)
    {
        string text = DescribeGround(tile);

        text += "\n\n";
        text += tile.IsSuitableForUltimateArtifact(world) || world.UltimateArtifact.IsAt(tile.Index)
            ? "(digging ok)"
            : "(no digging)";

        if (focusHero is not null)
        {
            uint cost = tile.IsRoad
                ? MapGrounds.RoadPenalty
                : MapGrounds.GetPenalty(tile.Ground, focusHero.GetLevelSkill(SecondarySkill.Pathfinding));

            if (cost > 0)
            {
                text += $"\npenalty: {cost}";
            }
        }

        return text;
    }

    /// <summary><c>OBJ_RESOURCE</c> branch: the resource the pile holds.</summary>
    private static string DescribeResourcePile(Tile tile, World world)
    {
        (int type, _) = tile.GetFundsFromTile(world).GetFirstValidResource();

        return ResourceText.Of(type);
    }

    /// <summary>
    /// <c>showGuardiansInfo()</c> (<c>dialog_quickinfo.cpp:167-202</c>).
    /// </summary>
    private static string DescribeGuarded(Tile tile, World world, bool isOwned)
    {
        Mp2ObjectType objectType = ObjectTypeOf(tile, world);
        string text = objectType == Mp2ObjectType.Mine
            ? DescribeMine(tile, world, isOwned)
            : Mp2ObjectName.Of(objectType);

        Troop troop = tile.GetTroop(world);

        if (!troop.IsValid)
        {
            return text;
        }

        text += "\n\n";

        if (isOwned)
        {
            text = ArmyTroops.StringReplaceWithLowercase(
                text + "guarded by %{count} %{monster}",
                "%{count}",
                ArmyTroops.FormatMonsterCount(troop.Count, true));

            return ArmyTroops.StringReplaceWithLowercase(text, "%{monster}", TroopName(troop));
        }

        return text + "guarded by " + ArmyTroops.TroopSizeString(troop).ToLowerInvariant();
    }

    /// <summary>
    /// <c>showMonsterInfo()</c> (<c>dialog_quickinfo.cpp:204-223</c>).
    /// </summary>
    /// <remarks>
    /// The reference remembers that a Crystal Ball showed this stack, so a later visit keeps the exact count
    /// while the spell lasts — a side effect hidden inside a text function.
    /// </remarks>
    private static string DescribeMonster(Tile tile, World world, GameKingdom kingdom)
    {
        if (world.Geometry is not { } geometry)
        {
            return ArmyTroops.TroopSizeString(tile.GetTroop(world));
        }

        bool visible = kingdom.IsTileVisibleFromCrystalBall(geometry, tile.Index);

        if (visible)
        {
            kingdom.AddMonsterUnderVision(tile.Index);
        }

        return DescribeTroop(tile.GetTroop(world), visible || kingdom.IsMonsterUnderVisionSpell(tile.Index));
    }

    /// <summary>
    /// <c>"%{count} %{monster}"</c> when the count is known, the size bracket otherwise.
    /// </summary>
    private static string DescribeTroop(Troop troop, bool detailed)
    {
        if (!detailed)
        {
            return ArmyTroops.TroopSizeString(troop);
        }

        string text = ArmyTroops.StringReplaceWithLowercase(
            "%{count} %{monster}",
            "%{count}",
            ArmyTroops.FormatMonsterCount(troop.Count, true));

        return ArmyTroops.StringReplaceWithLowercase(text, "%{monster}", TroopName(troop));
    }

    /// <summary>The singular or plural name of a stack, <c>Troop::GetName()</c> / <c>GetMultiName()</c>.</summary>
    private static string TroopName(Troop troop) =>
        troop.Count == 1 ? MonsterNames.GetSingular(troop.Monster) : MonsterNames.GetMulti(troop.Monster);

    /// <summary><c>showMineInfo()</c> (<c>dialog_quickinfo.cpp:154-165</c>).</summary>
    private static string DescribeMine(Tile tile, World world, bool isOwned)
    {
        (int type, _) = tile.GetDailyIncomeObjectResources(world).GetFirstValidResource();
        string text = MineText.GetMineName(type);

        return isOwned ? text + MineIncome(type) : text;
    }

    /// <summary>The <c>OBJ_ALCHEMIST_LAB</c> / <c>OBJ_SAWMILL</c> branch: name plus daily income.</summary>
    private static string DescribeIncomeObject(Tile tile, World world, bool isOwned)
    {
        Mp2ObjectType objectType = ObjectTypeOf(tile, world);
        string text = Mp2ObjectName.Of(objectType);

        if (!isOwned)
        {
            return text;
        }

        (int type, _) = tile.GetDailyIncomeObjectResources(world).GetFirstValidResource();

        return text + MineIncome(type);
    }

    /// <summary><c>showDwellingInfo()</c> (<c>dialog_quickinfo.cpp:225-244</c>).</summary>
    private static string DescribeDwelling(Tile tile, World world, bool isOwned)
    {
        Mp2ObjectType objectType = ObjectTypeOf(tile, world);
        string text = Mp2ObjectName.Of(objectType);

        if (!isOwned)
        {
            return text;
        }

        Troop troop = tile.GetTroop(world);

        return text + "\n\n" + (troop.IsValid
            ? ArmyTroops.StringReplaceWithLowercase(
                "(available: %{count})",
                "%{count}",
                ArmyTroops.FormatMonsterCount(troop.Count, true))
            : "(empty)");
    }

    /// <summary><c>showShrineInfo()</c> (<c>dialog_quickinfo.cpp:246-269</c>).</summary>
    private static string DescribeShrine(
        Tile tile,
        World world,
        Mp2ObjectType objectType,
        bool isVisited,
        Hero? focusHero)
    {
        string text = Mp2ObjectName.Of(objectType);

        if (!isVisited)
        {
            return text;
        }

        Spell spell = tile.GetSpellFromTile(world);

        text += $"\n\n({SpellText.GetName(spell.Type)})";

        if (focusHero is not null && focusHero.HasSpell(spell))
        {
            text += "\n(already learned)";
        }

        return text;
    }

    /// <summary><c>showTreeOfKnowledgeInfo()</c> (<c>dialog_quickinfo.cpp:271-307</c>).</summary>
    private static string DescribeTreeOfKnowledge(
        Tile tile,
        World world,
        Mp2ObjectType objectType,
        bool isVisited,
        Hero? focusHero)
    {
        string text = Mp2ObjectName.Of(objectType);

        if (!isVisited)
        {
            return focusHero is null
                ? text
                : text + (focusHero.IsVisited(tile) ? "\n\n(already claimed)" : "\n\n(not claimed)");
        }

        Funds payment = tile.GetTreeOfKnowledgeRequirement(world);

        text += "\n\n(";

        if (payment.GetValidItemsCount() == 0)
        {
            text += "free";
        }
        else
        {
            (int type, int amount) = payment.GetFirstValidResource();
            text += $"{amount} {ResourceText.Of(type).ToLowerInvariant()}";
        }

        text += ")";

        if (focusHero is not null)
        {
            text += focusHero.IsVisited(tile) ? "\n(already claimed)" : "\n(not claimed)";
        }

        return text;
    }

    /// <summary><c>showWitchHutInfo()</c> (<c>dialog_quickinfo.cpp:309-331</c>).</summary>
    private static string DescribeWitchHut(
        Tile tile,
        World world,
        Mp2ObjectType objectType,
        bool isVisited,
        Hero? focusHero)
    {
        string text = Mp2ObjectName.Of(objectType);

        if (!isVisited)
        {
            return text;
        }

        SecondarySkill skill = tile.GetSecondarySkillFromWitchsHut(world).Skill;

        text += $"\n\n({SecondarySkills.GetName(skill)})";

        if (focusHero is null)
        {
            return text;
        }

        if (focusHero.HasSecondarySkill(skill))
        {
            text += "\n(already knows this skill)";
        }
        else if (focusHero.HasMaxSecondarySkill())
        {
            text += "\n(already has max skills)";
        }

        return text;
    }

    /// <summary>The "visited by this hero" branch, <c>showLocalVisitTileInfo()</c>.</summary>
    private static string DescribeLocalVisitTile(Tile tile, Mp2ObjectType objectType, Hero? focusHero)
    {
        string text = Mp2ObjectName.Of(objectType);

        if (focusHero is null)
        {
            return text;
        }

        return text + "\n\n" + (focusHero.IsVisited(tile) ? "(already visited)" : "(not visited)");
    }

    /// <summary>The "visited by anyone" branch, <c>showLocalVisitObjectInfo()</c>.</summary>
    private static string DescribeLocalVisitObject(Mp2ObjectType objectType, Hero? focusHero)
    {
        string text = Mp2ObjectName.Of(objectType);

        if (focusHero is null)
        {
            return text;
        }

        return text + "\n\n" + (focusHero.IsObjectTypeVisited(objectType) ? "(already visited)" : "(not visited)");
    }

    /// <summary>The kingdom wide visit flag, <c>showObjectVisitInfo()</c>.</summary>
    private static string DescribeObjectVisit(Mp2ObjectType objectType, bool isVisited) =>
        Mp2ObjectName.Of(objectType) + "\n\n" + (isVisited ? "(already visited)" : "(not visited)");

    /// <summary><c>showBarrierInfo()</c> (<c>dialog_quickinfo.cpp:375-392</c>).</summary>
    private static string DescribeBarrier(Tile tile, World world, GameKingdom kingdom)
    {
        int color = tile.GetBarrierColorFromTile(world);
        string text = $"{BarrierColors.GetBarrierName(color)} Barrier";

        return kingdom.IsTravellerTentVisited(color) ? text + "\n\n(tent visited)" : text;
    }

    /// <summary><c>showTentInfo()</c> (<c>dialog_quickinfo.cpp:394-443</c>).</summary>
    private static string DescribeTent(Tile tile, World world, GameKingdom kingdom)
    {
        int color = tile.GetBarrierColorFromTile(world);
        string text = $"{BarrierColors.GetTentName(color)} Tent";

        return kingdom.IsTravellerTentVisited(color) ? text + "\n\n(already visited)" : text;
    }
}
