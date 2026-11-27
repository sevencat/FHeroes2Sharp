using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The editor's save flow, <c>EditorInterface::saveMapToFile()</c> (<c>editor_interface.cpp:3204-3266</c>): the save
/// window, the file name, the overwrite question and the file that ends up on disk.
/// </summary>
/// <remarks>
/// 保存目录用 <see cref="EditorScene.SaveMapDirectory"/> 换成临时目录，用例不会写进用户的 <c>%APPDATA%</c>。
/// 窗口是模态的，用例用 <see cref="EditorScene.RunModal"/> 这条缝代替帧循环：在里面给文件名、按 OKAY。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorSaveMapTests : IDisposable
{
    private const int MapSize = 36;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly string _directory = Path.Combine(Path.GetTempPath(),
        "fh2-save-" + Guid.NewGuid().ToString("N"));

    /// <summary>Publishes the archives, which the save window's artwork is read from.</summary>
    public EditorSaveMapTests()
    {
        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 临时目录清不掉不该让用例失败。
        }
    }

    [Fact]
    public void SavingWritesTheFileAndReadsBackWithTheSameGridAndName()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.SaveMapDirectory = _directory;
        scene.RunModal = screen =>
        {
            if (screen is EditorSaveMapModal { Window: var window })
            {
                window.FileName = "First Map";
                window.MapName = "First Map Name";
                screen.HotKey(ButtonHotKey.Okay);
            }
        };

        Assert.True(scene.SaveMapToFile());

        string path = Path.Combine(_directory, "First Map" + EditorSaveMapWindow.MapFileExtension);

        Assert.True(File.Exists(path), $"the map was not written to '{path}'");

        Mp2FileInfo written = Mp2FileInfo.Read(path);

        Assert.Equal(MapSize, written.Width);
        Assert.Equal(MapSize, written.Height);
        Assert.Equal("First Map Name", written.Name);
    }

    [Fact]
    public void CancellingTheWindowWritesNothing()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.SaveMapDirectory = _directory;
        scene.RunModal = screen => screen.HotKey(ButtonHotKey.Cancel);

        Assert.False(scene.SaveMapToFile());
        Assert.False(Directory.Exists(_directory) && Directory.GetFiles(_directory).Length > 0);
    }

    [Fact]
    public void AnExistingFileIsOnlyOverwrittenAfterAYes()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        string path = Path.Combine(_directory, "Twice" + EditorSaveMapWindow.MapFileExtension);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "not a map");

        scene.SaveMapDirectory = _directory;

        bool asked = false;

        // 覆盖问句答 NO（CANCEL 在这扇窗里只可能是 NO）：窗口重开，用例第二次把它取消掉。
        scene.RunModal = screen =>
        {
            switch (screen)
            {
                case EditorSaveMapModal { Window: var window } when !asked:
                    window.FileName = "Twice";
                    screen.HotKey(ButtonHotKey.Okay);
                    break;

                case EditorSaveMapModal:
                    screen.HotKey(ButtonHotKey.Cancel);
                    break;

                case MessageBoxModal box:
                    asked = true;
                    box.HotKey(ButtonHotKey.Cancel);
                    break;
            }
        };

        Assert.False(scene.SaveMapToFile());
        Assert.True(asked, "the overwrite question was never asked");
        Assert.Equal("not a map", File.ReadAllText(path));
    }

    /// <summary>Opens the editor on a fresh grass map, or reports the missing data by returning <see langword="null"/>.</summary>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene()
    {
        if (_assets is null)
        {
            return null;
        }

        var frame = new ScreenBuffer(FrameSize, new Palette());
        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateNew(frame, _assets, fonts, MapSize, out _);
    }
}
