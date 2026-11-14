using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers the object presets the editor places and the add-on editing <see cref="Mp2Map"/> grew for
/// them: a preset has to describe something drawable, and a chain built from it has to be a chain the
/// reader walks back out of a written file.
/// </summary>
public sealed class Mp2ObjectPresetsTests
{
    [Fact]
    public void EveryPresetHasSomethingToDraw()
    {
        Assert.NotEmpty(Mp2ObjectPresets.All);
        Assert.Equal(Mp2ObjectPresets.All.Count, Mp2ObjectPresets.Count);

        foreach (Mp2ObjectPreset preset in Mp2ObjectPresets.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.Name));
            Assert.NotEqual(Mp2ObjectType.None, preset.Type);
            Assert.True(preset.HasGroundPart || preset.HasTopPart, preset.GetSummary());

            if (preset.HasGroundPart)
            {
                Assert.NotEqual(Mp2ObjectIcnType.Unknown, preset.Template.GroundIcnType);
                Assert.NotEqual(Mp2TileInfo.EmptyImageIndex, preset.Template.BottomIcnImageIndex);
            }

            if (preset.HasTopPart)
            {
                Assert.NotEqual(Mp2ObjectIcnType.Unknown, preset.Template.TopIcnType);
                Assert.NotEqual(Mp2TileInfo.EmptyImageIndex, preset.Template.TopIcnImageIndex);
            }

            foreach (Mp2AddonInfo addon in preset.Addons)
            {
                Assert.NotEqual(Mp2ObjectIcnType.Unknown, addon.GroundIcnType);
                Assert.NotEqual(Mp2TileInfo.EmptyImageIndex, addon.BottomIcnImageIndex);
            }
        }
    }

    [Fact]
    public void PresetsAreFoundByNameAndByPosition()
    {
        Mp2ObjectPreset first = Mp2ObjectPresets.All[0];

        Assert.Same(first, Mp2ObjectPresets.Find(first.Name));
        Assert.Same(first, Mp2ObjectPresets.Find(first.Name.ToUpperInvariant()));
        Assert.Same(first, Mp2ObjectPresets.Find("1"));
        Assert.Same(Mp2ObjectPresets.All[^1], Mp2ObjectPresets.Find(Mp2ObjectPresets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Null(Mp2ObjectPresets.Find("0"));
        Assert.Null(Mp2ObjectPresets.Find((Mp2ObjectPresets.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Null(Mp2ObjectPresets.Find("no such object"));
        Assert.Null(Mp2ObjectPresets.Find("  "));

        Assert.Equal(0, Mp2ObjectPresets.IndexOf(first));
        Assert.Equal(-1, Mp2ObjectPresets.IndexOf(null));
    }

    [Fact]
    public void APresetRejectsAnEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new Mp2ObjectPreset(" ", Mp2ObjectPresets.All[0].Template));
    }

    [Fact]
    public void AppendAddonFillsThePlaceholderAFileWithoutAddonsLacks()
    {
        Mp2Map map = Mp2Map.CreateBlank(36, 30) with { Addons = new List<Mp2AddonInfo>() };

        int index = map.AppendAddon(Preset().Addons.Count > 0 ? Preset().Addons[0] : Addon());

        // Index 0 means "no add-on", so the first real piece is 1 even when the file had none.
        Assert.Equal(1, index);
        Assert.Equal(2, map.Addons.Count);
    }

    [Fact]
    public void AppendAddonRefusesAListThatCannotBeEdited()
    {
        // An array is what a reader that was handed a fixed slice keeps; the editor needs a list.
        Mp2AddonInfo[] addons = [default, Addon()];
        Mp2Map map = Mp2Map.CreateBlank(36, 30) with { Addons = addons };

        Assert.Throws<NotSupportedException>(() => map.AppendAddon(Addon()));
    }

    [Fact]
    public void SetAddonAtReplacesARealEntryAndRefusesThePlaceholder()
    {
        Mp2Map map = Mp2Map.CreateBlank(36, 30);
        Mp2AddonInfo addon = Addon() with { BottomIcnImageIndex = 12 };

        // A blank map only has the placeholder, so there is nothing to replace before an append.
        Assert.Equal(1, map.AppendAddon(Addon()));

        map.SetAddonAt(1, addon);

        Assert.Equal(12, map.Addons[1].BottomIcnImageIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetAddonAt(0, addon));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetAddonAt(2, addon));
    }

    [Fact]
    public void APlacedChainSurvivesAWriteAndRead()
    {
        string filePath = NewMapPath();

        try
        {
            Mp2Map map = Mp2Map.CreateBlank(36, 30);
            Mp2ObjectPreset preset = Mp2ObjectPresets.All.First(candidate => candidate.AddonCount > 0);

            uint uid = map.TakeObjectUid();
            ushort chain = 0;

            for (int index = preset.Addons.Count - 1; index >= 0; --index)
            {
                chain = (ushort)map.AppendAddon(preset.Addons[index] with
                {
                    NextAddonIndex = chain,
                    Level1ObjectUid = uid,
                });
            }

            map.SetTile(1, 1, map.Tile(1, 1) with
            {
                ObjectType = preset.Type,
                ObjectName1 = preset.Template.ObjectName1,
                BottomIcnImageIndex = preset.Template.BottomIcnImageIndex,
                Quantity1 = preset.Template.Quantity1,
                NextAddonIndex = chain,
                Level1ObjectUid = uid,
            });

            Mp2MapWriter.WriteAll(filePath, map);

            Mp2Map read = Mp2Map.Read(filePath);

            Assert.Equal(preset.Type, read.Tile(1, 1).ObjectType);
            Assert.Equal(chain, read.Tile(1, 1).NextAddonIndex);
            Assert.Equal(preset.AddonCount, read.EnumerateAddonChain(read.Tile(1, 1)).Count());
            Assert.Equal(uid, read.Tile(1, 1).Level1ObjectUid);

            // The counter travels with the file, so the next object of a reopened map does not reuse it.
            Assert.Equal(uid, read.LastObjectUid);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void TakeObjectUidHandsOutDistinctValues()
    {
        Mp2Map map = Mp2Map.CreateBlank(36, 30);

        // A new map has 0, which every empty tile uses for "no object".
        Assert.Equal(0u, map.LastObjectUid);
        Assert.Equal(1u, map.TakeObjectUid());
        Assert.Equal(2u, map.TakeObjectUid());
        Assert.Equal(2u, map.LastObjectUid);
    }

    private static Mp2ObjectPreset Preset() => Mp2ObjectPresets.All.First(candidate => candidate.HasGroundPart);

    /// <summary>An add-on with nothing on it, the base of the tests that need one value.</summary>
    private static Mp2AddonInfo Addon() => default(Mp2AddonInfo) with
    {
        NextAddonIndex = 0,
        ObjectNameN1 = 0,
        BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        QuantityN = 0,
        ObjectNameN2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        Level1ObjectUid = 0,
        Level2ObjectUid = 0,
    };

    private static string NewMapPath() =>
        Path.Combine(Path.GetTempPath(), $"fh2-object-{Guid.NewGuid():N}.mp2");
}
