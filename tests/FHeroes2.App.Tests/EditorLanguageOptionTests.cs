using FHeroes2.App.Fonts;
using FHeroes2.App.Localization;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The Language option of the editor options page, <c>editor_options.cpp:283-296</c>: pick the interface language with
/// <c>fheroes2::selectLanguage( ..., isGameLanguage = true )</c>, write it into the preferences, and tell the author
/// when the installed data cannot offer more than English.
/// </summary>
/// <remarks>
/// 显示名与语言目录解耦：宿主只收一串名字（<see cref="EditorSceneHost.RunLanguagePicker"/>），所以选择器本身不依赖装了几
/// 份译文就能测；整条链（<see cref="EditorScene.SwitchInterfaceLanguage"/>）走的是真实数据目录，装了几份译文就走哪一支，
/// 用例按实际数量断言对应的一支。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorLanguageOptionTests : IDisposable
{
    private const int MapSize = 36;

    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly string _previousLanguage;

    /// <summary>Opens the archive and the fonts once for the whole test, and keeps the language preference.</summary>
    public EditorLanguageOptionTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
        _previousLanguage = Settings.Get().GameLanguage;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Settings.Get().SetGameLanguage(_previousLanguage);
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void RunLanguagePicker_WithoutTheFramePump_IsSkipped()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        Assert.Equal(-1, host.RunLanguagePicker(["English", "简体中文"], 0, "Select Game Language:"));
    }

    [Fact]
    public void RunLanguagePicker_WithNoInstalledLanguage_IsSkipped()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        int windows = 0;
        host.RunModal = _ => ++windows;

        Assert.Equal(-1, host.RunLanguagePicker([], 0, "Select Game Language:"));
        Assert.Equal(0, windows);
    }

    [Fact]
    public void RunLanguagePicker_CancellingItReportsNone()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        string? highlighted = null;
        int rows = 0;

        host.RunModal = modal =>
        {
            var picker = (GameLanguagePickerModal)modal;

            rows = picker.Dialog.Count;
            highlighted = picker.Dialog.SelectedName;
            picker.HotKey(ButtonHotKey.Cancel);
        };

        Assert.Equal(-1, host.RunLanguagePicker(["English", "简体中文"], 1, "Select Game Language:"));
        Assert.Equal(2, rows);

        // 打开时高亮的是当前语言，取消之后什么都没变。
        Assert.Equal("简体中文", highlighted);
    }

    [Fact]
    public void RunLanguagePicker_PickingARowReportsItsIndex()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        host.RunModal = modal =>
        {
            var picker = (GameLanguagePickerModal)modal;

            picker.Dialog.Select(1);
            picker.HotKey(ButtonHotKey.Okay);
        };

        Assert.Equal(1, host.RunLanguagePicker(["English", "简体中文"], 0, "Select Game Language:"));
    }

    [Fact]
    public void SwitchInterfaceLanguage_WritesThePickedLanguageIntoThePreferences()
    {
        if (!TryCreateScene(out EditorScene scene))
        {
            return;
        }

        Settings settings = Settings.Get();
        IReadOnlyList<GameText> languages = GameText.LoadAll(_assets!.DataDirectory);
        settings.SetGameLanguage(languages[0].Culture);

        int windows = 0;
        scene.RunModal = modal =>
        {
            ++windows;

            if (modal is not GameLanguagePickerModal picker)
            {
                return;
            }

            picker.Dialog.Select(1);
            picker.HotKey(ButtonHotKey.Okay);
        };

        bool changed = scene.SwitchInterfaceLanguage();

        if (languages.Count > 1)
        {
            // 装了两份译文：选择器开出来，选中的那份写进设置，并告诉宿主落盘。
            Assert.True(changed);
            Assert.Equal(languages[1].Culture, settings.GameLanguage);
            Assert.Equal(1, windows);

            return;
        }

        // 只有英文：参考把语言钉成英文再解释一句（editor_options.cpp:287-295），这里同样弹一个提示框。
        Assert.False(changed);
        Assert.Equal(GameText.FallbackCulture, settings.GameLanguage);
        Assert.Equal(1, windows);
    }

    [Fact]
    public void SwitchInterfaceLanguage_WithoutTheGameData_IsSkipped()
    {
        var frame = new ScreenBuffer(new Size(640, 480), new Palette());
        Mp2Map map = Mp2Map.CreateBlank(MapSize, 0);
        EditorScene? scene = EditorScene.CreateFromMap(frame, assets: null, fonts: null, map, out _);

        if (scene is null)
        {
            return;
        }

        Assert.False(scene.SwitchInterfaceLanguage());
    }

    /// <summary>Builds a host with the archive, the fonts and a frame pump that keeps the windows open.</summary>
    private bool TryCreateHost(out EditorSceneHost host)
    {
        if (_assets is null || _fonts is null)
        {
            host = null!;

            return false;
        }

        host = new EditorSceneHost(_assets, _fonts);
        host.RunModal = _ => { };

        return true;
    }

    /// <summary>Builds a scene on a blank map, the smallest map the editor offers.</summary>
    private bool TryCreateScene(out EditorScene scene)
    {
        scene = null!;

        if (_assets is null || _fonts is null)
        {
            return false;
        }

        var frame = new ScreenBuffer(new Size(640, 480), new Palette());
        EditorScene? created = EditorScene.CreateFromMap(
            frame, _assets, _fonts, Mp2Map.CreateBlank(MapSize, 0), out _);

        if (created is null)
        {
            return false;
        }

        scene = created;

        return true;
    }
}
