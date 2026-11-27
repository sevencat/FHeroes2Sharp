using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The Audio option of the editor options page, <c>editor_options.cpp:313-316</c> opening
/// <c>Dialog::openAudioSettingsDialog( false )</c>: one modal per turn, the page's own <c>IsDirty</c> as
/// <c>saveConfiguration</c>.
/// </summary>
/// <remarks>
/// 音量那两处按参考只在有声卡时能动（<c>Audio::isValid()</c>），App 侧还没有混音器，所以用例改的是音乐类型那一格——
/// 它不碰设备，只翻 <see cref="Settings.MusicType"/>。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorAudioOptionTests : IDisposable
{
    private const int MapSize = 36;

    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly AssetBundle? _previousAssets;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());
    private readonly MusicSource _previousMusicType;

    /// <summary>Opens the archive and the fonts once, and keeps the settings the page writes.</summary>
    public EditorAudioOptionTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
        _previousMusicType = Settings.Get().MusicType;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Settings.Get().SetMusicType(_previousMusicType);
        GameAssets.Current = _previousAssets;
        _assets?.Dispose();
    }

    [Fact]
    public void WithoutTheGameDataThereIsNoPage()
    {
        var host = new EditorSceneHost(assets: null, fonts: null);

        Assert.Null(host.TryCreateAudioSettingsDialog());
        Assert.False(host.RunAudioSettingsDialog(Page()));
    }

    [Fact]
    public void ThePageIsBuiltFromTheInstalledArtwork()
    {
        if (!TryCreateHost(out EditorSceneHost host))
        {
            return;
        }

        AudioSettingsDialog? page = host.TryCreateAudioSettingsDialog();

        Assert.NotNull(page);
        Assert.Equal(AudioSettingsDialog.Width, page.Window.ActiveArea.Width);
        Assert.Equal(AudioSettingsDialog.Height, page.Window.ActiveArea.Height);
    }

    [Fact]
    public void WithoutTheFramePumpThePageIsSkipped()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateAudioSettingsDialog() is not { } page)
        {
            return;
        }

        Assert.Null(host.RunModal);
        Assert.False(host.RunAudioSettingsDialog(page));
    }

    [Fact]
    public void WithoutADeviceThePageStillOpensAndTheIconsCanBeClicked()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateAudioSettingsDialog() is not { } page)
        {
            return;
        }

        Assert.Null(host.Audio);

        MusicSource before = Settings.Get().MusicType;

        host.RunModal = modal =>
        {
            var audio = (AudioSettingsModal)modal;

            Click(audio, audio.Dialog.MusicTypeArea);
            Click(audio, audio.Dialog.Buttons[0].Area);
        };

        Assert.True(host.RunAudioSettingsDialog(page));
        Assert.NotEqual(before, Settings.Get().MusicType);
    }

    [Fact]
    public void ClosingThePageRightAwayChangesNothing()
    {
        if (!TryCreateHost(out EditorSceneHost host) ||
            host.TryCreateAudioSettingsDialog() is not { } page)
        {
            return;
        }

        host.RunModal = modal =>
        {
            var audio = (AudioSettingsModal)modal;

            Click(audio, audio.Dialog.Buttons[0].Area);
        };

        Assert.False(host.RunAudioSettingsDialog(page));
    }

    [Fact]
    public void OpenAudioSettings_ReportsTheChangeOfThePage()
    {
        if (!TryCreateEditor(out EditorScene scene))
        {
            return;
        }

        MusicSource before = Settings.Get().MusicType;

        scene.RunModal = modal =>
        {
            var audio = (AudioSettingsModal)modal;

            Click(audio, audio.Dialog.MusicTypeArea);
            Click(audio, audio.Dialog.Buttons[0].Area);
        };

        Assert.True(scene.OpenAudioSettings());
        Assert.NotEqual(before, Settings.Get().MusicType);
    }

    [Fact]
    public void OpenAudioSettings_ClosingItRightAwayChangesNothing()
    {
        if (!TryCreateEditor(out EditorScene scene))
        {
            return;
        }

        scene.RunModal = modal =>
        {
            var audio = (AudioSettingsModal)modal;

            Click(audio, audio.Dialog.Buttons[0].Area);
        };

        Assert.False(scene.OpenAudioSettings());
    }

    /// <summary>Builds a page over the installed data, or fails when the machine has none.</summary>
    private AudioSettingsDialog Page()
    {
        if (TryCreateHost(out EditorSceneHost host) && host.TryCreateAudioSettingsDialog() is { } page)
        {
            return page;
        }

        throw new InvalidOperationException("The audio page needs the game data, and the machine has none.");
    }

    /// <summary>Builds a host over the installed data; <see langword="false"/> when the machine has none.</summary>
    private bool TryCreateHost(out EditorSceneHost host)
    {
        host = new EditorSceneHost(_assets, _fonts);

        return _assets is not null && _fonts is not null;
    }

    /// <summary>Builds a scene on a blank map.</summary>
    private bool TryCreateEditor(out EditorScene scene)
    {
        scene = null!;

        if (_assets is null || _fonts is null)
        {
            return false;
        }

        EditorScene? created = EditorScene.CreateFromMap(
            _frame, _assets, _fonts, Mp2Map.CreateBlank(MapSize, 0), out _);

        if (created is null)
        {
            return false;
        }

        scene = created;

        return true;
    }

    /// <summary>Feeds one left click on an area of the page, the press and the release in the same frame.</summary>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }
}
