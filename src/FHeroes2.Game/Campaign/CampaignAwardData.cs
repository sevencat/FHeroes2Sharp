using System.Diagnostics;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// A reward the campaign hands out for finishing a scenario, <c>struct CampaignAwardData</c>
/// (<c>src/fheroes2/campaign/campaign_data.h:75-116</c> and <c>campaign_data.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 战役的「奖励」一律是数据：一个类型、一个副类型（兵种编号 / 神器编号 / 法术编号 / 英雄编号 / 资源编号）、一个数量，
/// 再挂一个「从第几关开始生效」。存档里只存编号，读档时按编号回到对应的表里取完整奖励，所以编号是全战役唯一的——两条
/// 不同奖励不能撞号。
/// </para>
/// <para>
/// 名字与描述是算出来的，不是存下来的：名字形如「Dwarves bane」「Ultimate Crown」，描述是一句带占位符的句子。这样
/// 换语言时不用改表，只改文本。
/// </para>
/// </remarks>
public sealed class CampaignAwardData
{
    /// <summary>
    /// The kinds of reward, <c>CampaignAwardData::AwardType</c> (<c>campaign_data.h:78-88</c>).
    /// </summary>
    public static class AwardType
    {
        /// <summary>A monster type runs away from the player, <c>TYPE_CREATURE_CURSE</c>.</summary>
        public const int CreatureCurse = 0;

        /// <summary>A monster type joins the player, <c>TYPE_CREATURE_ALLIANCE</c>.</summary>
        public const int CreatureAlliance = 1;

        /// <summary>An artifact is carried over, <c>TYPE_GET_ARTIFACT</c>.</summary>
        public const int GetArtifact = 2;

        /// <summary>A spell is carried over, <c>TYPE_GET_SPELL</c>.</summary>
        public const int GetSpell = 3;

        /// <summary>The army is carried over, <c>TYPE_CARRY_OVER_FORCES</c>.</summary>
        public const int CarryOverForces = 4;

        /// <summary>A hero becomes hireable, <c>TYPE_HIREABLE_HERO</c>.</summary>
        public const int HireableHero = 5;

        /// <summary>An enemy hero stays dead in later scenarios, <c>TYPE_DEFEAT_ENEMY_HERO</c>.</summary>
        public const int DefeatEnemyHero = 6;

        /// <summary>The kingdom gets resources every day, <c>TYPE_RESOURCE_BONUS</c>.</summary>
        public const int ResourceBonus = 7;
    }

    /// <summary>
    /// Creates an award of one unit, the reference's three-argument constructor
    /// (<c>campaign_data.cpp:750-754</c>).
    /// </summary>
    /// <param name="id">The unique identifier, <c>_id</c>.</param>
    /// <param name="type">The kind of reward, <c>_type</c>.</param>
    /// <param name="subType">What the reward is about, <c>_subType</c>.</param>
    public CampaignAwardData(int id, int type, int subType)
        : this(id, type, subType, 1, 0, string.Empty)
    {
    }

    /// <summary>
    /// Creates an award with an amount, the reference's four-argument constructor
    /// (<c>campaign_data.cpp:756-760</c>).
    /// </summary>
    /// <param name="id">The unique identifier, <c>_id</c>.</param>
    /// <param name="type">The kind of reward, <c>_type</c>.</param>
    /// <param name="subType">What the reward is about, <c>_subType</c>.</param>
    /// <param name="amount">How many, <c>_amount</c>.</param>
    public CampaignAwardData(int id, int type, int subType, int amount)
        : this(id, type, subType, amount, 0, string.Empty)
    {
    }

    /// <summary>
    /// Creates an award with a name of its own, the reference's four-argument constructor taking a string
    /// (<c>campaign_data.cpp:762-766</c>).
    /// </summary>
    /// <param name="id">The unique identifier, <c>_id</c>.</param>
    /// <param name="type">The kind of reward, <c>_type</c>.</param>
    /// <param name="subType">What the reward is about, <c>_subType</c>.</param>
    /// <param name="customName">The name to show instead of the derived one, <c>_customName</c>.</param>
    public CampaignAwardData(int id, int type, int subType, string customName)
        : this(id, type, subType, 1, 0, customName)
    {
    }

