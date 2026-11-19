using FHeroes2.Core.Io;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Army;

/// <summary>
/// Reading and writing a stack, <c>operator&lt;&lt;( OStreamBase &amp;, const Troop &amp; )</c> and
/// <c>operator&gt;&gt;( IStreamBase &amp;, Troop &amp; )</c> (<c>army_troop.cpp:211-219</c>).
/// </summary>
/// <remarks>
/// <para>
/// Eight bytes: the creature id as an <c>int32_t</c> and the count as a <c>uint32_t</c>. The reference's operators
/// are two lines each because they write the object's fields directly — <c>stream &lt;&lt; troop.id &lt;&lt;
/// troop._count</c> — and this port does the same through the two settable properties, so a save and its load stay
/// as symmetric as the format is.
/// </para>
/// <para>
/// <b>A corrupt id is not an error.</b> A save file can name a creature that does not exist; the reference reads it
/// and lets <c>isValid()</c> answer false afterwards. The port keeps that, because a stack of garbage is an empty
/// slot everywhere that matters and refusing the whole save over it would lose the player's game.
/// </para>
/// </remarks>
public sealed partial class Troop
{
    /// <summary>How many bytes a stack takes in a save file, <c>sizeof( int32_t ) + sizeof( uint32_t )</c>.</summary>
    public const int SerializedSize = sizeof(int) + sizeof(uint);

    /// <summary>
    /// Writes the stack, <c>operator&lt;&lt;</c> (<c>army_troop.cpp:211</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((int)Monster);
        stream.Write(Count);
    }

    /// <summary>
    /// Reads the stack into this object, <c>operator&gt;&gt;</c> (<c>army_troop.cpp:216</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>
    /// <see langword="false"/> when the stream had already failed or ran dry, in which case the stack is left empty
    /// rather than half read.
    /// </returns>
    public bool ReadFrom(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Fail)
        {
            return false;
        }

        int monster = stream.ReadInt32();
        uint count = stream.ReadUInt32();

        if (stream.Fail)
        {
            Monster = MonsterType.Unknown;
            Count = 0;

            return false;
        }

        Monster = (MonsterType)monster;
        Count = count;

        return true;
    }
}
