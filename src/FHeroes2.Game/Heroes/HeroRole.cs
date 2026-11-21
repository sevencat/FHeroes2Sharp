namespace FHeroes2.Game.Heroes;

/// <summary>
/// What the AI uses a hero for, <c>Heroes::Role</c> (<c>heroes.h:217-241</c>).
/// </summary>
/// <remarks>
/// <para>
/// The role drives the hero half of the AI and only the AI uses it — a human manages heroes by themselves. The
/// order matters: the reference compares roles to tell the more valuable hero from the less valuable one, so the
/// members must stay in declaration order.
/// </para>
/// <para>
/// Every hero starts as a <see cref="Hunter"/>, which is the only role the port can produce so far: nothing
/// assigns the others until the hero half of the planner is ported (ADR-180).
/// </para>
/// </remarks>
public enum HeroRole
{
    /// <summary>Runs towards the fog of war; what a hero becomes when there is nothing to do or too many of them.</summary>
    Scout,

    /// <summary>Carries troops from a dwelling, a castle or another hero to a fighter or a champion.</summary>
    Courier,

    /// <summary>
    /// The ordinary hero with no specialisation: collects resources, fights weak monsters, claims towns and mines
    /// and expands the visible territory.
    /// </summary>
    Hunter,

    /// <summary>Leans towards fighting: kills monsters and enemy heroes, takes castles and guarded mines.</summary>
    Fighter,

    /// <summary>
    /// The mightiest hero, sent to run over the enemy's territory. A hero that has to be defeated to win is marked
    /// with this role too, and one is promoted to it when it outgrows the others.
    /// </summary>
    Champion,
}
