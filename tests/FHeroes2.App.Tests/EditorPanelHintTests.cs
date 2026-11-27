using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The right button of the panel: the six system buttons answer with a hint, the map keeps the object popup
/// (<c>editor_interface_panel.cpp:1461-1479</c>).
/// </summary>
/// <remarks>
/// 消息框的样式框（<c>FrameBox</c>）走 <see cref="GameAssets.Current"/> 这个全局归档，和 UI 侧的消息框测试一样，
/// 用例自己换上去、跑完换回来。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorPanelHintTests : IDisposable
{
    private const int MapSize = 36;

    private readonly AssetBundle? _previousAssets;

    /// <summary>Remembers the archive of the process so the test can put it back.</summary>
    public EditorPanelHintTests() => _previousAssets = GameAssets.Current;

    /// <inheritdoc />
    public void Dispose() => GameAssets.Current = _previousAssets;

    /// <summary>Number of system buttons the panel draws, three per row and two rows.</summary>
    private const int SystemButtonCount = 6;

    /// <summary>Width of one button of the bay, the 48x36 frames of <c>EDITBTNS.ICN</c>.</summary>
    private const int ButtonWidth = 48;

    /// <summary>Height of one button of the bay.</summary>
    private const int ButtonHeight = 36;

    [Fact]
    public void RightClickingASystemButtonAsksForItsHint()
    {
        // 归档要活到测试结束：场景与窗口都按需读数，提前关掉文件会炸在 SystemButtonArtwork 上。
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory
            ? AssetBundle.Open(directory)
            : null;

        if (assets is null || GameFonts.TryLoad(assets) is not { } fonts)
        {
            return;
        }

        GameAssets.Current = assets;

        EditorScene? scene = CreateScene(assets, fonts);

        if (scene is null)
        {
            return;
        }

        int boxes = 0;
        scene.RunModal = _ => ++boxes;

        for (int index = 0; index < SystemButtonCount; ++index)
        {
            scene.SetCursor(SystemButtonCentre(scene, index));
            scene.SetRightButton(true);
            scene.SetRightButton(false);

            Assert.Equal(index + 1, boxes);
        }
    }

    [Fact]
    public void RightClickingASystemButtonWithoutFontsOpensNothing()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory
            ? AssetBundle.Open(directory)
            : null;

        var frame = new ScreenBuffer(new Size(640, 480), new Palette());
        EditorScene? scene = EditorScene.CreateNew(frame, assets, fonts: null, MapSize, out _);

        if (scene is null)
        {
            return;
        }

        bool asked = false;
        scene.RunModal = _ => asked = true;

        scene.SetCursor(SystemButtonCentre(scene, 0));
        scene.SetRightButton(true);
        scene.SetRightButton(false);

        // 没有字体就造不出箱子：提示只在日志里留一行，按钮也不会把这一下当成平移。
        Assert.False(asked);
    }

    /// <summary>Opens the editor on a new map, the way the host does, without SDL.</summary>
    private static EditorScene? CreateScene(AssetBundle assets, GameFonts fonts) =>
        EditorScene.CreateNew(
            new ScreenBuffer(new Size(640, 480), new Palette()), assets, fonts, MapSize, out _);

    /// <summary>
    /// Centre of a system button in logical pixels: the two rows sit at the bottom of the panel bay, three
    /// buttons each, the layout <c>EditorPanel.HitTest</c> reads.
    /// </summary>
    private static Point SystemButtonCentre(EditorScene scene, int index)
    {
        Rect panel = scene.PanelArea;

        return new Point(
            panel.X + ((index % 3) * ButtonWidth) + (ButtonWidth / 2),
            panel.Y + panel.Height - (2 * ButtonHeight) + ((index / 3) * ButtonHeight) + (ButtonHeight / 2));
    }
}
