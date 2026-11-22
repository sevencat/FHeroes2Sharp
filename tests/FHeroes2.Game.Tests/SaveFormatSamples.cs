using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Hand-written save blocks of older formats, for the tests that have to prove a reader stays aligned on a file an older
/// build wrote.
/// </summary>
/// <remarks>
/// <see cref="SaveFileInfo.Write"/> always emits the current format, so it cannot produce the older blocks the version
/// gates exist for. This helper writes the same fields as the reference's <c>Maps::operator&lt;&lt;</c> but stops where the
/// given version stopped: <c>mainLanguage</c> arrived in 1.1.03 and <c>creatorNotes</c> in 1.1.15.
/// </remarks>
internal static class SaveFormatSamples
{
    /// <summary>Writes a map information block as the given version wrote it, <c>maps_fileinfo.cpp:664-680</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="info">The map information.</param>
    /// <param name="version">The version whose field set to use.</param>
    public static void WriteMapInfo(StreamBase stream, Mp2FileInfo info, SaveFileFormat version)
    {
        stream.Write(info.FilePath);
        stream.Write(info.Name);
        stream.Write(info.Description);
        stream.Write((ushort)info.Width);
        stream.Write((ushort)info.Height);
        stream.Write((byte)info.Difficulty);
        stream.Write((byte)SaveFileInfo.MaxNumberOfPlayers);

        for (int player = 0; player < SaveFileInfo.MaxNumberOfPlayers; ++player)
        {
            stream.Write((byte)info.PlayerRaces[player]);
            stream.Write((byte)info.PlayerUnions[player]);
        }

        stream.Write((byte)info.KingdomColors);
        stream.Write((byte)info.HumanPlayers);
        stream.Write((byte)info.ComputerPlayers);
        stream.Write((byte)info.RandomRacePlayers);
        stream.Write((byte)info.VictoryCondition);
        stream.Write(info.ComputerAlsoWins);
        stream.Write(info.AllowNormalVictory);
        stream.Write((ushort)info.VictoryConditionParameters[0]);
        stream.Write((ushort)info.VictoryConditionParameters[1]);
        stream.Write((byte)info.LossCondition);
        stream.Write((ushort)info.LossConditionParameters[0]);
        stream.Write((ushort)info.LossConditionParameters[1]);
        stream.Write(info.Timestamp);
        stream.Write(info.StartWithHeroInFirstCastle);
        stream.Write((int)info.Version);
        stream.Write(info.WorldDay);
        stream.Write(info.WorldWeek);
        stream.Write(info.WorldMonth);

        if (version >= SaveFileFormat.Release1_1_03)
        {
            stream.Write(info.MainLanguage);
        }

        if (version >= SaveFileFormat.Release1_1_15)
        {
            stream.Write(info.CreatorNotes);
        }
    }
}