    /// <summary>
    /// Creates a complete award, the reference's six-argument constructor
    /// (<c>campaign_data.cpp:768-778</c>).
    /// </summary>
    /// <param name="id">The unique identifier, <c>_id</c>.</param>
    /// <param name="type">The kind of reward, <c>_type</c>.</param>
    /// <param name="subType">What the reward is about, <c>_subType</c>.</param>
    /// <param name="amount">How many, <c>_amount</c>.</param>
    /// <param name="startScenarioId">The scenario it starts working at, <c>_startScenarioID</c>.</param>
    /// <param name="customName">The name to show instead of the derived one, <c>_customName</c>.</param>
    public CampaignAwardData(int id, int type, int subType, int amount, int startScenarioId, string customName = "")
    {
        Id = id;
        Type = type;
        SubType = subType;
        Amount = amount;
        StartScenarioId = startScenarioId;
        CustomName = customName;
    }

    /// <summary>The unique identifier, <c>_id</c>.</summary>
    public int Id { get; }

    /// <summary>The kind of reward, one of <see cref="AwardType"/>, <c>_type</c>.</summary>
    public int Type { get; }

    /// <summary>What the reward is about, <c>_subType</c>.</summary>
    public int SubType { get; }

    /// <summary>How many, <c>_amount</c>.</summary>
    public int Amount { get; }

    /// <summary>The scenario the reward starts working at, <c>_startScenarioID</c>.</summary>
    public int StartScenarioId { get; }

    /// <summary>The name to show instead of the derived one, <c>_customName</c>.</summary>
    public string CustomName { get; }

    /// <summary>
    /// The name of the reward, <c>CampaignAwardData::getName()</c> (<c>campaign_data.cpp:780-809</c>).
    /// </summary>
    /// <returns>The custom name when there is one, else the name derived from the kind and the subject.</returns>
    public string GetName() => Type switch
    {
        AwardType.CreatureCurse => MonsterNames.GetSingular((MonsterType)SubType) + " bane",
        AwardType.CreatureAlliance => MonsterNames.GetSingular((MonsterType)SubType) + " alliance",
        AwardType.GetArtifact => ArtifactTable.Get((ArtifactType)SubType).Name,
        AwardType.CarryOverForces => "Carry-over forces",
        AwardType.ResourceBonus => ResourceText.Of(SubType) + " bonus",
        AwardType.GetSpell => SpellText.GetName((SpellType)SubType),
        AwardType.HireableHero => HeroNames.Get(SubType),
        AwardType.DefeatEnemyHero => HeroNames.Get(SubType) + " defeated",
        _ => UnknownAward(),
    };

    /// <summary>
    /// The sentence shown under the name, <c>CampaignAwardData::getDescription()</c>
    /// (<c>campaign_data.cpp:811-878</c>).
    /// </summary>
    /// <returns>The description with its placeholders filled in.</returns>
    /// <remarks>
    /// 兵种系的描述会把「这个兵种连同它的升级形态」都列出来——联盟与克星都是对整条升级链生效的，所以描述要跟着走完整条链。
    /// </remarks>
    public string GetDescription()
    {
        switch (Type)
        {
            case AwardType.CreatureCurse:
                return MonsterChain((MonsterType)SubType) + " will always run away from your army.";
            case AwardType.CreatureAlliance:
                return MonsterChain((MonsterType)SubType) + " will be willing to join your army.";
            case AwardType.GetArtifact:
                return $"\"{ArtifactTable.Get((ArtifactType)SubType).Name}\" artifact will be carried over in the campaign.";
            case AwardType.CarryOverForces:
                return "The army will be carried over in the campaign.";
            case AwardType.ResourceBonus:
                return $"The kingdom will have +{Amount} {ResourceText.Of(SubType)} each day.";
            case AwardType.GetSpell:
                return $"The \"{SpellText.GetName((SpellType)SubType)}\" spell will be carried over in the campaign.";
            case AwardType.HireableHero:
                return $"{HeroNames.Get(SubType)} can be hired during scenarios.";
            case AwardType.DefeatEnemyHero:
                return $"{HeroNames.Get(SubType)} has been defeated and will not appear in subsequent scenarios.";
            default:
                return UnknownAward();
        }
    }

