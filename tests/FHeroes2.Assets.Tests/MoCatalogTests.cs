using System.Buffers.Binary;
using System.Text;
using FHeroes2.Assets.Localization;
using FHeroes2.Core.Io;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests for the gettext catalog reader. The fixtures are assembled by hand, because the point of
/// these tests is the byte layout described in <c>docs/REFERENCE.md</c> (GNU MO), not the wording of
/// a particular translation.
/// </summary>
public sealed class MoCatalogTests
{
    private const string HeaderText =
        "Language: zh_CN\nContent-Type: text/plain; charset=UTF-8\nPlural-Forms: nplurals=1; plural=0;\n";

    [Fact]
    public void Parse_ReadsEntriesAndMetadata()
    {
        byte[] data = BuildMo(
            ("", HeaderText),
            ("Okay", "确定"),
            ("High Scores", "高分榜"));

        MoCatalog catalog = MoCatalog.Parse(data);

        Assert.Equal(2, catalog.Count);
        Assert.Equal("zh_CN", catalog.Language);
        Assert.Equal("UTF-8", catalog.Charset);
        Assert.Equal("确定", catalog.Translate("Okay"));
        Assert.True(catalog.Contains("High Scores"));
    }

    [Fact]
    public void Parse_ReadsBigEndianCatalogs()
    {
        byte[] data = BuildMo(
            bigEndian: true,
            ("", HeaderText),
            ("Okay", "确定"));

        MoCatalog catalog = MoCatalog.Parse(data);

        Assert.Equal("确定", catalog.Translate("Okay"));
    }

    [Fact]
    public void Parse_SplitsPluralFormsStoredInASingleMessage()
    {
        byte[] data = BuildMo(
            ("", HeaderText),
            ("day", "日\0天"));

        MoCatalog catalog = MoCatalog.Parse(data);

        Assert.Equal("日", catalog.Translate("day"));
        Assert.Equal("天", catalog.Translate("day", 1));

        // Out of range indexes and missing entries fall back to the message id.
        Assert.Equal("day", catalog.Translate("day", 7));
    }

    [Fact]
    public void Translate_FallsBackToTheMessageWithoutItsContext()
    {
        byte[] data = BuildMo(("", HeaderText), ("Okay", "确定"));

        MoCatalog catalog = MoCatalog.Parse(data);

        Assert.Equal("确定", catalog.Translate("Okay"));
        Assert.Equal("Few", catalog.Translate("army|Few"));
        Assert.Equal("Unknown string", catalog.Translate("Unknown string"));
    }

    [Fact]
    public void Parse_SkipsEmptyTranslations()
    {
        byte[] data = BuildMo(
            ("", HeaderText),
            ("Okay", "确定"),
            ("Cancel", string.Empty));

        MoCatalog catalog = MoCatalog.Parse(data);

        Assert.Equal(1, catalog.Count);
        Assert.Equal("Cancel", catalog.Translate("Cancel"));
    }

    [Fact]
    public void StripContext_RemovesOnlyTheFirstPrefix()
    {
        Assert.Equal("Few", MoCatalog.StripContext("army|Few"));
        Assert.Equal("a|b", MoCatalog.StripContext("context|a|b"));
        Assert.Equal("plain", MoCatalog.StripContext("plain"));
    }

    [Fact]
    public void Parse_RejectsAnInvalidMagicNumber()
    {
        byte[] data = BuildMo(("", HeaderText), ("Okay", "确定"));
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0xDEADBEEF);

        BinaryFormatException error = Assert.Throws<BinaryFormatException>(() => MoCatalog.Parse(data));

