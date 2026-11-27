using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The AUTO PLAYTEST branch of the editor's file menu, <c>_runAutoPlaytest()</c> plus
/// <c>openMapAutoPlayTest()</c> (<c>editor_interface.cpp:2144-2150</c>, <c>game_auto_playtest.cpp:386-660</c>).
/// </summary>
/// <remarks>
/// 窗口由宿主的 <see cref="EditorScene.RunModal"/> 跑，用例把它换成「认出自动试玩窗就按某个键」的桩，于是分支、
/// 设置回写与试玩本身都能单测。试玩真的跑一局，所以天数上限压到 1：36×36 的空图上六个王国都没有城与英雄，
/// 开局当天就全部出局，统计是六条 LOSER、没有一轮打满上限。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorAutoPlaytestTests : IDisposable
{
    private const int MapSize = 36;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    /// <summary>Publishes the archives the window draws from.</summary>
    public EditorAutoPlaytestTests()
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
    public void OkayRunsThePlaytestShowsTheTallyAndKeepsTheSettings()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.PlaytestSettings = new PlaytestSettings(maxPlaythroughs: 1, maxDays: 1);

        AutoPlaytestModal? config = null;
        AutoPlaytestResultsModal? results = null;

        scene.RunModal = screen =>
        {
            switch (screen)
            {
                case AutoPlaytestModal modal:
                    config = modal;
                    modal.HotKey(ButtonHotKey.Okay);

                    break;

                case AutoPlaytestResultsModal tally:
                    results = tally;
                    tally.HotKey(ButtonHotKey.Okay);

                    break;

                default:
                    break;
            }
        };

        scene.HandleFileMenuResult(EditorFileResult.AutoPlaytest);

        Assert.NotNull(config);
        Assert.Equal(AutoPlaytestOutcome.Accepted, config!.Outcome);

        PlaytestSummary? summary = scene.LastPlaytestSummary;

        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Count);
        Assert.Equal(0, summary.TimeLimitCount);

        // 空图上六个王国都没有城与英雄：开局当天全部出局，于是没有胜者。
        Assert.Equal(6, summary.Colors.Count);
        Assert.All(summary.Colors, color =>
        {
            Assert.Equal(0, color.Wins);
            Assert.Equal(0, color.Percentage);
        });

        // 参考在 runPlayTest() 的最后一句开结果窗（game_auto_playtest.cpp:348）。
        Assert.NotNull(results);
        Assert.Same(summary, results!.Window.Summary);

        Assert.Equal(1, scene.PlaytestSettings.MaxPlaythroughs);
        Assert.Equal(1, scene.PlaytestSettings.MaxDays);
    }

    [Fact]
    public void CancelRunsNothingAndKeepsTheOldSettings()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        PlaytestSettings settings = new(maxPlaythroughs: 3, maxDays: 20);

        scene.PlaytestSettings = settings;

        AutoPlaytestModal? opened = RunAutoPlaytest(scene, ButtonHotKey.Cancel, out bool resultsShown);

        Assert.NotNull(opened);
        Assert.Equal(AutoPlaytestOutcome.Cancelled, opened!.Outcome);
        Assert.Null(opened.Settings);
        Assert.Null(scene.LastPlaytestSummary);
        Assert.False(resultsShown, "a cancelled playtest must not show a tally");

        // CANCEL 不改设置：再开一次窗还是上次的三个轮、二十天。
        Assert.Same(settings, scene.PlaytestSettings);
    }

    [Fact]
    public void ThePlayersComeFromTheMapHeader()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.PlaytestSettings = new PlaytestSettings(maxPlaythroughs: 1, maxDays: 1);

        AutoPlaytestModal? opened = RunAutoPlaytest(scene, ButtonHotKey.Cancel, out _);

        Assert.NotNull(opened);

        // CreateBlank 的六个王国色全部在局中。
        Assert.Equal(6, opened!.Window.Players.Count);
    }

    /// <summary>Runs the AUTO PLAYTEST branch with a stub that presses <paramref name="hotKey"/>.</summary>
    /// <param name="scene">The scene to drive.</param>
    /// <param name="hotKey">The key the stub presses on the playtest window.</param>
    /// <param name="resultsShown">Whether the tally window was opened.</param>
    /// <returns>The window the branch built, or <see langword="null"/> when it built none.</returns>
    private static AutoPlaytestModal? RunAutoPlaytest(EditorScene scene, ButtonHotKey hotKey, out bool resultsShown)
    {
        AutoPlaytestModal? created = null;
        bool shown = false;

        scene.RunModal = screen =>
        {
            switch (screen)
            {
                case AutoPlaytestModal modal:
                    created = modal;
                    modal.HotKey(hotKey);

                    break;

                case AutoPlaytestResultsModal tally:
                    shown = true;
                    tally.HotKey(ButtonHotKey.Okay);

                    break;

                default:
                    break;
            }
        };

        scene.HandleFileMenuResult(EditorFileResult.AutoPlaytest);

        resultsShown = shown;

        return created;
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
