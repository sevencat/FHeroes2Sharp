using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// One of the two or three rewards offered when a campaign scenario starts,
/// <c>struct ScenarioBonusData</c> (<c>src/fheroes2/campaign/campaign_scenariodata.h:88-117</c>).
/// </summary>
/// <remarks>
/// <para>
/// 每关开场给玩家二选一或三选一：一件神器、一笔资源、一队兵、一个法术、换个起始种族、加一点主技能、给一个副技能。
/// 表按「战役 + 关号」写死在 <c>campaign_scenariodata.cpp</c> 里，没有随机成分。
/// </para>
/// <para>
/// 神器奖励有一条例外：卷轴神器要带法术编号（<see cref="ArtifactSpellId"/>），名字与描述都显示那个法术而不是「卷轴」。
/// 所以两条构造函数一个把法术编号留成 <see cref="SpellType.None"/>，另一个显式接。
/// </para>
/// </remarks>
public sealed class ScenarioBonusData
{
    /// <summary>
    /// The kinds of reward, <c>ScenarioBonusData::BonusType</c> (<c>campaign_scenariodata.h:91-101</c>).
    /// </summary>
    public static class BonusType
    {
        /// <summary>Resources at the start, <c>RESOURCES</c>.</summary>
        public const int Resources = 0;

        /// <summary>An artifact at the start, <c>ARTIFACT</c>.</summary>
        public const int Artifact = 1;

        /// <summary>A stack of monsters at the start, <c>TROOP</c>.</summary>
        public const int Troop = 2;

        /// <summary>A spell in the spell book, <c>SPELL</c>.</summary>
        public const int Spell = 3;

        /// <summary>A different starting race, <c>STARTING_RACE</c>.</summary>
        public const int StartingRace = 4;

        /// <summary>A point of a primary skill, <c>SKILL_PRIMARY</c>.</summary>
        public const int SkillPrimary = 5;

        /// <summary>A secondary skill, <c>SKILL_SECONDARY</c>.</summary>
        public const int SkillSecondary = 6;

        /// <summary>A different starting race together with its army, <c>STARTING_RACE_AND_ARMY</c>.</summary>
        public const int StartingRaceAndArmy = 7;
    }

    /// <summary>Creates an empty reward, the reference's default constructor (<c>campaign_scenariodata.cpp:625-630</c>).</summary>
    public ScenarioBonusData()
    {
        ArtifactSpellId = (int)SpellType.None;
    }

    /// <summary>
    /// Creates a reward without a spell, the reference's three-argument constructor
    /// (<c>campaign_scenariodata.cpp:632-637</c>).
    /// </summary>
    /// <param name="type">One of <see cref="BonusType"/>, <c>_type</c>.</param>
    /// <param name="subType">What the reward is about, <c>_subType</c>.</param>
    /// <param name="amount">How many, <c>_amount</c>.</param>
    public ScenarioBonusData(int type, int subType, int amount)
        : this(type, subType, amount, (int)SpellType.None)
    {
    }

    /// <summary>
    /// Creates a complete reward, the reference's four-argument constructor
    /// (<c>campaign_scenariodata.cpp:639-644</c>).
    /// </summary>
    /// <param name="type">One of <see cref="BonusType"/>, <c>_type</c>.</param>
    /// <param name="subType">What the reward is about, <c>_subType</c>.</param>
    /// <param name="amount">How many, <c>_amount</c>.</param>
    /// <param name="spellId">The spell of a spell scroll, <c>_artifactSpellId</c>.</param>
    public ScenarioBonusData(int type, int subType, int amount, int spellId)
    {
        Type = type;
        SubType = subType;
        Amount = amount;
        ArtifactSpellId = spellId;
    }

    /// <summary>One of <see cref="BonusType"/>, <c>_type</c>.</summary>
    public int Type { get; }

    /// <summary>What the reward is about, <c>_subType</c>.</summary>
    public int SubType { get; }

    /// <summary>How many, <c>_amount</c>.</summary>
    public int Amount { get; }

    /// <summary>The spell of a spell scroll, <c>_artifactSpellId</c>.</summary>
    public int ArtifactSpellId { get; }

