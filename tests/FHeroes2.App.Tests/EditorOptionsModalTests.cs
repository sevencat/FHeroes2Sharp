using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The editor's options page on the host's frame pump: it reports one action per turn, the explanations of the
/// right click open above it, and the system button applies the actions and saves the preferences.
/// </summary>
/// <remarks>
/// 用例自己当帧泵：<see cref="EditorSceneHost.RunModal"/> 换成一段直接推进模态的代码，事件塞进
/// <see cref="LocalEvent.Current"/>。页面要的只有四个归档与 OK 按钮，字体不在其中，所以场景可以用
/// <c>fonts: null</c> 造出来，和 <c>EditorSceneInputTests</c> 一样。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorOptionsModalTests : IDisposable
{
    private const int MapSize = 36;

    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly ScreenBuffer _frame = new(new Size(640, 480), new Palette());

    /// <summary>Opens the archive and the fonts once for the whole test.</summary>
    public EditorOptionsModalTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

        if (_assets is not null)
        {
            GameAssets.Current = _assets;
        }

        _fonts = GameFonts.TryLoad(_assets);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;

        _assets?.Dispose();
    }

    [Fact]
    public void TheFourArchivesAreReadFromTheGameData()
    {
        if (TryCreateHost(out _) is false)
        {
            return;
        }

        if (SystemButtonArtwork.TryCreate(_assets) is not { } buttons)
        {
            return;
        }

        EditorOptionsArtwork artwork = EditorOptionsArtwork.TryCreate(buttons)
            ?? throw new InvalidOperationException("The four archives should have been read.");

        Assert.True(artwork.IsComplete);
    }

    [Fact]
    public void EscapeLeavesThePage()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        host.RunModal = modal => Feed(modal, Key.Escape);

        Assert.Equal(EditorOptionsAction.Exit, host.RunEditorOptionsDialog());
    }

    [Fact]
    public void ClickingOneOfTheFiveSettingsReportsIt()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        host.RunModal = modal => Click(modal, ((EditorOptionsModal)modal).Dialog.PassabilityRoi);

        Assert.Equal(EditorOptionsAction.Passability, host.RunEditorOptionsDialog());
    }

    [Fact]
    public void ARightClickOpensTheExplanationAboveThePage()
    {
        if (TryCreateHost(out EditorSceneHost host) is false)
        {
            return;
        }

        bool pageSurvived = false;

        host.RunModal = modal =>
        {
            var options = (EditorOptionsModal)modal;

            RightClick(options, options.Dialog.AnimationRoi);

            // 说明框摞在上面：它吃掉的第一个键不会关掉页面。
            pageSurvived = !options.IsFinished && options.Action == EditorOptionsAction.None;

            options.HotKey(ButtonHotKey.Okay);
            Pump(options);

            pageSurvived &= !options.IsFinished;

            Feed(options, Key.Escape);
        };

        Assert.Equal(EditorOptionsAction.Exit, host.RunEditorOptionsDialog());
        Assert.True(pageSurvived);
    }

    [Fact]
    public void TheSystemButtonAppliesTheSettingAndSavesIt()
    {
        if (CreateScene() is not { } scene)
        {
            return;
        }

        Settings settings = Settings.Get();
        bool wasPassability = settings.IsEditorPassabilityEnabled;
        int saves = 0;
        int windows = 0;

        scene.RunModal = modal =>
        {
            ++windows;

            // 第一层点通行性：参考改完设置再重开一层（editor_options.cpp:235-378）。
            if (windows == 1)
            {
                Click(modal, ((EditorOptionsModal)modal).Dialog.PassabilityRoi);

                return;
            }

            Feed(modal, Key.Escape);
        };
        scene.SavePreferences = () => ++saves;

        try
        {
            scene.OpenEditorOptions();

            Assert.Equal(2, windows);
            Assert.Equal(!wasPassability, settings.IsEditorPassabilityEnabled);
            Assert.Equal(1, saves);
        }
        finally
        {
            settings.SetEditorPassability(wasPassability);
        }
    }

    [Fact]
    public void ClosingWithoutChangingAnythingSavesNothing()
    {
        if (CreateScene() is not { } scene)
        {
            return;
        }

        int saves = 0;
        int windows = 0;

        scene.RunModal = modal =>
        {
            ++windows;
            Feed(modal, Key.Escape);
        };
        scene.SavePreferences = () => ++saves;

        scene.OpenEditorOptions();

        Assert.Equal(1, windows);
        Assert.Equal(0, saves);
    }

    /// <summary>Runs one modal frame with a key pressed.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="key">The key the frame carries.</param>
    private void Feed(IModalScreen modal, Key key)
    {
        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Runs one modal frame without input.</summary>
    /// <param name="modal">The window the host runs.</param>
    private void Pump(IModalScreen modal)
    {
        LocalEvent.Current.BeginFrame();

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one left click on an area of the page, the press and the release in the same frame.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to click; its centre is the point.</param>
    private void Click(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>Feeds one right press on an area of the page, the <c>showStandardTextMessage</c> of the reference.</summary>
    /// <param name="modal">The window the host runs.</param>
    /// <param name="area">The area to right click; its centre is the point.</param>
    private void RightClick(IModalScreen modal, Rect area)
    {
        var point = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnMouseButton(true, MouseButtonType.Right, point);

        modal.Update(_frame, 0.016);
    }

    /// <summary>
    /// Builds a host over the installed data; <see langword="false"/> when the machine has none, the same way the
    /// smoke tests give up.
    /// </summary>
    /// <param name="host">The host, when one could be made.</param>
    /// <returns>Whether the host could be made.</returns>
    private bool TryCreateHost(out EditorSceneHost host)
    {
        host = new EditorSceneHost(_assets, _fonts);

        return _assets is not null && _fonts is not null;
    }

    /// <summary>Builds a scene over the installed data; <see langword="null"/> when the machine has none.</summary>
    /// <returns>The scene, or <see langword="null"/>.</returns>
    private EditorScene? CreateScene() =>
        EditorScene.CreateNew(_frame, _assets, _fonts, MapSize, out _);
}
