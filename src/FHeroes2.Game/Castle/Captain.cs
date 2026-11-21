using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

using ArmyTroops = FHeroes2.Game.Army.Army;
using IArmyCommander = FHeroes2.Game.Army.IArmyCommander;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The captain of a town's garrison, <c>Captain</c> (<c>castle/captain.h</c>, <c>castle/captain.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// A captain is a <c>HeroBase</c> in the reference and nothing else: no secondary skills, no artifacts, no
/// experience. Its four primary skills come from the race's <see cref="FactionProperties.CaptainPrimarySkills"/>
/// — <c>1/1/1/1</c> for the knight and the barbarian, <c>0/0/2/2</c> for the four spellcasting races — and its army
/// is the castle's, the same object, not a copy.
/// </para>
/// <para>
/// <c>GetPortrait()</c> lives in the UI, not here: the big portrait is <c>PORT0090</c>~<c>PORT0095</c> with the
/// color flag glued on, the small one is a <c>MINICAPT</c> frame (<c>FHeroes2.UI.Dialogs.CaptainPortrait</c>).
/// </para>
/// <para>
/// The captain is <b>not</b> a copy of the hero standing in the town: <c>Castle::GetActualArmy()</c> picks the hero
/// when there is one, and the captain only fights when there is nobody home (ADR-134).
/// </para>
/// </remarks>
public sealed class Captain : ISpellBookOwner, IArmyCommander
{
    /// <summary>
    /// The ceiling on spell power, <c>CAPTAIN_MAX_POWER</c> (<c>captain.cpp:32</c>) — the same 255 a hero's power is
    /// clamped to.
    /// </summary>
    public const int MaxPower = 255;

    private readonly SpellStorage _spellBook = new();

    private readonly ArtifactBag _bagArtifacts = new();

    private Castle? _home;

    private int _attack;

    private int _defense;

    private int _power;

    private int _knowledge;

    private uint _spellPoints;

    /// <summary>Creates a captain with no home, the default constructor of the reference.</summary>
    public Captain()
    {
    }

    /// <summary>Creates the captain of a town, <c>Captain( const Castle &amp; )</c>.</summary>
    /// <param name="castle">The town the captain defends.</param>
    public Captain(Castle castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        LoadDefaults(castle);
    }

    /// <summary>The town the captain defends, <c>home</c>.</summary>
    public Castle? Home => _home;

    /// <summary>Whether the captain can fight, <c>isValid()</c>.</summary>
    public bool IsValid => _home is not null && _home.IsBuild(BuildingType.Captain);

    /// <summary>The colour of the town, <c>GetColor()</c>.</summary>
    public PlayerColor Color => _home?.Color ?? PlayerColor.None;

    /// <summary>The race of the town, <c>GetRace()</c>.</summary>
    public MapRace Race => _home?.Race ?? MapRace.None;

    /// <summary>The name of the town, <c>GetName()</c>.</summary>
    public string Name => _home?.Name ?? string.Empty;

    /// <summary>The town's army, <c>GetArmy()</c>.</summary>
    public ArmyTroops Army => _home?.Army ?? new ArmyTroops();

    /// <summary>The captain's attack, <c>GetAttack()</c> (<c>captain.cpp:127</c>).</summary>
    /// <remarks>
    /// The reference adds <c>GetAttackModificator( nullptr )</c>, which is <c>HeroBase</c>'s castle term plus the
    /// artifacts'. A captain owns no artifacts, so only the town can move the value — and none of the six races has
    /// an attack modifier, so the value is currently always <c>_attack</c>.
    /// </remarks>
    public int GetAttack() => _attack + (_home?.GetAttackModificator() ?? 0);

    /// <summary>The captain's defence, <c>GetDefense()</c> (<c>captain.cpp:132</c>); see <see cref="GetAttack"/>.</summary>
    public int GetDefense() => _defense + (_home?.GetDefenseModificator() ?? 0);

    /// <summary>
    /// The captain's spell power, <c>GetPower()</c> (<c>captain.cpp:137</c>).
    /// </summary>
    /// <remarks>The one primary skill the reference clamps: <c>std::clamp( _power + modificator, 1, 255 )</c>.</remarks>
    public int GetPower() => Math.Clamp(_power + (_home?.GetPowerModificator() ?? 0), 1, MaxPower);

    /// <summary>The captain's knowledge, <c>GetKnowledge()</c> (<c>captain.cpp:142</c>); see <see cref="GetAttack"/>.</summary>
    public int GetKnowledge() => _knowledge + (_home?.GetKnowledgeModificator() ?? 0);

    /// <summary>
    /// The captain's morale, <c>GetMorale()</c> (<c>captain.cpp:147</c>).
    /// </summary>
    /// <remarks>
    /// The reference normalizes <c>GetMoraleModificator( nullptr )</c>, the <c>HeroBase</c> version: the town's
    /// morale modifier plus the artifacts' (the captain's bag is always empty). Unlike the army's, this sum has no
    /// race or undead terms — a captain is not troubled by the troops it commands.
    /// </remarks>
    public int GetMorale() => Morale.Normalize(Morale.Normal + (_home?.GetMoraleModificator() ?? 0));

    /// <summary>The captain's luck, <c>GetLuck()</c> (<c>captain.cpp:152</c>); see <see cref="GetMorale"/>.</summary>
    public int GetLuck() => Luck.Normalize(Luck.Normal + (_home?.GetLuckModificator() ?? 0));

    /// <summary>The captain's spell points, <c>GetSpellPoints()</c>.</summary>
    public uint SpellPoints => _spellPoints;

