using System.Buffers.Binary;
using System.Security.Cryptography;

namespace FHeroes2.Core.Random;

/// <summary>
/// The <c>Rand</c> namespace of <c>src/engine/rand.h</c> / <c>rand.cpp</c>: the thread level generator and
/// the helpers built on top of <see cref="Pcg32"/>.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps one generator per thread in
/// <c>CurrentThreadRandomDevice()</c> (<c>rand.cpp:85</c>), seeded from a <c>std::random_device</c> that is
/// constructed once per thread. <see cref="ThreadLocal{T}"/> with a factory is the same thing: the seeding
/// happens on the first use in a thread and never again, so a thread sees one continuous sequence.
/// </para>
/// <para>
/// The seed comes from the operating system (two 32-bit draws combined into the 64-bit seed and stream),
/// so the sequence is <b>not</b> reproducible across runs. Anything that has to be reproducible must use
/// <see cref="GetWithSeed(uint, uint, uint)"/> or a <see cref="Pcg32"/> of its own — the reference makes
/// the same distinction.
/// </para>
/// <para>
/// Unlike <c>Pcg32</c>, this class cannot be compared bit for bit with the reference: the seed is
/// unpredictable in both implementations. What is identical is everything downstream of a seed — the
/// interval mapping (Lemire, <c>rand.cpp:33</c>), the swap of reversed bounds and the shuffle order.
/// </para>
/// </remarks>
public static class Rand
{
    private static readonly ThreadLocal<Pcg32> ThreadGenerator = new(CreateSeededGenerator);

    /// <summary>
    /// The generator of the current thread, <c>CurrentThreadRandomDevice()</c> (<c>rand.cpp:85</c>).
    /// </summary>
    public static Pcg32 CurrentThreadRandomDevice => ThreadGenerator.Value!;

