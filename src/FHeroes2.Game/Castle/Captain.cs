using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Spells;
using ArmyTroops = FHeroes2.Game.Army.Army;

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
/// Two things are deliberately left for later: <c>GetPortrait()</c>, which needs the <c>MINICAPT</c> icon set the
/// port has no enumeration for, and <c>GetControl()</c>, which needs <c>Players</c>. Neither is reached by
/// anything that exists today — the captain is fought, not drawn.
/// </para>
/// <para>
/// The captain is <b>not</b> a copy of the hero standing in the town: <c>Castle::GetActualArmy()</c> picks the hero
/// when there is one, and the captain only fights when there is nobody home (ADR-134).
/// </para>
/// </remarks>
public sealed class Captain : ISpellBookOwner
{
    private readonly SpellStorage _spellBook = new();

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

    /// <summary>The captain's attack, <c>GetAttack()</c>.</summary>
    public int GetAttack() => _attack;

    /// <summary>The captain's defence, <c>GetDefense()</c>.</summary>
    public int GetDefense() => _defense;

    /// <summary>The captain's spell power, <c>GetPower()</c>.</summary>
    public int GetPower() => _power;

    /// <summary>The captain's knowledge, <c>GetKnowledge()</c>.</summary>
    public int GetKnowledge() => _knowledge;

    /// <summary>The captain's morale, <c>GetMorale()</c>: always normal, it has no modifiers of its own.</summary>
    public int GetMorale() => Morale.Normal;

    /// <summary>The captain's luck, <c>GetLuck()</c>: always normal.</summary>
    public int GetLuck() => Luck.Normal;

    /// <summary>The captain's spell points, <c>GetSpellPoints()</c>.</summary>
    public uint SpellPoints => _spellPoints;

    /// <summary>Ten per point of knowledge, <c>GetMaxSpellPoints()</c>.</summary>
    public int GetMaxSpellPoints() => 10 * _knowledge;

    /// <inheritdoc />
    public SpellStorage MagicBook => _spellBook;

    /// <inheritdoc />
    public bool HasSpellBook => _spellBook.Count > 0 || _knowledge > 0;

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
