using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The helpers every part of the AI uses, the free functions of <c>ai_common.h</c> (<c>ai_common.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference groups them in <c>namespace AI</c> as free functions because they are shared by the planner, the
/// hero actions and the battle AI; the port keeps that shape instead of hanging them off a class that owns no state.
/// </para>
/// <para>
/// Two calls of the reference have no one to one port because <c>Castle</c> does not own a kingdom yet: the
/// reference asks <c>castle.GetKingdom()</c> and <c>world.GetHero( *this )</c>, the port resolves both through
/// <see cref="World.Current"/>.
/// </para>
/// </remarks>
public static class AiCommon
{
    /// <summary>
    /// Builds a building, trading for what is missing, <c>AI::BuildIfPossible()</c> (<c>ai_common.h:46</c>,
    /// <c>ai_common.cpp:57</c>).
    /// </summary>
    /// <param name="castle">Castle to build in.</param>
    /// <param name="building">The building.</param>
    /// <returns><see langword="true"/> when the building now stands.</returns>
    public static bool BuildIfPossible(CastleTown castle, BuildingType building)
    {
        World world = World.Current;
        GameKingdom kingdom = world.GetKingdom(castle.Color);

        switch (castle.CheckBuyBuilding(building, kingdom.Treasury))
        {
            case BuildingStatus.LackResources:
                if (!TradeAtMarketplace(kingdom, PaymentConditions.BuyBuilding(castle.Race, building)))
                {
                    return false;
                }

                break;

            case BuildingStatus.AllowBuild:
                break;

            default:
                return false;
        }

        return castle.BuyBuilding(building, ref kingdom.Treasury, FindGuestHero(world, castle));
    }

    /// <summary>
    /// Builds a building if the kingdom can spare the resources, <c>AI::BuildIfEnoughFunds()</c>
    /// (<c>ai_common.h:52</c>, <c>ai_common.cpp:81</c>).
    /// </summary>
    /// <param name="castle">Castle to build in.</param>
    /// <param name="building">The building.</param>
    /// <param name="fundsMultiplier">How many times the price the kingdom must still have left afterwards.</param>
    /// <returns><see langword="true"/> when the building now stands.</returns>
    public static bool BuildIfEnoughFunds(CastleTown castle, BuildingType building, uint fundsMultiplier)
    {
        if (fundsMultiplier is < 1 or > 99)
        {
            return false;
        }

        GameKingdom kingdom = World.Current.GetKingdom(castle.Color);
        Funds requiredFunds = PaymentConditions.BuyBuilding(castle.Race, building) * fundsMultiplier;

        if (!kingdom.AllowPayment(requiredFunds) && CalculateMarketplaceTransaction(kingdom, requiredFunds) is null)
        {
            return false;
        }

        return BuildIfPossible(castle, building);
    }

    /// <summary>
    /// Rearranges an army before a battle, <c>AI::OptimizeTroopsOrder()</c> (<c>ai_common.h:55</c>,
    /// <c>ai_common.cpp:102</c>).
    /// </summary>
    /// <remarks>
    /// The slots are filled from the reference's three rules: archers first, then the melee troops sorted by speed,
    /// then flying before walking and slower before faster; the strongest of each group goes to the low slots. On
    /// hard and above the weakest stack is split at the end, which makes the enemy waste a whole attack on it.
    /// </remarks>
    /// <param name="army">The army to rearrange; modified in place.</param>
    public static void OptimizeTroopsOrder(GameArmy army)
    {
        if (!army.IsValid)
        {
            return;
        }

        army.MergeSameMonsterTroops();

        List<Troop> archers = [];
        List<Troop> others = [];

        for (int slot = 0; slot < GameArmy.MaximumTroopCount; ++slot)
        {
            Troop troop = army.GetTroop(slot);
            if (!troop.IsValid)
            {
                continue;
            }

            (troop.IsArchers ? archers : others).Add(new Troop(troop.Monster, troop.Count));
        }

        others.Sort(static (left, right) =>
        {
            uint leftSpeed = (uint)left.GetSpeed();
            uint rightSpeed = (uint)right.GetSpeed();
            if (leftSpeed != rightSpeed)
            {
                return leftSpeed < rightSpeed ? -1 : 1;
            }

            if (left.IsFlying != right.IsFlying)
            {
                return right.IsFlying ? -1 : 1;
            }

            return left.GetStrength().CompareTo(right.GetStrength());
        });

        archers.Sort(static (left, right) => left.GetStrength().CompareTo(right.GetStrength()));

        int[] slotOrder = [2, 1, 3, 0, 4];

        switch (archers.Count)
        {
            case 1:
                slotOrder = others.Count == 1 ? [0, 2, 1, 3, 4] : [0, 1, 2, 3, 4];
                break;

            case 2:
                slotOrder = others.Count == 3 ? [0, 2, 3, 1, 4] : [0, 4, 2, 1, 3];
                break;

            case 3:
                slotOrder = others.Count == 1 ? [0, 4, 2, 1, 3] : [0, 3, 2, 1, 4];
                break;

            case 4:
                slotOrder = [0, 4, 2, 3, 1];
                break;

            case 5:
                slotOrder = [0, 4, 1, 2, 3];
                break;
        }

        army.Clean();

        int archerIndex = archers.Count - 1;
        int otherIndex = others.Count - 1;

        foreach (int slot in slotOrder)
        {
            if (archerIndex >= 0)
            {
                army.GetTroop(slot).Set(archers[archerIndex]);
                --archerIndex;
            }
            else if (otherIndex >= 0)
            {
                army.GetTroop(slot).Set(others[otherIndex]);
                --otherIndex;
            }
            else
            {
                break;
            }
        }

        if (Difficulty.AllowAiToSplitWeakStacks(Settings.Get().GameDifficulty))
        {
            army.SplitStackOfWeakestUnitsIntoFreeSlots();
        }
    }

    /// <summary>
    /// Moves the slowest troops to the garrison so the hero gets a movement bonus next turn,
    /// <c>AI::transferSlowestTroopsToGarrison()</c> (<c>ai_common.h:58</c>, <c>ai_common.cpp:206</c>).
    /// </summary>
    /// <remarks>The fastest stack always stays with the hero; the rest move only when the garrison accepts them.</remarks>
    /// <param name="hero">The hero whose army is trimmed.</param>
    /// <param name="castle">The castle that receives the troops, or <see langword="null"/> when the hero is not in one.</param>
    public static void TransferSlowestTroopsToGarrison(Hero hero, CastleTown? castle)
    {
        if (castle is null)
        {
            return;
        }

        GameArmy army = hero.Army;
        GameArmy garrison = castle.Army;
        garrison.MergeSameMonsterTroops();

        List<Troop> armyTroops = [];

        for (int slot = 0; slot < GameArmy.MaximumTroopCount; ++slot)
        {
            Troop troop = army.GetTroop(slot);
            if (!troop.IsEmpty)
            {
                armyTroops.Add(troop);
            }
        }

        armyTroops.Sort(static (left, right) => ((uint)left.GetSpeed()).CompareTo((uint)right.GetSpeed()));

        if (armyTroops.Count <= 1)
        {
            return;
        }

        armyTroops.RemoveAt(armyTroops.Count - 1);

        foreach (Troop troop in armyTroops)
        {
            if (!garrison.JoinTroop(troop))
            {
                break;
            }

            troop.Reset();
        }
    }

    /// <summary>
    /// The marketplace deal that would yield a given amount, <c>AI::calculateMarketplaceTransaction()</c>
    /// (<c>ai_common.h:64</c>, <c>ai_common.cpp:249</c>).
    /// </summary>
    /// <remarks>
    /// The returned transaction has to be subtracted from the kingdom's funds to become real; the function itself
    /// changes nothing. Each missing resource is covered by exchanging whichever resource yields the most of it,
    /// one round at a time, until the balance is no longer negative.
    /// </remarks>
    /// <param name="kingdom">The kingdom that trades.</param>
    /// <param name="fundsToObtain">What the kingdom has to be able to pay afterwards.</param>
    /// <returns>The transaction, or <see langword="null"/> when trading cannot produce the amount.</returns>
    public static Funds? CalculateMarketplaceTransaction(GameKingdom kingdom, in Funds fundsToObtain)
    {
        uint marketplaceCount = (uint)kingdom.CountMarketplaces();
        if (marketplaceCount == 0)
        {
            return null;
        }

        int[] resourceTypes = [Resource.Wood, Resource.Mercury, Resource.Ore, Resource.Sulfur, Resource.Crystal, Resource.Gems, Resource.Gold];
        int[] plannedBalance = new int[resourceTypes.Length];
        int[] transaction = new int[resourceTypes.Length];

        Funds fundsDiff = kingdom.Treasury - fundsToObtain;
        for (int index = 0; index < resourceTypes.Length; ++index)
        {
            plannedBalance[index] = fundsDiff.Get(resourceTypes[index]);
        }

        for (int missing = 0; missing < resourceTypes.Length; ++missing)
        {
            while (plannedBalance[missing] < 0)
            {
                int resourceToExchange = -1;
                long maxPotentialRevenue = 0;

                for (int candidate = 0; candidate < resourceTypes.Length; ++candidate)
                {
                    if (candidate == missing || plannedBalance[candidate] <= 0)
                    {
                        continue;
                    }

                    uint tradeCost = ResourceTrading.GetTradeCost(marketplaceCount, resourceTypes[candidate], resourceTypes[missing]);
                    if (tradeCost == 0)
                    {
                        continue;
                    }

                    long potentialRevenue = resourceTypes[missing] == Resource.Gold
                        ? (long)plannedBalance[candidate] * tradeCost
                        : plannedBalance[candidate] / tradeCost;

                    if (maxPotentialRevenue < potentialRevenue)
                    {
                        resourceToExchange = candidate;
                        maxPotentialRevenue = potentialRevenue;
                    }
                }

                if (resourceToExchange < 0)
                {
                    return null;
                }

                uint cost = ResourceTrading.GetTradeCost(marketplaceCount, resourceTypes[resourceToExchange], resourceTypes[missing]);

                if (resourceTypes[missing] == Resource.Gold)
                {
                    --plannedBalance[resourceToExchange];
                    plannedBalance[missing] += (int)cost;
                    ++transaction[resourceToExchange];
                    transaction[missing] -= (int)cost;
                }
                else
                {
                    plannedBalance[resourceToExchange] -= (int)cost;
                    ++plannedBalance[missing];
                    transaction[resourceToExchange] += (int)cost;
                    --transaction[missing];
                }
            }
        }

        Funds plannedTransaction = default;
        for (int index = 0; index < resourceTypes.Length; ++index)
        {
            ref int amount = ref Funds.GetPtr(ref plannedTransaction, resourceTypes[index]);
            amount = transaction[index];
        }

        return plannedTransaction;
    }

    /// <summary>
    /// Trades on the marketplace until the kingdom can pay an amount, <c>AI::tradeAtMarketplace()</c>
    /// (<c>ai_common.h:69</c>, <c>ai_common.cpp:368</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom that trades; its funds change when the trade succeeds.</param>
    /// <param name="fundsToObtain">What the kingdom has to be able to pay afterwards.</param>
    /// <returns><see langword="true"/> when the funds are now available.</returns>
    public static bool TradeAtMarketplace(GameKingdom kingdom, in Funds fundsToObtain)
    {
        Funds? transaction = CalculateMarketplaceTransaction(kingdom, fundsToObtain);
        if (transaction is null)
        {
            return false;
        }

        kingdom.OddFundsResource(transaction.Value);
        return true;
    }

    /// <summary>
    /// Tells allied AI kingdoms about an object a hero just visited,
    /// <c>AI::shareObjectVisitInfoWithAllies()</c> (<c>ai_common.h:74</c>, <c>ai_common.cpp:389</c>).
    /// </summary>
    /// <remarks>
    /// Nothing is shared when any ally of the kingdom is a human player: the reference assumes that AI allies talk to
    /// each other and that a human ally does not. Easy and normal switch the sharing off entirely.
    /// </remarks>
    /// <param name="kingdom">The kingdom whose hero visited the object.</param>
    /// <param name="tileIndex">Tile the object stands on.</param>
    public static void ShareObjectVisitInfoWithAllies(GameKingdom kingdom, int tileIndex)
    {
        if (!Difficulty.IsObjectVisitInfoSharingAllowedForAi(Settings.Get().GameDifficulty))
        {
            return;
        }

        if (kingdom.IsControlHuman)
        {
            return;
        }

        PlayerColor friendColors = Players.GetPlayerFriends(kingdom.Color);
        if (friendColors == PlayerColor.None)
        {
            return;
        }

        if ((friendColors & Players.HumanColors()) != PlayerColor.None)
        {
            return;
        }

        World world = World.Current;

        // The port stores the stripped object type in the visit registries (ADR-133), unlike the reference.
        Mp2ObjectType objectType = world.GetTile(tileIndex).GetNonActionObjectType(world);

        foreach (PlayerColor color in PlayerColors.ToList(friendColors))
        {
            world.GetKingdom(color).SetVisited(tileIndex, objectType);
        }
    }

    /// <summary>
    /// Whether a hero may pick the ultimate artifact up, <c>AI::isUltimateArtifactAvailableToHero()</c>
    /// (<c>ai_common.h:79</c>, <c>ai_common.cpp:418</c>).
    /// </summary>
    /// <param name="artifact">The ultimate artifact of the current map.</param>
    /// <param name="hero">The hero that would dig.</param>
    /// <returns><see langword="true"/> when the hero knows where it is and has a free slot.</returns>
    public static bool IsUltimateArtifactAvailableToHero(UltimateArtifact artifact, Hero hero)
    {
        if (artifact.IsFound)
        {
            return false;
        }

        GameKingdom kingdom = World.Current.GetKingdom(hero.Color);
        if (kingdom.GetPuzzleMaps().Count != Puzzle.NumOfPuzzleTiles)
        {
            return false;
        }

        if (hero.BagArtifacts.IsFull)
        {
            return false;
        }

        World world = World.Current;
        int position = artifact.Position;

        return world.Geometry?.IsValidAbsIndex(position) == true &&
               world.GetTile(position).IsSuitableForUltimateArtifact(world);
    }

    /// <summary>
    /// Whether an adventure map object is worth visiting at all, <c>AI::isValuableAdventureMapObject()</c>
    /// (<c>ai_common.h:82</c>, <c>ai_common.cpp:438</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom that would visit it.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="tileIndex">Tile the object stands on.</param>
    /// <returns><see langword="true"/> when the object can benefit the AI under any circumstances.</returns>
    public static bool IsValuableAdventureMapObject(GameKingdom kingdom, Mp2ObjectType objectType, int tileIndex)
    {
        // The tile keeps the value from the map file, i.e. with the action bit; the port compares the stripped
        // value everywhere (ADR-133). Accept both so a caller that already stripped the bit works too.
        Mp2ObjectType type = Mp2ObjectTypes.NonActionType(objectType);

        // The reference's isInGameActionObject(): the event object carries the action bit but a hero does not
        // trigger it by walking onto it, and the decorations have no action twin at all.
        if (!Mp2ObjectTypes.CanBeActionObject(type) || type == Mp2ObjectType.Event)
        {
            return false;
        }

        World world = World.Current;

        switch (type)
        {
            case Mp2ObjectType.Castle:
            {
                CastleTown? castle = world.GetCastleEntrance(tileIndex);
                if (castle is null)
                {
                    Debug.Fail("A tile marked as a castle has no castle entrance.");
                    return false;
                }

                // We can interact only with enemy castles.
                return kingdom.Color == castle.Color || !Players.IsFriends(kingdom.Color, castle.Color);
            }

            case Mp2ObjectType.Hero:
            {
                Hero? otherHero = world.GetTile(tileIndex).GetHero(world);
                if (otherHero is null)
                {
                    // It could happen when adding new heroes at the start of the game.
                    return false;
                }

                // We can interact only with enemy heroes.
                return kingdom.Color == otherHero.Color || !Players.IsFriends(kingdom.Color, otherHero.Color);
            }

            case Mp2ObjectType.MagellansMaps:
                // Magellan's Maps object should be visited only once.
                return !kingdom.IsVisited(type);

            case Mp2ObjectType.AlchemistLab:
            case Mp2ObjectType.Lighthouse:
            case Mp2ObjectType.Mine:
            case Mp2ObjectType.Sawmill:
                // Heroes cannot re-capture their own or ally's objects.
                return !Players.IsFriends(kingdom.Color, world.GetTile(tileIndex).GetColor(world));

            case Mp2ObjectType.DaemonCave:
            case Mp2ObjectType.LeanTo:
            case Mp2ObjectType.Skeleton:
            case Mp2ObjectType.Wagon:
                // Single visit objects with no resources have no value.
                return world.GetTile(tileIndex).DoesTileContainValuableItems(world);

            case Mp2ObjectType.ObservationTower:
            {
                // If an Observation Tower uncovers nothing, then we skip it.
                Point point = world.Geometry!.Value.GetPoint(tileIndex);
                int fogCountToUncover = world.Fog!.CountFoggedAround(
                    point.X, point.Y, MapFog.ObservationTowerRevealDistance, kingdom.Color);

                return fogCountToUncover > 0;
            }

            case Mp2ObjectType.Obelisk:
                // An obelisk should be visited only once.
                return !kingdom.IsVisited(tileIndex, type);

            case Mp2ObjectType.TravellerTent:
                // Traveller's Tent should be visited only once.
                return !kingdom.IsTravellerTentVisited(world.GetTile(tileIndex).GetBarrierColorFromTile(world));

            case Mp2ObjectType.DerelictShip:
            case Mp2ObjectType.Graveyard:
            case Mp2ObjectType.Pyramid:
            case Mp2ObjectType.Shipwreck:
                // Single visit objects with no resources have no value.
                return !kingdom.IsVisited(tileIndex, type) &&
                       world.GetTile(tileIndex).DoesTileContainValuableItems(world);

            case Mp2ObjectType.HutOfMagi:
                // A Hut of Magi should uncover all areas at once so visiting any other similar object makes no sense.
                return !kingdom.IsVisited(type) && world.DoesObjectExistOnMap(Mp2ObjectType.EyeOfMagi);

            case Mp2ObjectType.Boat:
            case Mp2ObjectType.EyeOfMagi:
            case Mp2ObjectType.Oracle:
            case Mp2ObjectType.Sign:
            case Mp2ObjectType.StoneLiths:
            case Mp2ObjectType.Whirlpool:
                // These objects aren't considered valuable.
                return false;

            default:
                break;
        }

        return true;
    }

    /// <summary>
    /// The hero standing on the castle, <c>world.GetHero( castle )</c> of the reference.
    /// </summary>
    /// <remarks>
    /// Kept as a thin alias of <see cref="World.GetHero(CastleTown)"/> for the planner, which asks the same question
    /// when it decides whether a castle has anyone to hand its garrison to.
    /// </remarks>
    internal static Hero? FindGuestHero(World world, CastleTown castle) => world.GetHero(castle);
}
