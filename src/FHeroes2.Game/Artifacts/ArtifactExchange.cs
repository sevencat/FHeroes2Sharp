using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// Hands the artifacts of two heroes between them when one meets the other,
/// <c>BagArtifacts::exchangeArtifacts()</c> (<c>artifact.cpp:891-1038</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference has this on the bag because a hero meet is a bag operation: the two bags are pooled, some
/// artifacts are given away on rules that look at the heroes' skills and spells, and the rest is dealt out by value
/// — the hero who does the exchanging (the taker) keeps the best, and keeps a free slot for a spell book if it has
/// none. The port keeps the same order of decisions, in a separate class so that <see cref="ArtifactBag"/> does not
/// have to know about heroes.
/// </para>
/// <para>
/// The rule that needs the most context is the last one: the taker gets the best artifacts but only up to
/// <c>MaxCapacity - 1</c> of them, because a hero without a spell book has to be able to pick one up from the bag
/// afterwards. When the taker's bag still has room after that, the leftover is turned back into gold on the spot
/// (the same slot the reference returns to the taker) and only then does the giver take what is left.
/// </para>
/// <para>
/// Spell scrolls carry no spell in the port, so <c>getSpellId()</c> answers <see cref="SpellType.None"/> for all of
/// them: a scroll is never "already in the book", and two scrolls are indistinguishable. Both consequences follow
/// the reference's code for a scroll with no spell, which is what a blank scroll is.
/// </para>
/// </remarks>
public static class ArtifactExchange
{
    /// <summary>
    /// Pools the artifacts of the two heroes and deals them out,
    /// <c>BagArtifacts::exchangeArtifacts( taker, giver )</c>.
    /// </summary>
    /// <remarks>
    /// The magic book never moves: it stays with the hero that carries it, which is what makes the spell book a
    /// slot of its own in the bag. Everything else is pooled, including the artifacts both heroes already have.
    /// </remarks>
    /// <param name="taker">The hero who does the exchanging; it ends up with the best artifacts.</param>
    /// <param name="giver">The hero giving artifacts away; it gets whatever the taker did not take.</param>
    public static void Exchange(Hero taker, Hero giver)
    {
        ArtifactBag takerBag = taker.BagArtifacts;
        ArtifactBag giverBag = giver.BagArtifacts;
        var combined = new List<ArtifactType>(ArtifactBag.MaxCapacity * 2);

        Pool(takerBag, combined);
        Pool(giverBag, combined);

        // Artifacts with nothing but curses never do the taker any good, so they go back first.
        Transfer(combined, giverBag, static artifact =>
        {
            ArtifactData data = ArtifactTable.Get(artifact);
            return data.Curses.Count > 0 && data.Bonuses.Count == 0;
        });

        // A necromancer has a use for necromancy artifacts that a hero without the skill does not.
        if (!taker.HasSecondarySkill(SecondarySkill.Necromancy) && giver.HasSecondarySkill(SecondarySkill.Necromancy))
        {
            Transfer(combined, giverBag, static artifact =>
            {
                ArtifactData data = ArtifactTable.Get(artifact);

                if (data.Bonuses.Count == 0)
                {
                    return false;
                }

                foreach (ArtifactBonus bonus in data.Bonuses)
                {
                    if (bonus.Type != ArtifactBonusType.NecromancySkill)
                    {
                        return false;
                    }
                }

                return true;
            });
        }

        // A scroll of a spell the taker already knows is useless to it.
        if (taker.HasSpellBook)
        {
            Transfer(combined, giverBag, artifact =>
            {
                ArtifactData data = ArtifactTable.Get(artifact);

                if (data.Bonuses.Count == 0)
                {
                    return false;
                }

                SpellType spell = SpellType.None;

                foreach (ArtifactBonus bonus in data.Bonuses)
                {
                    if (bonus.Type != ArtifactBonusType.AddSpell)
                    {
                        return false;
                    }

                    spell = bonus.Spell;
                }

                return spell != SpellType.None && taker.MagicBook.IsPresent(new Spell(spell));
            });
        }

        // Copies of a unique artifact are useless, so every copy after the first goes to the giver.
        for (int index = 0; index < combined.Count; ++index)
        {
            if (!IsUniqueArtifact(combined[index]))
            {
                continue;
            }

            for (int other = index + 1; other < combined.Count;)
            {
                if (combined[other] != combined[index]
                    || (combined[index] == ArtifactType.SpellScroll && GetScrollSpell(combined[other]) != GetScrollSpell(combined[index])))
                {
                    ++other;
                    continue;
                }

                if (!giverBag.Push(combined[other]))
                {
                    // The bag is full; nothing more can be given away.
                    break;
                }

                combined.RemoveAt(other);
            }
        }

        // Worst first, so that the best are at the end of the list and the taker can take from there.
        combined.Sort(static (left, right) => ArtifactValues.Value(left).CompareTo(ArtifactValues.Value(right)));

        for (int slot = 1; slot < ArtifactBag.MaxCapacity; ++slot)
        {
            if (combined.Count == 0 || !takerBag.Push(combined[^1]))
            {
                break;
            }

            combined.RemoveAt(combined.Count - 1);
        }

        // When the giver has fewer holes than there are artifacts left, the taker must have failed to leave room
        // for its spell book; the extra artifact then goes back to the taker rather than being lost.
        int emptySlots = 0;

        foreach (ArtifactType slot in giverBag.Slots)
        {
            if (!ArtifactTypes.IsValid(slot))
            {
                ++emptySlots;
            }
        }

        if (combined.Count > emptySlots && takerBag.Push(combined[^1]))
        {
            combined.RemoveAt(combined.Count - 1);
        }

        while (combined.Count > 0 && giverBag.Push(combined[^1]))
        {
            combined.RemoveAt(combined.Count - 1);
        }

        // The parts of a set that ended up in one bag may now complete it.
        takerBag.AssembleArtifactSets();
        giverBag.AssembleArtifactSets();
    }

