using System.Diagnostics;

namespace FHeroes2.Game.Spells;

/// <summary>
/// What a spell is called and what it claims to do, the two text columns of the <c>spells</c> table
/// (<c>spell.cpp:55-150</c>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SpellTable"/> keeps the same rows' numbers and drops these two columns on purpose, so the numbers stay
/// comparable in tests that do not care about text. The words live here, in <see cref="SpellType"/> order, and they
/// are the reference's own English: the port has no translation layer, and <see cref="Spell.GetName"/> is the
/// reference's <c>Spell::GetName()</c> over this table (ADR-137, ADR-171).
/// </para>
/// <para>
/// Two of the names carry a gettext context prefix in the reference — <c>"spell|Slow"</c>, <c>"spell|Blind"</c> — so
/// that a translator can tell the spell from the adjective. gettext strips the prefix before displaying it, and so
/// does this table; what is returned is what a player reads.
/// </para>
/// <para>
/// The reference's spelling wins where it differs from the enum's: the id is <see cref="SpellType.FireBlast"/> and the
/// word is <c>"Fireblast"</c>, the id is <see cref="SpellType.StoneSkin"/> and the word is <c>"Stoneskin"</c>. The six
/// placeholders and <see cref="SpellType.Petrify"/> have words too, because the table is what describes a spell that
/// a corrupt save names — the reference prints them the same way.
/// </para>
/// </remarks>
public static class SpellText
{
    /// <summary>
    /// The name of a spell, <c>Spell::GetName()</c> (<c>spell.cpp:153-156</c>).
    /// </summary>
    /// <param name="spell">The spell id.</param>
    /// <returns>Its name; <c>"Unknown"</c> for <see cref="SpellType.None"/>.</returns>
    public static string GetName(SpellType spell) => Rows[Index(spell)].Name;

    /// <summary>
    /// What a spell does, <c>Spell::GetDescription()</c> (<c>spell.cpp:158-161</c>).
    /// </summary>
    /// <remarks>
    /// The sentence is a template where the reference has one: <c>%{count}</c> is the number the spell's own code
    /// will add — the hit points a Cure restores per point of spell power, the percentage a Blind takes off a
    /// retaliation. Substituting it belongs to whoever shows the text, exactly as in the reference.
    /// </remarks>
    /// <param name="spell">The spell id.</param>
    /// <returns>The description; <c>"Unknown spell."</c> for <see cref="SpellType.None"/>.</returns>
    public static string GetDescription(SpellType spell) => Rows[Index(spell)].Description;

    /// <summary>One row of the table: the two <c>const char *</c> columns of a <c>SpellStats</c>.</summary>
    private readonly record struct Entry(string Name, string Description);

