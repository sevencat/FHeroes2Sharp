using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The magnifier of the editor's system row, <c>eventViewWorld()</c> (<c>editor_interface.cpp:2183-2186</c>):
/// the click opens the View World window over the map being edited, the window covers the whole frame, and the
/// exit button brings the editor back with the zoom level the window was left at.
/// </summary>
/// <remarks>
/// 参考把窗口当阻塞循环开在编辑器里，端口走模态。这条用例从面板最右下那一格（系统排的第一颗，放大镜）按下去，
/// 假宿主在模态里画一帧、点退出，于是整条链——世界现装、缓存、窗口、关闭——都走了一遍，还留下一档缩放记住与否的证据。
/// </remarks>
public sealed class EditorViewWorldTests
{
    private const int MapSize = 36;

    [Fact]
    public void ClickingTheMagnifierOpensTheViewWorldAndTheExitButtonClosesIt()
    {
        if (!TryCreateAssets(out AssetBundle assets, out GameFonts fonts))
        {
            return;
        }

        AssetBundle? previousAssets = GameAssets.Current;

        try
        {
            GameAssets.Current = assets;

            var frame = new ScreenBuffer(new Size(640, 480), new Palette());
            EditorScene? scene = EditorScene.CreateFromMap(frame, assets, fonts, EmptyMap(), out string? error);

            if (scene is null)
            {
                Assert.NotNull(error);

                return;
            }

            scene.SetCursor(new Point(0, 0));
            scene.DrawFrame(default);
            byte[,] editorFrame = Copy(frame);

            ViewWorldModal? opened = null;

            scene.RunModal = modal =>
            {
                ViewWorldModal world = Assert.IsType<ViewWorldModal>(modal);

                // 编辑器不按玩家裁雾，参考开的是整图。
                Assert.Equal(ViewWorldMode.ViewAll, world.Overlay.Window.Mode);

                modal.Draw(frame);

                // 窗口是整屏的：画完之后帧与编辑器自己画的那一帧逐点都不同。
                Assert.True(Differs(frame, editorFrame));

                ClickExit(modal, world.Overlay);

                Assert.True(modal.IsFinished);

                opened = world;
            };

            Click(scene, MagnifierCenter(scene));

            Assert.NotNull(opened);

            // 关掉之后缩放档还在，下一回从这里开。
            Assert.Equal(ZoomLevel.ZoomLevel2, opened.Result);

            // 回到编辑器：世界与缓存都随模态一起丢了，帧又回到打开前的那一帧。
            scene.SetCursor(new Point(0, 0));
            scene.DrawFrame(default);
            Assert.False(Differs(frame, editorFrame));
        }
        finally
        {
            GameAssets.Current = previousAssets;
        }
    }

    [Fact]
    public void TheZoomLevelTheWindowWasLeftAtIsUsedNextTime()
    {
        if (!TryCreateAssets(out AssetBundle assets, out GameFonts fonts))
        {
            return;
        }

        AssetBundle? previousAssets = GameAssets.Current;

        try
        {
            GameAssets.Current = assets;

            var frame = new ScreenBuffer(new Size(640, 480), new Palette());
            EditorScene? scene = EditorScene.CreateFromMap(frame, assets, fonts, EmptyMap(), out string? error);

            if (scene is null)
            {
                Assert.NotNull(error);

                return;
            }

            ZoomLevel? first = null;
            ZoomLevel? second = null;

            // 第一回：滚一格换档，再点退出。
            scene.RunModal = modal =>
            {
                ViewWorldModal world = Assert.IsType<ViewWorldModal>(modal);

                // 普通模式只有三档（1:4、1:6、1:12），从默认的 1:12 再放大已经没有档位，缩小一档才能变。
                world.Overlay.Wheel(-1);
                ClickExit(modal, world.Overlay);

                first = world.Result;
            };

            Click(scene, MagnifierCenter(scene));

            Assert.NotNull(first);
            Assert.NotEqual(ZoomLevel.ZoomLevel2, first);

            // 第二回：窗口从上一回离开的那一档开出来。
            scene.RunModal = modal =>
            {
                ViewWorldModal world = Assert.IsType<ViewWorldModal>(modal);

                Assert.Equal(first, world.Overlay.Window.Zoom.ZoomLevel);

                ClickExit(modal, world.Overlay);

                second = world.Result;
            };

            Click(scene, MagnifierCenter(scene));

            Assert.Equal(first, second);
        }
        finally
        {
            GameAssets.Current = previousAssets;
        }
    }