        Assert.Contains("magic", error.Message);
    }

    [Fact]
    public void Parse_RejectsAnUnsupportedMajorVersion()
    {
        byte[] data = BuildMo(("", HeaderText), ("Okay", "确定"));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), 1);

        Assert.Throws<BinaryFormatException>(() => MoCatalog.Parse(data));
    }

    [Fact]
    public void Parse_RejectsATruncatedFile()
    {
        byte[] data = BuildMo(("", HeaderText), ("Okay", "确定"));

        Assert.Throws<BinaryFormatException>(() => MoCatalog.Parse(data.AsSpan(0, 27)));
        Assert.Throws<BinaryFormatException>(() => MoCatalog.Parse(data.AsSpan(0, data.Length - 3)));
    }

    [Fact]
    public void Parse_RejectsACatalogWithoutTranslations()
    {
        byte[] data = BuildMo(("", HeaderText));

        Assert.Throws<BinaryFormatException>(() => MoCatalog.Parse(data));
    }

    [Fact]
    public void Parse_RejectsANonUtf8Charset()
    {
        byte[] data = BuildMo(
            ("", "Language: ru\nContent-Type: text/plain; charset=CP1251\n"),
            ("Okay", "Okay"));

        Assert.Throws<NotSupportedException>(() => MoCatalog.Parse(data));
    }

    [Fact]
    public void FindCatalog_ProbesTheDataFolderAndItsParent()
    {
        string root = Path.Combine(Path.GetTempPath(), "fheroes2sharp-" + Guid.NewGuid().ToString("N"));
        string dataDirectory = Path.Combine(root, "Data");
        string langDirectory = Path.Combine(root, "files", "lang");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(langDirectory);

        string catalogPath = Path.Combine(langDirectory, "zh_CN.mo");
        File.WriteAllBytes(catalogPath, BuildMo(("", HeaderText), ("Okay", "确定")));

        try
        {
            // The game data lives in "Data" while the catalogs sit next to the executable.
            Assert.Equal(Path.GetFullPath(catalogPath), MoCatalog.FindCatalog(dataDirectory, "zh_CN"));
            Assert.Equal(Path.GetFullPath(catalogPath), MoCatalog.FindCatalog(root, "zh_CN"));
            Assert.Null(MoCatalog.FindCatalog(dataDirectory, "de"));
            Assert.Null(MoCatalog.FindCatalog(null, "zh_CN"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ZhCnCatalog_TranslatesTheShellStrings()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        string? catalogPath = MoCatalog.FindCatalog(dataDirectory, "zh_CN");

        if (catalogPath is null)
        {
            // The catalog is a build artifact installed by tools/scripts/BuildTranslations.ps1.
            return;
        }

        MoCatalog catalog = MoCatalog.Load(catalogPath);

        Assert.Equal("zh_CN", catalog.Language);
        Assert.True(catalog.Count > 50, $"Only {catalog.Count} entries were translated.");
        Assert.Equal("确定", catalog.Translate("Okay"));
        Assert.Equal("取消", catalog.Translate("Cancel"));
        Assert.Equal("新游戏", catalog.Translate("New Game"));
        Assert.Equal("游戏设置", catalog.Translate("Game Settings"));
        Assert.Equal("自动战斗", catalog.Translate("Auto Resolve"));

        // Context prefixed ids are translated without the prefix.
        Assert.Equal("手动", catalog.Translate("combatMode|Manual"));

        // Anything that is not translated yet has to fall back to English.
        Assert.Equal("Army", catalog.Translate("Army"));
    }

    private static byte[] BuildMo(params (string Id, string Text)[] entries)
        => BuildMo(false, entries);

    /// <summary>
    /// Writes a minimal GNU MO file: header, the two string tables, then the NUL terminated strings.
    /// </summary>
    private static byte[] BuildMo(bool bigEndian, params (string Id, string Text)[] entries)
    {
        (string Id, string Text)[] sorted = [.. entries.OrderBy(entry => entry.Id, StringComparer.Ordinal)];

        const int HeaderSize = 28;
        int count = sorted.Length;
        int originalTable = HeaderSize;
        int translationTable = originalTable + (count * 8);

        byte[][] ids = new byte[count][];
        byte[][] texts = new byte[count][];
        int size = translationTable + (count * 8);
        for (int index = 0; index < count; ++index)
        {
            ids[index] = Encoding.UTF8.GetBytes(sorted[index].Id);
            texts[index] = Encoding.UTF8.GetBytes(sorted[index].Text);
            size += ids[index].Length + texts[index].Length + 2;
        }

        byte[] data = new byte[size];
        WriteUInt32(data, 0, 0x950412DE, bigEndian);
        WriteUInt16(data, 4, 0, bigEndian);
        WriteUInt16(data, 6, 0, bigEndian);
        WriteUInt32(data, 8, (uint)count, bigEndian);
        WriteUInt32(data, 12, (uint)originalTable, bigEndian);
        WriteUInt32(data, 16, (uint)translationTable, bigEndian);
        WriteUInt32(data, 20, 0, bigEndian);
        WriteUInt32(data, 24, 0, bigEndian);

        int position = translationTable + (count * 8);
        for (int index = 0; index < count; ++index)
        {
            WriteUInt32(data, originalTable + (index * 8), (uint)ids[index].Length, bigEndian);
            WriteUInt32(data, originalTable + (index * 8) + 4, (uint)position, bigEndian);
            ids[index].CopyTo(data, position);
            position += ids[index].Length + 1;

            WriteUInt32(data, translationTable + (index * 8), (uint)texts[index].Length, bigEndian);
            WriteUInt32(data, translationTable + (index * 8) + 4, (uint)position, bigEndian);
            texts[index].CopyTo(data, position);
            position += texts[index].Length + 1;
        }

        return data;
    }

    private static void WriteUInt16(byte[] data, int offset, ushort value, bool bigEndian)
    {
        if (bigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
        }
    }

    private static void WriteUInt32(byte[] data, int offset, uint value, bool bigEndian)
    {
        if (bigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
        }
    }
}

