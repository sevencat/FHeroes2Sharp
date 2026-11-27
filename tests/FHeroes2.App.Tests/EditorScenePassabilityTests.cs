using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The passability overlay of the editor, <c>Settings::isEditorPassabilityEnabled()</c> plus <c>redrawPassable()</c>
/// (<c>editor_interface.cpp:2026-2030</c>, <c>maps_tiles_render.cpp:916-929</c>): switched on, the tiles a hero cannot
/// leave get their marker, switched off the map is drawn as before.
/// </summary>
/// <remarks>
/// 这条用例顺带把整条链走一遍：编辑器 → <c>MapTileSet</c> 现算通行性 → <c>AdventureMapView</c> 画标记。地图上只有左上角
/// 那一格放了山，视口不滚动，所以帧里的视口左上角就是那一格的左上角，读一个像素就能分辨开没开。
/// </remarks>
public sealed class EditorScenePassabilityTests
{
    private const int MapSize = 36;

    /// <summary>Size of a map tile, the offset of the marker's bottom edge inside it.</summary>
    private const int TileSize = 32;

    /// <summary>Colour of a marker where the tile can be left, <c>90</c> in the reference.</summary>
    private const byte Free = 90;

    [Fact]
    public void SwitchingTheOverlayOnShowsTheMarkerOfABlockedTile()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory
            ? AssetBundle.Open(directory)
            : null;

        if (assets is null || GameFonts.TryLoad(assets) is not { } fonts)
        {
            return;
        }

        AssetBundle? previousAssets = GameAssets.Current;
        Settings settings = Settings.Get();
        bool previousPassability = settings.IsEditorPassabilityEnabled;

        try
        {
            GameAssets.Current = assets;
            settings.SetEditorPassability(false);

            var frame = new ScreenBuffer(new Size(640, 480), new Palette());
            EditorScene? scene = EditorScene.CreateFromMap(frame, assets, fonts, BlockedMap(), out string? error);

            if (scene is null)
            {
                Assert.NotNull(error);
                return;
            }

            scene.View.ScrollTo(0, 0);

            scene.DrawFrame(default);
            byte plain = Marker(frame, scene);

            // 打开叠层：左上角那格是山，标记的底边整段都是绿的（山只封了中排与下排）。
            settings.SetEditorPassability(true);
            scene.DrawFrame(default);
            Assert.Equal(Free, Marker(frame, scene));

            // 关掉就回到原来的地形，不留下标记。
            settings.SetEditorPassability(false);
            scene.DrawFrame(default);
            Assert.Equal(plain, Marker(frame, scene));
            Assert.NotEqual(Free, plain);
        }
        finally
        {
            settings.SetEditorPassability(previousPassability);
            GameAssets.Current = previousAssets;
        }
    }

    /// <summary>A blank map whose first tile carries a mountain, so a hero cannot leave it.</summary>
    private static Mp2Map BlockedMap()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, 0);
        Mp2TileInfo[] tiles = [.. map.Tiles];

        tiles[0] = tiles[0] with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.MtnGras << 2),
            BottomIcnImageIndex = 14,
            Quantity1 = (byte)ObjectLayerType.Object,
            Level1ObjectUid = 1,
            ObjectType = Mp2ObjectType.Mountains,
        };

        return map with { Tiles = tiles };
    }

    /// <summary>
    /// The pixel at the middle of the bottom edge of the first tile in the viewport — a marker pixel that the editor
    /// grid does not paint over (the grid runs along the top and left edges of the tile).
    /// </summary>
    private static byte Marker(ScreenBuffer frame, EditorScene scene) =>
        frame.Pixels[scene.ViewportArea.X + 15, scene.ViewportArea.Y + TileSize - 2];
}
