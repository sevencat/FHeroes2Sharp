namespace FHeroes2.Assets.Agg;

/// <summary>One record inside an AGG container.</summary>
public readonly record struct AssetEntry(string Name, uint Offset, uint Size)
{
    public uint EndOffset => Offset + Size;

    public override string ToString() => $"{Name} @ 0x{Offset:X8} ({Size} bytes)";
}