    /// <summary>
    /// The awards obtainable in one scenario, <c>CampaignAwardData::getCampaignAwardData()</c>
    /// (<c>campaign_data.cpp:880-905</c>).
    /// </summary>
    /// <param name="scenarioInfo">The scenario.</param>
    /// <returns>The awards of that scenario, empty for most of them.</returns>
    /// <remarks>Voyage Home 一关奖励都没有，参考在这里专门写了注释说明「不是漏了」。</remarks>
    public static List<CampaignAwardData> GetCampaignAwardData(ScenarioInfoId scenarioInfo)
    {
        return scenarioInfo.CampaignId switch
        {
            CampaignId.Roland => GetRolandAwards(scenarioInfo.ScenarioId),
            CampaignId.Archibald => GetArchibaldAwards(scenarioInfo.ScenarioId),
            CampaignId.PriceOfLoyalty => GetPriceOfLoyaltyAwards(scenarioInfo.ScenarioId),
            CampaignId.Descendants => GetDescendantsAwards(scenarioInfo.ScenarioId),
            CampaignId.WizardsIsle => GetWizardsIsleAwards(scenarioInfo.ScenarioId),
            CampaignId.VoyageHome => [],
            _ => UnknownCampaign(),
        };
    }

    /// <summary>
    /// The awards of the whole campaign rather than of one scenario,
    /// <c>CampaignAwardData::getExtraCampaignAwardData()</c> (<c>campaign_data.cpp:574-597</c>).
    /// </summary>
    /// <param name="campaignId">The campaign.</param>
    /// <returns>The extra awards; only Price of Loyalty has one.</returns>
    /// <remarks>
    /// Price of Loyalty 的三件安德兰套装凑齐后合成「战斗装束」，那是跨三关的条件，只能挂在战役级别上。
    /// </remarks>
    public static List<CampaignAwardData> GetExtraCampaignAwardData(int campaignId)
    {
        switch (campaignId)
        {
            case CampaignId.PriceOfLoyalty:
                return [new CampaignAwardData(10, AwardType.GetArtifact, (int)ArtifactType.BattleGarb)];
            case CampaignId.Roland:
            case CampaignId.Archibald:
            case CampaignId.Descendants:
            case CampaignId.WizardsIsle:
            case CampaignId.VoyageHome:
                return [];
            default:
                return UnknownCampaign();
        }
    }

    /// <summary>
    /// What the monsters of an alliance say when they join,
    /// <c>CampaignAwardData::getAllianceJoiningMessage()</c> (<c>campaign_data.cpp:907-930</c>).
    /// </summary>
    /// <param name="monsterId">The monster type the player met.</param>
    /// <returns>The message to show.</returns>
    public static string GetAllianceJoiningMessage(int monsterId)
    {
        switch ((MonsterType)monsterId)
        {
            case MonsterType.Dwarf:
            case MonsterType.BattleDwarf:
                return "The dwarves recognize their allies and gladly join your forces.";
            case MonsterType.Ogre:
            case MonsterType.OgreLord:
                return "The ogres recognize you as the Dwarfbane and lumber over to join you.";
            case MonsterType.GreenDragon:
            case MonsterType.RedDragon:
            case MonsterType.BlackDragon:
                return "The dragons, snarling and growling, agree to join forces with you, their 'Ally'.";
            case MonsterType.Elf:
            case MonsterType.GrandElf:
                return "As you approach the group of elves, their leader calls them all to attention. "
                    + "He shouts to them, \"Who of you is brave enough to join this fearless ally of ours?\" "
                    + "The group explodes with cheers as they run to join your ranks.";
            default:
                return UnknownMessage();
        }
    }

    /// <summary>
    /// What the monsters of an alliance say when the army is full,
    /// <c>CampaignAwardData::getAllianceFleeingMessage()</c> (<c>campaign_data.cpp:932-956</c>).
    /// </summary>
    /// <param name="monsterId">The monster type the player met.</param>
    /// <returns>The message to show.</returns>
    public static string GetAllianceFleeingMessage(int monsterId)
    {
        switch ((MonsterType)monsterId)
        {
            case MonsterType.Dwarf:
            case MonsterType.BattleDwarf:
                return "The dwarves hail you, \"Any friend of Roland is a friend of ours. You may pass.\"";
            case MonsterType.Ogre:
            case MonsterType.OgreLord:
                return "The ogres give you a grunt of recognition, \"Archibald's allies may pass.\"";
            case MonsterType.GreenDragon:
            case MonsterType.RedDragon:
            case MonsterType.BlackDragon:
                return "The dragons see you and call out. \"Our alliance with Archibald compels us to join you. "
                    + "Unfortunately you have no room. A pity!\" They quickly scatter.";
            case MonsterType.Elf:
            case MonsterType.GrandElf:
                return "The elves stand at attention as you approach. Their leader calls to you and says, "
                    + "\"Let us not impede your progress, ally! Move on, and may victory be yours.\"";
            default:
                return UnknownMessage();
        }
    }

