using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="Mp2ObjectIcnTypes"/>: which object tile set lives in which archive entry, and
/// which of them a tile part may draw directly.
/// </summary>
public sealed class Mp2ObjectIcnTypesTests
{
    [Theory]
    [InlineData(Mp2ObjectIcnType.Unknown)]
    [InlineData(Mp2ObjectIcnType.Unused1)]
    [InlineData(Mp2ObjectIcnType.Unused28)]
    [InlineData(Mp2ObjectIcnType.Unused47)]
    public void NothingToDraw_HasNoArchive(Mp2ObjectIcnType type) =>
        Assert.Null(Mp2ObjectIcnTypes.GetArchiveName(type, priceOfLoyaltySupported: true));

    [Theory]
    [InlineData(Mp2ObjectIcnType.ObjnGras, "OBJNGRAS.ICN")]
    [InlineData(Mp2ObjectIcnType.ObjnTown, "OBJNTOWN.ICN")]
    [InlineData(Mp2ObjectIcnType.MtnGras, "MTNGRAS.ICN")]
    [InlineData(Mp2ObjectIcnType.MiniMon, "MINIMON.ICN")]
    [InlineData(Mp2ObjectIcnType.Flag32, "FLAG32.ICN")]
    [InlineData(Mp2ObjectIcnType.ObjnHaun, "OBJNHAUN.ICN")]
    public void KnownType_UsesTheEnumNamePlusTheExtension(Mp2ObjectIcnType type, string expected)
    {
        Assert.Equal(expected, Mp2ObjectIcnTypes.GetArchiveName(type));
    }

    [Fact]
    public void PriceOfLoyaltyTileSets_NeedTheExpansionArchive()
    {
        Assert.Null(Mp2ObjectIcnTypes.GetArchiveName(Mp2ObjectIcnType.XLoc1));
        Assert.Null(Mp2ObjectIcnTypes.GetArchiveName(Mp2ObjectIcnType.XLoc3));

        Assert.Equal("X_LOC1.ICN", Mp2ObjectIcnTypes.GetArchiveName(Mp2ObjectIcnType.XLoc1, true));
        Assert.Equal("X_LOC3.ICN", Mp2ObjectIcnTypes.GetArchiveName(Mp2ObjectIcnType.XLoc3, true));
    }

    [Fact]
    public void EveryDrawableType_HasItsOwnEntry()
    {
        // Two enum members sharing one archive entry would mean one of them is a typo; the
        // reference has a one to one mapping, so this catches a misspelled switch arm.
        List<string> names = [];
        foreach (Mp2ObjectIcnType type in Enum.GetValues<Mp2ObjectIcnType>())
        {
            if (Mp2ObjectIcnTypes.GetArchiveName(type, priceOfLoyaltySupported: true) is { } name)
            {
                names.Add(name);
            }
        }

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.EndsWith(".ICN", name, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(Mp2ObjectIcnType.Unknown, false)]
    [InlineData(Mp2ObjectIcnType.Boat32, false)]
    [InlineData(Mp2ObjectIcnType.MiniHero, false)]
    [InlineData(Mp2ObjectIcnType.Mons32, false)]
    [InlineData(Mp2ObjectIcnType.ObjnGras, true)]
    [InlineData(Mp2ObjectIcnType.Flag32, true)]
    [InlineData(Mp2ObjectIcnType.MiniMon, true)]
    public void OnlyStandaloneSets_AreDrawnAsParts(Mp2ObjectIcnType type, bool expected) =>
        Assert.Equal(expected, Mp2ObjectIcnTypes.IsDrawnAsObjectPart(type));
}
