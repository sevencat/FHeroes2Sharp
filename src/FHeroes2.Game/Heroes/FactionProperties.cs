using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The four primary skills of a hero — <c>Skill::PrimarySkillValues</c> (<c>skill_static.h:37-43</c>).
/// </summary>
/// <remarks>
/// The same shape is used for four different things in <see cref="FactionProperties"/>: the primary skills a
/// captain starts with, the ones a hero of the race starts with, and the weights the two level-up pools draw
/// from. The reference uses one struct for all four; so does the port, and the property names say which is
/// which.
/// </remarks>
/// <param name="Attack">Attack.</param>
/// <param name="Defense">Defense.</param>
/// <param name="Power">Power.</param>
/// <param name="Knowledge">Knowledge.</param>
public readonly record struct PrimarySkillValues(int Attack, int Defense, int Power, int Knowledge);

/// <summary>
/// Fourteen numbers, one per <see cref="SecondarySkill"/>, in id order — the shape behind
/// <c>Skill::SecondarySkillValues</c> (<c>skill_static.h:45-61</c>) once its alphabetical fields are laid out
/// by id.
/// </summary>
/// <remarks>
/// <para>
/// The reference gets the by-id lookup from named fields and a giant constructor call; the port gets it from
/// an index and a fourteen-element literal. The trade is deliberate: the literal is readable as a row and
/// testable as a row, while fourteen named properties would have to be written out twice per race and could
/// be filled in the wrong order without anyone noticing.
/// </para>
/// <para>
/// The reshuffle from the reference's field order into id order happens exactly once, in
/// <see cref="Factions"/>, and the comment on every row there names the reference line it came from.
/// </para>
/// </remarks>
public sealed class SecondarySkillTable
{
    private readonly int[] _values;

    private SecondarySkillTable(int[] values) => _values = values;

    /// <summary>
    /// The value of one skill.
    /// </summary>
    /// <param name="skill">One of the fourteen skills.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="skill"/> is not one of the fourteen.</exception>
    public int this[SecondarySkill skill]
    {
        get
        {
            int index = (int)skill - 1;
            if (index < 0 || index >= SecondarySkills.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(skill), skill, "Not one of the fourteen secondary skills.");
            }

            return _values[index];
        }
    }

    /// <summary>
    /// Builds a table from fourteen values in id order.
    /// </summary>
    /// <param name="idOrder">The values, <see cref="SecondarySkill.Pathfinding"/> first.</param>
    /// <returns>The table.</returns>
    /// <exception cref="ArgumentException">Not exactly fourteen values, or a negative one.</exception>
    public static SecondarySkillTable Create(params int[] idOrder)
    {
        ArgumentNullException.ThrowIfNull(idOrder);

        if (idOrder.Length != SecondarySkills.Count)
        {
            throw new ArgumentException($"Expected {SecondarySkills.Count} values, got {idOrder.Length}.", nameof(idOrder));
        }

        if (idOrder.Any(value => value < 0))
        {
            throw new ArgumentException("A table of secondary skills cannot hold a negative value.", nameof(idOrder));
        }

        return new SecondarySkillTable((int[])idOrder.Clone());
    }
}

