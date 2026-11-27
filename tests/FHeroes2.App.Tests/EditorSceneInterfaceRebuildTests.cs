using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.System;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The interface re-skin behind the options page, <c>rebuildEditor( true )</c> of the reference
/// (<c>editor_options.cpp:281-285</c>, <c>Editor::Interface::reset</c> at <c>editor_interface.cpp:1413-1470</c>):
/// the border and the instrument panel follow the interface set while the layout stays where it was.
/// </summary>
public sealed class EditorSceneInterfaceRebuildTests
{
    private const int MapSize = 36;

    [Fact]
    public void SwitchingTheThemeReskinsTheChromeAndKeepsTheLayout()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory
            ? AssetBundle.Open(directory)
            : null;

        if (assets is null || GameFonts.TryLoad(assets) is not { } fonts)
        {
            return;
        }

        // 两块边框都是归档里的独立文件，缺哪块都说明数据不全，这条用例没有意义。
        Assert.NotNull(MapFrame.Load(assets, isEvilInterface: false));
        Assert.NotNull(MapFrame.Load(assets, isEvilInterface: true));

        AssetBundle? previousAssets = GameAssets.Current;
        Settings settings = Settings.Get();
        InterfaceType previousType = settings.InterfaceType;

        try
        {
            GameAssets.Current = assets;
            settings.SetInterfaceType(InterfaceType.Good);

            var frame = new ScreenBuffer(new Size(640, 480), new Palette());
            EditorScene? scene = EditorScene.CreateNew(frame, assets, fonts, MapSize, out string? error);

            if (scene is null)
            {
                Assert.NotNull(error);
                return;
            }

            scene.DrawFrame(default);

            Rect panel = scene.PanelArea;
            Point scroll = scene.View.Scroll;
            uint goodPanel = Hash(frame, panel);
            uint goodBorder = Hash(frame, new Rect(0, 0, 640, MapFrame.BorderWidth));

            settings.SetInterfaceType(InterfaceType.Evil);
            scene.RebuildInterface();
            scene.DrawFrame(default);

            // 布局不随主题动：换皮只换图，面板还在原地，地图也没跳。
            Assert.Equal(panel, scene.PanelArea);
            Assert.Equal(scroll, scene.View.Scroll);

            // 边框与面板都换了图：两处的像素都不一样了。
            Assert.NotEqual(goodBorder, Hash(frame, new Rect(0, 0, 640, MapFrame.BorderWidth)));
            Assert.NotEqual(goodPanel, Hash(frame, scene.PanelArea));
        }
        finally
        {
            settings.SetInterfaceType(previousType);
            GameAssets.Current = previousAssets;
        }
    }

    [Fact]
    public void TheEvilPanelIsAColourTransformOfTheGoodOne()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory
            ? AssetBundle.Open(directory)
            : null;

        if (assets is null)
        {
            return;
        }

        AssetBundle? previousAssets = GameAssets.Current;

        try
        {
            GameAssets.Current = assets;

            if (EditorButtonSet.TryCreate(assets) is not { } good ||
                EditorButtonSet.TryCreate(assets, isEvilInterface: true) is not { } evil)
            {
                return;
            }

            Assert.Equal(EditorButtonSet.FrameCount, good.Count);
            Assert.Equal(EditorButtonSet.FrameCount, evil.Count);

            // 帧 0 是地形按钮的原料帧：尺寸一样，像素必须不同，换色才真的跑过。
            Sprite goodFrame = good[0]!;
            Sprite evilFrame = evil[0]!;

            Assert.Equal(goodFrame.Image.Width, evilFrame.Image.Width);
            Assert.Equal(goodFrame.Image.Height, evilFrame.Image.Height);
            Assert.NotEqual(Hash(goodFrame.Image), Hash(evilFrame.Image));

            // 面板的石头底也换了：STONEBAK_EVIL 归档里没有条目，是现算出来的，取不到就只剩平色。
            var area = new Rect(16, 16, 608, 200);

            Assert.NotNull(EditorPanel.TryCreate(area, assets));
            Assert.True(EditorPanel.TryCreate(area, assets)!.HasBackground);
            Assert.True(EditorPanel.TryCreate(area, assets, isEvilInterface: true)!.HasBackground);
        }
        finally
        {
            GameAssets.Current = previousAssets;
        }
    }

    /// <summary>Hashes the pixels of an image, an order sensitive fingerprint of the art.</summary>
    /// <param name="image">The image to read.</param>
    /// <returns>The fingerprint; equal artwork gives an equal value.</returns>
    private static uint Hash(PixelBuffer image)
    {
        uint hash = 2166136261;

        for (int y = 0; y < image.Height; ++y)
        {
            for (int x = 0; x < image.Width; ++x)
            {
                hash = (hash ^ image[x, y]) * 16777619;
            }
        }

        return hash;
    }

    /// <summary>Hashes the pixels of a rectangle of the frame, an order sensitive fingerprint of the art.</summary>
    /// <param name="frame">The frame to read.</param>
    /// <param name="area">The rectangle to read.</param>
    /// <returns>The fingerprint; equal artwork gives an equal value.</returns>
    private static uint Hash(ScreenBuffer frame, Rect area)
    {
        uint hash = 2166136261;

        for (int y = area.Y; y < area.Y + area.Height; ++y)
        {
            for (int x = area.X; x < area.X + area.Width; ++x)
            {
                hash = (hash ^ frame.Pixels[x, y]) * 16777619;
            }
        }

        return hash;
    }
}
