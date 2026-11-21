using System.Diagnostics;
using System.Globalization;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Artifacts;

/// <summary>A single bonus of an artifact, <c>fheroes2::ArtifactBonus</c> (<c>artifact_info.h:116-127</c>).</summary>
/// <param name="Type">What the artifact grants.</param>
/// <param name="Value">How much of it; the unit depends on the type.</param>
public readonly record struct ArtifactBonus(ArtifactBonusType Type, int Value)
{
    /// <summary>
    /// The value read as a spell id, for <see cref="ArtifactBonusType.AddSpell"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ArtifactType.SpellScroll"/> carries <c>SpellType.None</c> here: the reference keeps the actual
    /// spell of a scroll in the artifact's <c>ext</c> field, which the port has not needed yet, so a blank
    /// scroll stays blank.
    /// </remarks>
    public SpellType Spell => (SpellType)Value;
}

/// <summary>A single curse of an artifact, <c>fheroes2::ArtifactCurse</c> (<c>artifact_info.h:129-140</c>).</summary>
/// <param name="Type">What the artifact takes away.</param>
/// <param name="Value">How much of it; the unit depends on the type.</param>
public readonly record struct ArtifactCurse(ArtifactCurseType Type, int Value);

/// <summary>
/// One artifact: its three text strings plus its bonuses and curses, <c>fheroes2::ArtifactData</c>
/// (<c>artifact_info.h:182-195</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的一张表两遍填：<c>artifact_info.cpp:43-582</c> 是名字与两段描述，<c>586-982</c> 是加成与诅咒。
/// 端口把第二遍按 <see cref="ArtifactType"/> 可读地写在 <see cref="ArtifactTable"/> 里，第一遍整段搬到
/// <see cref="ArtifactTexts"/>（一百多行长句子只有原样搬才不会抄错），构造时两遍按编号拼在一起。
/// </para>
/// <para>
/// 数字为空但确实存在的编号（魔法书、编辑器专用项）文案照有；表外的编号共用 <see cref="Empty"/>。
/// </para>
/// </remarks>
public sealed class ArtifactData
{
    private ArtifactData(ArtifactBonus[] bonuses, ArtifactCurse[] curses, string name, string description,
        string? discoveryEventDescription)
    {
        Bonuses = bonuses;
        Curses = curses;
        Name = name;
        BaseDescription = description;
        DiscoveryEventDescription = discoveryEventDescription;
    }

    /// <summary>
    /// Creates the data of one artifact with its numbers only, <c>ArtifactData{ "", "", nullptr, bonuses, curses }</c>.
    /// </summary>
    /// <param name="bonuses">What it grants; empty is allowed.</param>
    /// <param name="curses">What it takes away; empty is allowed.</param>
    public ArtifactData(ArtifactBonus[] bonuses, ArtifactCurse[] curses)
        : this(bonuses, curses, string.Empty, string.Empty, null)
    {
    }

    /// <summary>An artifact that does nothing and says nothing, used for the ids outside the table.</summary>
    public static ArtifactData Empty { get; } = new([], []);

    /// <summary>What the artifact grants, <c>ArtifactData::bonuses</c>.</summary>
    public IReadOnlyList<ArtifactBonus> Bonuses { get; }

    /// <summary>What the artifact takes away, <c>ArtifactData::curses</c>.</summary>
    public IReadOnlyList<ArtifactCurse> Curses { get; }

    /// <summary>The name, <c>ArtifactData::untranslatedName</c>.</summary>
    public string Name { get; }

    /// <summary>
    /// The description template, <c>ArtifactData::untranslatedBaseDescription</c>; read it through
    /// <see cref="GetDescription"/> rather than directly.
    /// </summary>
    public string BaseDescription { get; }

    /// <summary>
    /// The paragraph shown when a hero picks the artifact up, <c>ArtifactData::untranslatedDiscoveryEventDescription</c>;
    /// <see langword="null"/> for artifacts that never lie on a map.
    /// </summary>
    public string? DiscoveryEventDescription { get; }

