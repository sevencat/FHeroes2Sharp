using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// Reading and writing one kingdom, <c>operator&lt;&lt;</c> and <c>operator&gt;&gt;</c> of <c>Kingdom</c>
/// (<c>kingdom.cpp:905-946</c>).
/// </summary>
/// <remarks>
/// <para>
/// Thirteen fields in a fixed order: modes, colour, treasury, days without a town, castles, heroes, the two tavern
/// slots, the visit list, the puzzle map, the visited tents, the two "top in the kingdom view" pointers and the monsters
/// under vision.
/// </para>
/// <para>
/// <c>VecCastles</c> and <c>VecHeroes</c> are pointers in the reference, but a save file writes <b>tile indexes and hero
/// ids</b>, not pointers (<c>castle.cpp:2462-2498</c>, <c>heroes.cpp:2493-2558</c>). Reading is therefore a lookup
/// through the world, and a missing object is corruption in the reference too — it fails the stream and skips the entry.
/// </para>
/// <para>
/// Three gates: the colour is an <c>int32_t</c> before 1.1.09; a word of nothing sits before the two view pointers
/// before 1.1.00 pre-release 2; and the monsters-under-vision set only exists from 1.1.18 on, an older save leaves it
/// empty (<c>kingdom.cpp:915-943</c>).
/// </para>
/// </remarks>
public sealed partial class Kingdom
{
    /// <summary>
    /// Writes the kingdom (<c>kingdom.cpp:905</c>).
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    internal void WriteTo(StreamBase stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Write((uint)(int)_modes);
        stream.Write((byte)_color);
        _funds.WriteTo(stream);
        stream.Write((uint)_lostTownDays);

        stream.Write((uint)_castles.Count);

        foreach (CastleTown castle in _castles)
        {
            stream.Write(castle.TileIndex);
        }

        stream.Write((uint)_heroes.Count);

        foreach (Hero hero in _heroes)
        {
            stream.Write(hero.Id);
        }

        _recruits.WriteTo(stream);

        stream.Write((uint)_visitedObjects.Count);

        foreach (IndexObject visited in _visitedObjects)
        {
            stream.Write(visited.Index);
            stream.Write((ushort)visited.Object);
        }

        _puzzleMaps.WriteTo(stream);
        stream.Write(_visitedTentsColors);
        stream.Write(TopCastleInKingdomView);
        stream.Write(TopHeroInKingdomView);

        stream.Write((uint)_monstersUnderVision.Count);

        // std::set 是有序的，写出去也必须是升序，否则同样的内容会变成不同的字节。
        foreach (int index in _monstersUnderVision.Order())
        {
            stream.Write(index);
        }
    }

    /// <summary>
    /// Reads the kingdom (<c>kingdom.cpp:912</c>).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="version">The version of the save file being read.</param>
    /// <param name="world">The world the castles and the heroes are looked up in.</param>
    /// <returns>
    /// The kingdom; a stream that failed on the way keeps its <c>Fail</c> flag set and the list it was reading stops
    /// growing.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="world"/> is
    /// <see langword="null"/>.</exception>
    internal static Kingdom ReadFrom(StreamBase stream, SaveFileFormat version, World world)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(world);

        Kingdom kingdom = new();

        kingdom._modes = (KingdomModes)(int)stream.ReadUInt32();

        // The reference reads an int32 here before 1.1.09 even though PlayerColor is a uint8_t (kingdom.cpp:916-923).
        kingdom._color = version < SaveFileFormat.Release1_1_09
            ? (PlayerColor)stream.ReadInt32()
            : (PlayerColor)stream.ReadUInt8();

        kingdom._funds = Funds.ReadFrom(stream);
        kingdom._lostTownDays = (int)stream.ReadUInt32();

        uint castleCount = stream.ReadUInt32();

        for (uint i = 0; i < castleCount && !stream.Fail; ++i)
        {
            CastleTown? castle = world.GetCastleEntrance(stream.ReadInt32());

            if (castle is null)
            {
                // Most likely the save file is corrupted (castle.cpp:2487).
                stream.SetFail();
                continue;
            }

            kingdom._castles.Add(castle);
        }

        uint heroCount = stream.ReadUInt32();

        for (uint i = 0; i < heroCount && !stream.Fail; ++i)
        {
            int heroId = stream.ReadInt32();
            Hero? hero;

            if (version < SaveFileFormat.Release1_0_10)
            {
                // UNKNOWN was 72 before 1.0.10 and must not appear in a kingdom (heroes.cpp:2517-2535).
                if (heroId == 72 || !Hero.IsValidId(heroId + 1))
                {
                    stream.SetFail();
                    continue;
                }

                hero = world.GetHero(heroId + 1);
            }
            else
            {
                if (!Hero.IsValidId(heroId))
                {
                    stream.SetFail();
                    continue;
                }

                hero = world.GetHero(heroId);
            }

            if (hero is null)
            {
                // Most likely the save file is corrupted (heroes.cpp:2546).
                stream.SetFail();
                continue;
            }

            kingdom._heroes.Add(hero);
        }

        kingdom._recruits.ReadFrom(stream, version);

        uint visitedCount = stream.ReadUInt32();

        for (uint i = 0; i < visitedCount && !stream.Fail; ++i)
        {
            int index = stream.ReadInt32();

            // IndexObject's type is a word, but it was a full int32 before 1.1.03 pre-release 1 (pairs.h:72-90).
            Mp2ObjectType objectType = version < SaveFileFormat.PreRelease1_1_1_03
                ? (Mp2ObjectType)stream.ReadInt32()
                : (Mp2ObjectType)stream.ReadUInt16();

            kingdom._visitedObjects.Add(new IndexObject(index, objectType));
        }

        kingdom._puzzleMaps.ReadFrom(stream);
        kingdom._visitedTentsColors = stream.ReadInt32();

        if (version < SaveFileFormat.PreRelease2_1_1_00)
        {
            // A word of nothing (kingdom.cpp:928-933).
            stream.ReadInt32();
        }

        kingdom.TopCastleInKingdomView = stream.ReadInt32();
        kingdom.TopHeroInKingdomView = stream.ReadInt32();

        if (version < SaveFileFormat.Release1_1_18)
        {
            kingdom._monstersUnderVision.Clear();
        }
        else
        {
            uint count = stream.ReadUInt32();

            for (uint i = 0; i < count && !stream.Fail; ++i)
            {
                kingdom._monstersUnderVision.Add(stream.ReadInt32());
            }
        }

        return kingdom;
    }
}
