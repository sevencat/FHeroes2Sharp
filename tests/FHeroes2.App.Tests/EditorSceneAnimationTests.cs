using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;

namespace FHeroes2.App.Tests;

/// <summary>
/// The animation tick of the editor, the <c>MAPS_DELAY</c> block of <c>editor_interface.cpp:2017-2024</c>: one step
/// of <c>Game::getAdventureMapAnimationIndex()</c> per delay, and none at all while the author keeps animation off.
/// </summary>
public sealed class EditorSceneAnimationTests
{
    private const int MapSize = 36;

    [Fact]
    public void Update_AdvancesTheAnimationOnlyWhileItIsEnabled()
    {
        using AssetBundle? assets = AssetLocator.Resolve() is { } directory
            ? AssetBundle.Open(directory)
            : null;

        if (assets is null || GameFonts.TryLoad(assets) is not { } fonts)
        {
            return;
        }

        Settings settings = Settings.Get();
        bool previousAnimation = settings.IsEditorAnimationEnabled;

        try
        {
            var frame = new ScreenBuffer(new Size(640, 480), new Palette());
            EditorScene? scene = EditorScene.CreateNew(frame, assets, fonts, MapSize, out string? error);

            if (scene is null)
            {
                Assert.NotNull(error);
                return;
            }

            Assert.Equal(0u, scene.AnimationPhase);

            settings.SetEditorAnimation(true);
            scene.Update(EditorScene.AnimationInterval);
            Assert.Equal(1u, scene.AnimationPhase);

            // 不足一格的时间不推进：索引是台阶，不是连续值。
            scene.Update(EditorScene.AnimationInterval / 2.0);
            Assert.Equal(1u, scene.AnimationPhase);

            // 关掉动画后索引停在原处，参考也是停着而不是归零。
            settings.SetEditorAnimation(false);
            scene.Update(EditorScene.AnimationInterval * 3.0);
            Assert.Equal(1u, scene.AnimationPhase);

            settings.SetEditorAnimation(true);
            scene.Update(EditorScene.AnimationInterval);
            Assert.Equal(2u, scene.AnimationPhase);
        }
        finally
        {
            settings.SetEditorAnimation(previousAnimation);
        }
    }
}
