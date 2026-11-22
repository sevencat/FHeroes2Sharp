using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Reading and writing a sphinx, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>MapSphinx</c>
/// (<c>maps_objects.cpp:378-396</c>).
/// </summary>
/// <remarks>
/// <para>
/// The position and the UID, the resources, the artifact, the answers, the riddle, the "worth asking" flag and the
/// "four characters only" flag. The answers are a container: a <c>uint32_t</c> count followed by that many strings
/// (<c>serialize.h</c>), and the riddle is a single string.
/// </para>
/// <para>
/// The truncation flag is the last field and it is what 1.1.00 changed: before that release every answer was compared
/// on four characters, so a reader of an older save sets the flag to <see langword="true"/> instead of reading it
/// (<c>maps_objects.cpp:387-393</c>).
/// </para>
/// </remarks>
public sealed partial class MapSphinx
{
    /// <summary>
    /// Writes the sphinx, <c>operator&lt;&lt;</c> (<c>maps_objects.cpp:378</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="geometry">The map, to turn the tile index into a point.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        WritePosition(stream, geometry);
        Resources.WriteTo(stream);
        stream.Write((int)Artifact);
        stream.Write(0);
        stream.WriteList(Answers, static (target, answer) => target.Write(answer));
        stream.Write(Riddle);
        stream.Write(IsValid);
        stream.Write(IsTruncatedAnswer);
    }

    /// <summary>
    /// Reads a sphinx, <c>operator&gt;&gt;</c> (<c>maps_objects.cpp:383</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map, to turn the point into a tile index.</param>
    /// <returns>The sphinx; a stream that failed on the way leaves the defaults and its <c>Fail</c> flag set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal static MapSphinx ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        (int tileIndex, uint uid) = ReadPosition(stream, version, geometry);

        Funds resources = Funds.ReadFrom(stream);
        ArtifactType artifact = (ArtifactType)stream.ReadInt32();

        // ext, the spell of a spell scroll; dumped, like the ultimate artifact does.
        stream.ReadInt32();

        List<string> answers = stream.ReadList(static source => source.ReadString());
        string riddle = stream.ReadString();
        bool isValid = stream.ReadBool();
        bool isTruncatedAnswer = version < SaveFileFormat.Release1_1_00 || stream.ReadBool();

        MapSphinx sphinx = new()
        {
            Resources = resources,
            Artifact = artifact,
            Answers = answers,
            Riddle = riddle,
            IsValid = isValid,
            IsTruncatedAnswer = isTruncatedAnswer,
        };

        sphinx.PlaceOn(tileIndex, uid);

        return sphinx;
    }
}