/// <summary>
/// One row of <c>factionProperties</c> (<c>game_static.cpp:37-89</c>) — what is fixed about a race before any
/// hero of it is created: the primary skills of its captain and of its heroes, the spell a hero starts with,
/// the secondary skills a hero starts with, and the four weight tables the level-up draws from.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the whole of <c>GameStatic</c> that has no dependency on the game systems.</b> The rest of
/// <c>game_static.cpp</c> — the moat penalty, the wall range penalty, the movement point bonus of an oasis,
/// the neutral monster limit — is ported with the systems that call it, or not yet at all.
/// </para>
/// <para>
/// The <c>id</c> field of the reference row is kept as <see cref="Id"/>: it is the only handle the data has in
/// the original game (the tables are looked up by position, never by name), and it makes a mismatch in a test
/// report readable.
/// </para>
/// </remarks>
/// <param name="Id">The reference id, e.g. <c>knight</c>.</param>
/// <param name="CaptainPrimarySkills">The primary skills of the race's castle captain.</param>
/// <param name="HeroInitialPrimarySkills">The primary skills a hero of the race starts with.</param>
/// <param name="InitialSpellId">
/// The spell a hero of the race starts with, as the reference's <c>Spell::</c> id. Zero means none — knights and
/// barbarians start without a spell book. The port has no <c>Spell</c> enum yet, so the number is kept raw and
/// documented; it is not compared against anything until the spell system lands.
/// </param>
/// <param name="HeroInitialSecondarySkills">The level of every skill a hero of the race starts with.</param>
/// <param name="BoundaryBetweenLowAndHighLevels">
/// The level at which the primary-skill level-up pool switches from the low table to the high one. Ten for
/// every race of the original game — the reference keeps it per race anyway.
/// </param>
/// <param name="WeightsOfPrimarySkillsForLowLevels">The primary-skill weights below the boundary.</param>
/// <param name="WeightsOfPrimarySkillsForHighLevels">The primary-skill weights from the boundary on.</param>
/// <param name="WeightsOfSecondarySkills">The secondary-skill weights, used when a level-up offers skills.</param>
public sealed record FactionProperties(
    string Id,
    PrimarySkillValues CaptainPrimarySkills,
    PrimarySkillValues HeroInitialPrimarySkills,
    int InitialSpellId,
    SecondarySkillTable HeroInitialSecondarySkills,
    int BoundaryBetweenLowAndHighLevels,
    PrimarySkillValues WeightsOfPrimarySkillsForLowLevels,
    PrimarySkillValues WeightsOfPrimarySkillsForHighLevels,
    SecondarySkillTable WeightsOfSecondarySkills);

/// <summary>
/// The six rows of <c>factionProperties</c> and the lookup of <c>GameStatic::GetFactionProperties</c>
/// (<c>game_static.cpp:205</c>).
/// </summary>
public static class Factions
{
    /// <summary>
    /// The properties of a playable race.
    /// </summary>
    /// <param name="race">The race.</param>
    /// <returns>
    /// The row, or <see langword="null"/> for <see cref="MapRace.None"/>, <see cref="MapRace.Multiple"/>,
    /// <see cref="MapRace.Random"/> and any value that is not exactly one race — the reference returns
    /// <c>nullptr</c> for all of them, and its callers treat that as "no hero" rather than as an error.
    /// </returns>
    /// <remarks>
    /// The comparison is by equality, not by flag: a random race is <c>Random</c>, never a bit set on top of
    /// a race, and the map format does not allow combinations.
    /// </remarks>
    public static FactionProperties? Get(MapRace race)
    {
        return race switch
        {
            MapRace.Knight => Knight,
            MapRace.Barbarian => Barbarian,
            MapRace.Sorceress => Sorceress,
            MapRace.Warlock => Warlock,
            MapRace.Wizard => Wizard,
            MapRace.Necromancer => Necromancer,
            _ => null,
        };
    }

    /// <summary>
    /// <c>game_static.cpp:37-44</c>. The row whose comment in the reference is the only documentation of
    /// «a knight starts with basic ballistics and basic leadership».
    /// </summary>
    private static readonly FactionProperties Knight = new(
        "knight",
        CaptainPrimarySkills: new PrimarySkillValues(1, 1, 1, 1),
        HeroInitialPrimarySkills: new PrimarySkillValues(2, 2, 1, 1),
        InitialSpellId: 0,
        HeroInitialSecondarySkills: SecondarySkillTable.Create(
            0, // pathfinding
            0, // archery
            0, // logistics
            0, // scouting
            0, // diplomacy
            0, // navigation
            1, // leadership
            0, // wisdom
            0, // mysticism
            0, // luck
            1, // ballistics
            0, // eagle eye
            0, // necromancy
            0), // estates
        BoundaryBetweenLowAndHighLevels: 10,
        WeightsOfPrimarySkillsForLowLevels: new PrimarySkillValues(35, 45, 10, 10),
        WeightsOfPrimarySkillsForHighLevels: new PrimarySkillValues(25, 25, 25, 25),
        WeightsOfSecondarySkills: SecondarySkillTable.Create(3, 2, 3, 2, 3, 2, 5, 2, 1, 1, 4, 1, 0, 3));