    /// <summary>
    /// The short name shown on the button, <c>ScenarioBonusData::getName()</c>
    /// (<c>campaign_scenariodata.cpp:646-682</c>).
    /// </summary>
    /// <returns>The name, with the amount in front when the kind of reward shows one.</returns>
    /// <remarks>
    /// 神器、部队、主技能三类的名字前面带数量（「2 Thunder Mace」），但只有数量大于 1 才带——1 写成「1 雷锤」很蠢，
    /// 而资源与副技能自己就把数量算进名字里了，所以不在这个名单上。
    /// </remarks>
    public string GetName()
    {
        string objectName;

        switch (Type)
        {
            case BonusType.Artifact:
                objectName = GetArtifactCampaignName(SubType, ArtifactSpellId);
                break;
            case BonusType.Resources:
                objectName = Amount + " " + ResourceText.Of(SubType);
                break;
            case BonusType.Troop:
                objectName = MonsterNames.GetMulti((MonsterType)SubType);
                break;
            case BonusType.Spell:
                objectName = GetSpellCampaignName(SubType);
                break;
            case BonusType.StartingRace:
            case BonusType.StartingRaceAndArmy:
                objectName = MapRaces.Name((MapRace)SubType);
                break;
            case BonusType.SkillPrimary:
                objectName = PrimarySkillNames.Get((PrimarySkill)SubType);
                break;
            case BonusType.SkillSecondary:
                objectName = GetSecondarySkillCampaignName(SubType, Amount);
                break;
            default:
                return UnknownBonus();
        }

        bool useAmount = Type is BonusType.Artifact or BonusType.Troop or BonusType.SkillPrimary && Amount > 1;

        return useAmount ? Amount + " " + objectName : objectName;
    }

    /// <summary>
    /// The sentence under the name, <c>ScenarioBonusData::getDescription()</c>
    /// (<c>campaign_scenariodata.cpp:684-733</c>).
    /// </summary>
    /// <returns>The description with its placeholders filled in.</returns>
    /// <remarks>资源可以是负数（那关的「奖励」是减资源），描述因此有两条：加与减。</remarks>
    public string GetDescription() => Type switch
    {
        BonusType.Artifact =>
            $"The main hero will have the \"{ArtifactTable.Get((ArtifactType)SubType).Name}\" artifact at the start of the scenario.",
        BonusType.Resources => Amount > 0
            ? $"The kingdom will receive {Amount} additional {ResourceText.Of(SubType)} at the start of the scenario."
            : $"The kingdom will have {Math.Abs(Amount)} less {ResourceText.Of(SubType)} at the start of the scenario.",
        BonusType.Troop =>
            $"The main hero will have {MonsterNames.GetPluralName((MonsterType)SubType, (uint)Amount)} at the start of the scenario.",
        BonusType.Spell =>
            $"The main hero will have the \"{SpellText.GetName((SpellType)SubType)}\" spell at the start of the scenario.",
        BonusType.StartingRace or BonusType.StartingRaceAndArmy =>
            $"The starting alignment of the scenario will be {MapRaces.Name((MapRace)SubType)}.",
        BonusType.SkillPrimary =>
            $"The main hero will receive a +{Amount} to their {PrimarySkillNames.Get((PrimarySkill)SubType)} at the start of the scenario.",
        BonusType.SkillSecondary =>
            $"The main hero will have {SecondarySkills.GetFullName((SecondarySkill)SubType, (SkillLevel)Amount)} at the start of the scenario.",
        _ => UnknownBonus(),
    };

    /// <summary>
    /// The rewards offered in one scenario, <c>ScenarioBonusData::getCampaignBonusData()</c>
    /// (<c>campaign_scenariodata.cpp:735-759</c>).
    /// </summary>
    /// <param name="scenarioInfo">The scenario.</param>
    /// <returns>The rewards, empty for scenarios without any.</returns>
    public static List<ScenarioBonusData> GetCampaignBonusData(ScenarioInfoId scenarioInfo)
    {
        Debug.Assert(scenarioInfo.ScenarioId >= 0);

        return scenarioInfo.CampaignId switch
        {
            CampaignId.Roland => CampaignScenarioBonusTables.Roland(scenarioInfo.ScenarioId),
            CampaignId.Archibald => CampaignScenarioBonusTables.Archibald(scenarioInfo.ScenarioId),
            CampaignId.PriceOfLoyalty => CampaignScenarioBonusTables.PriceOfLoyalty(scenarioInfo.ScenarioId),
            CampaignId.VoyageHome => CampaignScenarioBonusTables.VoyageHome(scenarioInfo.ScenarioId),
            CampaignId.WizardsIsle => CampaignScenarioBonusTables.WizardsIsle(scenarioInfo.ScenarioId),
            CampaignId.Descendants => CampaignScenarioBonusTables.Descendants(scenarioInfo.ScenarioId),
            _ => UnknownCampaign(),
        };
    }

