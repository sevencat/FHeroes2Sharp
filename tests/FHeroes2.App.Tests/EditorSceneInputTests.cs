using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Input;

namespace FHeroes2.App.Tests;

/// <summary>
/// Serializes the tests that drive the process wide <see cref="LocalEvent.Current"/>: the editor reads
/// its input from that instance once per frame (<c>EditorScene.Update</c>), exactly like the host does,
/// so two tests filling it at the same time would read each other's pointer.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalEventCollection
{
    /// <summary>Name of the collection.</summary>
    public const string Name = "App editor input";
}

/// <summary>
/// The feel of the editor: what a click, a drag, the wheel, the edge and the minimap do to the map.
/// </summary>
/// <remarks>
/// <para>
/// The scene reads <see cref="LocalEvent.Current"/> in <see cref="EditorScene.Update"/>, the way
/// <c>editor_interface.cpp</c> reads <c>LocalEvent::Get()</c>, so these tests fill that instance and let
/// the scene dispatch: <c>OnMouseMotion</c> / <c>OnMouseButton</c> / <c>OnMouseWheel</c> are what the
/// SDL engine would call, and the assertions are on the state the panel shows.
/// </para>
/// <para>
/// Every test needs the archives, because the scene refuses to open a map without
/// <c>GROUND32.TIL</c>; a machine without game data skips them the way the UI tests do.
/// </para>
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorSceneInputTests
{
    private const int MapSize = 36;

    [Fact]
    public void ThePanelBayIsTheOneOfTheFrame()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        var frame = new MapFrame(scene.FrameSize, null);

        Assert.Equal(frame.Viewport, scene.ViewportArea);
        Assert.Equal(EditorScene.PanelBayOf(scene.FrameSize), scene.PanelArea);

        // The two numbers the mouth feel is pinned on: a press travels ten pixels before it pans, and
        // an edge step is eight pixels every twenty milliseconds, the SCROLL_DELAY of the reference.
        Assert.Equal(10, EditorScene.DragThreshold);
        Assert.Equal(8, EditorScene.EdgeScrollStep);
        Assert.Equal(0.02, EditorScene.EdgeScrollInterval, 6);
    }

    [Fact]
    public void AClickThatGoesDownAndUpInOneFramePaintsOneTile()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        scene.Edits.SetBrush(MapGround.Dirt);
        LocalEvent events = LocalEvent.Current;
        Point pointer = At(scene, 40, 40);

        Frame(events, scene, fill: e =>
        {
            e.OnMouseMotion(pointer);
            e.OnMouseButton(true, MouseButtonType.Left, pointer);
            e.OnMouseButton(false, MouseButtonType.Left, pointer);
        });

        Assert.Equal(1, scene.Edits.UndoDepth);
        Assert.Equal(MapGround.Dirt, GroundAt(scene, 40, 40));
    }

    [Fact]
    public void AStrokeKeepsTheTilesItCrossedAsOneUndoStep()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        scene.Edits.SetBrush(MapGround.Dirt);
        LocalEvent events = LocalEvent.Current;
        Point start = At(scene, 40, 40);
        Point end = At(scene, 40 + 3 * 32, 40);

        Frame(events, scene, e =>
        {
            e.OnMouseMotion(start);
            e.OnMouseButton(true, MouseButtonType.Left, start);
        });

        for (int column = 1; column <= 3; ++column)
        {
            Point step = At(scene, 40 + column * 32, 40);

            Frame(events, scene, e => e.OnMouseMotion(step));
        }

        Frame(events, scene, e => e.OnMouseButton(false, MouseButtonType.Left, end));

        Assert.Equal(1, scene.Edits.UndoDepth);

        for (int column = 0; column <= 3; ++column)
        {
            Assert.Equal(MapGround.Dirt, GroundAt(scene, 40 + column * 32, 40));
        }
    }

    [Fact]
    public void AMoveWithinTheThresholdDoesNotPan()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        LocalEvent events = LocalEvent.Current;
        Point start = At(scene, 100, 100);
        Point moved = At(scene, 110, 110);
        Point before = scene.View.Scroll;

        Frame(events, scene, e =>
        {
            e.OnMouseMotion(start);
            e.OnMouseButton(true, MouseButtonType.Right, start);
        });

        Frame(events, scene, e => e.OnMouseMotion(moved));

        Assert.Equal(before, scene.View.Scroll);

        Frame(events, scene, e => e.OnMouseButton(false, MouseButtonType.Right, moved));
    }

    [Fact]
    public void AMovePastTheThresholdPansByTheWholeOffset()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        LocalEvent events = LocalEvent.Current;
        Point start = At(scene, 100, 100);

        // One pixel past the threshold on the X axis, the Y axis still inside it: the drag starts from
        // the press, so the whole offset is part of the first shift.
        Point moved = At(scene, 111, 110);
        Point before = scene.View.Scroll;

        Frame(events, scene, e =>
        {
            e.OnMouseMotion(start);
            e.OnMouseButton(true, MouseButtonType.Right, start);
        });

        Frame(events, scene, e => e.OnMouseMotion(moved));
        Frame(events, scene, e => e.OnMouseButton(false, MouseButtonType.Right, moved));

        Assert.Equal(new Point(before.X - 11, before.Y - 10), scene.View.Scroll);
        Assert.Equal(0, scene.Edits.UndoDepth);
    }

    [Fact]
    public void TheEdgeScrollsOneStepPerInterval()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        LocalEvent events = LocalEvent.Current;
        Point edge = At(scene, 2, 100);
        Point before = scene.View.Scroll;

        // A frame that took no time scrolls nothing, however long the pointer has been at the edge.
        Frame(events, scene, e => e.OnMouseMotion(edge), elapsedSeconds: 0.0);
        Assert.Equal(before, scene.View.Scroll);

        // One whole interval is one step, and only one.
        Frame(events, scene, _ => { }, elapsedSeconds: EditorScene.EdgeScrollInterval);
        Assert.Equal(new Point(before.X - EditorScene.EdgeScrollStep, before.Y), scene.View.Scroll);

        // Less than an interval waits, the next one adds up to a whole step.
        Frame(events, scene, _ => { }, elapsedSeconds: EditorScene.EdgeScrollInterval / 2.0);
        Assert.Equal(new Point(before.X - EditorScene.EdgeScrollStep, before.Y), scene.View.Scroll);

        Frame(events, scene, _ => { }, elapsedSeconds: EditorScene.EdgeScrollInterval / 2.0);
        Assert.Equal(new Point(before.X - 2 * EditorScene.EdgeScrollStep, before.Y), scene.View.Scroll);
    }

    [Fact]
    public void ALongFrameScrollsAtMostFourSteps()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        LocalEvent events = LocalEvent.Current;

        Frame(events, scene, e => e.OnMouseMotion(At(scene, 2, 100)));
        Point scroll = scene.View.Scroll;

        // A stalled main loop hands in a whole second at once: four steps, not fifty.
        Frame(events, scene, _ => { }, elapsedSeconds: 1.0);
        Assert.Equal(scroll.X - 4 * EditorScene.EdgeScrollStep, scene.View.Scroll.X);

        // What the cap did not spend is cut down to one interval instead of being flushed in a burst;
        // that leftover, plus the interval this frame brings, is two steps and then the clock is empty
        // again.
        Frame(events, scene, _ => { }, elapsedSeconds: EditorScene.EdgeScrollInterval);
        Assert.Equal(scroll.X - 6 * EditorScene.EdgeScrollStep, scene.View.Scroll.X);

        Frame(events, scene, _ => { }, elapsedSeconds: 0.0);
        Assert.Equal(scroll.X - 6 * EditorScene.EdgeScrollStep, scene.View.Scroll.X);
    }

    [Fact]
    public void TheWheelScrollsWholeTiles()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        LocalEvent events = LocalEvent.Current;
        Point scroll = scene.View.Scroll;

        Frame(events, scene, e => e.OnMouseWheel(new Point(0, -1)));
        Assert.Equal(new Point(scroll.X, scroll.Y + scene.View.Tiles.TileHeight), scene.View.Scroll);

        Frame(events, scene, e => e.OnMouseWheel(new Point(0, 1)));
        Assert.Equal(scroll, scene.View.Scroll);
    }

    [Fact]
    public void APressOnTheRadarCentresTheMapAndOpensNoStroke()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        LocalEvent events = LocalEvent.Current;
        Rect radar = new MapFrame(scene.FrameSize, null).Radar;
        Point corner = new(radar.X + 4, radar.Y + 4);
        Point before = scene.View.Scroll;

        Frame(events, scene, e =>
        {
            e.OnMouseMotion(corner);
            e.OnMouseButton(true, MouseButtonType.Left, corner);
        });

        Frame(events, scene, e => e.OnMouseButton(false, MouseButtonType.Left, corner));

        Assert.NotEqual(before, scene.View.Scroll);
        Assert.Equal(0, scene.Edits.UndoDepth);
    }

    [Fact]
    public void TheRightButtonTakesTheGroundUnderThePointer()
    {
        using AssetBundle? assets = OpenAssets();

        if (Create(assets) is not { } scene)
        {
            return;
        }

        scene.Edits.SetBrush(MapGround.Dirt);
        LocalEvent events = LocalEvent.Current;
        Point pointer = At(scene, 40, 40);

        Frame(events, scene, e =>
        {
            e.OnMouseMotion(pointer);
            e.OnMouseButton(true, MouseButtonType.Left, pointer);
        });

        Frame(events, scene, e => e.OnMouseButton(false, MouseButtonType.Left, pointer));
        scene.Edits.SetBrush(MapGround.Grass);

        // The press shows the popup of the object under the pointer, the release takes the brush: a
        // click without a drag is the eyedropper of the editor.
        Frame(events, scene, e =>
        {
            e.OnMouseMotion(pointer);
            e.OnMouseButton(true, MouseButtonType.Right, pointer);
        });

        Frame(events, scene, e => e.OnMouseButton(false, MouseButtonType.Right, pointer));

        Assert.Equal(MapGround.Dirt, scene.Edits.Brush);
    }

    private static AssetBundle? OpenAssets() => AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private static EditorScene? Create(AssetBundle? assets) =>
        EditorScene.CreateNew(
            new ScreenBuffer(new Size(640, 480), new Palette()), assets, fonts: null, MapSize, out _);

    /// <summary>A pointer position in logical pixels from a point in viewport pixels.</summary>
    /// <param name="scene">Scene whose viewport anchors the point.</param>
    /// <param name="x">Pixels from the left edge of the viewport.</param>
    /// <param name="y">Pixels from the top edge of the viewport.</param>
    private static Point At(EditorScene scene, int x, int y) =>
        new(scene.ViewportArea.X + x, scene.ViewportArea.Y + y);

    /// <summary>The ground of the tile a viewport pixel lands on.</summary>
    /// <param name="scene">Scene holding the map.</param>
    /// <param name="x">Pixels from the left edge of the viewport.</param>
    /// <param name="y">Pixels from the top edge of the viewport.</param>
    private static MapGround GroundAt(EditorScene scene, int x, int y)
    {
        Point tile = scene.View.TileAt(new Point(x, y));

        return MapGrounds.FromTerrainImageIndex(scene.Map.Tile(tile.X, tile.Y).TerrainImageIndex);
    }

    /// <summary>
    /// Runs one frame the way the host does: reset the one-shot states, let the test fill in what the
    /// engine would report, then hand the frame to the scene.
    /// </summary>
    /// <param name="events">The process wide event state.</param>
    /// <param name="scene">Scene to drive.</param>
    /// <param name="fill">What the platform engine would report in this frame.</param>
    /// <param name="elapsedSeconds">
    /// Frame time; <see langword="null"/> dispatches the events only, which is what a frame without a
    /// clock does. A value goes through <see cref="EditorScene.Update"/> so the edge scroll advances.
    /// </param>
    private static void Frame(
        LocalEvent events, EditorScene scene, Action<LocalEvent> fill, double? elapsedSeconds = null)
    {
        events.BeginFrame();
        fill(events);

        if (elapsedSeconds is { } seconds)
        {
            scene.Update(seconds);
        }
        else
        {
            scene.ProcessEvents(events);
        }
    }
}