    /// <summary>
    /// What the dwarves say when they run from the Dwarfbane,
    /// <c>CampaignAwardData::getBaneFleeingMessage()</c> (<c>campaign_data.cpp:958-970</c>).
    /// </summary>
    /// <param name="monsterId">The monster type the player met.</param>
    /// <returns>The message to show.</returns>
    public static string GetBaneFleeingMessage(int monsterId)
    {
        return (MonsterType)monsterId switch
        {
            MonsterType.Dwarf or MonsterType.BattleDwarf => "\"The Dwarfbane!!!!, run for your lives.\"",
            _ => UnknownMessage(),
        };
    }

    /// <summary>
    /// The name of a campaign, <c>Campaign::getCampaignName()</c> (<c>campaign_scenariodata.cpp:808-830</c>).
    /// </summary>
    /// <param name="campaignId">The campaign.</param>
    /// <returns>The name, or an empty string after an assertion for an unknown campaign.</returns>
    public static string GetCampaignName(int campaignId)
    {
        return campaignId switch
        {
            CampaignId.Roland => "Roland",
            CampaignId.Archibald => "Archibald",
            CampaignId.PriceOfLoyalty => "Price of Loyalty",
            CampaignId.VoyageHome => "Voyage Home",
            CampaignId.WizardsIsle => "Wizard's Isle",
            CampaignId.Descendants => "Descendants",
            _ => UnknownCampaignName(),
        };
    }

    /// <summary>The awards of Roland's campaign, <c>getRolandCampaignAwardData()</c> (<c>campaign_data.cpp:44-69</c>).</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The awards.</returns>
    private static List<CampaignAwardData> GetRolandAwards(int scenarioId)
    {
        return scenarioId switch
        {
            2 => [new CampaignAwardData(0, AwardType.CreatureAlliance, (int)MonsterType.Dwarf, "Dwarven Alliance")],
            5 => [new CampaignAwardData(1, AwardType.HireableHero, Hero.Eliza, 0, 0, "Sorceress Guild")],
            6 => [new CampaignAwardData(2, AwardType.CarryOverForces, 0, 0, 9)],
            7 => [new CampaignAwardData(3, AwardType.GetArtifact, (int)ArtifactType.UltimateCrown, 1, 9)],
            8 => [new CampaignAwardData(4, AwardType.DefeatEnemyHero, Hero.Corlagon, 0, 9)],
            _ => [],
        };
    }

    /// <summary>The awards of Archibald's campaign, <c>getArchibaldCampaignAwardData()</c> (<c>campaign_data.cpp:71-97</c>).</summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The awards.</returns>
    private static List<CampaignAwardData> GetArchibaldAwards(int scenarioId)
    {
        return scenarioId switch
        {
            2 => [new CampaignAwardData(1, AwardType.HireableHero, Hero.Brax, 0, 0, "Necromancer Guild")],
            3 =>
            [
                new CampaignAwardData(2, AwardType.CreatureAlliance, (int)MonsterType.Ogre, "Ogre Alliance"),
                new CampaignAwardData(3, AwardType.CreatureCurse, (int)MonsterType.Dwarf, "Dwarfbane"),
            ],
            6 => [new CampaignAwardData(4, AwardType.CreatureAlliance, (int)MonsterType.GreenDragon, "Dragon Alliance")],
            8 => [new CampaignAwardData(5, AwardType.GetArtifact, (int)ArtifactType.UltimateCrown)],
            9 => [new CampaignAwardData(6, AwardType.CarryOverForces, 0)],
            _ => [],
        };
    }

