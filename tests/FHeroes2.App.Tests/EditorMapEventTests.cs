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
/// The adventure map event editor, <c>editor_interface.cpp:2453-2462</c>: a click opens the window, its message and
/// count sub-windows write into the metadata, and only an accepted window turns into a table entry.
/// </summary>
[Collection(LocalEventCollection.Name)]
public sealed class EditorMapEventTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 81;
    private const int ObjectX = 24;
    private const int ObjectY = 18;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the window reads its artwork from.</summary>
    public EditorMapEventTests()
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
    public void AClickOnAnEventOpensItsWindow()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        bool opened = false;

        scene.RunModal = screen =>
        {
            Assert.IsType<EditorEventDetailsModal>(screen);

            opened = true;
            screen.HotKey(ButtonHotKey.Cancel);
        };

        Assert.True(scene.EditMapEvent(new Point(ObjectX, ObjectY)));
        Assert.True(opened, "the event window did not open");
        Assert.Equal(new Point(ObjectX, ObjectY), scene.LastObjectEditorTile);

        Assert.False(scene.Metadata.AdventureMapEvents.ContainsKey(ObjectUid));
    }

    [Fact]
    public void TheMessageTypedIntoTheBoxLandsOnTheMetadata()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var details = (EditorEventDetailsModal)screen;

            // 点消息区开输入框（参考 :271-289）。
            Click(details, details.Window.MessageArea);

            Assert.True(details.HasInnerWindow, "the message box did not open");

            var input = Assert.IsType<InputStringModal>(details.TopWindow);
            Assert.Equal(EditorEventDetailsModal.MessageBody, input.Window.Dialog.Body);

            input.PressKey(Key.Q, KeyModifier.None);

            // 多行输入里回车是换行，确认只能点 OKAY；输入框吃光标 + 左键。
            Point okay = input.Window.OkayArea.Center;

            details.SetCursor(okay);
            details.SetLeftButton(true);
            details.SetLeftButton(false);

            Pump(details);

            Assert.Equal("q", details.Window.Event.Message);

            Click(details, details.Window.Okay.Area);
            Pump(details);
        };

        Assert.True(scene.EditMapEvent(new Point(ObjectX, ObjectY)));

        Assert.True(scene.Metadata.AdventureMapEvents.TryGetValue(ObjectUid, out AdventureMapEventMetadata? kept));
        Assert.Equal("q", kept.Message);
    }

    [Fact]
    public void ARecurringEventIsWrittenEvenWithoutOtherChanges()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var details = (EditorEventDetailsModal)screen;

            // 勾上「每次经过都触发」——这一下只改元数据、不开窗。
            Click(details, details.Window.RecurringEventArea);

            Click(details, details.Window.Okay.Area);
            Pump(details);
        };

        Assert.True(scene.EditMapEvent(new Point(ObjectX, ObjectY)));

        Assert.True(scene.Metadata.AdventureMapEvents.TryGetValue(ObjectUid, out AdventureMapEventMetadata? kept));
        Assert.True(kept.IsRecurringEvent);
    }

    [Fact]
    public void AnUnchangedEventWritesNothing()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var details = (EditorEventDetailsModal)screen;

            Click(details, details.Window.Okay.Area);
            Pump(details);
        };

        Assert.True(scene.EditMapEvent(new Point(ObjectX, ObjectY)));

        // 打开就确定、什么都没改：参考的 `newEventData != eventMetadata` 不成立，表里不该多一条。
        Assert.False(scene.Metadata.AdventureMapEvents.ContainsKey(ObjectUid));
    }

    [Fact]
    public void APlainTileIsNotAnEvent()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.Sign);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on a sign");

        Assert.False(scene.EditMapEvent(new Point(ObjectX, ObjectY)));
    }

    /// <summary>Opens the editor on a map with one object on (24, 18).</summary>
    /// <param name="type">The object type; an event when omitted.</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(Mp2ObjectType type = Mp2ObjectType.ActionEvent)
    {
        if (_assets is null)
        {
            return null;
        }

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTile(ObjectX, ObjectY, map.Tile(ObjectX, ObjectY) with
        {
            ObjectType = type,
            Level1ObjectUid = ObjectUid,
        });

        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateFromMap(_frame, _assets, fonts, map, out _);
    }

    /// <summary>Feeds one left click on an area through the frame's event state.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(IModalScreen modal, Rect area)
    {
        Point point = area.Center;
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Runs one modal frame without input.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }
}
