using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.System;

/// <summary>
/// The way a player travels inside a save file, <c>operator&lt;&lt;</c> and
/// <c>operator&gt;&gt;( IStreamBase &amp;, Player &amp; )</c> (<c>players.cpp:244-281</c>).
/// </summary>
/// <remarks>
/// <para>
/// The order is the reference's: the <c>BitModes</c> word, the controller, the colour, the race, the friends, the name,
/// the camera, the AI personality and the handicap. Every integer is written with the width of the reference's field,
/// not of the port's: the race is an <c>int32_t</c> even though <see cref="MapRace"/> is a byte here, and the colour and
/// the friends are bytes even though the old formats wrote them as <c>int32_t</c>. Getting that wrong would shift every
/// following field by three bytes.
/// </para>
/// <para>
/// The two version gates are the reference's: before 1.0.09 the word held a second field that was read and dropped, and
/// before 1.1.09 the colour and the friends were <c>int32_t</c>. They are checked against the version passed in rather
/// than against <c>GameSave.VersionOfCurrentSaveFile</c>, so that the caller decides which file it is reading.
/// </para>
/// <para>
/// The AI auto-control pair is deliberately <b>not</b> saved: the reference marks it as "should not be saved anywhere"
/// (<c>players.h:229-234</c>), because it describes who is moving <i>right now</i> and a loaded game always starts with
/// nobody having handed their turn over.
/// </para>
/// </remarks>
public sealed partial class Player
{
    /// <summary>
    /// Writes the player, <c>operator&lt;&lt;( OStreamBase &amp;, const Player &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(Modes);
        stream.Write((int)Control);
        stream.Write((byte)Color);
        stream.Write((int)Race);
        stream.Write((byte)Friends);
        stream.Write(CustomName);
        Focus.Write(stream);
        stream.Write((int)Personality);
        stream.Write((byte)Handicap);
    }

    /// <summary>
    /// Reads the player, <c>operator&gt;&gt;( IStreamBase &amp;, Player &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="saveFileVersion">The version in the file's header, already parsed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 位集是整体覆盖而不是叠加：读到的是文件里的值，不是「再打开几个开关」。<see cref="BitModes"/> 只有置位与清位，
    /// 所以先清全 1 再置入。
    /// </remarks>
    public void Read(StreamBase stream, SaveFileFormat saveFileVersion)
    {
        ArgumentNullException.ThrowIfNull(stream);

        uint modes = stream.ReadUInt32();

        ResetModes(uint.MaxValue);
        SetModes(modes);

        if (saveFileVersion < SaveFileFormat.Release1_0_09)
        {
            // players.cpp:258-262：1.0.09 之前这里还有一个整型字段，读完就丢。
            _ = stream.ReadUInt32();
        }

        Control = (PlayerControl)stream.ReadInt32();

        if (saveFileVersion < SaveFileFormat.Release1_1_09)
        {
            // players.cpp:266-275：老格式把颜色和盟友写成 int32_t。
            int color = stream.ReadInt32();
            int race = stream.ReadInt32();
            int friends = stream.ReadInt32();

            Color = (PlayerColor)unchecked((byte)color);
            Race = (MapRace)unchecked((byte)race);
            Friends = (PlayerColor)unchecked((byte)friends);
        }
        else
        {
            Color = (PlayerColor)stream.ReadUInt8();
            Race = (MapRace)unchecked((byte)stream.ReadInt32());
            Friends = (PlayerColor)stream.ReadUInt8();
        }

        CustomName = stream.ReadString();
        Focus.Read(stream);
        Personality = (AiPersonality)stream.ReadInt32();
        Handicap = (HandicapStatus)stream.ReadUInt8();
    }
}