    /// <summary>Loads the game data and fonts, or reports that the tests cannot run here.</summary>
    /// <param name="assets">The opened bundle.</param>
    /// <param name="fonts">The loaded fonts.</param>
    /// <returns>Whether the data was found.</returns>
    private static bool TryCreateAssets(out AssetBundle assets, out GameFonts fonts)
    {
        assets = null!;
        fonts = null!;

        if (AssetLocator.Resolve() is not { } directory)
        {
            return false;
        }

        AssetBundle opened = AssetBundle.Open(directory);
        assets = opened;

        if (GameFonts.TryLoad(opened) is not { } loaded)
        {
            return false;
        }

        fonts = loaded;

        return true;
    }

    /// <summary>An empty grass map, the smallest thing the editor can open.</summary>
    /// <returns>The map.</returns>
    private static Mp2Map EmptyMap() => Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

    /// <summary>Centre of the magnifier, the first cell of the system row at the bottom of the panel.</summary>
    /// <param name="scene">The editor.</param>
    /// <returns>The point to click.</returns>
    private static Point MagnifierCenter(EditorScene scene)
    {
        Rect panel = scene.PanelArea;
        int top = panel.Y + panel.Height - EditorPanel.SystemHeight;

        return new Point(panel.X + (EditorPanel.ButtonWidth / 2), top + (EditorPanel.ButtonHeight / 2));
    }

    /// <summary>Presses and releases the left button on a point.</summary>
    /// <param name="scene">The editor.</param>
    /// <param name="point">Where to click.</param>
    private static void Click(EditorScene scene, Point point)
    {
        scene.SetCursor(point);
        scene.SetLeftButton(true);
        scene.SetLeftButton(false);
    }

    /// <summary>Clicks the exit button of the window, the way the reference closes it.</summary>
    /// <param name="modal">The running window.</param>
    /// <param name="overlay">The window it drives.</param>
    private static void ClickExit(IModalScreen modal, ViewWorldOverlay overlay)
    {
        Rect exit = overlay.Window.ExitButtonArea;

        modal.SetCursor(new Point(exit.X + (exit.Width / 2), exit.Y + (exit.Height / 2)));
        modal.SetLeftButton(true);
        modal.SetLeftButton(false);
    }

    /// <summary>Snapshots the index bytes of a frame.</summary>
    /// <param name="frame">The frame to copy.</param>
    /// <returns>A copy indexed by <c>[x, y]</c>.</returns>
    private static byte[,] Copy(ScreenBuffer frame)
    {
        Size size = frame.Size;
        var copy = new byte[size.Width, size.Height];

        for (int y = 0; y < size.Height; ++y)
        {
            for (int x = 0; x < size.Width; ++x)
            {
                copy[x, y] = frame.Pixels[x, y];
            }
        }

        return copy;
    }

    /// <summary>Whether any point of the frame differs from a snapshot.</summary>
    /// <param name="frame">The frame to compare.</param>
    /// <param name="snapshot">The snapshot taken by <see cref="Copy"/>.</param>
    /// <returns><see langword="true"/> when at least one point differs.</returns>
    private static bool Differs(ScreenBuffer frame, byte[,] snapshot)
    {
        Size size = frame.Size;

        for (int y = 0; y < size.Height; ++y)
        {
            for (int x = 0; x < size.Width; ++x)
            {
                if (frame.Pixels[x, y] != snapshot[x, y])
                {
                    return true;
                }
            }
        }

        return false;
    }
}
