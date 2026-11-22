using FHeroes2.Core.Io;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Reading and writing the secondary skills of a hero, the <c>std::vector&lt;Skill::Secondary&gt;</c> operators
/// <c>SecSkills</c> inherits (<c>skill.h:120-135</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>Skill::SecSkills</c> is a <c>std::vector&lt;Secondary&gt;</c> and <c>Secondary</c> is a
/// <c>std::pair&lt;int32_t, int32_t&gt;</c> — the skill and its level — so the file holds a <c>uint32_t</c> count and
/// two <c>int32_t</c> per entry.
/// </para>
/// <para>
/// <b>Empty slots are written too.</b> A hero that filled a level-up offers an "empty" entry (skill zero) the same way
/// the reference's vector holds it, and the count is what says how many entries there are; skipping them would make the
/// count disagree with the list.
/// </para>
/// <para>
/// Reading <b>replaces</b> the list rather than merging it: the reference's vector operator clears the vector first, and
/// a merge would turn a corrupted file into a hero with more than eight skills.
/// </para>
/// </remarks>
public sealed partial class SecSkills
{
    /// <summary>Writes the skills, empty slots included.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_skills.Count);

        foreach (SecondarySkillLevel skill in _skills)
        {
            stream.Write((int)skill.Skill);
            stream.Write((int)skill.Level);
        }
    }

    /// <summary>Reads the skills, replacing what the hero knew.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint count = stream.ReadUInt32();

        _skills.Clear();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            var skill = (SecondarySkill)stream.ReadInt32();
            var level = (SkillLevel)stream.ReadInt32();

            _skills.Add(new SecondarySkillLevel(skill, level));
        }
    }
}
