using System.Security.Cryptography;
using FHeroes2.Assets.Agg;
using FHeroes2.Assets.Cursor;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Decodes real sprites and tiles from the game archives. The digests pin the exact bytes the
/// decoder produces, which is the only practical way to prove that the port matches the original
/// engine without shipping its output. When the data directory cannot be found the tests return
/// early (see docs/MISC.md).
/// </summary>
public sealed class SpriteAssetIntegrationTests
{
    private const string HeroesIcnDigest = "C27A3BB3ECA49C809FC9398BEBB694CAEDB6CCF21C6129FED12FB757C172734A";
    private const string Ground32TilDigest = "8AAB8C877A65D2D7F11F90A9157AC6532A88F7A5906950D30758F37C1064BA5F";
    private const string AdventureMapCursorDigest = "EA6B3726046B986E06C75A572AB891CF817230A00CB22F1AF70FD9678AA2C402";
    private const string SpellCursorDigest = "52DB46A2C56550676728095A628BCE7D0E307033ED2659C3FF6A8E6E02C9B50B";

    [Fact]
    public void HeroesIcn_DecodesEverySprite()
    {
        byte[]? data = ReadEntry("HEROES.ICN");
        if (data is null)
        {
            return;
        }

        IcnSpriteFile icn = IcnSpriteFile.Parse(data);

        // HEROES.ICN is a single, fully opaque screen sized sprite.
        Assert.Equal(1, icn.Count);
        Assert.Equal(640, icn.Headers[0].Width);
        Assert.Equal(480, icn.Headers[0].Height);
        Assert.False(icn.Sprites[0].HasTransformLayer);
        Assert.Equal(data.Length, IcnSpriteFile.HeaderSize + icn.DataBlockSize);
        Assert.Equal(icn.Count, icn.Headers.Count);

        for (int index = 0; index < icn.Count; ++index)
        {
            Sprite sprite = icn.Sprites[index];
            Assert.Equal(icn.Headers[index].Width, sprite.Width);
            Assert.Equal(icn.Headers[index].Height, sprite.Height);
            Assert.Equal(icn.Headers[index].OffsetX, sprite.OffsetX);
        }

        Assert.Equal(HeroesIcnDigest, Digest(icn));
    }

    [Fact]
    public void Ground32Til_DecodesEveryTile()
    {
        byte[]? data = ReadEntry("GROUND32.TIL");
        if (data is null)
        {
            return;
        }

        TilFile til = TilFile.Parse(data);

        Assert.Equal(432, til.Count);
        Assert.Equal(32, til.Width);
        Assert.Equal(32, til.Height);
        Assert.All(til.Images, tile => Assert.Equal(32 * 32, tile.Pixels.Length));

        Assert.Equal(Ground32TilDigest, Digest(til));
    }

    [Fact]
    public void AdventureMapMonoCursor_Decodes()
    {
        byte[]? data = ReadEntry("ADVMBW01.BMP");
        if (data is null)
        {
            return;
        }

        Sprite cursor = MonoCursorFile.Parse(data);

        Assert.Equal(32, cursor.Width);
        Assert.Equal(32, cursor.Height);
        Assert.True(cursor.HasTransformLayer);

        // The archived cursors use palette index 33 for the outline and 11 for the body.
        Assert.Equal(37, cursor.Image.Pixels.Count(pixel => pixel == MonoCursorFile.WhitePaletteIndex));
        Assert.Equal(53, cursor.Image.Pixels.Count(pixel => pixel == 33));
        Assert.Equal(934, cursor.Transform!.Count(entry => entry == 1));

        Assert.Equal(AdventureMapCursorDigest, Digest(cursor));
    }

    [Fact]
    public void SpellMonoCursor_Decodes()
    {
        byte[]? data = ReadEntry("SPELBW00.BMP");
        if (data is null)
        {
            return;
        }

        Sprite cursor = MonoCursorFile.Parse(data);

        Assert.Equal(32, cursor.Width);
        Assert.Equal(32, cursor.Height);
        Assert.Equal(108, cursor.Image.Pixels.Count(pixel => pixel == MonoCursorFile.WhitePaletteIndex));
        Assert.Equal(124, cursor.Image.Pixels.Count(pixel => pixel == 33));
        Assert.Equal(792, cursor.Transform!.Count(entry => entry == 1));

        Assert.Equal(SpellCursorDigest, Digest(cursor));
    }

    private static byte[]? ReadEntry(string entryName)
    {
        string? path = GameData.FindArchive(AssetLocator.BaseArchiveName);
        if (path is null)
        {
            return null;
        }

        using AssetArchive archive = AssetArchive.Open(path);

        return archive.Read(entryName);
    }

    /// <summary>Hashes the image layer and the transform layer of every sprite.</summary>
    private static string Digest(IcnSpriteFile icn)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] size = new byte[4];

        foreach (Sprite sprite in icn.Sprites)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(size, sprite.Width * sprite.Height);
            hash.AppendData(size);
            hash.AppendData(sprite.Image.AsSpan());

            hash.AppendData([sprite.HasTransformLayer ? (byte)1 : (byte)0]);
            if (sprite.Transform is not null)
            {
                hash.AppendData(sprite.Transform);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Hashes the image layer and the transform layer of a single sprite.</summary>
    private static string Digest(Sprite sprite)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] size = new byte[4];

        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(size, sprite.Width * sprite.Height);
        hash.AppendData(size);
        hash.AppendData(sprite.Image.AsSpan());
        hash.AppendData([sprite.HasTransformLayer ? (byte)1 : (byte)0]);

        if (sprite.Transform is not null)
        {
            hash.AppendData(sprite.Transform);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string Digest(TilFile til)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (PixelBuffer tile in til.Images)
        {
            hash.AppendData(tile.AsSpan());
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