    /// <summary>
    /// The bonus of the type, <c>std::find( bonuses.begin(), bonuses.end(), ArtifactBonus( type ) )</c>
    /// (<c>artifact.cpp:563-597</c>).
    /// </summary>
    /// <remarks>
    /// The reference's <c>operator==</c> compares only the type, so the first row of a type wins; no artifact in
    /// the table has the same type twice, but the port keeps the "first wins" rule instead of asserting.
    /// </remarks>
    /// <param name="type">What to look for.</param>
    /// <param name="bonus">The row, when there is one.</param>
    /// <returns>Whether the artifact grants it.</returns>
    public bool TryGetBonus(ArtifactBonusType type, out ArtifactBonus bonus)
    {
        foreach (ArtifactBonus row in Bonuses)
        {
            if (row.Type == type)
            {
                bonus = row;
                return true;
            }
        }

        bonus = default;
        return false;
    }

    /// <summary>The curse of the type, the curse twin of <see cref="TryGetBonus"/>.</summary>
    /// <param name="type">What to look for.</param>
    /// <param name="curse">The row, when there is one.</param>
    /// <returns>Whether the artifact has it.</returns>
    public bool TryGetCurse(ArtifactCurseType type, out ArtifactCurse curse)
    {
        foreach (ArtifactCurse row in Curses)
        {
            if (row.Type == type)
            {
                curse = row;
                return true;
            }
        }

        curse = default;
        return false;
    }

