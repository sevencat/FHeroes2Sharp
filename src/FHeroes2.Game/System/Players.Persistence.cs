using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.System;

/// <summary>
/// The way the player slots travel inside a save file, <c>operator&lt;&lt;</c> and
/// <c>operator&gt;&gt;( IStreamBase &amp;, Players &amp; )</c> (<c>players.cpp:551-599</c>).
/// </summary>
/// <remarks>
/// <para>
/// The colour set comes first and the current colour second; the players follow in colour order, without a count,
/// because the set already says how many there are. A reader therefore has to build the slots from the set before it can
/// read them — which is why <see cref="Read"/> goes through <see cref="Init(PlayerColor)"/> instead of appending.
/// </para>
/// <para>
/// 1.1.09 之前颜色集合与当前颜色都是 <c>int32_t</c>，之后是 <c>uint8_t</c>；参考的两处 <c>static_assert</c> 就是在钉这件事。
/// </para>
/// </remarks>
public sealed partial class Players
{
    /// <summary>
    /// Writes the slots, <c>operator&lt;&lt;( OStreamBase &amp;, const Players &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((byte)GetColors());
        stream.Write((byte)CurrentColor);

        foreach (Player player in players)
        {
            player.Write(stream);
        }
    }

    /// <summary>
    /// Reads the slots, <c>operator&gt;&gt;( IStreamBase &amp;, Players &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="saveFileVersion">The version in the file's header, already parsed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void Read(StreamBase stream, SaveFileFormat saveFileVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);

        PlayerColor colors;
        PlayerColor current;

        if (saveFileVersion < SaveFileFormat.Release1_1_09)
        {
            colors = (PlayerColor)unchecked((byte)stream.ReadInt32());
            current = (PlayerColor)unchecked((byte)stream.ReadInt32());
        }
        else
        {
            colors = (PlayerColor)stream.ReadUInt8();
            current = (PlayerColor)stream.ReadUInt8();
        }

        Init(colors);
        SetCurrentColor(current);

        foreach (Player player in players)
        {
            player.Read(stream, saveFileVersion);
        }
    }
}
