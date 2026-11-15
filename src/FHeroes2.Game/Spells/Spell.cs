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
/// <b>What is ported</b>: the numbers (<see cref="SpellTable"/>), the level, and the predicates the strategic
/// value and the spell book need — <see cref="IsCombat"/>, <see cref="IsDamage"/>, <see cref="IsResurrect"/>,
/// <see cref="IsSummon"/>, <see cref="IsMassActions"/>, <see cref="IsMindInfluence"/> — plus
/// <see cref="GetStrategicValue"/>.
/// </para>
/// <para>
/// <b>What is not</b>: every method that needs the text layer (<c>GetName</c>, <c>GetDescription</c>,
/// <c>getDescription</c>), the targeting predicates the battle engine asks (<c>isSingleTarget</c>,
/// <c>isApplyToFriends</c>, <c>isApplyToEnemies</c>, <c>isApplyToAnyTroops</c>, <c>isEffectDispel</c>,
/// <c>isUndeadOnly</c>, <c>isAliveOnly</c>, <c>isApplyWithoutFocusObject</c>), the random-spell queries
/// (<c>getRandomSpell</c>, <c>getTotalSpellsPerLevel</c>, <c>weightForRace</c>) and the spell point reduction
/// of <c>spellPoints( hero )</c>, which walks
/// <c>BagArtifacts::getTotalArtifactMultipliedPercent</c> — not ported yet.
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
    /// The number the behaviour of the spell reads, <c>Spell::ExtraValue()</c> (<c>spell.cpp:479-482</c>).
    /// </summary>
    /// <returns>The <c>extraValue</c> column of the table.</returns>
    public uint ExtraValue() => SpellTable.Get(Type).ExtraValue;

    /// <summary>
    /// What the spell costs to cast, <c>Spell::spellPoints()</c> (<c>spell.cpp:173-219</c>).
    /// </summary>
    /// <remarks>
    /// The hero parameter of the reference is dropped: it only picks the artifact bonus that lowers the cost
    /// through <c>BagArtifacts::getTotalArtifactMultipliedPercent</c>, which the port does not have yet. So
    /// this is the <c>hero == nullptr</c> branch — the list price.
    /// </remarks>
    /// <returns>The spell points the spell costs.</returns>
    public uint SpellPoints() => SpellTable.Get(Type).SpellPoints;

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
}
