using FHeroes2.Assets.Maps;

using FHeroes2.Core.Random;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Spells;

/// <summary>
/// A spell, the id wrapper of <c>Spell</c> (<c>spell.h:31-333</c>, <c>spell.cpp:152-750</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the ids in an <c>enum</c> inside the class and the behaviour in the class itself, so
/// <see cref="SpellType"/> plus this struct is the same split: the enum is the numbering the data files use,
/// the struct is the value a spell book stores. It is a value type for the same reason the reference passes
/// <c>Spell</c> around by const reference — it is one integer.
/// </para>
/// <para>
/// <b>What is ported</b>: the numbers (<see cref="SpellTable"/>), the level, every predicate the battle engine
/// and the spell book ask — the traits (<see cref="IsCombat"/>, <see cref="IsDamage"/>, <see cref="IsRestore"/>,
/// <see cref="IsResurrect"/>, <see cref="IsSummon"/>, <see cref="IsMassActions"/>, <see cref="IsMindInfluence"/>,
/// <see cref="IsFire"/>, <see cref="IsCold"/>, <see cref="IsElementalSpell"/>, <see cref="IsBuiltinOnly"/>,
/// <see cref="IsGuardianType"/>), the targeting rules (<see cref="IsSingleTarget"/>,
/// <see cref="IsApplyToFriends"/>, <see cref="IsApplyToEnemies"/>, <see cref="IsApplyToAnyTroops"/>,
/// <see cref="IsEffectDispel"/>, <see cref="IsUndeadOnly"/>, <see cref="IsAliveOnly"/>,
/// <see cref="IsApplyWithoutFocusObject"/>) — the strategic value, the spell point cost including the artifact
/// reduction, and the random-spell queries (<see cref="RandomSpell"/>, <see cref="TotalSpellsPerLevel"/>,
/// <see cref="SuitableForSpellBook"/>, <see cref="WeightForRace"/>).
/// </para>
/// <para>
/// <b>What is not</b>: every method that needs the text layer (<c>GetName</c>, <c>GetDescription</c>) and the
/// serialize operators, which the port leaves to the batch that ports <c>serialize.cpp</c>.
/// </para>
/// </remarks>
public readonly record struct Spell
{
    /// <summary>
    /// Wraps an id, <c>Spell( const int32_t )</c> (<c>spell.h:132</c>).
    /// </summary>
    /// <remarks>
    /// Anything outside <c>0..SPELL_COUNT</c> becomes <see cref="SpellType.None"/> exactly as in the
    /// reference, so a corrupt file cannot index the table.
    /// </remarks>
    /// <param name="id">The spell id.</param>
    public Spell(int id) => Type = id > 0 && id < (int)SpellType.Count ? (SpellType)id : SpellType.None;

    /// <summary>Wraps a spell id, the <see cref="Spell(int)"/> twin.</summary>
    /// <param name="type">The spell id.</param>
    public Spell(SpellType type)
        : this((int)type)
    {
    }

    /// <summary>The id of the spell, <c>GetID()</c>.</summary>
    public SpellType Type { get; }

    /// <summary>
    /// The level of the spell, 1 to 5, or zero when it has none, <c>Spell::Level()</c>
    /// (<c>spell.cpp:269-357</c>).
    /// </summary>
    /// <remarks>
    /// The adventure map spells have levels too — they are what a mage guild offers — so a zero means "no
    /// spell" (<see cref="SpellType.None"/>, the random placeholders, <see cref="SpellType.Petrify"/>) rather
    /// than "not a combat spell".
    /// </remarks>
    /// <returns>The level of the spell.</returns>
    public int Level() => Type switch
    {
        SpellType.Bless or SpellType.BloodLust or SpellType.Cure or SpellType.Curse or SpellType.Dispel
            or SpellType.Haste or SpellType.Arrow or SpellType.Shield or SpellType.Slow or SpellType.StoneSkin
            or SpellType.ViewMines or SpellType.ViewResources => 1,

        SpellType.Blind or SpellType.ColdRay or SpellType.DeathRipple or SpellType.DisruptingRay
            or SpellType.DragonSlayer or SpellType.LightningBolt or SpellType.SteelSkin or SpellType.Haunt
            or SpellType.SummonBoat or SpellType.ViewArtifacts or SpellType.Visions => 2,

        SpellType.AnimateDead or SpellType.AntiMagic or SpellType.ColdRing or SpellType.DeathWave
            or SpellType.Earthquake or SpellType.Fireball or SpellType.HolyWord or SpellType.MassBless
            or SpellType.MassCurse or SpellType.MassDispel or SpellType.MassHaste or SpellType.Paralyze
            or SpellType.Teleport or SpellType.IdentifyHero or SpellType.ViewHeroes or SpellType.ViewTowns => 3,

        SpellType.Berserker or SpellType.ChainLightning or SpellType.ElementalStorm or SpellType.FireBlast
            or SpellType.HolyShout or SpellType.MassCure or SpellType.MassShield or SpellType.MassSlow
            or SpellType.MeteorShower or SpellType.Resurrect or SpellType.SetEGuardian
            or SpellType.SetAGuardian or SpellType.SetFGuardian or SpellType.SetWGuardian
            or SpellType.TownGate or SpellType.ViewAll => 4,

        SpellType.Armageddon or SpellType.Hypnotize or SpellType.MirrorImage or SpellType.ResurrectTrue
            or SpellType.SummonEElement or SpellType.SummonAElement or SpellType.SummonFElement
            or SpellType.SummonWElement or SpellType.DimensionDoor or SpellType.TownPortal => 5,

        _ => 0,
    };

    /// <summary>Whether the spell is of the given level, <c>Spell::isLevel()</c> (<c>spell.h:262</c>).</summary>
    /// <param name="level">Level to test, 1 to 5.</param>
    /// <returns>Whether the spell is of that level.</returns>
    public bool IsLevel(int level) => Level() == level;

    /// <summary>
    /// Whether the spell is cast in battle, <c>Spell::isCombat()</c> (<c>spell.cpp:359-385</c>).
    /// </summary>
    /// <remarks>
    /// A deny list: every spell is a combat spell except the eighteen the adventure map casts and
    /// <see cref="SpellType.None"/>.
    /// </remarks>
    /// <returns>Whether the spell is a combat spell.</returns>
    public bool IsCombat() => Type switch
    {
        SpellType.None or SpellType.ViewMines or SpellType.ViewResources or SpellType.ViewArtifacts
            or SpellType.ViewTowns or SpellType.ViewHeroes or SpellType.ViewAll or SpellType.IdentifyHero
            or SpellType.SummonBoat or SpellType.DimensionDoor or SpellType.TownGate or SpellType.TownPortal
            or SpellType.Visions or SpellType.Haunt or SpellType.SetEGuardian or SpellType.SetAGuardian
            or SpellType.SetFGuardian or SpellType.SetWGuardian => false,

        _ => true,
    };

    /// <summary>Whether the spell is cast on the adventure map, <c>isAdventure()</c> (<c>spell.h:261</c>).</summary>
    /// <returns>Whether the spell is an adventure map spell.</returns>
    public bool IsAdventure() => !IsCombat();

    /// <summary>
    /// The damage the spell does per point of spell power, <c>Spell::Damage()</c>
    /// (<c>spell.cpp:403-427</c>).
    /// </summary>
    /// <returns>The <c>extraValue</c> of the fourteen damage spells, zero for everything else.</returns>
    public uint Damage() => Type switch
    {
        SpellType.Arrow or SpellType.Fireball or SpellType.FireBlast or SpellType.LightningBolt
            or SpellType.ColdRing or SpellType.DeathWave or SpellType.HolyWord or SpellType.ChainLightning
            or SpellType.Armageddon or SpellType.ElementalStorm or SpellType.MeteorShower or SpellType.ColdRay
            or SpellType.HolyShout or SpellType.DeathRipple => ExtraValue(),

        _ => 0,
    };

    /// <summary>Whether the spell does damage, <c>isDamage()</c> (<c>spell.h:266</c>).</summary>
    /// <returns>Whether <see cref="Damage"/> is not zero.</returns>
    public bool IsDamage() => Damage() != 0;

    /// <summary>
    /// The hit points the spell brings back, <c>Spell::Resurrect()</c> (<c>spell.cpp:464-477</c>).
    /// </summary>
    /// <returns>The <c>extraValue</c> of the three resurrection spells, zero for everything else.</returns>
    public uint Resurrect() => Type switch
    {
        SpellType.AnimateDead or SpellType.Resurrect or SpellType.ResurrectTrue => ExtraValue(),
        _ => 0,
    };

    /// <summary>Whether the spell brings troops back, <c>isResurrect()</c> (<c>spell.h:265</c>).</summary>
    /// <returns>Whether <see cref="Resurrect"/> is not zero.</returns>
    public bool IsResurrect() => Resurrect() != 0;

    /// <summary>
    /// Whether the spell summons elementals, <c>Spell::isSummon()</c> (<c>spell.cpp:656-670</c>).
    /// </summary>
    /// <returns>Whether the spell is one of the four summoning spells.</returns>
    public bool IsSummon() => Type is SpellType.SummonEElement or SpellType.SummonAElement
        or SpellType.SummonFElement or SpellType.SummonWElement;

    /// <summary>
    /// Whether the spell is a mass version of a single-target one, <c>Spell::isMassActions()</c>
    /// (<c>spell.cpp:733-750</c>).
    /// </summary>
    /// <returns>Whether the spell is one of the seven mass spells.</returns>
    public bool IsMassActions() => Type is SpellType.MassCure or SpellType.MassHaste or SpellType.MassSlow
        or SpellType.MassBless or SpellType.MassCurse or SpellType.MassDispel or SpellType.MassShield;

    /// <summary>
    /// Whether the spell works on the mind, <c>Spell::isMindInfluence()</c> (<c>spell.cpp:429-443</c>).
    /// </summary>
    /// <remarks>
    /// Only asked by <c>spellPoints( hero )</c> to pick the artifact bonus that lowers the cost, and by the
    /// battle engine to refuse a target that cannot be influenced.
    /// </remarks>
    /// <returns>Whether the spell is one of the four mind spells.</returns>
    public bool IsMindInfluence() => Type is SpellType.Blind or SpellType.Paralyze or SpellType.Berserker
        or SpellType.Hypnotize;

    /// <summary>
    /// The hit points the spell heals, <c>Spell::Restore()</c> (<c>spell.cpp:450-462</c>).
    /// </summary>
    /// <returns>The <c>extraValue</c> of the two healing spells, zero for everything else.</returns>
    public uint Restore() => Type is SpellType.Cure or SpellType.MassCure ? ExtraValue() : 0;

    /// <summary>Whether the spell heals, <c>isRestore()</c> (<c>spell.h:203</c>).</summary>
    /// <returns>Whether <see cref="Restore"/> is not zero.</returns>
    public bool IsRestore() => Restore() != 0;

    /// <summary>
    /// Whether the spell only affects undead troops, <c>Spell::isUndeadOnly()</c>
    /// (<c>spell.cpp:562-575</c>).
    /// </summary>
    /// <remarks>
    /// The three spells are the ones that are defined by the undead: raising them and the two holy blasts that
    /// hit only them. The battle engine uses this to decide whether a stack can be a target at all.
    /// </remarks>
    /// <returns>Whether the spell works on undead troops alone.</returns>
    public bool IsUndeadOnly() => Type is SpellType.AnimateDead or SpellType.HolyWord or SpellType.HolyShout;

    /// <summary>
    /// Whether the spell only affects living troops, <c>Spell::isAliveOnly()</c>
    /// (<c>spell.cpp:577-595</c>).
    /// </summary>
    /// <remarks>
    /// The eight spells that are meaningless on a corpse: the bless/curse pair, the two death blasts that feed
    /// on the living, and resurrection.
    /// </remarks>
    /// <returns>Whether the spell works on living troops alone.</returns>
    public bool IsAliveOnly() => Type is SpellType.Bless or SpellType.MassBless or SpellType.Curse
        or SpellType.MassCurse or SpellType.DeathRipple or SpellType.DeathWave or SpellType.Resurrect
        or SpellType.ResurrectTrue;

    /// <summary>
    /// Whether the spell needs one stack picked out, <c>Spell::isSingleTarget()</c>
    /// (<c>spell.cpp:597-632</c>).
    /// </summary>
    /// <remarks>
    /// The twenty-five spells the battle engine asks the player to click a stack for. Everything else either
    /// covers the whole battlefield or picks its own targets, which is the
    /// <see cref="IsApplyWithoutFocusObject"/> list.
    /// </remarks>
    /// <returns>Whether the spell is cast on a single stack.</returns>
    public bool IsSingleTarget() => Type is SpellType.LightningBolt or SpellType.Teleport or SpellType.Cure
        or SpellType.Resurrect or SpellType.ResurrectTrue or SpellType.Haste or SpellType.Slow
        or SpellType.Blind or SpellType.Bless or SpellType.StoneSkin or SpellType.SteelSkin or SpellType.Curse
        or SpellType.AntiMagic or SpellType.Dispel or SpellType.Arrow or SpellType.Berserker
        or SpellType.Paralyze or SpellType.Hypnotize or SpellType.ColdRay or SpellType.DisruptingRay
        or SpellType.DragonSlayer or SpellType.BloodLust or SpellType.AnimateDead
        or SpellType.MirrorImage or SpellType.Shield;

    /// <summary>
    /// Whether the spell is cast without a target stack, <c>Spell::isApplyWithoutFocusObject()</c>
    /// (<c>spell.cpp:634-654</c>).
    /// </summary>
    /// <remarks>
    /// The mass spells and the summons need no target because they apply to the whole side, plus the seven
    /// spells that hit everything at once.
    /// </remarks>
    /// <returns>Whether the spell needs no target stack.</returns>
    public bool IsApplyWithoutFocusObject() => IsMassActions() || IsSummon()
        || Type is SpellType.DeathRipple or SpellType.DeathWave or SpellType.Earthquake
        or SpellType.HolyWord or SpellType.HolyShout or SpellType.Armageddon or SpellType.ElementalStorm;

    /// <summary>
    /// Whether the spell takes a spell effect off the target, <c>Spell::isEffectDispel()</c>
    /// (<c>spell.cpp:672-686</c>).
    /// </summary>
    /// <returns>Whether the spell is one of the four dispels.</returns>
    public bool IsEffectDispel() => Type is SpellType.Cure or SpellType.MassCure or SpellType.Dispel
        or SpellType.MassDispel;

    /// <summary>
    /// Whether the spell may target either side, <c>Spell::isApplyToAnyTroops()</c>
    /// (<c>spell.cpp:688-700</c>).
    /// </summary>
    /// <returns>Whether the spell is one of the two dispels that work on friend and foe alike.</returns>
    public bool IsApplyToAnyTroops() => Type is SpellType.Dispel or SpellType.MassDispel;

    /// <summary>
    /// Whether the spell may be cast on friendly stacks, <c>Spell::isApplyToFriends()</c>
    /// (<c>spell.cpp:702-731</c>).
    /// </summary>
    /// <returns>Whether the spell is one of the eighteen friendly spells.</returns>
    public bool IsApplyToFriends() => Type is SpellType.Bless or SpellType.BloodLust or SpellType.Cure
        or SpellType.Haste or SpellType.Shield or SpellType.StoneSkin or SpellType.DragonSlayer
        or SpellType.SteelSkin or SpellType.AnimateDead or SpellType.AntiMagic or SpellType.Teleport
        or SpellType.Resurrect or SpellType.MirrorImage or SpellType.ResurrectTrue or SpellType.MassBless
        or SpellType.MassCure or SpellType.MassHaste or SpellType.MassShield;

    /// <summary>
    /// Whether the spell may be cast on enemy stacks, <c>Spell::isApplyToEnemies()</c>
    /// (<c>spell.cpp:752-776</c>).
    /// </summary>
    /// <returns>Whether the spell is one of the thirteen hostile spells.</returns>
    public bool IsApplyToEnemies() => Type is SpellType.MassSlow or SpellType.MassCurse or SpellType.Curse
        or SpellType.Arrow or SpellType.Slow or SpellType.Blind or SpellType.ColdRay
        or SpellType.DisruptingRay or SpellType.LightningBolt or SpellType.ChainLightning
        or SpellType.Paralyze or SpellType.Berserker or SpellType.Hypnotize;

    /// <summary>Whether the spell is a fire spell, <c>isFire()</c> (<c>spell.h:224</c>).</summary>
    /// <returns>Whether the spell is Fireball or Fireblast.</returns>
    public bool IsFire() => Type is SpellType.Fireball or SpellType.FireBlast;

    /// <summary>Whether the spell is a cold spell, <c>isCold()</c> (<c>spell.h:229</c>).</summary>
    /// <returns>Whether the spell is Cold Ray or Cold Ring.</returns>
    public bool IsCold() => Type is SpellType.ColdRay or SpellType.ColdRing;

    /// <summary>
    /// Whether the spell belongs to one of the magic schools' damaging elementals,
    /// <c>isElementalSpell()</c> (<c>spell.h:234-248</c>).
    /// </summary>
    /// <remarks>
    /// The seven spells a magic mirror can be told to reflect as an elemental — fire, cold and lightning.
    /// </remarks>
    /// <returns>Whether the spell is one of the seven elemental spells.</returns>
    public bool IsElementalSpell() => Type is SpellType.ColdRay or SpellType.ColdRing or SpellType.Fireball
        or SpellType.FireBlast or SpellType.LightningBolt or SpellType.ChainLightning
        or SpellType.ElementalStorm;

    /// <summary>
    /// Whether the spell is part of a creature and cannot be learned,
    /// <c>isBuiltinOnly()</c> (<c>spell.h:250-253</c>).
    /// </summary>
    /// <returns>Whether the spell is Petrify, the medusa's gaze.</returns>
    public bool IsBuiltinOnly() => Type == SpellType.Petrify;

    /// <summary>
    /// Whether the spell guards a tile instead of affecting troops, <c>Spell::isGuardianType()</c>
    /// (<c>spell.cpp:387-401</c>).
    /// </summary>
    /// <returns>Whether the spell is Haunt or one of the four elemental guardians.</returns>
    public bool IsGuardianType() => Type is SpellType.Haunt or SpellType.SetEGuardian
        or SpellType.SetAGuardian or SpellType.SetFGuardian or SpellType.SetWGuardian;

    /// <summary>
    /// The number the behaviour of the spell reads, <c>Spell::ExtraValue()</c> (<c>spell.cpp:479-482</c>).
    /// </summary>
    /// <returns>The <c>extraValue</c> column of the table.</returns>
    public uint ExtraValue() => SpellTable.Get(Type).ExtraValue;

    /// <summary>
    /// What the spell costs to cast, <c>Spell::spellPoints()</c> (<c>spell.cpp:173-219</c>).
    /// </summary>
    /// <remarks>
    /// The <c>hero == nullptr</c> branch of the reference: the list price from the table, no artifact taken
    /// into account.
    /// </remarks>
    /// <returns>The spell points the spell costs.</returns>
    public uint SpellPoints() => SpellTable.Get(Type).SpellPoints;

    /// <summary>
    /// What the spell costs a hero carrying <paramref name="bag"/>,
    /// <c>Spell::spellPoints( const HeroBase * )</c> (<c>spell.cpp:173-219</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only four kinds of spell are ever cheaper: the ones whose cost reduction artifacts exist — bless, curse,
    /// summoning and mind influence. Every reduction is applied to the running cost one artifact at a time and
    /// truncated, in slot order, exactly like the reference; the cost never goes below one point.
    /// </para>
    /// <para>
    /// The hero argument of the reference is replaced by its bag: that is the only part of the hero the method
    /// reads, and taking the bag keeps this file free of <c>Hero</c>.
    /// </para>
    /// </remarks>
    /// <param name="bag">The artifacts of the hero, or null for a hero without any.</param>
    /// <returns>The spell points the spell costs that hero.</returns>
    public uint SpellPoints(ArtifactBag? bag)
    {
        if (bag is null)
        {
            return SpellPoints();
        }

        ArtifactBonusType reduction = Type switch
        {
            SpellType.Bless or SpellType.MassBless => ArtifactBonusType.BlessSpellCostReductionPercent,
            SpellType.Curse or SpellType.MassCurse => ArtifactBonusType.CurseSpellCostReductionPercent,
            SpellType.SummonEElement or SpellType.SummonAElement or SpellType.SummonFElement
                or SpellType.SummonWElement => ArtifactBonusType.SummoningSpellCostReductionPercent,
            SpellType.Blind or SpellType.Paralyze or SpellType.Berserker or SpellType.Hypnotize
                => ArtifactBonusType.MindInfluenceSpellCostReductionPercent,
            _ => ArtifactBonusType.None,
        };

        if (reduction == ArtifactBonusType.None)
        {
            return SpellPoints();
        }

        int cost = (int)SpellPoints();

        foreach (int percent in bag.GetTotalMultipliedPercent(reduction))
        {
            cost = cost * (100 - percent) / 100;
        }

        return cost < 1 ? 1u : (uint)cost;
    }

    /// <summary>
    /// How far Dimension Door takes a hero, <c>Spell::CalculateDimensionDoorDistance()</c>
    /// (<c>spell.cpp:803-822</c>).
    /// </summary>
    /// <remarks>
    /// A fixed fourteen tiles before the spell power is added: the reference returns
    /// <c>14 + hero->GetPower()</c> where this constant is the fourteen.
    /// </remarks>
    /// <returns>The tiles the spell covers before the spell power is added.</returns>
    public static int DimensionDoorDistance() => 14;

    /// <summary>The movement points casting the spell on the adventure map costs, <c>movePoints()</c>.</summary>
    /// <returns>The movement points the cast costs.</returns>
    public uint MovePoints() => SpellTable.Get(Type).MovePoints;

    /// <summary>
    /// The movement points the hero must have left to cast the spell, <c>minMovePoints()</c>.
    /// </summary>
    /// <returns>The movement points the hero needs.</returns>
    public uint MinMovePoints() => SpellTable.Get(Type).MinMovePoints;

    /// <summary>The icon of the spell in <c>ICN::SPELLS</c>, <c>Spell::IndexSprite()</c>.</summary>
    /// <returns>The image id.</returns>
    public uint IndexSprite() => SpellTable.Get(Type).ImageId;

    /// <summary>
    /// What the spell is worth in a strategic fight, <c>Spell::getStrategicValue()</c>
    /// (<c>spell.cpp:221-267</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is the best a hero can do with the spell: damage spells scale with the spell power, the
    /// spells that turn a battle — resurrection, the mass spells, Blind, Paralyze — scale with the army, and
    /// everything else is a flat four percent of it. Casts are capped at ten and their worth is deliberately
    /// concave (<c>casts - 0.05 * casts²</c>), so ten casts of the same spell are worth five times one, not
    /// ten.
    /// </para>
    /// <para>
    /// <b>A quirk of the reference, kept on purpose</b>: the summoning branch asks
    /// <c>Monster( id ).GetMonsterStrength()</c>, passing the <b>spell</b> id where a monster id belongs
    /// (<c>spell.cpp:263</c>). For the four summoning spells that is monster 44 to 47 — Mage, Archmage, Giant
    /// and Titan — not the elementals, so the strength of a summon is measured with a far more expensive
    /// monster. The numbers of fheroes2's AI ranking depend on it, so the port copies the expression instead
    /// of correcting it.
    /// </para>
    /// </remarks>
    /// <param name="armyStrength">The strength of the army the hero leads.</param>
    /// <param name="currentSpellPoints">The spell points the hero has.</param>
    /// <param name="spellPower">The spell power of the hero.</param>
    /// <returns>The strategic value; zero when the spell cannot be cast at all.</returns>
    public double GetStrategicValue(double armyStrength, uint currentSpellPoints, int spellPower)
    {
        uint spellCost = SpellPoints();
        uint casts = spellCost != 0 ? Math.Min(10u, currentSpellPoints / spellCost) : 0;

        if (casts == 0)
        {
            return 0;
        }

        double amountModifier = casts == 1 ? 1 : casts - (0.05 * casts * casts);

        if (IsAdventure())
        {
            return Type switch
            {
                SpellType.DimensionDoor => 500 * amountModifier,
                SpellType.TownGate or SpellType.TownPortal => 250 * amountModifier,
                SpellType.ViewAll => 500,
                _ => 0,
            };
        }

        if (IsDamage())
        {
            return amountModifier * Damage() * spellPower;
        }

        if (IsResurrect() || IsMassActions() || Type is SpellType.Blind or SpellType.Paralyze)
        {
            return armyStrength * 0.1 * amountModifier;
        }

        if (IsSummon())
        {
            return MonsterStatistics.GetMonsterStrength((MonsterType)(int)Type) * ExtraValue() * spellPower;
        }

        return armyStrength * 0.04 * amountModifier;
    }

    /// <summary>
    /// How likely the spell is to turn up in a mage guild of <paramref name="race"/>,
    /// <c>Spell::weightForRace()</c> (<c>spell.cpp:483-560</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 十是「随便挑」，零是「几乎不挑」——参考的掷骰是 <c>Rand::Get( 0, 10 ) &gt; weight</c> 才跳过，
    /// 所以 0 仍有十分之一的概率被选中，而不是永不出现。法师公会的掷骰循环因此要多试几次，见
    /// <see cref="FHeroes2.Game.Castle.MageGuild"/>。
    /// </para>
    /// <para>
    /// 四张零权重的表：神圣之词/圣咏对亡灵、死亡波纹/死亡之波对其他五族、四种召唤元素、以及
    /// 时空门/透视/作祟/守护元素那几张只有地图事件才该出现的法术。
    /// </para>
    /// </remarks>
    /// <param name="race">The race of the castle whose guild rolls the spell.</param>
    /// <returns>The weight, ten for a spell of the race's own schools, zero for one it should not get.</returns>
    public int WeightForRace(MapRace race) => Type switch
    {
        SpellType.HolyWord or SpellType.HolyShout when race == MapRace.Necromancer => 0,
        SpellType.DeathRipple or SpellType.DeathWave when race != MapRace.Necromancer => 0,
        SpellType.SummonEElement or SpellType.SummonAElement or SpellType.SummonFElement or SpellType.SummonWElement => 0,
        SpellType.TownPortal or SpellType.Visions or SpellType.Haunt => 0,
        SpellType.SetEGuardian or SpellType.SetAGuardian or SpellType.SetFGuardian or SpellType.SetWGuardian => 0,
        _ => 10,
    };

    /// <summary>
    /// The spells that may be put into a mage guild of that level,
    /// <c>getAllSpellIdsSuitableForSpellBook()</c> (<c>spell.cpp:532-560</c>).
    /// </summary>
    /// <remarks>
    /// The reference filters by <c>Level() &gt;= 1</c> alone: the placeholders (<see cref="SpellType.Random"/>
    /// and its five level slots) and <see cref="SpellType.Petrify"/> are level zero, so they fall out by
    /// themselves, while <see cref="SpellType.ViewMines"/> and <see cref="SpellType.ViewResources"/> are level
    /// one and <b>are</b> in the list — a guild may teach them. The port used to keep an extra hand-written
    /// exclusion list that left ViewMines out; it is gone.
    /// </remarks>
    /// <param name="level">Spell level to keep, negative for all five.</param>
    /// <param name="exclude">Spells to leave out, the <c>excludeSpells</c> set of the reference.</param>
    /// <returns>The ids, in table order; empty for a level that no spell has.</returns>
    public static IReadOnlyList<SpellType> SuitableForSpellBook(int level = -1, IReadOnlySet<SpellType>? exclude = null)
    {
        if (level < -1 || level == 0 || level > 5)
        {
            return [];
        }

        List<SpellType> result = [];

        for (int id = 1; id < (int)SpellType.Count; id++)
        {
            Spell spell = new(id);

            if (spell.Level() < 1 || (level != -1 && spell.Level() != level))
            {
                continue;
            }

            if (exclude is not null && exclude.Contains(spell.Type))
            {
                continue;
            }

            result.Add(spell.Type);
        }

        return result;
    }

    /// <summary>
    /// How many spells a mage guild of that level may draw from, <c>getTotalSpellsPerLevel()</c>
    /// (<c>spell.cpp:563-575</c>).
    /// </summary>
    /// <param name="level">The spell level, one to five.</param>
    /// <returns>The number of spells of that level; zero for any other level.</returns>
    public static uint TotalSpellsPerLevel(int level) => (uint)SuitableForSpellBook(level).Count;

    /// <summary>
    /// A random spell of that level, <c>Spell::getRandomSpell()</c> (<c>spell.cpp:578-620</c>).
    /// </summary>
    /// <remarks>
    /// Uniform over the spells of the level, the placeholders and the built-in spells excluded. The reference
    /// asserts that the level has spells; the port throws instead, because an empty draw is a programming
    /// error and the assert would only fire in debug builds.
    /// </remarks>
    /// <param name="level">The spell level, one to five.</param>
    /// <returns>A spell of that level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level has no spells.</exception>
    public static Spell RandomSpell(int level)
    {
        IReadOnlyList<SpellType> spells = SuitableForSpellBook(level);

        if (spells.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "No spell of this level exists.");
        }

        return new Spell(Rand.Get(spells));
    }
}