    /// <summary>
    /// The description a tooltip shows, <c>ArtifactData::getDescription()</c> (<c>artifact_info.cpp:949-982</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 先把 <c>%{name}</c> 换成名字，再按加成决定剩下一个占位符：法术卷轴的 <c>ADD_SPELL</c> 值为 <c>NONE</c> 时
    /// 由 <paramref name="extraParameter"/> 指定法术（为 0 时说明调用方在看列表，写「由玩家选择」）；
    /// 其余神器填第一条加成的数值，没有加成则填第一条诅咒的数值。
    /// </para>
    /// <para>
    /// 参考在「既没有加成也没有诅咒」时 <c>assert( 0 )</c>：那种神器是不存在的。端口用
    /// <see cref="Debug.Assert(bool)"/> 记一笔，然后原样返回只换过名字的模板——调试版会炸，发行版不会把
    /// 界面卡死。
    /// </para>
    /// </remarks>
    /// <param name="extraParameter">For a blank spell scroll, the spell it actually carries; 0 for a list view.</param>
    /// <returns>The filled-in description.</returns>
    public string GetDescription(int extraParameter = (int)SpellType.None)
    {
        string description = BaseDescription.Replace("%{name}", Name, StringComparison.Ordinal);

        if (TryGetBonus(ArtifactBonusType.AddSpell, out ArtifactBonus addSpell))
        {
            if (addSpell.Value == (int)SpellType.None)
            {
                description = description.Replace("%{spell}",
                    extraParameter == (int)SpellType.None
                        ? "spellBonus|selected by user"
                        : new Spell((SpellType)extraParameter).GetName(),
                    StringComparison.Ordinal);
            }
            else
            {
                description = description.Replace("%{spell}", new Spell(addSpell.Spell).GetName(), StringComparison.Ordinal);
            }
        }
        else if (Bonuses.Count > 0)
        {
            description = description.Replace("%{count}", Bonuses[0].Value.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }
        else if (Curses.Count > 0)
        {
            description = description.Replace("%{count}", Curses[0].Value.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }
        else
        {
            Debug.Assert(false, "An artifact with neither a bonus nor a curse has no description.");
        }

        return description;
    }

    /// <summary>
    /// The paragraph shown on pickup, <c>Artifact::getDiscoveryDescription()</c> (<c>artifact.cpp:502-505</c>).
    /// </summary>
    /// <remarks>
    /// 参考直接返回那个指针，调用方自己判空；端口把空翻成空串，调用方只需判 <see cref="string.IsNullOrEmpty"/>。
    /// </remarks>
    /// <returns>The paragraph, or an empty string when the artifact never lies on a map.</returns>
    public string DiscoveryDescription => DiscoveryEventDescription ?? string.Empty;

    /// <summary>Copies the numbers and attaches the texts of one row of <see cref="ArtifactTexts"/>.</summary>
    /// <param name="row">The row of the same id.</param>
    /// <returns>A new instance; <see cref="Empty"/>'s numbers are not touched.</returns>
    internal ArtifactData WithText(in ArtifactTexts.Row row) =>
        new([.. Bonuses], [.. Curses], row.Name, row.Description, row.Discovery);
}

/// <summary>
/// The numeric half of <c>fheroes2::artifactData</c> (<c>artifact_info.cpp:586-982</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference fills a 104-entry vector: first the names and the description templates, then one
/// <c>emplace_back</c> per number, grouped by artifact. This table keeps only the second pass; the grouping is
/// the same, which is why the artifact ids are readable in the file.
/// </para>
/// <para>
/// Generated from the reference by a script, like the monster tables: the numbers of about two hundred
/// statements are not something to retype by hand.
/// </para>
/// </remarks>
public static class ArtifactTable
{
    private static readonly ArtifactData[] Table = BuildTable();

    /// <summary>
    /// The data of an artifact, <c>getArtifactData()</c> (<c>artifact_info.cpp:984</c>).
    /// </summary>
    /// <remarks>
    /// The reference returns the first row for an out-of-range id, because it indexes a vector after a bounds
    /// check that it turns into a warning. The port returns <see cref="ArtifactData.Empty"/> instead: an
    /// artifact id outside the table grants nothing, which is the value a player sees.
    /// </remarks>
    /// <param name="artifact">The artifact.</param>
    /// <returns>Its numbers; never <see langword="null"/>.</returns>
    public static ArtifactData Get(ArtifactType artifact)
    {
        int id = (int)artifact;

        return id >= 0 && id < Table.Length ? Table[id] : ArtifactData.Empty;
    }

    private static ArtifactData[] BuildTable()
    {
        var table = new ArtifactData[(int)ArtifactType.ArtifactCount];
        Array.Fill(table, ArtifactData.Empty);

        table[(int)ArtifactType.UltimateBook] = new ArtifactData([new(ArtifactBonusType.KnowledgeSkill, 12)], []);
        table[(int)ArtifactType.UltimateSword] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 12)], []);
        table[(int)ArtifactType.UltimateCloak] = new ArtifactData([new(ArtifactBonusType.DefenceSkill, 12)], []);
        table[(int)ArtifactType.UltimateWand] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 12)], []);
        table[(int)ArtifactType.UltimateShield] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 6), new(ArtifactBonusType.DefenceSkill, 6)], []);
        table[(int)ArtifactType.UltimateStaff] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 6), new(ArtifactBonusType.KnowledgeSkill, 6)], []);
        table[(int)ArtifactType.UltimateCrown] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 4), new(ArtifactBonusType.DefenceSkill, 4), new(ArtifactBonusType.SpellPowerSkill, 4), new(ArtifactBonusType.KnowledgeSkill, 4)], []);
        table[(int)ArtifactType.GoldenGoose] = new ArtifactData([new(ArtifactBonusType.GoldIncome, 10000)], []);
        table[(int)ArtifactType.ArcaneNecklace] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 4)], []);
        table[(int)ArtifactType.CasterBracelet] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 2)], []);
        table[(int)ArtifactType.MageRing] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 2)], []);
        table[(int)ArtifactType.WitchesBroach] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 3)], []);
        table[(int)ArtifactType.MedalValor] = new ArtifactData([new(ArtifactBonusType.Morale, 1)], []);
        table[(int)ArtifactType.MedalCourage] = new ArtifactData([new(ArtifactBonusType.Morale, 1)], []);
        table[(int)ArtifactType.MedalHonor] = new ArtifactData([new(ArtifactBonusType.Morale, 1)], []);
        table[(int)ArtifactType.MedalDistinction] = new ArtifactData([new(ArtifactBonusType.Morale, 1)], []);
        table[(int)ArtifactType.FizbinMisfortune] = new ArtifactData([], [new(ArtifactCurseType.Morale, 2)]);
        table[(int)ArtifactType.ThunderMace] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 1)], []);
        table[(int)ArtifactType.ArmoredGauntlets] = new ArtifactData([new(ArtifactBonusType.DefenceSkill, 1)], []);
        table[(int)ArtifactType.DefenderHelm] = new ArtifactData([new(ArtifactBonusType.DefenceSkill, 1)], []);
        table[(int)ArtifactType.GiantFlail] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 1)], []);
        table[(int)ArtifactType.Ballista] = new ArtifactData([new(ArtifactBonusType.ExtraCatapultShots, 1)], []);
        table[(int)ArtifactType.StealthShield] = new ArtifactData([new(ArtifactBonusType.DefenceSkill, 2)], []);
        table[(int)ArtifactType.DragonSword] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 3)], []);
        table[(int)ArtifactType.PowerAxe] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 2)], []);
        table[(int)ArtifactType.DivineBreastplate] = new ArtifactData([new(ArtifactBonusType.DefenceSkill, 3)], []);
        table[(int)ArtifactType.MinorScroll] = new ArtifactData([new(ArtifactBonusType.KnowledgeSkill, 2)], []);
        table[(int)ArtifactType.MajorScroll] = new ArtifactData([new(ArtifactBonusType.KnowledgeSkill, 3)], []);
        table[(int)ArtifactType.SuperiorScroll] = new ArtifactData([new(ArtifactBonusType.KnowledgeSkill, 4)], []);
        table[(int)ArtifactType.ForemostScroll] = new ArtifactData([new(ArtifactBonusType.KnowledgeSkill, 5)], []);
        table[(int)ArtifactType.EndlessSackGold] = new ArtifactData([new(ArtifactBonusType.GoldIncome, 1000)], []);
        table[(int)ArtifactType.EndlessBagGold] = new ArtifactData([new(ArtifactBonusType.GoldIncome, 750)], []);
        table[(int)ArtifactType.EndlessPurseGold] = new ArtifactData([new(ArtifactBonusType.GoldIncome, 500)], []);
        table[(int)ArtifactType.NomadBootsMobility] = new ArtifactData([new(ArtifactBonusType.LandMobility, 600)], []);
        table[(int)ArtifactType.TravelerBootsMobility] = new ArtifactData([new(ArtifactBonusType.LandMobility, 300)], []);
        table[(int)ArtifactType.RabbitFoot] = new ArtifactData([new(ArtifactBonusType.Luck, 1)], []);
        table[(int)ArtifactType.GoldenHorseshoe] = new ArtifactData([new(ArtifactBonusType.Luck, 1)], []);
        table[(int)ArtifactType.GamblerLuckyCoin] = new ArtifactData([new(ArtifactBonusType.Luck, 1)], []);
        table[(int)ArtifactType.FourLeafClover] = new ArtifactData([new(ArtifactBonusType.Luck, 1)], []);
        table[(int)ArtifactType.TrueCompassMobility] = new ArtifactData([new(ArtifactBonusType.LandMobility, 500), new(ArtifactBonusType.SeaMobility, 500)], []);
        table[(int)ArtifactType.SailorsAstrolabeMobility] = new ArtifactData([new(ArtifactBonusType.SeaMobility, 1000)], []);
        table[(int)ArtifactType.EvilEye] = new ArtifactData([new(ArtifactBonusType.CurseSpellCostReductionPercent, 50)], []);
        table[(int)ArtifactType.EnchantedHourglass] = new ArtifactData([new(ArtifactBonusType.EveryCombatSpellDuration, 2)], []);
        table[(int)ArtifactType.GoldWatch] = new ArtifactData([new(ArtifactBonusType.HypnotizeSpellExtraEffectivenessPercent, 100)], []);
        table[(int)ArtifactType.Skullcap] = new ArtifactData([new(ArtifactBonusType.MindInfluenceSpellCostReductionPercent, 50)], []);
        table[(int)ArtifactType.IceCloak] = new ArtifactData([new(ArtifactBonusType.ColdSpellDamageReductionPercent, 50)], []);
        table[(int)ArtifactType.FireCloak] = new ArtifactData([new(ArtifactBonusType.FireSpellDamageReductionPercent, 50)], []);
        table[(int)ArtifactType.LightningHelm] = new ArtifactData([new(ArtifactBonusType.LightningSpellDamageReductionPercent, 50)], []);
        table[(int)ArtifactType.EvercoldIcicle] = new ArtifactData([new(ArtifactBonusType.ColdSpellExtraEffectivenessPercent, 50)], []);
        table[(int)ArtifactType.EverhotLavaRock] = new ArtifactData([new(ArtifactBonusType.FireSpellExtraEffectivenessPercent, 50)], []);
        table[(int)ArtifactType.LightningRod] = new ArtifactData([new(ArtifactBonusType.LightningSpellExtraEffectivenessPercent, 50)], []);
        table[(int)ArtifactType.SnakeRing] = new ArtifactData([new(ArtifactBonusType.BlessSpellCostReductionPercent, 50)], []);
        table[(int)ArtifactType.Ankh] = new ArtifactData([new(ArtifactBonusType.ResurrectSpellExtraEffectivenessPercent, 100)], []);
        table[(int)ArtifactType.BookElements] = new ArtifactData([new(ArtifactBonusType.SummoningSpellExtraEffectivenessPercent, 100)], []);
        table[(int)ArtifactType.ElementalRing] = new ArtifactData([new(ArtifactBonusType.SummoningSpellCostReductionPercent, 50)], []);
        table[(int)ArtifactType.HolyPendant] = new ArtifactData([new(ArtifactBonusType.CurseSpellImmunity, 0)], []);
        table[(int)ArtifactType.PendantFreeWill] = new ArtifactData([new(ArtifactBonusType.HypnotizeSpellImmunity, 0)], []);
        table[(int)ArtifactType.PendantLife] = new ArtifactData([new(ArtifactBonusType.DeathSpellImmunity, 0)], []);
        table[(int)ArtifactType.SerenityPendant] = new ArtifactData([new(ArtifactBonusType.BerserkSpellImmunity, 0)], []);
        table[(int)ArtifactType.SeeingEyePendant] = new ArtifactData([new(ArtifactBonusType.BlindSpellImmunity, 0)], []);
        table[(int)ArtifactType.KineticPendant] = new ArtifactData([new(ArtifactBonusType.ParalyzeSpellImmunity, 0)], []);
        table[(int)ArtifactType.PendantDeath] = new ArtifactData([new(ArtifactBonusType.HolySpellImmunity, 0)], []);
        table[(int)ArtifactType.WandNegation] = new ArtifactData([new(ArtifactBonusType.DispelSpellImmunity, 0)], []);
        table[(int)ArtifactType.GoldenBow] = new ArtifactData([new(ArtifactBonusType.NoShootingPenalty, 50)], []);
        table[(int)ArtifactType.Telescope] = new ArtifactData([new(ArtifactBonusType.AreaRevealDistance, 1)], []);
        table[(int)ArtifactType.StatesmanQuill] = new ArtifactData([new(ArtifactBonusType.SurrenderCostReductionPercent, 10)], []);
        table[(int)ArtifactType.WizardHat] = new ArtifactData([new(ArtifactBonusType.EveryCombatSpellDuration, 10)], []);
        table[(int)ArtifactType.PowerRing] = new ArtifactData([new(ArtifactBonusType.SpellPointsDailyGeneration, 2)], []);
        table[(int)ArtifactType.AmmoCart] = new ArtifactData([new(ArtifactBonusType.EndlessAmmunition, 0)], []);
        table[(int)ArtifactType.TaxLien] = new ArtifactData([], [new(ArtifactCurseType.GoldPenalty, 250)]);
        table[(int)ArtifactType.HideousMask] = new ArtifactData([], [new(ArtifactCurseType.NoJoiningArmies, 0)]);
        table[(int)ArtifactType.EndlessPouchSulfur] = new ArtifactData([new(ArtifactBonusType.SulfurIncome, 1)], []);
        table[(int)ArtifactType.EndlessVialMercury] = new ArtifactData([new(ArtifactBonusType.MercuryIncome, 1)], []);
        table[(int)ArtifactType.EndlessPouchGems] = new ArtifactData([new(ArtifactBonusType.GemsIncome, 1)], []);
        table[(int)ArtifactType.EndlessCordWood] = new ArtifactData([new(ArtifactBonusType.WoodIncome, 1)], []);
        table[(int)ArtifactType.EndlessCartOre] = new ArtifactData([new(ArtifactBonusType.OreIncome, 1)], []);
        table[(int)ArtifactType.EndlessPouchCrystal] = new ArtifactData([new(ArtifactBonusType.CrystalIncome, 1)], []);
        table[(int)ArtifactType.SpikedHelm] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 1), new(ArtifactBonusType.DefenceSkill, 1)], []);
        table[(int)ArtifactType.SpikedShield] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 2), new(ArtifactBonusType.DefenceSkill, 2)], []);
        table[(int)ArtifactType.WhitePearl] = new ArtifactData([new(ArtifactBonusType.KnowledgeSkill, 1), new(ArtifactBonusType.SpellPowerSkill, 1)], []);
        table[(int)ArtifactType.BlackPearl] = new ArtifactData([new(ArtifactBonusType.KnowledgeSkill, 2), new(ArtifactBonusType.SpellPowerSkill, 2)], []);
        table[(int)ArtifactType.MagicBook] = new ArtifactData([new(ArtifactBonusType.None, 0)], []);
        table[(int)ArtifactType.EditorAnyUltimateArtifact] = new ArtifactData([new(ArtifactBonusType.None, 0)], []);
        table[(int)ArtifactType.Unused84] = new ArtifactData([new(ArtifactBonusType.None, 0)], []);
        table[(int)ArtifactType.Unused85] = new ArtifactData([new(ArtifactBonusType.None, 0)], []);
        table[(int)ArtifactType.Unused86] = new ArtifactData([new(ArtifactBonusType.None, 0)], []);
        table[(int)ArtifactType.SpellScroll] = new ArtifactData([new(ArtifactBonusType.AddSpell, 0)], []);
        table[(int)ArtifactType.ArmMartyr] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 3)], [new(ArtifactCurseType.UndeadMoralePenalty, 0)]);
        table[(int)ArtifactType.BreastplateAnduran] = new ArtifactData([new(ArtifactBonusType.DefenceSkill, 5)], []);
        table[(int)ArtifactType.BroachShielding] = new ArtifactData([new(ArtifactBonusType.ElementalSpellDamageReductionPercent, 50)], [new(ArtifactCurseType.SpellPowerSkill, 2)]);
        table[(int)ArtifactType.BattleGarb] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 5), new(ArtifactBonusType.DefenceSkill, 5), new(ArtifactBonusType.SpellPowerSkill, 5), new(ArtifactBonusType.MaximumMorale, 0), new(ArtifactBonusType.MaximumLuck, 0), new(ArtifactBonusType.AddSpell, 59)], []);
        table[(int)ArtifactType.CrystalBall] = new ArtifactData([new(ArtifactBonusType.ViewMonsterInformation, 0)], []);
        table[(int)ArtifactType.HeartFire] = new ArtifactData([new(ArtifactBonusType.FireSpellDamageReductionPercent, 50)], [new(ArtifactCurseType.ColdSpellExtraDamagePercent, 100)]);
        table[(int)ArtifactType.HeartIce] = new ArtifactData([new(ArtifactBonusType.ColdSpellDamageReductionPercent, 50)], [new(ArtifactCurseType.FireSpellExtraDamagePercent, 100)]);
        table[(int)ArtifactType.HelmetAnduran] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 5)], []);
        table[(int)ArtifactType.HolyHammer] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 5)], []);
        table[(int)ArtifactType.LegendaryScepter] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 2), new(ArtifactBonusType.DefenceSkill, 2), new(ArtifactBonusType.KnowledgeSkill, 2), new(ArtifactBonusType.SpellPowerSkill, 2)], []);
        table[(int)ArtifactType.Masthead] = new ArtifactData([new(ArtifactBonusType.SeaBattleMoraleBoost, 1), new(ArtifactBonusType.SeaBattleLuckBoost, 1)], []);
        table[(int)ArtifactType.SphereNegation] = new ArtifactData([new(ArtifactBonusType.DisableAllSpellCombatCasting, 0)], []);
        table[(int)ArtifactType.StaffWizardry] = new ArtifactData([new(ArtifactBonusType.SpellPowerSkill, 5)], []);
        table[(int)ArtifactType.SwordBreaker] = new ArtifactData([new(ArtifactBonusType.DefenceSkill, 4), new(ArtifactBonusType.AttackSkill, 1)], []);
        table[(int)ArtifactType.SwordAnduran] = new ArtifactData([new(ArtifactBonusType.AttackSkill, 5)], []);
        table[(int)ArtifactType.SpadeNecromancy] = new ArtifactData([new(ArtifactBonusType.NecromancySkill, 10)], []);

        // 参考是同一张表的两遍：数字填完，名字与两段描述按编号套上。
        for (int i = 0; i < table.Length && i < ArtifactTexts.Rows.Length; ++i)
        {
            table[i] = table[i].WithText(ArtifactTexts.Rows[i]);
        }

        return table;
    }
}
