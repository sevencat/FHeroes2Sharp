using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game;

/// <summary>
/// Writing and reading the end-of-scenario verdict, <c>operator&lt;&lt;( OStreamBase &amp;, const GameOver::Result &amp; )</c>
/// and <c>operator&gt;&gt;( IStreamBase &amp;, GameOver::Result &amp; )</c> (<c>game_over.cpp:684-702</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考写五个字节：颜色集合（<c>PlayerColorsSet</c> 是 <c>uint8_t</c>）加四字节的条件掩码。读的时候要分版本：
/// 1.1.09 之前这个字段还是 <c>int32_t</c>，老存档里是四字节，读错版本会让后面整份存档错位——与
/// <c>Army</c> 的颜色字段是同一处收窄，所以 <see cref="ReadFrom"/> 同样收版本而不是去问全局。
/// </para>
/// <para>
/// <see cref="GameOverRequests"/> 不入档：它们是「这一次调用还没兑现的界面动作」，重新读档后由下一帧的
/// <see cref="CheckGameOver"/> 重新算出来。
/// </para>
/// </remarks>
public sealed partial class GameOverResult
{
    /// <summary>How many bytes the verdict takes in a save file written today, <c>1 + 4</c>.</summary>
    /// <remarks>1.1.09 之前的存档里是 8 字节，见 <see cref="ReadFrom"/>。</remarks>
    public const int SerializedSize = sizeof(byte) + sizeof(uint);

    /// <summary>
    /// Writes the verdict, <c>operator&lt;&lt;</c> (<c>game_over.cpp:684</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((byte)_colors);
        stream.Write(Condition);
    }

    /// <summary>
    /// Reads the verdict into this object, <c>operator&gt;&gt;</c> (<c>game_over.cpp:689</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The format of the save file being read.</param>
    /// <returns>
    /// <see langword="false"/> when the stream had already failed or ran dry, in which case the verdict is reset to
    /// <see cref="GameOver.ConditionNone"/> with no colors rather than left half read.
    /// </returns>
    public bool ReadFrom(StreamBase stream, SaveFileFormat version)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Fail)
        {
            return false;
        }

        // 参考 game_over.cpp:691-699：1.1.09 之前颜色集合是 int32_t，之后才收窄成 uint8_t。
        _colors = version < SaveFileFormat.Release1_1_09
            ? (PlayerColor)stream.ReadInt32()
            : (PlayerColor)stream.ReadUInt8();
        Condition = stream.ReadUInt32();

        if (stream.Fail)
        {
            _colors = PlayerColor.None;
            Condition = GameOver.ConditionNone;

            return false;
        }

        Requests.Clear();

        return true;
    }
}
