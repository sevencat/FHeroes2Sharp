using System.Text;
using FHeroes2.Assets.Agg;

namespace FHeroes2.Assets.Tests;

public sealed class AggFileNameHashTests
{
    [Fact]
    public void Compute_IsCaseInsensitive()
    {
        Assert.Equal(AggFileNameHash.Compute("KB.PAL"), AggFileNameHash.Compute("kb.pal"));
        Assert.Equal(AggFileNameHash.Compute("SMALFONT.ICN"), AggFileNameHash.Compute("SmalFont.Icn"));
    }

    [Fact]
    public void Compute_IsOrderSensitive()
    {
        Assert.NotEqual(AggFileNameHash.Compute("AB"), AggFileNameHash.Compute("BA"));
    }

    [Fact]
    public void Compute_MatchesAsciiOverload()
    {
        const string name = "HEROES.ICN";

        uint fromChars = AggFileNameHash.Compute(name);
        uint fromBytes = AggFileNameHash.ComputeAscii(Encoding.ASCII.GetBytes(name));

        Assert.Equal(fromChars, fromBytes);
    }

    [Fact]
    public void Compute_EmptyName_IsZero()
    {
        Assert.Equal(0u, AggFileNameHash.Compute(string.Empty));
    }
}
