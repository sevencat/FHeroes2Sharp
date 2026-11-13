using System.Security.Cryptography;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// The transform tables are copied from the reference engine, so these tests double as a
/// provenance check: the SHA-256 pins the exact bytes of <c>transformTable</c> in
/// fheroes2 <c>src/engine/image.cpp</c>.
/// </summary>
public sealed class PaletteTransformTableTests
{
    private const string ExpectedSha256 = "2CCCF14A4715A0D55B6845EFB01232D5385DB279E19D715CCC1210DCBFC97A28";

    [Fact]
    public void All_HasSixteenTablesOf256Entries()
    {
        Assert.Equal(
            PaletteTransformTable.TransformCount * PaletteTransformTable.TransformSize,
            PaletteTransformTable.All.Length);
    }

    [Fact]
    public void Data_MatchesTheReferenceTransformTable()
    {
        string actual = Convert.ToHexString(SHA256.HashData(PaletteTransformTable.All));

        Assert.Equal(ExpectedSha256, actual);
    }

    [Fact]
    public void TablesZeroAndOne_AreUnusedPlaceholders()
    {
        Assert.All(PaletteTransformTable.Get(0).ToArray(), value => Assert.Equal(0, value));
        Assert.All(PaletteTransformTable.Get(1).ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void TableFifteen_MapsTheCyclingColorsToTheirStaticDuplicates()
    {
        ReadOnlySpan<byte> staticColors = PaletteTransformTable.Get(15);

        // The restorer leaves colors outside the cycling ranges alone.
        foreach (int index in new[] { 0, 1, 32, 128, 200, 255 })
        {
            Assert.Equal((byte)index, staticColors[index]);
        }

        // Water cycling slots 231..233 and 235, lava slots 214..217.
        Assert.Equal(246, staticColors[231]);
        Assert.Equal(248, staticColors[233]);
        Assert.Equal(248, staticColors[235]);
        Assert.Equal(250, staticColors[214]);
        Assert.Equal(253, staticColors[217]);
    }

    [Fact]
    public void Get_RejectsOutOfRangeIds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaletteTransformTable.Get(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaletteTransformTable.Get(PaletteTransformTable.TransformCount));
    }

    [Fact]
    public void ShadowTables_GetWeakerWithTheTableId()
    {
        // The reference engine documents 2 as the strongest shadow, 5 as the weakest. Every
        // table maps a mid grey to a different palette entry, so the check is only that they
        // are distinct and monotonic in the shadow ramp.
        byte strongest = PaletteTransformTable.Apply(2, 64);
        byte weakest = PaletteTransformTable.Apply(5, 64);

        Assert.NotEqual(strongest, weakest);
        Assert.True(strongest > weakest, $"Table 2 mapped 64 to {strongest}, table 5 to {weakest}.");
    }
}
