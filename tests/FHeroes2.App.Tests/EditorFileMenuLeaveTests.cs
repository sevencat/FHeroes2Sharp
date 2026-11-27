using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The two ways out of the editor's file menu, <c>processEditorExitEvent()</c> and the Main Menu button
/// (<c>editor_interface.cpp:1393-1401</c>, <c>:2127-2136</c>): each asks first and only then leaves.
/// </summary>
/// <remarks>
/// 文件菜单本身由宿主的 <see cref="EditorScene.RunModal"/> 跑，用例把它换成「直接返回某个结果」的桩，于是
/// <see cref="EditorScene.OpenFileMenu"/> 的分支与随后的确认框都能单测。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorFileMenuLeaveTests : IDisposable
{
    private const int MapSize = 36;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    /// <summary>Publishes the archives the confirmation box draws its buttons from.</summary>
    public EditorFileMenuLeaveTests()
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
    }

    [Fact]
    public void QuitAsksAndThenLeavesForTheQuitRequest()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        EditorLeaveRequest? left = null;
        string? asked = null;
        bool confirmShown = false;

        scene.RunModal = screen =>
        {
            if (screen is MessageBoxModal box)
            {
                confirmShown = true;
                asked = box.Box.Header;
                box.HotKey(ButtonHotKey.Okay);

                return;
            }

            // 第一次是文件菜单：直接交出 Quit。
            screen.HotKey(ButtonHotKey.Cancel);
        };

        scene.LeaveEditor = request => left = request;

        // 文件菜单的 Quit：菜单本身由宿主跑，这里用一个只认出 Quit 的替代。
        InvokeFileMenu(scene, EditorFileResult.Quit);

        Assert.True(confirmShown, "the quit question was never shown");
        Assert.Equal(EditorScene.QuitTitle, asked);
        Assert.Equal(EditorLeaveRequest.Quit, left);
    }

    [Fact]
    public void SayingNoToQuitKeepsTheEditorOpen()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        bool left = false;

        scene.RunModal = screen =>
        {
            if (screen is MessageBoxModal box)
            {
                // 这扇框是 YES/NO，CANCEL 就是 NO。
                box.HotKey(ButtonHotKey.Cancel);
                return;
            }
        };

        scene.LeaveEditor = _ => left = true;

        InvokeFileMenu(scene, EditorFileResult.Quit);

        Assert.False(left, "the editor was left although the author said no");
    }

    [Fact]
    public void MainMenuAsksItsOwnQuestion()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        EditorLeaveRequest? left = null;
        string? body = null;

        scene.RunModal = screen =>
        {
            if (screen is MessageBoxModal box)
            {
                body = box.Box.Header;
                box.HotKey(ButtonHotKey.Okay);
            }
        };

        scene.LeaveEditor = request => left = request;

        InvokeFileMenu(scene, EditorFileResult.MainMenu);

        Assert.Equal(EditorScene.MainMenuTitle, body);
        Assert.Equal(EditorLeaveRequest.MainMenu, left);
    }

    [Fact]
    public void NewMapAsksBeforeAskingTheHostToLeave()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        EditorLeaveRequest? left = null;
        string? body = null;

        scene.RunModal = screen =>
        {
            if (screen is MessageBoxModal box)
            {
                body = box.Box.Body;
                box.HotKey(ButtonHotKey.Okay);
            }
        };

        scene.LeaveEditor = request => left = request;

        InvokeFileMenu(scene, EditorFileResult.NewMap);

        Assert.Equal(EditorScene.NewMapQuestion, body);
        Assert.Equal(EditorLeaveRequest.NewMap, left);
    }

    [Fact]
    public void LoadMapAsksBeforeAskingTheHostToLeave()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        EditorLeaveRequest? left = null;
        string? body = null;

        scene.RunModal = screen =>
        {
            if (screen is MessageBoxModal box)
            {
                body = box.Box.Body;
                box.HotKey(ButtonHotKey.Okay);
            }
        };

        scene.LeaveEditor = request => left = request;

        InvokeFileMenu(scene, EditorFileResult.LoadMap);

        Assert.Equal(EditorScene.LoadMapQuestion, body);
        Assert.Equal(EditorLeaveRequest.LoadMap, left);
    }

    [Fact]
    public void SayingNoToNewMapKeepsTheEditorOpen()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        bool left = false;

        scene.RunModal = screen =>
        {
            if (screen is MessageBoxModal box)
            {
                box.HotKey(ButtonHotKey.Cancel);
            }
        };

        scene.LeaveEditor = _ => left = true;

        InvokeFileMenu(scene, EditorFileResult.NewMap);

        Assert.False(left);
    }

    [Fact]
    public void StartMapAsksToSaveFirstWhenTheMapHasNoFile()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        bool left = false;
        string? asked = null;

        // 新图没有 SavePath：先问「要不要保存」，答 NO 就该放弃整支。
        scene.RunModal = screen =>
        {
            if (screen is MessageBoxModal box)
            {
                asked ??= box.Box.Body;
                box.HotKey(ButtonHotKey.Cancel);
            }
        };

        scene.LeaveEditor = _ => left = true;

        InvokeFileMenu(scene, EditorFileResult.StartMap);

        Assert.Equal(EditorScene.UnsavedChangesQuestion, asked);
        Assert.False(left, "the map was started although the author said not to save it");
    }

    [Fact]
    public void APlayableSavedMapIsHandedToTheHost()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        EditorLeaveRequest? left = null;
        string? confirmed = null;

        // 已经有文件：跳过「要不要保存」，直接问「要离开编辑器开始这张图吗」。
        string path = Path.Combine(Path.GetTempPath(), "fh2-start-" + Guid.NewGuid().ToString("N") + ".mp2");

        try
        {
            Assert.True(scene.Edits.SaveAs(path, out string? error), error);

            scene.RunModal = screen =>
            {
                if (screen is MessageBoxModal box)
                {
                    confirmed ??= box.Box.Body;
                    box.HotKey(ButtonHotKey.Okay);
                }
            };

            scene.LeaveEditor = request => left = request;

            InvokeFileMenu(scene, EditorFileResult.StartMap);

            Assert.Equal(EditorScene.StartMapQuestion, confirmed);
            Assert.Equal(EditorLeaveRequest.StartMap, left);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AMapWithoutHumanPlayersCannotBeStarted()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), "fh2-start-" + Guid.NewGuid().ToString("N") + ".mp2");
        bool left = false;
        string? shown = null;

        try
        {
            Assert.True(scene.Edits.SaveAs(path, out string? error), error);

            // 一张只有电脑玩家的图：参考弹「Unplayable Map」并放弃。
            scene.Edits.ApplyMapInfo(scene.Map.Info with { HumanPlayers = PlayerColor.None });

            scene.RunModal = screen =>
            {
                if (screen is MessageBoxModal box)
                {
                    shown = box.Box.Body;
                    box.HotKey(ButtonHotKey.Okay);
                }
            };

            scene.LeaveEditor = _ => left = true;

            InvokeFileMenu(scene, EditorFileResult.StartMap);

            Assert.Equal(EditorScene.UnplayableMapMessage, shown);
            Assert.False(left);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WithoutAFramePumpTheEditorStaysOpen()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        bool left = false;

        scene.RunModal = null;
        scene.LeaveEditor = _ => left = true;

        InvokeFileMenu(scene, EditorFileResult.Quit);

        Assert.False(left, "a confirmation that cannot be asked must not leave the editor");
    }

    /// <summary>
    /// Runs the file menu branch of <see cref="EditorScene.OpenFileMenu"/> with the result the stub returns.
    /// </summary>
    /// <param name="scene">The scene to drive.</param>
    /// <param name="result">What the file menu answers; the stub always answers it.</param>
    private static void InvokeFileMenu(EditorScene scene, EditorFileResult result) =>
        scene.HandleFileMenuResult(result);

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
