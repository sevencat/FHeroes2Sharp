using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Army;

/// <summary>
/// Reading and writing an army, <c>operator&lt;&lt;( OStreamBase &amp;, const Army &amp; )</c> and
/// <c>operator&gt;&gt;( IStreamBase &amp;, Army &amp; )</c> (<c>army.cpp:1977-2032</c>).
/// </summary>
/// <remarks>
/// <para>
/// A count, the stacks, the formation flag and the colour. The count is written even though it can be derived from
/// the slot array: it is what lets a reader notice that the save file and the build disagree about how many slots
/// an army has, which is the difference between an old save being read wrongly and being refused.
/// </para>
/// <para>
/// <b>The colour changed width.</b> Before 1.1.09 the colour was written as a 4 byte <c>int32_t</c>; from that
/// release on it is one byte, because <c>PlayerColor</c> became an <c>uint8_t</c> enum. A reader therefore needs to
/// know which format it is looking at — <see cref="ReadFrom"/> takes the version rather than asking a global — and
/// the branch cannot be dropped until
/// <see cref="SaveFileFormatInfo.LastSupported"/> passes <see cref="SaveFileFormat.Release1_1_09"/>.
/// </para>
/// <para>
/// <b>The commander is dropped on purpose.</b> The reference sets <c>army.commander = nullptr</c> with the comment
/// that the owner (the hero or the castle) sets it later; an army does not own its commander and must not keep a
/// stale one from the previous game. The port stores a nullable <see cref="IArmyCommander"/> for the same reason and
/// clears it the same way.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>How many bytes an army takes in a save file.</summary>
    /// <remarks>The count, five stacks, the formation flag and the one byte colour.</remarks>
    public const int SerializedSize = sizeof(uint) + (Army.MaximumTroopCount * Troop.SerializedSize) + sizeof(byte) + sizeof(byte);

    /// <summary>
    /// Writes the army, <c>operator&lt;&lt;</c> (<c>army.cpp:1977</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)_slots.Length);

        foreach (Troop troop in _slots)
        {
            troop.WriteTo(stream);
        }

        stream.Write(IsSpreadFormation);
        stream.Write((byte)Color);
    }

    /// <summary>
    /// Reads the army into this object, <c>operator&gt;&gt;</c> (<c>army.cpp:1990</c>).
    /// </summary>
    /// <remarks>
    /// A slot count that disagrees with this build is treated as a corrupt save: the stream is marked failed and the
    /// army is emptied, because the alternative is reading five stacks into seven slots or the reverse and handing
    /// the player an army that never existed. The reference keeps reading the flag and the colour afterwards; the
    /// port stops instead, since nothing downstream may use a failed stream anyway.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The format of the save file being read.</param>
    /// <returns><see langword="true"/> when the army was read whole.</returns>
    public bool ReadFrom(StreamBase stream, SaveFileFormat version)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Fail)
        {
            return false;
        }

        bool ok = true;
        uint size = stream.ReadUInt32();

        if (size != (uint)_slots.Length)
        {
            stream.SetFail();

            foreach (Troop troop in _slots)
            {
                troop.Reset();
            }

            return false;
        }

        foreach (Troop troop in _slots)
        {
            ok &= troop.ReadFrom(stream);
        }

        IsSpreadFormation = stream.ReadBool();

        Color = version < SaveFileFormat.Release1_1_09 ? (PlayerColor)stream.ReadInt32() : (PlayerColor)stream.ReadUInt8();

        // The owner re-attaches itself; see the remarks on the type.
        Commander = null;

        return ok && !stream.Fail;
    }
}
