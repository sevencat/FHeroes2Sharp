using FHeroes2.Core.Random;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Covers the <c>Rand</c> namespace of <c>src/engine/rand.h</c> / <c>rand.cpp</c>:
/// <c>Get</c> (<c>:93</c>), <c>GetWithSeed</c> (<c>:102</c>), <c>GetWithGen</c> (<c>:112</c>), the vector
/// overload (<c>rand.h:196</c>), the shuffle wrappers (<c>rand.h:184</c>) and <c>Rand::Queue</c>
/// (<c>rand.h:224</c> / <c>rand.cpp:121</c>).
/// </summary>
public sealed class RandTests
{
    [Fact]
    public void Get_StaysInsideBoundsAndTreatsTheDefaultAsUpperBound()
    {
        for (int i = 0; i < 1000; ++i)
        {
            // Get( 6 ) is Get( 6, 0 ), and the reversed bounds are swapped, so the range is [0, 6].
            Assert.InRange(Rand.Get(6u), 0u, 6u);
            Assert.InRange(Rand.Get(1u, 6u), 1u, 6u);
            Assert.InRange(Rand.Get(6u, 1u), 1u, 6u);
            Assert.InRange(Rand.Get(0, 99), 0, 99);
        }

        Assert.Equal(5u, Rand.Get(5u, 5u));
        Assert.Equal(5, Rand.Get(5, 5));
    }

    [Fact]
    public void Get_RejectsNegativeBounds()
    {
        // The reference takes uint32_t, so a negative bound would silently become a huge one. The signed
        // overload exists for ergonomics and refuses them instead.
        Assert.Throws<ArgumentOutOfRangeException>(() => Rand.Get(-1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rand.Get(0, -5));
    }

    [Fact]
    public void GetWithSeed_IsReproducibleAndMatchesADedicatedGenerator()
    {
        // The reference builds PCG32( seed ) with the default stream for the call and throws it away
        // (rand.cpp:108), so the same arguments always give the same value.
        for (uint seed = 0; seed < 50; ++seed)
        {
            uint value = Rand.GetWithSeed(10u, 20u, seed);

            Assert.Equal(value, Rand.GetWithSeed(10u, 20u, seed));
            Assert.Equal(new Pcg32(seed).NextInclusive(10u, 20u), value);
            Assert.InRange(value, 10u, 20u);
        }
    }

    [Fact]
    public void GetWithGen_DrawsFromTheGivenGenerator()
    {
        Pcg32 plain = new(seed: 4711);
        Pcg32 withGen = new(seed: 4711);

        for (int i = 0; i < 100; ++i)
        {
            Assert.Equal(plain.NextInclusive(0u, 9u), Rand.GetWithGen(0u, 9u, withGen));
        }
    }

    [Fact]
    public void GetOfList_PicksAnElementAndRejectsAnEmptyList()
    {
        int[] values = [4, 8, 15, 16];

        for (int i = 0; i < 1000; ++i)
        {
            Assert.Contains(Rand.Get(values), values);
        }

        Assert.Throws<ArgumentException>(() => Rand.Get(Array.Empty<int>()));
        Assert.Throws<ArgumentNullException>(() => Rand.Get<int>(null!));

        // The element is picked with the generator of the current thread, so two calls can differ; the
        // seeded form is the reproducible one and it is the same arithmetic (rand.h:205).
        Pcg32 generator = new(seed: 1);
        Assert.Equal(values[(int)generator.NextInclusive(0, 3)], Rand.GetWithGen(values, new Pcg32(seed: 1)));
    }

    [Fact]
    public void Shuffle_KeepsEveryElementAndIsReproducible()
    {
        int[] first = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        int[] second = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        Rand.Shuffle(first);
        Rand.ShuffleWithGen(second, new Pcg32(seed: 2026));

        Assert.Equal(10, first.Distinct().Count());
        Assert.Equal(10, second.Distinct().Count());

        int[] third = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Rand.ShuffleWithGen(third, new Pcg32(seed: 2026));
        Assert.Equal(second, third);

        Assert.Throws<ArgumentNullException>(() => Rand.Shuffle<int>(null!));
    }

    [Fact]
    public void Queue_OfNothingIsZeroAndZeroWeightsAreDropped()
    {
        Rand.Queue empty = new();

        Assert.Equal(0, empty.Size);
        Assert.Equal(0, empty.Get());
        Assert.Equal(0, empty.GetWithSeed(3));

        empty.Push(17, 0);
        Assert.Equal(0, empty.Size);

        empty.Push(23, 5);
        Assert.Equal(1, empty.Size);
        Assert.Equal(23, empty.Get());
        Assert.Equal(23, empty.GetWithSeed(9));
    }

    [Fact]
    public void Queue_OnlyEverReturnsPushedValuesRespectingTheWeights()
    {
        Rand.Queue queue = new();
        queue.Push(1, 3);
        queue.Push(2, 0);
        queue.Push(3, 1);

        HashSet<int> seen = [];

        for (int i = 0; i < 2000; ++i)
        {
            seen.Add(queue.Get());
        }

        Assert.Equal(new[] { 1, 3 }, seen.Order());
    }

    [Fact]
    public void QueueWeights_ShiftTheDistribution()
    {
        // A 9:1 split must hand out the heavy value far more often. The exact counts are random, so the
        // test only checks that the heavy value dominates, which a weight-blind implementation cannot fake.
        Rand.Queue queue = new();
        queue.Push(10, 9);
        queue.Push(20, 1);

        int heavy = 0;
        int light = 0;

        for (int i = 0; i < 5000; ++i)
        {
            if (queue.Get() == 10)
            {
                ++heavy;
            }
            else
            {
                ++light;
            }
        }

        Assert.True(heavy > light * 3, $"heavy={heavy} light={light}");
    }

    [Fact]
    public void CurrentThreadRandomDevice_IsStableWithinAThread()
    {
        // thread_local PCG32 gen( rd ) (rand.cpp:87): one generator per thread, created on first use.
        Pcg32 device = Rand.CurrentThreadRandomDevice;

        Assert.Same(device, Rand.CurrentThreadRandomDevice);
        Assert.InRange(Rand.Get(1000u), 0u, 1000u);
    }
}
