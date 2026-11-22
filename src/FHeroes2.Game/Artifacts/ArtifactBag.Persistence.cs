using FHeroes2.Core.Io;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// Reading and writing an artifact bag, the <c>std::vector&lt;Artifact&gt;</c> operators <c>BagArtifacts</c> inherits
/// (<c>artifact.h:296-312</c>, <c>artifact.cpp:505-512</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference's bag is a <c>std::vector&lt;Artifact&gt;</c> that its constructor sizes to fourteen, so the file holds a
/// count (fourteen) and that many artifacts, each an <c>int32_t</c> id plus a <c>uint32_t</c> <c>ext</c>.
/// </para>
/// <para>
/// <b>The count is checked, the ids are not.</b> The port's bag is a fixed array, so a count that is not fourteen cannot
/// be laid out and fails the stream — the reference would resize its vector and then index fourteen slots regardless.
/// An id outside the table is kept as it is, like the reference keeps it: <c>Artifact( const int32_t )</c> does not
/// filter either, and the artifact tables are asked about it later.
/// </para>
/// <para>
/// The <c>ext</c> word is the reference's random-artifact extension (quality and bonuses). The port has no random
/// artifacts yet, so it is written as zero and discarded on reading.
/// </para>
/// </remarks>
public sealed partial class ArtifactBag
{
    /// <summary>Writes the fourteen slots, empty ones included.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)MaxCapacity);

        foreach (ArtifactType artifact in _slots)
        {
            stream.Write((int)artifact);
            stream.Write(0u);
        }
    }

    /// <summary>Reads the fourteen slots, replacing what the bag held.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint count = stream.ReadUInt32();

        if (count != MaxCapacity)
        {
            // A bag of another width cannot be laid out on top of a fixed fourteen (artifact.cpp:517).
            stream.SetFail();
            return;
        }

        for (int i = 0; i < MaxCapacity && !stream.Fail; ++i)
        {
            _slots[i] = (ArtifactType)stream.ReadInt32();

            // The random-artifact extension; the port has no random artifacts.
            _ = stream.ReadUInt32();
        }
    }
}
