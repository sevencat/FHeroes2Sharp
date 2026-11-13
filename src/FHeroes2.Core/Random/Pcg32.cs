namespace FHeroes2.Core.Random;

/// <summary>
/// Portable PCG32 generator mirroring <c>Rand::PCG32</c> from <c>src/engine/rand.h</c>.
/// The exact same state transitions are used so map generation and combat rolls stay
/// reproducible with the reference implementation when seeded identically.
/// </summary>
public sealed class Pcg32
{
    /// <summary>Default seed used by the reference implementation.</summary>
    public const ulong DefaultSeed = 42;

    /// <summary>Default stream used by the reference implementation.</summary>
    public const ulong DefaultStream = 54;

    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private ulong _increment;

    public Pcg32()
        : this(DefaultSeed, DefaultStream)
    {
    }

    public Pcg32(ulong seed, ulong stream = DefaultStream)
    {
        _increment = stream;
        _state = 0;

        AdvanceState();
        _state = unchecked(_state + seed);
        AdvanceState();
    }

    public ulong Stream => _increment;

    public void SetStream(ulong stream) => _increment = stream;

    /// <summary>Produces the next raw 32-bit value in the full range.</summary>
    public uint NextUInt32()
    {
        ulong previousState = _state;
        AdvanceState();

        uint xorShifted = (uint)(((previousState >> 18) ^ previousState) >> 27);
        int rotations = (int)(previousState >> 59);

        return RotateRight(xorShifted, rotations);
    }

    /// <summary>Inclusive random value in the <c>[from, to]</c> interval.</summary>
    public uint NextInclusive(uint from, uint to)
    {
        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (from == to)
        {
            return from;
        }

        uint rangeExclusive = to - from;
        if (rangeExclusive == uint.MaxValue)
        {
            return NextUInt32();
        }

        return from + UniformInInterval(rangeExclusive + 1);
    }

    /// <summary>Inclusive random value in the <c>[from, to]</c> interval.</summary>
    public int NextInclusive(int from, int to) => (int)NextInclusive((uint)from, (uint)to);

    /// <summary>Returns <see langword="true"/> with the given probability in percent (0..100).</summary>
    public bool Chance(uint percent) => percent != 0 && NextInclusive(0, 99) < percent;

    /// <summary>Fisher-Yates shuffle identically to <c>Rand::shuffle</c>.</summary>
    public void Shuffle<T>(IList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        int last = values.Count - 1;
        if (last <= 0)
        {
            return;
        }

        for (uint remainingSwaps = (uint)last; remainingSwaps > 0; --remainingSwaps)
        {
            uint index = NextInclusive(0, remainingSwaps);
            (values[last], values[(int)index]) = (values[(int)index], values[last]);
            --last;
        }
    }

    private void AdvanceState() => _state = unchecked((_state * Multiplier) + (_increment | 1UL));

    // Reference: "Fast Random Integer Generation in an Interval" (Lemire), same rejection step as rand.cpp.
    private uint UniformInInterval(uint range)
    {
        if (range == 0)
        {
            return 0;
        }

        uint generated = NextUInt32();
        ulong mult = (ulong)generated * range;
        uint lowerPart = (uint)mult;

        if (lowerPart >= range)
        {
            return (uint)(mult >> 32);
        }

        uint discardBound = (uint)(~(range - 1)) % range;

        while (lowerPart < discardBound)
        {
            generated = NextUInt32();
            mult = (ulong)generated * range;
            lowerPart = (uint)mult;
        }

        return (uint)(mult >> 32);
    }

    private static uint RotateRight(uint value, int rotations) =>
        (value >> rotations) | (value << ((~(rotations - 1)) & 31));
}
