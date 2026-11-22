using FHeroes2.Core.Io;

namespace FHeroes2.Game.System;

/// <summary>
/// The way the adventure map camera travels inside a save file, <c>operator&lt;&lt;</c> and
/// <c>operator&gt;&gt;( IStreamBase &amp;, Focus &amp; )</c> (<c>players.cpp:205-242</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference pair is <c>&lt;int32_t, void *&gt;</c>, so it stores the kind and then — depending on the kind — the tile
/// index of the hero or of the castle; anything else is written as <c>-1</c>. Reading resolves the index back through
/// the world, which is why the world has to be loaded before the players are: <c>Game::Load</c> reads the world first
/// and the settings second, and the settings own the players.
/// </para>
/// <para>
/// 端口多一层保护：索引找不到对象时只保留 <see cref="Kind"/>，两个引用都留空。参考在这种情况下存的是空指针，
/// 效果相同（<c>GetHeroes()</c>/<c>GetCastle()</c> 都会回空），只是端口不会留下一个「有一半是坏的」焦点。
/// </para>
/// </remarks>
public sealed partial class PlayerFocus
{
    /// <summary>
    /// Writes the camera, <c>operator&lt;&lt;( OStreamBase &amp;, const Focus &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public void Write(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((int)Kind);

        int index = Kind switch
        {
            FocusKind.Heroes => Hero?.MapIndex ?? -1,
            FocusKind.Castle => Castle?.TileIndex ?? -1,
            _ => -1,
        };

        stream.Write(index);
    }

    /// <summary>
    /// Reads the camera, <c>operator&gt;&gt;( IStreamBase &amp;, Focus &amp; )</c>.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考用 <c>Maps::GetPoint( index )</c> 换成坐标再问世界；端口的世界两头都有按瓦片索引的查询
    /// （<c>AllHeroes.GetAtMapIndex</c>、<c>World.GetCastle</c>），所以直接用索引。
    /// </remarks>
    public void Read(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        FocusKind kind = (FocusKind)stream.ReadInt32();
        int index = stream.ReadInt32();

        Hero = null;
        Castle = null;
        Kind = kind;

        if (index < 0)
        {
            return;
        }

        switch (kind)
        {
        case FocusKind.Heroes:
            Hero = World.Current.Heroes.GetAtMapIndex(index);
            break;

        case FocusKind.Castle:
            Castle = World.Current.GetCastle(index);
            break;

        default:
            break;
        }
    }
}
