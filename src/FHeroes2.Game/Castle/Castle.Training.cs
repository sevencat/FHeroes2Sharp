using FHeroes2.Core.Random;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The mage guild's teaching duty: <c>castle.h:233-236</c> (<c>trainHeroInMageGuild</c>) and
/// <c>castle.cpp:597-611</c> (<c>_trainGuestHeroAndCaptainInMageGuild</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two callers, one rule: whoever is in the town when the guild exists learns what it teaches. The reference's
/// <c>HeroBase &amp;</c> is both the visiting hero and the town's captain, which is why the port's
/// <see cref="ISpellBookOwner"/> is the parameter type — the guild itself only needs a spell book.
/// </para>
/// <para>
/// The level handed to the guild is the town's, not the hero's: a level three guild teaches first to third level
/// spells to a first level hero and to a level twenty hero alike, and the wizard's library adds one more spell per
/// level on top (<see cref="IsLibraryBuilt"/>).
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>
    /// Teaches one spell book owner whatever the guild knows, <c>Castle::trainHeroInMageGuild()</c>
    /// (<c>castle.h:233-236</c>).
    /// </summary>
    /// <param name="hero">The hero or captain to teach.</param>
    public void TrainHeroInMageGuild(ISpellBookOwner hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        _mageGuild.TrainHero(hero, GetLevelMageGuild(), IsLibraryBuilt);
    }

    /// <summary>
    /// Teaches the visiting hero and the captain, <c>Castle::_trainGuestHeroAndCaptainInMageGuild()</c>
    /// (<c>castle.cpp:597-611</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A town without a guild teaches nothing, and that is the early return: the reference checks
    /// <c>GetLevelMageGuild() &lt; 1</c> before it looks for anybody, so building the guild is what makes the
    /// captain learn his first spells.
    /// </para>
    /// <para>
    /// The guest hero is a parameter rather than a lookup because the reference's <c>world.GetHero( *this )</c>
    /// asks the map which hero stands on the town's tile, and the port's <c>World</c> has no such query yet.
    /// Whoever knows the map passes the hero; a <see langword="null"/> means the town is empty, which is also what
    /// the reference sees when nobody is home. <see cref="PostLoad"/> passes <see langword="null"/> for the same
    /// reason: a town is loaded before the heroes are placed.
    /// </para>
    /// </remarks>
    /// <param name="guestHero">The hero in the town, or <see langword="null"/> for none.</param>
    public void TrainGuestHeroAndCaptainInMageGuild(Hero? guestHero = null)
    {
        if (GetLevelMageGuild() < 1)
        {
            return;
        }

        if (guestHero is not null)
        {
            TrainHeroInMageGuild(guestHero);
        }

        if (_captain.IsValid)
        {
            TrainHeroInMageGuild(_captain);
        }
    }

    /// <summary>
    /// Rolls the town's spell storage, <c>_mageGuild.initialize( _race, HaveLibraryCapability() )</c>
    /// (<c>castle.cpp:392</c>, <c>castle.cpp:423</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called by <see cref="PostLoad"/> once the race is final — a random town may have been given its buildings
    /// before its race, so the guild cannot be rolled until after that repair. The library flag is
    /// <see cref="HasLibraryCapability"/> (the race), <b>not</b> <see cref="IsLibraryBuilt"/> (the race and the
    /// building): the reference rolls the extra slot for a wizard town whatever it has built so far, and the
    /// <c>hasLibrary</c> argument of <see cref="MageGuild.GetSpells"/> keeps the bonus hidden until the library is
    /// actually there.
    /// </para>
    /// <para>
    /// Public because a town built by hand — a test, a scenario editor — has no map block to roll it from. The
    /// generator is the same seam the rest of the port uses: <see langword="null"/> means the thread's own device.
    /// </para>
    /// </remarks>
    /// <param name="generator">The generator of the spell rolls; <see langword="null"/> for the thread's device.</param>
    public void InitializeMageGuild(Pcg32? generator = null) => _mageGuild.Initialize(Race, HasLibraryCapability, generator);
}
