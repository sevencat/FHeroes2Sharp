using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The data half of the save/load file chooser: listing, sorting and name matching of
/// <c>getSortedMapsFileInfoList()</c> and <c>findInMapInfos()</c> (<c>dialog_selectfile.cpp:64-124</c>).
/// </summary>
public sealed class SelectFileDialogTests
{
    [Fact]
    public void TheNameOrderIgnoresCase()
    {
        List<SaveFileEntry> files = [Row("beta", 3), Row("Alpha", 2), Row("gamma", 1)];

        SelectFileDialog.SortSaveFiles(files, SaveFileSortingMethod.Filename);

        Assert.Equal(["Alpha", "beta", "gamma"], files.Select(file => file.Name));
    }

    [Fact]
    public void TheDateOrderIsNewestFirst()
    {
        List<SaveFileEntry> files = [Row("old", 1), Row("new", 3), Row("middle", 2)];

        SelectFileDialog.SortSaveFiles(files, SaveFileSortingMethod.Timestamp);

        Assert.Equal(["new", "middle", "old"], files.Select(file => file.Name));
    }

    [Fact]
    public void TheDateOrderFallsBackToTheNameWithinTheSameSecond()
    {
        List<SaveFileEntry> files = [Row("bravo", 2), Row("alpha", 2)];

        SelectFileDialog.SortSaveFiles(files, SaveFileSortingMethod.Timestamp);

        Assert.Equal(["alpha", "bravo"], files.Select(file => file.Name));
    }

    [Fact]
    public void ASaveIsFoundByPathByNameAndByStem()
    {
        List<SaveFileEntry> files = [Row("alpha", 1), Row("bravo", 2)];

        Assert.Equal(1, SelectFileDialog.FindSaveFileIndex(files, @"C:\saves\BRAVO.SAV"));
        Assert.Equal(1, SelectFileDialog.FindSaveFileIndex(files, "bravo"));
        Assert.Equal(1, SelectFileDialog.FindSaveFileIndex(files, "Bravo.SAV"));
        Assert.Equal("bravo", SelectFileDialog.FindSaveFile(files, "bravo")?.Name);
    }

    [Fact]
    public void AnEmptyOrUnknownNameFindsNothing()
    {
        List<SaveFileEntry> files = [Row("alpha", 1)];

        Assert.Equal(-1, SelectFileDialog.FindSaveFileIndex(files, null));
        Assert.Equal(-1, SelectFileDialog.FindSaveFileIndex(files, string.Empty));
        Assert.Equal(-1, SelectFileDialog.FindSaveFileIndex(files, "charlie"));
        Assert.Null(SelectFileDialog.FindSaveFile(files, "charlie"));
    }

    [Fact]
    public void TheStemDropsTheDirectoryAndTheExtension()
    {
        Assert.Equal("bravo_0003", SelectFileDialog.GetStem(@"C:\saves\bravo_0003.SAV"));
        Assert.Equal("bravo", SelectFileDialog.GetStem("bravo.SAV"));
        Assert.Equal("bravo", SelectFileDialog.GetStem("bravo"));
        Assert.Equal(string.Empty, SelectFileDialog.GetStem(string.Empty));
    }

    [Fact]
    public void AMissingDirectoryListsNothing()
    {
        string directory = Path.Combine(Path.GetTempPath(), "fh2-missing-" + Guid.NewGuid().ToString("N"));

        Assert.Empty(SelectFileDialog.GetSaveFiles(SaveFileSortingMethod.Filename, directory));
    }

    [Fact]
    public void FilesThatAreNotSavesAreSkipped()
    {
        string directory = Path.Combine(Path.GetTempPath(), "fh2-saves-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            // 头部读不出来的文件不进列表，参考的 getSortedMapsFileInfoList() 同样跳过它们。
            File.WriteAllText(Path.Combine(directory, "junk" + GameSave.GetSaveFileExtension()), "not a save");
            File.WriteAllText(Path.Combine(directory, "notes.txt"), "not even a save");

            Assert.Empty(SelectFileDialog.GetSaveFiles(SaveFileSortingMethod.Filename, directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AMissingFileHasNoRow()
    {
        Assert.Null(SaveFileEntry.TryRead(Path.Combine(Path.GetTempPath(), "fh2-no-such-save.SAV")));
    }

    [Fact]
    public void TheDateLineCarriesTheTimeOfDay()
    {
        string text = SelectFileDialog.DescribeDate(new DateTime(2024, 3, 9, 7, 5, 0));

        // 星期与月份名跟着当前区域设置走，时分是固定的。
        Assert.Contains("07:05", text);
        Assert.Contains("24", text);
    }

    private static SaveFileEntry Row(string name, int timestamp) => new(
        Path.Combine(@"C:\saves", name + ".SAV"), name, "Island",
        DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime, 0, 0, 0);
}
