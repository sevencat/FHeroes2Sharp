using FHeroes2.Core.Random;

namespace FHeroes2.Core.Tests;

public sealed class Pcg32Tests
{
    [Fact]
    public void SameSeedAndStream_ProduceIdenticalSequences()
    {
        Pcg32 first = new(seed: 12345, stream: 6789);
        Pcg32 second = new(seed: 12345, stream: 6789);

        for (int i = 0; i < 5000; ++i)
        {
            Assert.Equal(first.NextUInt32(), second.NextUInt32());
        }
    }

    [Fact]
    public void DifferentStreams_ProduceDifferentSequences()
    {
        // PCG32 forces the stream to be odd (stream | 1), so 54 and 55 would be identical.
        Pcg32 first = new(seed: 42, stream: 54);
        Pcg32 second = new(seed: 42, stream: 56);

        bool differenceFound = false;
        for (int i = 0; i < 64 && !differenceFound; ++i)
        {
            differenceFound = first.NextUInt32() != second.NextUInt32();
        }

        Assert.True(differenceFound);
    }

    [Fact]
    public void DefaultConstructor_UsesReferenceSeedAndStream()
    {
        Pcg32 defaults = new();
        Pcg32 explicitValues = new(Pcg32.DefaultSeed, Pcg32.DefaultStream);

        Assert.Equal(explicitValues.NextUInt32(), defaults.NextUInt32());
        Assert.Equal(54UL, defaults.Stream);
    }

    [Fact]
    public void NextInclusive_StaysInsideBounds()
    {
        Pcg32 generator = new(seed: 7);

        for (int i = 0; i < 100_000; ++i)
        {
            uint value = generator.NextInclusive(10u, 20u);
            Assert.InRange(value, 10u, 20u);
        }
    }

    [Fact]
    public void NextInclusive_CoversWholeRange()
    {
        Pcg32 generator = new(seed: 99);
        HashSet<uint> seen = [];

        for (int i = 0; i < 100_000; ++i)
        {
            seen.Add(generator.NextInclusive(0u, 99u));
        }

        Assert.Equal(100, seen.Count);
    }

    [Fact]
    public void NextInclusive_SwapsReversedRange()
    {
        Pcg32 forward = new(seed: 3);
        Pcg32 reversed = new(seed: 3);

        for (int i = 0; i < 100; ++i)
        {
            Assert.Equal(forward.NextInclusive(1u, 6u), reversed.NextInclusive(6u, 1u));
        }
    }

    [Fact]
    public void Shuffle_IsDeterministicAndKeepsAllElements()
    {
        int[] first = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        int[] second = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        new Pcg32(seed: 2026).Shuffle(first);
        new Pcg32(seed: 2026).Shuffle(second);

        Assert.Equal(first, second);
        Assert.Equal(10, first.Distinct().Count());
        Assert.NotEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], first);
    }

    [Fact]
    public void Chance_ZeroIsNeverAndHundredIsAlways()
    {
        Pcg32 generator = new(seed: 11);

        for (int i = 0; i < 1000; ++i)
        {
            Assert.False(generator.Chance(0u));
            Assert.True(generator.Chance(100u));
        }
    }
}