    /// <summary>
    /// Moves the transferable artifacts of the bag into the pool, <c>for</c> loop at <c>artifact.cpp:898-908</c>.
    /// </summary>
    /// <remarks>
    /// The magic book is skipped and stays where it is; everything else is taken out of the slot it occupied, which
    /// is the reference's <c>artifact.Reset()</c>.
    /// </remarks>
    /// <param name="bag">The bag to empty.</param>
    /// <param name="pool">The pool to fill.</param>
    private static void Pool(ArtifactBag bag, List<ArtifactType> pool)
    {
        foreach (ArtifactType artifact in bag.Slots)
        {
            if (!ArtifactTypes.IsValid(artifact) || artifact == ArtifactType.MagicBook)
            {
                continue;
            }

            pool.Add(artifact);
            bag.Remove(artifact);
        }
    }

    /// <summary>
    /// Gives the pool's matching artifacts to the bag, <c>transferArtifactsByCondition()</c>
    /// (<c>artifact.cpp:75-90</c>).
    /// </summary>
    /// <remarks>
    /// Stops at the first artifact that does not fit, and leaves the rest of the pool alone — including the
    /// artifacts that matched but came after the one that did not fit, which is what the reference's early
    /// <c>return</c> does.
    /// </remarks>
    /// <param name="pool">The artifacts to consider.</param>
    /// <param name="bag">The bag to give them to.</param>
    /// <param name="condition">Which artifacts the bag wants.</param>
    private static void Transfer(List<ArtifactType> pool, ArtifactBag bag, Func<ArtifactType, bool> condition)
    {
        for (int index = 0; index < pool.Count;)
        {
            if (condition(pool[index]))
            {
                if (!bag.Push(pool[index]))
                {
                    return;
                }

                pool.RemoveAt(index);
                continue;
            }

            ++index;
        }
    }

    /// <summary>
    /// Whether the artifact has no curses and no cumulative bonus, <c>isUniqueArtifact</c> lambda
    /// (<c>artifact.cpp:963-976</c>).
    /// </summary>
    /// <param name="artifact">The artifact to test.</param>
    /// <returns>Whether the artifact is unique in the reference's sense.</returns>
    private static bool IsUniqueArtifact(ArtifactType artifact)
    {
        ArtifactData data = ArtifactTable.Get(artifact);

        if (data.Curses.Count > 0)
        {
            return false;
        }

        foreach (ArtifactBonus bonus in data.Bonuses)
        {
            if (ArtifactBonuses.IsCumulative(bonus.Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The spell of a scroll, <c>Artifact::getSpellId()</c> for the scroll case.</summary>
    /// <param name="artifact">The artifact.</param>
    /// <returns>The spell, <see cref="SpellType.None"/> for a blank scroll or an artifact that is not one.</returns>
    private static SpellType GetScrollSpell(ArtifactType artifact) =>
        ArtifactTable.Get(artifact).TryGetBonus(ArtifactBonusType.AddSpell, out ArtifactBonus row)
            ? row.Spell
            : SpellType.None;
}
