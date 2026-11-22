using FHeroes2.Core.Geometry;
using FHeroes2.Core.Io;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// Reading and writing the ultimate artifact, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of
/// <c>UltimateArtifact</c> (<c>artifact_ultimate.cpp:58-70</c>).
/// </summary>
/// <remarks>
/// <para>
/// The base <c>Artifact</c> is written first, and it is two <c>int32_t</c>: the id and <c>ext</c>. The port has no
/// <c>Artifact</c> class and no spell field — an artifact is an <see cref="ArtifactType"/> — so <c>ext</c> is written
/// as zero. That is what it holds for every artifact the puzzle can hide: <c>ext</c> is the spell of a spell scroll, and
/// a scroll is never the ultimate artifact (<c>artifact_ultimate.cpp:36-44</c>).
/// </para>
/// <para>
/// The tile index is <c>int32_t</c> and <c>-1</c> before <see cref="Set"/> places one; the "found" flag is a single
/// byte here, unlike the <c>bool</c> of <c>BitModes</c>; the offset is a <c>fheroes2::Point</c>, which is two
/// <c>int32_t</c> (<c>serialize.cpp:520-526</c>).
/// </para>
/// </remarks>
public sealed partial class UltimateArtifact
{
    /// <summary>How many bytes the ultimate artifact takes in a save file.</summary>
    /// <remarks>4 (id) + 4 (ext) + 4 (tile) + 1 (found) + 8 (offset).</remarks>
    public const int SerializedSize = (3 * sizeof(int)) + sizeof(byte) + (2 * sizeof(int));

    /// <summary>
    /// Writes the artifact, <c>operator&lt;&lt;</c> (<c>artifact_ultimate.cpp:58</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((int)Artifact);
        stream.Write(0);
        stream.Write(Position);
        stream.Write(IsFound);
        stream.Write(Offset.X);
        stream.Write(Offset.Y);
    }

    /// <summary>
    /// Reads the artifact, <c>operator&gt;&gt;</c> (<c>artifact_ultimate.cpp:64</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>
    /// The artifact; a stream that ran out leaves the defaults and its <c>Fail</c> flag set.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static UltimateArtifact ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        UltimateArtifact artifact = new();

        artifact.Artifact = (ArtifactType)stream.ReadInt32();

        // ext, the spell of a spell scroll; dumped, because the port keeps the spell in the artifact bag and never
        // reaches this path with a scroll.
        stream.ReadInt32();

        artifact.Position = stream.ReadInt32();
        artifact.IsFound = stream.ReadBool();
        artifact.Offset = new Point(stream.ReadInt32(), stream.ReadInt32());

        return artifact;
    }
}
