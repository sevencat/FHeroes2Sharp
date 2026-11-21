using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// The reward tables of every campaign scenario, the six functions of
/// <c>campaign_scenariodata.cpp:44-334</c> (<c>getRolandCampaignBonusData()</c> and friends).
/// </summary>
/// <remarks>
/// 表按「战役 + 关号」硬编码：每关二到三个选项，选中的编号存进 <see cref="CampaignSaveData.CurrentScenarioBonusId"/>。
/// 关号从 0 开始，没有奖励的关直接落到默认分支。
/// </remarks>
internal static partial class CampaignScenarioBonusTables
{
    /// <summary>The rewards of Roland's campaign, <c>getRolandCampaignBonusData()</c>.</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The rewards.</returns>
    internal static List<ScenarioBonusData> Roland(int scenarioId)
    {
        return scenarioId switch
        {
            0 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.ThunderMace, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.ArmoredGauntlets, 1),
            ],
            1 or 2 or 3 or 4 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Wizard, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Sorceress, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Knight, 1),
            ],
            5 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.MirrorImage, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.SummonEElement, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.Resurrect, 1),
            ],
            6 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.BlackPearl, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.DragonSword, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.DivineBreastplate, 1),
            ],
            7 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Wizard, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Sorceress, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Knight, 1),
            ],
            8 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Crystal, 20),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gems, 20),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Mercury, 20),
            ],
            9 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.TaxLien, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.HideousMask, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.FizbinMisfortune, 1),
            ],
            10 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Necromancer, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Warlock, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Barbarian, 1),
            ],
            _ => [],
        };
    }

    /// <summary>The rewards of Archibald's campaign, <c>getArchibaldCampaignBonusData()</c>.</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The rewards.</returns>
    internal static List<ScenarioBonusData> Archibald(int scenarioId)
    {
        return scenarioId switch
        {
            0 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MageRing, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MinorScroll, 1),
            ],
            1 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Necromancer, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Warlock, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Barbarian, 1),
            ],
            2 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.MassCurse, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.DefenderHelm, 1),
            ],
            3 or 4 or 5 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Necromancer, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Warlock, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Barbarian, 1),
            ],
            6 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRaceAndArmy, (int)MapRace.Necromancer, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRaceAndArmy, (int)MapRace.Warlock, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRaceAndArmy, (int)MapRace.Barbarian, 1),
            ],
            7 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Logistics, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.PowerAxe, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.WhitePearl, 1),
            ],
            8 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Necromancer, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Warlock, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Barbarian, 1),
            ],
            9 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.BlackPearl, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.DragonSword, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.DivineBreastplate, 1),
            ],
            10 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.TaxLien, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.HideousMask, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.FizbinMisfortune, 1),
            ],
            11 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Wizard, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Sorceress, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Knight, 1),
            ],
            _ => [],
        };
    }

    /// <summary>The rewards of Price of Loyalty, <c>getPriceOfLoyaltyCampaignBonusData()</c>.</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The rewards.</returns>
    internal static List<ScenarioBonusData> PriceOfLoyalty(int scenarioId)
    {
        return scenarioId switch
        {
            0 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MedalValor, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.StealthShield, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MinorScroll, 1),
            ],
            1 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.WhitePearl, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.Ballista, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
            ],
            2 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MageRing, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MajorScroll, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Logistics, (int)SkillLevel.Advanced),
            ],
            3 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MedalHonor, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.CasterBracelet, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.DefenderHelm, 1),
            ],
            4 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.PowerAxe, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillPrimary, (int)PrimarySkill.Defense, 2),
            ],
            5 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Troop, (int)MonsterType.Crusader, 3),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.TravelerBootsMobility, 1),
            ],
            6 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Sulfur, 10),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.PowerAxe, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.SpellScroll, 1, (int)SpellType.AnimateDead),
            ],
            7 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.ViewHeroes, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Troop, (int)MonsterType.Mage, 5),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Estates, (int)SkillLevel.Advanced),
            ],
            _ => [],
        };
    }

    /// <summary>The rewards of Wizard's Isle, <c>getWizardsIsleCampaignBonusData()</c>.</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The rewards.</returns>
    internal static List<ScenarioBonusData> WizardsIsle(int scenarioId)
    {
        return scenarioId switch
        {
            0 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.PowerAxe, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.StealthShield, 1),
            ],
            1 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MageRing, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.ForemostScroll, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.FireBlast, 1),
            ],
            2 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.MassHaste, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.SummonEElement, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.ChainLightning, 1),
            ],
            3 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Wood, 5),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Ore, 5),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 1000),
            ],
            _ => [],
        };
    }

    /// <summary>The rewards of Voyage Home, <c>getVoyageHomeCampaignBonusData()</c>.</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The rewards.</returns>
    internal static List<ScenarioBonusData> VoyageHome(int scenarioId)
    {
        return scenarioId switch
        {
            0 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.NomadBootsMobility, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 500),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Scouting, (int)SkillLevel.Basic),
            ],
            1 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Pathfinding, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Navigation, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Sulfur, 15),
            ],
            2 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Archery, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 3000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillPrimary, (int)PrimarySkill.Attack, 1),
            ],
            3 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Necromancy, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Logistics, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 2000),
            ],
            _ => [],
        };
    }

    /// <summary>The rewards of Descendants, <c>getDescendantsCampaignBonusData()</c>.</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The rewards.</returns>
    internal static List<ScenarioBonusData> Descendants(int scenarioId)
    {
        return scenarioId switch
        {
            0 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 1000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Wood, 50),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Crystal, 10),
            ],
            1 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillPrimary, (int)PrimarySkill.Attack, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillPrimary, (int)PrimarySkill.Defense, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 1000),
            ],
            2 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Logistics, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Spell, (int)SpellType.SteelSkin, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.MageRing, 1),
            ],
            3 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Logistics, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Scouting, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.PowerAxe, 1),
            ],
            4 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Luck, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Leadership, 1),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Artifact, (int)ArtifactType.SpikedShield, 1),
            ],
            5 =>
            [
                // 参考在这里明确注释过：这三笔就是负数（这一关的「奖励」是扣资源），不要改成正值。
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, -1000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Wood, -10),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Mercury, -2),
            ],
            6 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Gold, 1000),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Scouting, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, Resource.Wood, 20),
            ],
            7 =>
            [
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Leadership, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillSecondary, (int)SecondarySkill.Archery, (int)SkillLevel.Basic),
                new ScenarioBonusData(ScenarioBonusData.BonusType.SkillPrimary, (int)PrimarySkill.Power, 1),
            ],
            _ => [],
        };
    }
}
