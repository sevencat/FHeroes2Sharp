using System.Text;
using FHeroes2.Core.Io;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Tests of <see cref="Crc32"/>, the port of <c>fheroes2::calculateCRC32</c> (<c>src/engine/tools.cpp:125</c>).
/// </summary>
public sealed class Crc32Tests
{
    [Fact]
    public void MatchesTheZlibCheckValue()
    {
        // The check value the CRC-32 specification (and zlib's crc32_rfc1952 test) publishes.
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));
    }

    [Fact]
    public void EmptyInputIsZero()
    {
        Assert.Equal(0u, Crc32.Compute(default));
        Assert.Equal(0u, Crc32.Compute(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void SplittingTheInputDoesNotChangeTheChecksum()
    {
        byte[] data = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");

        uint whole = Crc32.Compute(data);

        for (int split = 1; split < data.Length; ++split)
        {
            Assert.Equal(whole, Crc32.Compute(data.AsSpan(0, split), data.AsSpan(split)));
        }
    }

    [Fact]
    public void AppendKeepsTheRunningStateConsistent()
    {
        byte[] data = Encoding.ASCII.GetBytes("FONT.ICN translation fingerprint");

        uint running = Crc32.Append(0xFFFFFFFFu, data.AsSpan(0, 10));
        running = Crc32.Append(running, data.AsSpan(10));

        Assert.Equal(Crc32.Compute(data), ~running);
    }

    [Fact]
    public void SingleByteValuesAreTheReferenceTable()
    {
        // Pinned values of zlib's crc32 of one byte, so that a table/polynomial mistake cannot slip through.
        Assert.Equal(0xD202EF8Du, Crc32.Compute([0x00]));
        Assert.Equal(0xA505DF1Bu, Crc32.Compute([0x01]));
        Assert.Equal(0x3C0C8EA1u, Crc32.Compute([0x02]));
        Assert.Equal(0xFF000000u, Crc32.Compute([0xFF]));
    }
}
