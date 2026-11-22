using FHeroes2.Core.Io;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Reading and writing a hero's route, <c>Route::operator&lt;&lt;</c> and <c>operator&gt;&gt;</c>
/// (<c>route.cpp:380-409</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <c>bool</c> for whether the route is drawn, then a <c>uint32_t</c> count and that many steps of
/// <c>int32_t from</c>, <c>int32_t direction</c> and <c>uint32_t penalty</c>.
/// </para>
/// <para>
/// <b>The landing tile is not stored.</b> The reader rebuilds it with <c>Maps::GetDirectionIndex( from, direction )</c>
/// (<c>route.cpp:393</c>), which is why <see cref="RouteStep.Index"/> is computed here instead of read.
/// </para>
/// <para>
/// Before 1.0.7 the file opened with a word of nothing (<c>route.cpp:401-406</c>).
/// </para>
/// </remarks>
public sealed partial class HeroPath
{
    /// <summary>Writes the route, <c>route.cpp:385</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write(!_hide);
        stream.Write((uint)_steps.Count);

        foreach (RouteStep step in _steps)
        {
            stream.Write(step.From);
            stream.Write((int)step.Direction);
            stream.Write(step.Penalty);
        }
    }

    /// <summary>Reads the route, <c>route.cpp:397</c>.</summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="geometry">The map geometry, which turns "from plus direction" into a tile index.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void ReadFrom(StreamBase stream, SaveFileFormat version, MapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (version < SaveFileFormat.Release1_0_07)
        {
            // A word of nothing (route.cpp:401-406).
            stream.ReadInt32();
        }

        _hide = !stream.ReadBool();

        uint count = stream.ReadUInt32();

        _steps.Clear();

        for (uint i = 0; i < count && !stream.Fail; ++i)
        {
            int from = stream.ReadInt32();
            var direction = (Direction)stream.ReadInt32();
            uint penalty = stream.ReadUInt32();

            // The landing tile is rebuilt, not stored (route.cpp:393).
            _steps.Add(new RouteStep(geometry.GetDirectionIndex(from, direction), from, direction, penalty));
        }
    }
}