    /// <summary>Ten per point of knowledge, <c>GetMaxSpellPoints()</c>.</summary>
    public uint GetMaxSpellPoints() => 10 * (uint)GetKnowledge();

    /// <inheritdoc />
    public bool CanCastSpell(Spell spell) => HasSpellBook && spell.SpellPoints(BagArtifacts) <= _spellPoints;

    /// <inheritdoc />
    public SpellStorage MagicBook => _spellBook;

    /// <inheritdoc />
    public bool HasSpellBook => _spellBook.Count > 0 || _knowledge > 0;

    /// <inheritdoc />
    /// <remarks>
    /// A captain carries no artifacts in the reference either — <c>HeroBase</c> gives it a bag that nothing ever
    /// fills. The army reads the bag for the "Arm of the Martyr" curse, which therefore never applies to a
    /// garrison.
    /// </remarks>
    public ArtifactBag BagArtifacts => _bagArtifacts;

    /// <inheritdoc />
    public Castle? InCastle => _home;

    /// <inheritdoc />
    /// <remarks>A captain has no secondary skills: <c>HeroBase::GetSecondarySkillValue</c> answers 0.</remarks>
    public uint GetSecondarySkillValue(SecondarySkill skill) => 0;

    /// <inheritdoc />
    /// <remarks>A captain knows no skills, so its diplomacy discount is always zero.</remarks>
    public SkillLevel GetLevelSkill(SecondarySkill skill) => SkillLevel.None;

    /// <inheritdoc />
    /// <remarks>
    /// <c>Captain::GetControl()</c> asks its castle (<c>captain.cpp:180-183</c>), and a castle answers
    /// <c>Players::GetPlayerControl</c> unless it is neutral — a neutral one is always AI. A battle-only captain is
    /// never neutral, so the two answers coincide.
    /// </remarks>
    public PlayerControl GetControl() => _home is null ? PlayerControl.Ai : Players.GetPlayerControl(_home.Color);

    /// <inheritdoc />
    /// <remarks>The captain belongs to the town it defends, <c>HeroBase::GetColor()</c> (<c>captain.cpp:88</c>).</remarks>
    public PlayerColor GetColor() => Color;

    /// <inheritdoc />
    /// <remarks>
    /// <c>HeroBase::GetMagicStrategicValue()</c> (<c>heroes_base.cpp:340-400</c>): with no spell points, nothing;
    /// otherwise the best combat spell the book holds, given the army's strength, the available points and the
    /// captain's spell power. A captain's book holds at most the race's initial spell.
    /// </remarks>
    public double GetMagicStrategicValue(double armyStrength)
    {
        if (_spellPoints == 0)
        {
            return 0.0;
        }

        double bestValue = 0.0;
        int spellPower = GetPower();

        foreach (Spell spell in _spellBook)
        {
            if (spell.IsCombat())
            {
                bestValue = Math.Max(bestValue, spell.GetStrategicValue(armyStrength, _spellPoints, spellPower));
            }
        }

        return bestValue;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>HeroBase::isHeroes()</c> is <c>false</c>; only <c>Heroes</c> overrides it. The distinction matters to
    /// <c>Army::GetStrength()</c>, which adds a hero's army bonuses but not a captain's.
    /// </remarks>
    public bool IsHeroes => false;

    /// <summary>
    /// Fills the captain from its town, <c>HeroBase::LoadDefaults( CAPTAIN, race )</c>
    /// (<c>castle.cpp:501</c>, <c>heroes_base.cpp:60-96</c>).
    /// </summary>
    /// <remarks>
    /// Called twice in the reference's life of a town: once in <c>_postLoad</c>, and again when the captain's
    /// quarters are built. The book is emptied first, so a captain never keeps an old initial spell.
    /// </remarks>
    /// <param name="castle">The town the captain defends.</param>
    public void LoadDefaults(Castle castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        _home = castle;

        FactionProperties? faction = Factions.Get(castle.Race);

        if (faction is null)
        {
            _attack = 1;
            _defense = 1;
            _power = 1;
            _knowledge = 1;
        }
        else
        {
            _attack = (int)faction.CaptainPrimarySkills.Attack;
            _defense = (int)faction.CaptainPrimarySkills.Defense;
            _power = (int)faction.CaptainPrimarySkills.Power;
            _knowledge = (int)faction.CaptainPrimarySkills.Knowledge;
        }

        _spellBook.Clear();
        _spellPoints = 0;

        if (faction?.InitialSpellId is > 0)
        {
            AppendSpellToBook(new Spell((SpellType)faction.InitialSpellId), true);
        }

        SetSpellPoints((uint)GetMaxSpellPoints());
    }

    /// <summary>Sets the spell points, <c>SetSpellPoints()</c>.</summary>
    /// <param name="points">The points to set.</param>
    public void SetSpellPoints(uint points) => _spellPoints = points;

    /// <summary>Restores the spell points between battles, <c>ActionAfterBattle()</c>.</summary>
    public void RestoreSpellPoints() => _spellPoints = (uint)GetMaxSpellPoints();

    /// <inheritdoc />
    public bool CanLearnSpell(Spell spell) => spell.Level() <= 2;

    /// <inheritdoc />
    public bool AppendSpellToBook(Spell spell, bool ignoreWisdom = false)
    {
        if (spell.Type == SpellType.None || (!ignoreWisdom && !CanLearnSpell(spell)))
        {
            return false;
        }

        int before = _spellBook.Count;
        _spellBook.Append(spell);

        return _spellBook.Count != before;
    }
}
