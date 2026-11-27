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
/// The owner colour of a capturable object, <c>editor_interface.cpp:2537-2571</c>: a click opens the colour picker, the
/// answer changes the metadata table, and the neutral plate deletes the entry.
/// </summary>
[Collection(LocalEventCollection.Name)]
public sealed class EditorCapturableOwnerTests : IDisposable
{
    private const int MapSize = 36;
    private const uint ObjectUid = 61;
    private const int ObjectX = 20;
    private const int ObjectY = 14;

    private static readonly Size FrameSize = new(640, 480);

    private readonly AssetBundle? _assets = AssetLocator.Resolve() is { } directory
        ? AssetBundle.Open(directory)
        : null;

    private readonly AssetBundle? _previousAssets = GameAssets.Current;

    private readonly ScreenBuffer _frame = new(FrameSize, new Palette());

    /// <summary>Publishes the archives the window reads its artwork from.</summary>
    public EditorCapturableOwnerTests()
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
    public void ChoosingAColourWritesItToTheTable()
    {
        EditorScene? scene = CreateScene(PlayerColor.Blue | PlayerColor.Red);

        if (scene is null)
        {
            return;
        }

        bool opened = false;

        scene.RunModal = screen =>
        {
            opened = true;
            var picker = Assert.IsType<PlayerColorSelectionModal>(screen);

            // 第一格是中立、第二格是蓝、第三格是红：点第三格再确定。
            Point red = picker.Window.Cells[2].Area.Center;

            picker.SetCursor(red);
            picker.SetLeftButton(true);
            picker.SetLeftButton(false);

            Point okay = picker.Window.Buttons[0].Area.Center;

            picker.SetCursor(okay);
            picker.SetLeftButton(true);
            picker.SetLeftButton(false);
        };

        Assert.True(scene.EditCapturableOwner(new Point(ObjectX, ObjectY)));
        Assert.True(opened, "the colour picker did not open");

        Assert.True(scene.Metadata.CapturableObjects.TryGetValue(ObjectUid, out CapturableObjectMetadata? kept));
        Assert.Equal(PlayerColor.Red, kept.OwnerColor);
    }

    [Fact]
    public void TheNeutralPlateDeletesTheEntry()
    {
        EditorScene? scene = CreateScene(PlayerColor.Blue);

        if (scene is null)
        {
            return;
        }

        scene.Metadata.CapturableObjects[ObjectUid] = new CapturableObjectMetadata { OwnerColor = PlayerColor.Blue };

        scene.RunModal = screen =>
        {
            var picker = (PlayerColorSelectionModal)screen;

            Point neutral = picker.Window.Cells[0].Area.Center;

            picker.SetCursor(neutral);
            picker.SetLeftButton(true);
            picker.SetLeftButton(false);

            Point okay = picker.Window.Buttons[0].Area.Center;

            picker.SetCursor(okay);
            picker.SetLeftButton(true);
            picker.SetLeftButton(false);
        };

        Assert.True(scene.EditCapturableOwner(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.CapturableObjects.ContainsKey(ObjectUid));
    }

    [Fact]
    public void CancellingKeepsTheColourTheObjectHad()
    {
        EditorScene? scene = CreateScene(PlayerColor.Blue | PlayerColor.Red);

        if (scene is null)
        {
            return;
        }

        scene.Metadata.CapturableObjects[ObjectUid] = new CapturableObjectMetadata { OwnerColor = PlayerColor.Blue };

        scene.RunModal = screen =>
        {
            var picker = (PlayerColorSelectionModal)screen;

            Point red = picker.Window.Cells[2].Area.Center;

            picker.SetCursor(red);
            picker.SetLeftButton(true);
            picker.SetLeftButton(false);

            Point cancel = picker.Window.Buttons[1].Area.Center;

            picker.SetCursor(cancel);
            picker.SetLeftButton(true);
            picker.SetLeftButton(false);
        };

        Assert.True(scene.EditCapturableOwner(new Point(ObjectX, ObjectY)));
        Assert.Equal(PlayerColor.Blue, scene.Metadata.CapturableObjects[ObjectUid].OwnerColor);
    }

    [Fact]
    public void AMapWithoutPlayersCannotBeOwned()
    {
        EditorScene? scene = CreateScene(PlayerColor.None);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open without players");

        Assert.True(scene.EditCapturableOwner(new Point(ObjectX, ObjectY)));
        Assert.False(scene.Metadata.CapturableObjects.ContainsKey(ObjectUid));
    }

    [Fact]
    public void APlainTileIsNotCapturable()
    {
        EditorScene? scene = CreateScene(PlayerColor.Blue, Mp2ObjectType.Sign);

        if (scene is null)
        {
            return;
        }

        scene.RunModal = _ => throw new InvalidOperationException("no window should open on a sign");

        Assert.False(scene.EditCapturableOwner(new Point(ObjectX, ObjectY)));
    }

    /// <summary>Opens the editor on a map with one object on (20, 14).</summary>
    /// <param name="players">The colours the map lets objects be owned by.</param>
    /// <param name="type">The object type; a mine when omitted.</param>
    /// <returns>The scene, or <see langword="null"/> without GROUND32.TIL.</returns>
    private EditorScene? CreateScene(PlayerColor players, Mp2ObjectType type = Mp2ObjectType.ActionMine)
    {
        if (_assets is null)
        {
            return null;
        }

        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map = map with { Info = map.Info with { KingdomColors = players } };

        map.SetTile(ObjectX, ObjectY, map.Tile(ObjectX, ObjectY) with
        {
            ObjectType = type,
            Level1ObjectUid = ObjectUid,
        });

        GameFonts? fonts = GameFonts.TryLoad(_assets);

        return EditorScene.CreateFromMap(_frame, _assets, fonts, map, out _);
    }
}