    /// <summary>
    /// The campaign name of a spell, <c>getSpellCampaignName()</c> (<c>campaign_scenariodata.cpp:336-362</c>).
    /// </summary>
    /// <param name="spellId">The spell.</param>
    /// <returns>The name used on the bonus buttons.</returns>
    /// <remarks>
    /// 十一个法术在战役里换了短名字：「Animate Dead」变「Animate Dead」但去掉了说明里的量词，土元素召唤直接叫「Summon Earth」。
    /// 其余法术沿用常规名字。
    /// </remarks>
    private static string GetSpellCampaignName(int spellId) => (SpellType)spellId switch
    {
        SpellType.AnimateDead => "Animate Dead",
        SpellType.ChainLightning => "Chain Lightning",
        SpellType.FireBlast => "Fireblast",
        SpellType.MassCurse => "Mass Curse",
        SpellType.MassHaste => "Mass Haste",
        SpellType.MirrorImage => "Mirror Image",
        SpellType.Resurrect => "Resurrect",
        SpellType.SteelSkin => "Steelskin",
        SpellType.SummonEElement => "Summon Earth",
        SpellType.ViewHeroes => "View Heroes",
        _ => SpellText.GetName((SpellType)spellId),
    };

    /// <summary>
    /// The campaign name of an artifact, <c>getArtifactCampaignName()</c> (<c>campaign_scenariodata.cpp:364-418</c>).
    /// </summary>
    /// <param name="artifactId">The artifact.</param>
    /// <param name="spellId">The spell of a spell scroll.</param>
    /// <returns>The name used on the bonus buttons.</returns>
    /// <remarks>
    /// 二十三个神器用了去掉前缀的短名（「Thunder Mace of Dominion」→「Thunder Mace」），按钮上放不下全名；卷轴神器
    /// 直接显示里面的法术。
    /// </remarks>
    private static string GetArtifactCampaignName(int artifactId, int spellId) => (ArtifactType)artifactId switch
    {
        ArtifactType.Ballista => "Ballista",
        ArtifactType.BlackPearl => "Black Pearl",
        ArtifactType.CasterBracelet => "Caster's Bracelet",
        ArtifactType.DefenderHelm => "Defender Helm",
        ArtifactType.DivineBreastplate => "Breastplate",
        ArtifactType.DragonSword => "Dragon Sword",
        ArtifactType.FizbinMisfortune => "Fizbin Medal",
        ArtifactType.ForemostScroll => "Foremost Scroll",
        ArtifactType.ArmoredGauntlets => "Gauntlets",
        ArtifactType.HideousMask => "Hideous Mask",
        ArtifactType.MageRing => "Mage's Ring",
        ArtifactType.MajorScroll => "Major Scroll",
        ArtifactType.MedalHonor => "Medal of Honor",
        ArtifactType.MedalValor => "Medal of Valor",
        ArtifactType.MinorScroll => "Minor Scroll",
        ArtifactType.NomadBootsMobility => "Nomad Boots",
        ArtifactType.PowerAxe => "Power Axe",
        ArtifactType.SpikedShield => "Spiked Shield",
        ArtifactType.StealthShield => "Stealth Shield",
        ArtifactType.TaxLien => "Tax Lien",
        ArtifactType.ThunderMace => "Thunder Mace",
        ArtifactType.TravelerBootsMobility => "Traveler's Boots",
        ArtifactType.WhitePearl => "White Pearl",
        ArtifactType.SpellScroll => GetSpellCampaignName(spellId),
        _ => ArtifactTable.Get((ArtifactType)artifactId).Name,
    };

    /// <summary>
    /// The campaign name of a secondary skill, <c>getSecondarySkillCampaignName()</c>
    /// (<c>campaign_scenariodata.cpp:420-610</c>).
    /// </summary>
    /// <param name="secondarySkillId">The skill.</param>
    /// <param name="secondarySkillLevel">The level of the skill.</param>
    /// <returns>Something like <c>"Basic Logistics"</c>.</returns>
    /// <remarks>
    /// 参考把十四个技能乘三个等级的四十二种写法全写成字面量；端口已有 <see cref="SecondarySkills.GetFullName"/>，
    /// 拼出来的正是「等级 + 技能名」，所以不再抄那张表——文案与参考一致。
    /// </remarks>
    private static string GetSecondarySkillCampaignName(int secondarySkillId, int secondarySkillLevel)
    {
        if (!Enum.IsDefined(typeof(SecondarySkill), secondarySkillId) || !Enum.IsDefined(typeof(SkillLevel), secondarySkillLevel))
        {
            return UnknownBonus();
        }

        return SecondarySkills.GetFullName((SecondarySkill)secondarySkillId, (SkillLevel)secondarySkillLevel);
    }

    /// <summary>The fallback of the name and the description for an unknown kind.</summary>
    /// <returns>An empty string after an assertion.</returns>
    private static string UnknownBonus()
    {
        Debug.Assert(false, "A campaign bonus kind was added without a name or a description.");

        return string.Empty;
    }

    /// <summary>The fallback of <see cref="GetCampaignBonusData"/> for an unknown campaign.</summary>
    /// <returns>An empty list after an assertion.</returns>
    private static List<ScenarioBonusData> UnknownCampaign()
    {
        Debug.Assert(false, "A campaign was added without its bonus table.");

        return [];
    }
}
