using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The sphinx editor of the map editor, <c>editor_interface.cpp:2438-2452</c>: a click opens the window, its four kinds
/// of sub-window write into the metadata, and only an accepted window turns into a table entry.
/// </summary>
[Collection(LocalEventCollection.Name)]
public sealed class EditorSphinxTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 71;
    private const int ObjectX = 22;
    private const int ObjectY = 16;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the window reads its artwork from.</summary>
    public EditorSphinxTests()
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
    public void AClickOnASphinxOpensItsWindow()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        bool opened = false;

        scene.RunModal = screen =>
        {
            Assert.IsType<EditorSphinxModal>(screen);

            opened = true;
            screen.HotKey(ButtonHotKey.Cancel);
        };

        Assert.True(scene.EditSphinx(new Point(ObjectX, ObjectY)));
        Assert.True(opened, "the sphinx window did not open");
        Assert.Equal(new Point(ObjectX, ObjectY), scene.LastObjectEditorTile);

        // 取消什么都不写。
        Assert.False(scene.Metadata.Sphinxes.ContainsKey(ObjectUid));
    }

    [Fact]
    public void TheRiddleTypedIntoTheBoxLandsOnTheMetadata()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var sphinx = (EditorSphinxModal)screen;

            // 点谜题区开输入框（参考 :314-322），正文是「Riddle:」。
            Click(sphinx, sphinx.Window.RiddleArea);

            Assert.True(sphinx.HasInnerWindow, "the riddle box did not open");

            var input = Assert.IsType<InputStringModal>(sphinx.TopWindow);
            Assert.Equal(EditorSphinxModal.RiddleBody, input.Window.Dialog.Body);

            input.PressKey(Key.Q, KeyModifier.None);
            ClickInput(sphinx, input);

            Pump(sphinx);

            Assert.False(sphinx.HasInnerWindow, "the riddle box did not close");
            Assert.Equal("q", sphinx.Window.Riddle);

            // 接受整扇窗。
            Click(sphinx, sphinx.Window.Okay.Area);
            Pump(sphinx);
        };

        Assert.True(scene.EditSphinx(new Point(ObjectX, ObjectY)));

        Assert.True(scene.Metadata.Sphinxes.TryGetValue(ObjectUid, out SphinxMetadata? kept));
        Assert.Equal("q", kept.Riddle);
    }

    [Fact]
    public void AnAddedAnswerLandsOnTheMetadata()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var sphinx = (EditorSphinxModal)screen;

            Click(sphinx, sphinx.Window.AddButton.Area);

            Assert.True(sphinx.HasInnerWindow, "the answer box did not open");

            var input = Assert.IsType<InputStringModal>(sphinx.TopWindow);
            Assert.Equal(EditorSphinxModal.AnswerBody, input.Window.Dialog.Body);

            input.PressKey(Key.Z, KeyModifier.None);
            ClickInput(sphinx, input);
            Pump(sphinx);

            Click(sphinx, sphinx.Window.Okay.Area);
            Pump(sphinx);
        };

        Assert.True(scene.EditSphinx(new Point(ObjectX, ObjectY)));

        Assert.True(scene.Metadata.Sphinxes.TryGetValue(ObjectUid, out SphinxMetadata? kept));
        Assert.Single(kept.Answers);
        Assert.Equal("z", kept.Answers[0]);
    }

    [Fact]
    public void TheArtifactPickerWritesTheReward()
    {
        EditorScene? scene = CreateScene();

        if (scene is null)
        {
            return;
        }

        scene.RunModal = screen =>
        {
            var sphinx = (EditorSphinxModal)screen;

            // 点神器格开选择窗（参考 :262-289）。
            Click(sphinx, sphinx.Window.ArtifactArea);

            var picker = Assert.IsType<ArtifactSelectModal>(sphinx.TopWindow);

            // 选择窗自己读 LocalEvent。列表的 CurrentId 初值就是 0，所以「点当前行」在第一次点击时已经成立
            // （双击分支），一下即可确定；这里点第二行的两下，免得依赖那个初值。
            int rowHeight = picker.Window.ItemsArea.Height / picker.Window.VisibleItemCount;
            Point row = new(picker.Window.ItemsArea.X + 20,
                picker.Window.ItemsArea.Y + rowHeight + (rowHeight / 2));

            ClickRaw(sphinx, row);
            ClickRaw(sphinx, row);

            Assert.Null(sphinx.TopWindow);

            ArtifactType chosen = sphinx.Window.Artifact;

            Assert.NotEqual(ArtifactType.Unknown, chosen);

            Click(sphinx, sphinx.Window.Okay.Area);
            Pump(sphinx);
        };

        Assert.True(scene.EditSphinx(new Point(ObjectX, ObjectY)));

        Assert.True(scene.Metadata.Sphinxes.TryGetValue(ObjectUid, out SphinxMetadata? kept));
        Assert.NotEqual((int)ArtifactType.Unknown, kept.Artifact);
    }

    [Fact]
    public void APlainTileIsNotASphinx()
    {
        EditorScene? scene = CreateScene(Mp2ObjectType.Mine);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on a mine");

        Assert.False(scene.EditSphinx(new Point(ObjectX, ObjectY)));
    }

    /// <summary>Opens the editor on a map with one object on (22, 16).</summary>
    /// <param name="type">The object type; a sphinx when omitted.</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(Mp2ObjectType type = Mp2ObjectType.ActionSphinx)
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
    private void Click(IModalScreen modal, Rect area) =>
        ClickRaw(modal, new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2)));

    /// <summary>Feeds one left click on a point through the frame's event state.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="point">The point to click.</param>
    private void ClickRaw(IModalScreen modal, Point point)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseMotion(point);
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>
    /// Clicks OKAY of an input box that takes the cursor and the button separately, which is what
    /// <see cref="InputStringModal"/> does.
    /// </summary>
    /// <param name="owner">The sphinx window, whose input box is on top.</param>
    /// <param name="input">The input box.</param>
    private static void ClickInput(EditorSphinxModal owner, InputStringModal input)
    {
        Point okay = input.Window.OkayArea.Center;

        owner.SetCursor(okay);
        owner.SetLeftButton(true);
        owner.SetLeftButton(false);
    }

    /// <summary>Runs one modal frame without input.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }
}
