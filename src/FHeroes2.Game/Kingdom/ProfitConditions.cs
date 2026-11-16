using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// What one mine, one building and one artifact are worth every day, <c>ProfitConditions</c>
/// (<c>profit.cpp:33-122</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference spells its answers as <c>Funds( Cost{ ... } )</c>, and <c>Cost</c> orders its fields
/// <c>{ gold, wood, mercury, ore, sulfur, crystal, gems }</c> (<c>resource.h:37-46</c>) — note that mercury comes
/// before ore, which is the one place where the tables below look wrong and are not.
/// </para>
/// <para>
/// This class is the whole of <c>profit.cpp</c>: three tables and nothing else. It is kept apart from
/// <see cref="Kingdom"/> because the castles' buy dialog shows the same numbers (ADR-112).
/// </para>
/// </remarks>
public static class ProfitConditions
{
    /// <summary>
    /// The daily production of one mine, <c>ProfitConditions::FromMine()</c> (<c>profit.cpp:98</c>).
    /// </summary>
    /// <remarks>
    /// The reference asserts on an unknown type; the port returns an empty <see cref="Funds"/> for it, because a
    /// mine whose resource byte is garbage earns nothing rather than aborting the game.
    /// </remarks>
    /// <param name="resourceType">A <see cref="Resource"/> constant.</param>
    /// <returns>What one mine of that resource produces per day.</returns>
    public static Funds FromMine(int resourceType) => resourceType switch
    {
        // profit.cpp:101-114. Two of wood and ore, one of the four gems and crystals, a thousand of gold.
        Resource.Wood => new Funds { Wood = 2 },
        Resource.Mercury => new Funds { Mercury = 1 },
        Resource.Ore => new Funds { Ore = 2 },
        Resource.Sulfur => new Funds { Sulfur = 1 },
        Resource.Crystal => new Funds { Crystal = 1 },
        Resource.Gems => new Funds { Gems = 1 },
        Resource.Gold => new Funds { Gold = 1000 },
        _ => default,
    };

    /// <summary>
    /// The daily production of one building, <c>ProfitConditions::FromBuilding()</c> (<c>profit.cpp:33</c>).
    /// </summary>
    /// <remarks>
    /// Only four buildings pay anything, and one of the four pays only for the warlock: the dungeon
    /// (<c>BUILD_SPEC</c>) is a gold mine for him alone.
    /// </remarks>
    /// <param name="building">The building.</param>
    /// <param name="race">The race of its town; only <see cref="MapRace.Warlock"/> matters.</param>
    /// <returns>What the building produces per day; empty for the other twenty-nine.</returns>
    public static Funds FromBuilding(BuildingType building, MapRace race) => building switch
    {
        BuildingType.Castle => new Funds { Gold = 1000 },
        BuildingType.Tent or BuildingType.Statue => new Funds { Gold = 250 },
        BuildingType.Spec when race == MapRace.Warlock => new Funds { Gold = 500 },
        _ => default,
    };

    /// <summary>
    /// What one artifact pays or costs every day, <c>ProfitConditions::FromArtifact()</c>
    /// (<c>profit.cpp:52</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference walks the artifact's bonuses and curses and keeps the eight that name a resource; every other
    /// bonus is somebody else's business. The port walks <see cref="ArtifactTable.Get"/> for the same reason.
    /// </para>
    /// <para>
    /// The result can be negative: the tax lien artifact takes gold away instead of giving it.
    /// </para>
    /// </remarks>
    /// <param name="artifact">The artifact.</param>
    /// <returns>Its daily income, or its daily penalty.</returns>
    public static Funds FromArtifact(ArtifactType artifact)
    {
        ArtifactData data = ArtifactTable.Get(artifact);

        Funds income = default;

        foreach (ArtifactBonus bonus in data.Bonuses)
        {
            switch (bonus.Type)
            {
                case ArtifactBonusType.GoldIncome:
                    income.Gold += bonus.Value;
                    break;
                case ArtifactBonusType.WoodIncome:
                    income.Wood += bonus.Value;
                    break;
                case ArtifactBonusType.OreIncome:
                    income.Ore += bonus.Value;
                    break;
                case ArtifactBonusType.MercuryIncome:
                    income.Mercury += bonus.Value;
                    break;
                case ArtifactBonusType.GemsIncome:
                    income.Gems += bonus.Value;
                    break;
                case ArtifactBonusType.SulfurIncome:
                    income.Sulfur += bonus.Value;
                    break;
                case ArtifactBonusType.CrystalIncome:
                    income.Crystal += bonus.Value;
                    break;
                default:
                    break;
            }
        }

        foreach (ArtifactCurse curse in data.Curses)
        {
            // The only curse that names a resource (profit.cpp:85-93).
            if (curse.Type == ArtifactCurseType.GoldPenalty)
            {
                income.Gold -= curse.Value;
            }
        }

        return income;
    }
}
