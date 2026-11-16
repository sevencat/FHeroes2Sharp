namespace FHeroes2.Game.System;

/// <summary>
/// A word of flags with the four operations everything in the game needs, <c>class BitModes</c>
/// (<c>bitmodes.h:31-59</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is a base class because two very different things share it: a <c>Player</c> and a
/// <c>Settings</c> singleton. Neither has anything else in common, and both want to ask "is this flag set" in
/// the same words, so the storage and the four operations live here rather than in each of them.
/// </para>
/// <para>
/// <c>modes</c> is protected, as in the reference: the serialisation operators of <c>bitmodes.cpp</c> reach into
/// it, and so do the two subclasses when they mirror their own enums. The operators themselves are not ported —
/// the port has no stream layer yet (ADR-139).
/// </para>
/// </remarks>
public class BitModes
{
    /// <summary>The flag word itself, <c>modes</c>.</summary>
    protected uint modes;

    /// <summary>The flag word, for a caller that has to dump or compare it.</summary>
    public uint Modes => modes;

    /// <summary>Sets every flag of <paramref name="flags"/>, <c>BitModes::SetModes()</c>.</summary>
    /// <param name="flags">The flags to add.</param>
    public void SetModes(uint flags) => modes |= flags;

    /// <summary>Clears every flag of <paramref name="flags"/>, <c>BitModes::ResetModes()</c>.</summary>
    /// <param name="flags">The flags to remove.</param>
    public void ResetModes(uint flags) => modes &= ~flags;

    /// <summary>
    /// Whether <b>any</b> of <paramref name="flags"/> is set, <c>BitModes::Modes()</c>.
    /// </summary>
    /// <param name="flags">The flags to test; zero always answers false.</param>
    /// <returns>True when at least one flag is set.</returns>
    public bool HasModes(uint flags) => (modes & flags) != 0;

    /// <summary>
    /// Whether <b>all</b> of <paramref name="flags"/> are set, <c>BitModes::AllModes()</c>.
    /// </summary>
    /// <param name="flags">The flags to test; zero always answers false, as in the reference.</param>
    /// <returns>True when every flag is set.</returns>
    public bool HasAllModes(uint flags) => (modes & flags) == flags && flags != 0;
}