    /// <summary>
    /// The awards of Price of Loyalty, <c>getPriceOfLoyaltyCampaignAwardData()</c> (<c>campaign_data.cpp:99-127</c>).
    /// </summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The awards.</returns>
    /// <remarks>第六关的敌人英雄在文件里叫 Dainwin，剧情里叫 Kraeger，所以这条奖励给了自定义名字。</remarks>
    private static List<CampaignAwardData> GetPriceOfLoyaltyAwards(int scenarioId)
    {
        return scenarioId switch
        {
            1 => [new CampaignAwardData(0, AwardType.GetArtifact, (int)ArtifactType.BreastplateAnduran)],
            2 =>
            [
                new CampaignAwardData(1, AwardType.CreatureAlliance, (int)MonsterType.Elf, "Elven Alliance"),
                new CampaignAwardData(2, AwardType.ResourceBonus, Resource.Wood, 2),
            ],
            5 => [new CampaignAwardData(3, AwardType.GetArtifact, (int)ArtifactType.HelmetAnduran)],
            6 =>
            [
                new CampaignAwardData(4, AwardType.GetArtifact, (int)ArtifactType.SwordAnduran),
                new CampaignAwardData(5, AwardType.DefeatEnemyHero, Hero.Dainwin, "Kraeger defeated"),
            ],
            _ => [],
        };
    }

    /// <summary>
    /// The awards of Wizard's Isle, <c>getWizardsIsleCampaignAwardData()</c> (<c>campaign_data.cpp:129-145</c>).
    /// </summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The awards.</returns>
    private static List<CampaignAwardData> GetWizardsIsleAwards(int scenarioId)
    {
        return scenarioId switch
        {
            1 => [new CampaignAwardData(0, AwardType.GetSpell, (int)SpellType.SetEGuardian)],
            2 => [new CampaignAwardData(1, AwardType.GetArtifact, (int)ArtifactType.SphereNegation)],
            _ => [],
        };
    }

    /// <summary>
    /// The awards of Descendants, <c>getDescendantsCampaignAwardData()</c> (<c>campaign_data.cpp:147-169</c>).
    /// </summary>
    /// <param name="scenarioId">The scenario.</param>
    /// <returns>The awards.</returns>
    private static List<CampaignAwardData> GetDescendantsAwards(int scenarioId)
    {
        return scenarioId switch
        {
            2 => [new CampaignAwardData(0, AwardType.HireableHero, Hero.Joseph, 0, 0, "Wayward Son")],
            3 => [new CampaignAwardData(1, AwardType.HireableHero, Hero.UncleIvan, 0, 0, "Uncle Ivan")],
            5 => [new CampaignAwardData(2, AwardType.GetArtifact, (int)ArtifactType.LegendaryScepter)],
            6 => [new CampaignAwardData(3, AwardType.CreatureAlliance, (int)MonsterType.Elf, "Elven Alliance")],
            _ => [],
        };
    }

    /// <summary>
    /// The whole upgrade chain of a monster, comma separated, as the descriptions of the two monster awards print it.
    /// </summary>
    /// <param name="monster">The base monster.</param>
    /// <returns>The names of the chain, from the base to the top upgrade.</returns>
    private static string MonsterChain(MonsterType monster)
    {
        string description = MonsterNames.GetMulti(monster);
        MonsterType current = monster;

        while (MonsterUpgrades.GetUpgrade(current) != current)
        {
            current = MonsterUpgrades.GetUpgrade(current);
            description += ", " + MonsterNames.GetMulti(current);
        }

        return description;
    }

    /// <summary>The fallback of <see cref="GetName"/> and <see cref="GetDescription"/> for an unknown kind.</summary>
    /// <returns>An empty name after an assertion.</returns>
    private static string UnknownAward()
    {
        Debug.Assert(false, "A campaign award kind was added without a name or a description.");

        return string.Empty;
    }

    /// <summary>The fallback of <see cref="GetCampaignAwardData"/> for an unknown campaign.</summary>
    /// <returns>An empty list after an assertion.</returns>
    private static List<CampaignAwardData> UnknownCampaign()
    {
        Debug.Assert(false, "A campaign was added without its award table.");

        return [];
    }

    /// <summary>The fallback of <see cref="GetCampaignName"/> for an unknown campaign.</summary>
    /// <returns>An empty name after an assertion.</returns>
    private static string UnknownCampaignName()
    {
        Debug.Assert(false, "A campaign was added without a name.");

        return string.Empty;
    }

    /// <summary>The fallback of the three messages for an unknown monster.</summary>
    /// <returns>An empty message after an assertion.</returns>
    private static string UnknownMessage()
    {
        Debug.Assert(false, "A monster was given an alliance or a bane without a message.");

        return string.Empty;
    }
}