    /// <summary><c>game_static.cpp:45-53</c>: advanced pathfinding, the only skill a barbarian starts with.</summary>
    private static readonly FactionProperties Barbarian = new(
        "barbarian",
        CaptainPrimarySkills: new PrimarySkillValues(1, 1, 1, 1),
        HeroInitialPrimarySkills: new PrimarySkillValues(3, 1, 1, 1),
        InitialSpellId: 0,
        HeroInitialSecondarySkills: SecondarySkillTable.Create(2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        BoundaryBetweenLowAndHighLevels: 10,
        WeightsOfPrimarySkillsForLowLevels: new PrimarySkillValues(55, 35, 5, 5),
        WeightsOfPrimarySkillsForHighLevels: new PrimarySkillValues(30, 30, 20, 20),
        WeightsOfSecondarySkills: SecondarySkillTable.Create(4, 3, 3, 4, 2, 3, 3, 1, 1, 2, 3, 1, 0, 2));

    /// <summary><c>game_static.cpp:54-62</c>: advanced navigation and basic wisdom.</summary>
    private static readonly FactionProperties Sorceress = new(
        "sorceress",
        CaptainPrimarySkills: new PrimarySkillValues(0, 0, 2, 2),
        HeroInitialPrimarySkills: new PrimarySkillValues(0, 0, 2, 3),
        InitialSpellId: 15,
        HeroInitialSecondarySkills: SecondarySkillTable.Create(0, 0, 0, 0, 0, 2, 0, 1, 0, 0, 0, 0, 0, 0),
        BoundaryBetweenLowAndHighLevels: 10,
        WeightsOfPrimarySkillsForLowLevels: new PrimarySkillValues(10, 10, 30, 50),
        WeightsOfPrimarySkillsForHighLevels: new PrimarySkillValues(20, 20, 30, 30),
        WeightsOfSecondarySkills: SecondarySkillTable.Create(2, 3, 2, 1, 2, 4, 1, 4, 3, 3, 3, 2, 0, 2));

    /// <summary><c>game_static.cpp:63-71</c>: advanced scouting and basic wisdom.</summary>
    private static readonly FactionProperties Warlock = new(
        "warlock",
        CaptainPrimarySkills: new PrimarySkillValues(0, 0, 2, 2),
        HeroInitialPrimarySkills: new PrimarySkillValues(0, 0, 3, 2),
        InitialSpellId: 19,
        HeroInitialSecondarySkills: SecondarySkillTable.Create(0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0),
        BoundaryBetweenLowAndHighLevels: 10,
        WeightsOfPrimarySkillsForLowLevels: new PrimarySkillValues(10, 10, 50, 30),
        WeightsOfPrimarySkillsForHighLevels: new PrimarySkillValues(20, 20, 30, 30),
        WeightsOfSecondarySkills: SecondarySkillTable.Create(2, 1, 2, 4, 2, 2, 1, 5, 3, 1, 3, 3, 1, 2));

    /// <summary><c>game_static.cpp:72-80</c>: advanced wisdom, the only skill a wizard starts with.</summary>
    private static readonly FactionProperties Wizard = new(
        "wizard",
        CaptainPrimarySkills: new PrimarySkillValues(0, 0, 2, 2),
        HeroInitialPrimarySkills: new PrimarySkillValues(0, 1, 2, 2),
        InitialSpellId: 17,
        HeroInitialSecondarySkills: SecondarySkillTable.Create(0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0),
        BoundaryBetweenLowAndHighLevels: 10,
        WeightsOfPrimarySkillsForLowLevels: new PrimarySkillValues(10, 10, 40, 40),
        WeightsOfPrimarySkillsForHighLevels: new PrimarySkillValues(20, 20, 30, 30),
        WeightsOfSecondarySkills: SecondarySkillTable.Create(2, 1, 2, 2, 2, 2, 2, 5, 4, 2, 3, 3, 0, 2));

    /// <summary><c>game_static.cpp:81-89</c>: basic necromancy and basic wisdom.</summary>
    private static readonly FactionProperties Necromancer = new(
        "necromancer",
        CaptainPrimarySkills: new PrimarySkillValues(0, 0, 2, 2),
        HeroInitialPrimarySkills: new PrimarySkillValues(1, 0, 2, 2),
        InitialSpellId: 10,
        HeroInitialSecondarySkills: SecondarySkillTable.Create(0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0),
        BoundaryBetweenLowAndHighLevels: 10,
        WeightsOfPrimarySkillsForLowLevels: new PrimarySkillValues(15, 15, 35, 35),
        WeightsOfPrimarySkillsForHighLevels: new PrimarySkillValues(25, 25, 25, 25),
        WeightsOfSecondarySkills: SecondarySkillTable.Create(3, 1, 2, 1, 2, 2, 0, 4, 3, 1, 3, 3, 5, 2));
}