    /// <summary>
    /// An inclusive value in <c>[from, to]</c>, <c>Rand::Get()</c> (<c>rand.cpp:93</c>).
    /// </summary>
    /// <remarks>
    /// Reversed bounds are swapped first (<c>rand.cpp:95</c>), which is what makes <c>Get(6)</c> mean
    /// <c>[0, 6]</c> as the default argument suggests.
    /// </remarks>
    /// <param name="from">One end of the interval.</param>
    /// <param name="to">The other end, <c>0</c> by default.</param>
    /// <returns>The value.</returns>
    public static uint Get(uint from, uint to = 0)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        return CurrentThreadRandomDevice.NextInclusive(from, to);
    }

    /// <summary>
    /// An inclusive value in <c>[from, to]</c> from the generator of the current thread,
    /// <c>Rand::Get()</c> over <see cref="int"/> bounds.
    /// </summary>
    /// <remarks>
    /// The reference has no signed overload because it takes <c>uint32_t</c>, which would silently turn a
    /// negative bound into a huge one. The port refuses such bounds instead.
    /// </remarks>
    /// <param name="from">One end of the interval.</param>
    /// <param name="to">The other end, <c>0</c> by default.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A bound is negative.</exception>
    public static int Get(int from, int to = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfNegative(to);

        return (int)Get((uint)from, (uint)to);
    }

    /// <summary>
    /// An inclusive value in <c>[from, to]</c> from a generator made from <paramref name="seed"/>,
    /// <c>Rand::GetWithSeed()</c> (<c>rand.cpp:102</c>).
    /// </summary>
    /// <remarks>
    /// The generator is created for the call and thrown away (<c>rand.cpp:108</c>), so every call with the
    /// same arguments returns the same value.
    /// </remarks>
    /// <param name="from">One end of the interval.</param>
    /// <param name="to">The other end.</param>
    /// <param name="seed">Seed of the throwaway generator.</param>
    /// <returns>The value.</returns>
    public static uint GetWithSeed(uint from, uint to, uint seed)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        return new Pcg32(seed).NextInclusive(from, to);
    }

    /// <summary>
    /// An inclusive value in <c>[from, to]</c> from the given generator,
    /// <c>Rand::GetWithGen()</c> (<c>rand.cpp:112</c>).
    /// </summary>
    /// <param name="from">One end of the interval.</param>
    /// <param name="to">The other end.</param>
    /// <param name="generator">Generator to draw from.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null"/>.</exception>
    public static uint GetWithGen(uint from, uint to, Pcg32 generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        return generator.NextInclusive(from, to);
    }

    /// <summary>
    /// A random element of <paramref name="values"/>, <c>Rand::Get( const std::vector&lt;T&gt; &amp; )</c>
    /// (<c>rand.h:196</c>).
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="values">The elements to choose from.</param>
    /// <returns>The element, by reference in the reference and by value here.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> is empty.</exception>
    public static T Get<T>(IReadOnlyList<T> values) => GetWithGen(values, CurrentThreadRandomDevice);

    /// <summary>
    /// A random element of <paramref name="values"/> from the given generator,
    /// <c>Rand::GetWithGen()</c> (<c>rand.h:205</c>).
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="values">The elements to choose from.</param>
    /// <param name="generator">Generator to draw from.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="values"/> or <paramref name="generator"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> is empty.</exception>
    public static T GetWithGen<T>(IReadOnlyList<T> values, Pcg32 generator)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(generator);

        if (values.Count == 0)
        {
            throw new ArgumentException("Cannot pick an element of an empty list.", nameof(values));
        }

        return values[(int)generator.NextInclusive(0, (uint)(values.Count - 1))];
    }

    /// <summary>
    /// Shuffles <paramref name="values"/> with the generator of the current thread,
    /// <c>Rand::Shuffle()</c> (<c>rand.h:184</c>).
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="values">The list to shuffle in place.</param>
    public static void Shuffle<T>(IList<T> values) => ShuffleWithGen(values, CurrentThreadRandomDevice);

    /// <summary>
    /// Shuffles <paramref name="values"/> with the given generator, <c>Rand::ShuffleWithGen()</c>
    /// (<c>rand.h:190</c>).
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="values">The list to shuffle in place.</param>
    /// <param name="generator">Generator to draw from.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="values"/> or <paramref name="generator"/> is <see langword="null"/>.
    /// </exception>
    public static void ShuffleWithGen<T>(IList<T> values, Pcg32 generator)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(generator);

        generator.Shuffle(values);
    }

    /// <summary>
    /// A weighted pool of values, <c>Rand::Queue</c> (<c>rand.h:224</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="Push"/> drops zero weights (<c>rand.h:234</c>) and <see cref="Get"/> of an empty pool is
    /// <c>0</c> (<c>rand.cpp:123</c>) — both are relied upon by the callers the reference has, so they are
    /// part of the contract rather than defensive coding. The reference's constructor takes a capacity hint
    /// for <c>reserve()</c>; a <see cref="List{T}"/> needs no such hint, so the port has no argument.
    /// </remarks>
    public sealed class Queue
    {
        private readonly List<(int Value, uint Weight)> _entries = [];

        /// <summary>Number of values in the pool, <c>Queue::Size()</c>.</summary>
        public int Size => _entries.Count;

        /// <summary>
        /// Adds a value with a weight, <c>Queue::Push()</c> (<c>rand.h:232</c>).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="weight">Its weight; zero weights are ignored.</param>
        public void Push(int value, uint weight)
        {
            if (weight == 0)
            {
                return;
            }

            _entries.Add((value, weight));
        }

        /// <summary>
        /// A value picked with the generator of the current thread, <c>Queue::Get()</c>
        /// (<c>rand.h:246</c>).
        /// </summary>
        /// <returns>The value, or <c>0</c> when the pool is empty.</returns>
        public int Get() => Pick(max => Rand.Get(0, max));

        /// <summary>
        /// A value picked with a generator made from <paramref name="seed"/>,
        /// <c>Queue::GetWithSeed()</c> (<c>rand.h:251</c>).
        /// </summary>
        /// <param name="seed">Seed of the throwaway generator.</param>
        /// <returns>The value, or <c>0</c> when the pool is empty.</returns>
        public int GetWithSeed(uint seed) => Pick(max => Rand.GetWithSeed(0, max, seed));

        /// <summary>
        /// Walks the weights with the value in <c>[0, sum - 1]</c>, the body of <c>Queue::Get()</c>
        /// (<c>rand.cpp:121</c>).
        /// </summary>
        /// <param name="randomValue">Draws a value in <c>[0, max]</c>.</param>
        /// <returns>The value whose weight slice the draw landed in.</returns>
        private int Pick(Func<uint, uint> randomValue)
        {
            if (_entries.Count == 0)
            {
                return 0;
            }

            uint sum = 0;
            foreach ((int _, uint weight) in _entries)
            {
                sum += weight;
            }

            uint value = randomValue(sum - 1);

            foreach ((int entryValue, uint weight) in _entries)
            {
                if (value < weight)
                {
                    return entryValue;
                }

                value -= weight;
            }

            // Unreachable while the weights are non-zero: the reference asserts here as well.
            return 0;
        }
    }

    /// <summary>
    /// Builds the thread generator the way <c>PCG32( std::random_device &amp; )</c> does (<c>rand.h:56</c>):
    /// the seed and the stream are two 64-bit draws, each assembled from two 32-bit ones (<c>rand.h:106</c>).
    /// </summary>
    /// <returns>The seeded generator.</returns>
    private static Pcg32 CreateSeededGenerator() => new(GenerateUInt64(), GenerateUInt64());

    /// <summary>One 64-bit value from the operating system, built from two 32-bit draws.</summary>
    /// <returns>The value.</returns>
    private static ulong GenerateUInt64()
    {
        Span<byte> buffer = stackalloc byte[4];

        RandomNumberGenerator.Fill(buffer);
        ulong high = BinaryPrimitives.ReadUInt32LittleEndian(buffer);

        RandomNumberGenerator.Fill(buffer);
        ulong low = BinaryPrimitives.ReadUInt32LittleEndian(buffer);

        return (high << 32) | low;
    }
}