    private static readonly Entry[] Rows =
    [
        new("Unknown", "Unknown spell."), // 0 Unknown
        new("Fireball", "Causes a giant fireball to strike the selected area, damaging all nearby creatures."), // 1 Fireball
        new("Fireblast", "An improved version of fireball, fireblast affects two hexes around the center point of the spell, rather than one."), // 2 Fireblast
        new("Lightning Bolt", "Causes a bolt of electrical energy to strike the selected creature."), // 3 Lightning Bolt
        new("Chain Lightning", "Causes a bolt of electrical energy to strike a selected creature, then strike the nearest creature with half damage, then strike the NEXT nearest creature with half again damage, and so on, until it becomes too weak to be harmful. Warning: This spell can hit your own creatures!"), // 4 Chain Lightning
        new("Teleport", "Teleports the creature you select to any open position on the battlefield."), // 5 Teleport
        new("Cure", "Removes all negative spells cast upon one of your units, and restores up to %{count} HP per level of spell power."), // 6 Cure
        new("Mass Cure", "Removes all negative spells cast upon your forces, and restores up to %{count} HP per level of spell power, per creature."), // 7 Mass Cure
        new("Resurrect", "Resurrects creatures from a damaged or dead unit until end of combat."), // 8 Resurrect
        new("Resurrect True", "Resurrects creatures from a damaged or dead unit permanently."), // 9 Resurrect True
        new("Haste", "Increases the speed of any creature by %{count}."), // 10 Haste
        new("Mass Haste", "Increases the speed of all of your creatures by %{count}."), // 11 Mass Haste
        new("Slow", "Slows target to half movement rate."), // 12 spell|Slow
        new("Mass Slow", "Slows all enemies to half movement rate."), // 13 Mass Slow
        new("Blind", "Clouds the affected creatures' eyes, preventing them from moving and reduces the damage when retaliating by %{count} percent."), // 14 spell|Blind
        new("Bless", "Causes the selected creatures to inflict maximum damage."), // 15 Bless
        new("Mass Bless", "Causes all of your units to inflict maximum damage."), // 16 Mass Bless
        new("Stoneskin", "Magically increases the defense skill of the selected creatures."), // 17 Stoneskin
        new("Steelskin", "Increases the defense skill of the targeted creatures. This is an improved version of Stoneskin."), // 18 Steelskin
        new("Curse", "Causes the selected creatures to inflict minimum damage."), // 19 Curse
        new("Mass Curse", "Causes all enemy troops to inflict minimum damage."), // 20 Mass Curse
        new("Holy Word", "Damages all undead in the battle."), // 21 Holy Word
        new("Holy Shout", "Damages all undead in the battle. This is an improved version of Holy Word."), // 22 Holy Shout
        new("Anti-Magic", "Prevents any magic against the selected creatures."), // 23 Anti-Magic
        new("Dispel Magic", "Removes all magic spells from a single target."), // 24 Dispel Magic
        new("Mass Dispel", "Removes all magic spells from all creatures."), // 25 Mass Dispel
        new("Magic Arrow", "Causes a magic arrow to strike the selected target."), // 26 Magic Arrow
        new("Berserker", "Causes a creature to attack its nearest neighbor."), // 27 Berserker
        new("Armageddon", "Holy terror strikes the battlefield, causing severe damage to all creatures."), // 28 Armageddon
        new("Elemental Storm", "Magical elements pour down on the battlefield, damaging all creatures."), // 29 Elemental Storm
        new("Meteor Shower", "A rain of rocks strikes an area of the battlefield, damaging all nearby creatures."), // 30 Meteor Shower
        new("Paralyze", "The targeted creatures are paralyzed, unable to move or retaliate."), // 31 Paralyze
        new("Hypnotize", "Brings a single enemy unit under your control if its hits are less than %{count} times the caster's spell power."), // 32 Hypnotize
        new("Cold Ray", "Drains body heat from a single enemy unit."), // 33 Cold Ray
        new("Cold Ring", "Drains body heat from all units surrounding the center point, but not including the center point."), // 34 Cold Ring
        new("Disrupting Ray", "Reduces the defense rating of an enemy unit by three."), // 35 Disrupting Ray
        new("Death Ripple", "Damages all living (non-undead) units in the battle."), // 36 Death Ripple
        new("Death Wave", "Damages all living (non-undead) units in the battle. This spell is an improved version of Death Ripple."), // 37 Death Wave
        new("Dragon Slayer", "Greatly increases a unit's attack skill vs. Dragons."), // 38 Dragon Slayer
        new("Blood Lust", "Increases a unit's attack skill by 3. The effect lasts for 3 rounds of combat, regardless of spell power."), // 39 Blood Lust
        new("Animate Dead", "Resurrects creatures from a damaged or dead undead unit permanently."), // 40 Animate Dead
        new("Mirror Image", "Creates an illusionary unit that duplicates one of your existing units. This illusionary unit does the same damages as the original, but will vanish if it takes any damage."), // 41 Mirror Image
        new("Shield", "Halves damage received from ranged attacks for a single unit. Does not affect damage received from Turrets or Ballistae."), // 42 Shield
        new("Mass Shield", "Halves damage received from ranged attacks for all of your units. Does not affect damage received from Turrets or Ballistae."), // 43 Mass Shield
        new("Summon Earth Elemental", "Summons Earth Elementals to fight for your army."), // 44 Summon Earth Elemental
        new("Summon Air Elemental", "Summons Air Elementals to fight for your army."), // 45 Summon Air Elemental
        new("Summon Fire Elemental", "Summons Fire Elementals to fight for your army."), // 46 Summon Fire Elemental
        new("Summon Water Elemental", "Summons Water Elementals to fight for your army."), // 47 Summon Water Elemental
        new("Earthquake", "Damages castle walls."), // 48 Earthquake
        new("View Mines", "Causes all mines across the land to become visible."), // 49 View Mines
        new("View Resources", "Causes all resources across the land to become visible."), // 50 View Resources
        new("View Artifacts", "Causes all artifacts across the land to become visible."), // 51 View Artifacts
        new("View Towns", "Causes all towns and castles across the land to become visible."), // 52 View Towns
        new("View Heroes", "Causes all Heroes across the land to become visible."), // 53 View Heroes
        new("View All", "Causes the entire land to become visible."), // 54 View All
        new("Identify Hero", "Allows the caster to view detailed information on enemy Heroes."), // 55 Identify Hero
        new("Summon Boat", "Summons the nearest unoccupied and either friendly or neutral boat to an adjacent shore location. A friendly boat is one which you just built or were the most recent player to occupy. A neutral boat is one which no player has occupied yet."), // 56 Summon Boat
        new("Dimension Door", "Allows the caster to magically transport to a nearby location."), // 57 Dimension Door
        new("Town Gate", "Returns the caster to the nearest town or castle currently owned. This spell cannot be cast if the hero is already in a town or a castle."), // 58 Town Gate
        new("Town Portal", "Returns the hero to the town or castle of choice, provided it is controlled by you."), // 59 Town Portal
        new("Visions", "Visions predicts the likely outcome of an encounter with a neutral army camp."), // 60 Visions
        new("Haunt", "Haunts a mine you control with Ghosts. This mine stops producing resources. (If I can't keep it, nobody will!)"), // 61 Haunt
        new("Set Earth Guardian", "Sets Earth Elementals to guard a mine against enemy armies."), // 62 Set Earth Guardian
        new("Set Air Guardian", "Sets Air Elementals to guard a mine against enemy armies."), // 63 Set Air Guardian
        new("Set Fire Guardian", "Sets Fire Elementals to guard a mine against enemy armies."), // 64 Set Fire Guardian
        new("Set Water Guardian", "Sets Water Elementals to guard a mine against enemy armies."), // 65 Set Water Guardian
        new("Random Spell", "Randomly selected spell of any level."), // 66 Random Spell
        new("Random 1st Level Spell", "Randomly selected 1st level spell."), // 67 Random 1st Level Spell
        new("Random 2nd Level Spell", "Randomly selected 2nd level spell."), // 68 Random 2nd Level Spell
        new("Random 3rd Level Spell", "Randomly selected 3rd level spell."), // 69 Random 3rd Level Spell
        new("Random 4th Level Spell", "Randomly selected 4th level spell."), // 70 Random 4th Level Spell
        new("Random 5th Level Spell", "Randomly selected 5th level spell."), // 71 Random 5th Level Spell
        new("Petrification", "Turns the affected creature into stone. A petrified creature receives half damage from a direct attack."), // 72 Petrification
    ];

    private static int Index(SpellType spell)
    {
        int index = (int)spell;

        Debug.Assert(index >= 0 && index < Rows.Length, "spell id outside the text table");

        return index;
    }
}
