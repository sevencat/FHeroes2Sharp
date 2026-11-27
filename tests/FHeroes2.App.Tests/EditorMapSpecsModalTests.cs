using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.UI.Input;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Tests;

/// <summary>
/// The specifications button of the panel: it opens the page the UI layer already has, and the page writes
/// its state back only when the author accepts it, <c>openMapSpecificationsDialog()</c>.
/// </summary>
/// <remarks>
/// 用例自己当帧泵：场景的 <see cref="EditorScene.RunModal"/> 换成一段直接推进模态的代码，事件塞进
/// <see cref="LocalEvent.Current"/>——宿主平时也是这么喂的（<c>GameHost.PumpEvents</c> 先填 <c>_le</c> 再叫模态）。
/// 关窗用回车 / ESC，参考的 <c>:2266-2272</c> 两条分支认的就是它们。
/// </remarks>
[Collection(LocalEventCollection.Name)]
public sealed class EditorMapSpecsModalTests : IDisposable
{
    private const int MapSize = 36;

    /// <summary>Entry of the specifications button in the system table, three columns per row.</summary>
    private const int SpecsButtonIndex = 3;

    /// <summary>Width of one button of the bay, the 48x36 frames of <c>EDITBTNS.ICN</c>.</summary>
    private const int ButtonWidth = 48;

    /// <summary>Height of one button of the bay.</summary>
    private const int ButtonHeight = 36;

    private readonly AssetBundle? _previousAssets;
    private readonly AssetBundle? _assets;

    /// <summary>Opens the archive for the whole test and remembers the one of the process.</summary>
    public EditorMapSpecsModalTests()
    {
        _previousAssets = GameAssets.Current;
        _assets = AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GameAssets.Current = _previousAssets;

        _assets?.Dispose();
    }

    [Fact]
    public void AcceptingTheSpecificationsWritesThemBack()
    {
        if (!TryOpenEditor(out EditorScene scene, out ScreenBuffer frame))
        {
            return;
        }

        int boxes = 0;
        scene.RunModal = modal =>
        {
            ++boxes;

            Feed(frame, modal, Key.Enter);
        };

        string name = scene.MapName;

        ClickSpecsButton(scene);

        Assert.Equal(1, boxes);
        Assert.True(scene.Edits.IsDirty);
        Assert.Equal(name, scene.MapName);
    }

    [Fact]
    public void CancellingTheSpecificationsLeavesTheMapAlone()
    {
        if (!TryOpenEditor(out EditorScene scene, out ScreenBuffer frame))
        {
            return;
        }

        scene.RunModal = modal => Feed(frame, modal, Key.Escape);

        ClickSpecsButton(scene);

        // 取消不写回，地图还是「没动过」的样子：参考的 mapSpecificationsDialog 返回 false 时什么都不拷。
        Assert.False(scene.Edits.IsDirty);
    }

    [Fact]
    public void TheSpecificationsButtonDoesNothingWithoutAFramePump()
    {
        if (!TryOpenEditor(out EditorScene scene, out _))
        {
            return;
        }

        scene.RunModal = null;

        // 没有帧泵就只在日志里留一行，按钮不能把这一下当成别的东西。
        ClickSpecsButton(scene);

        Assert.False(scene.Edits.IsDirty);
    }

    /// <summary>Runs one modal frame with a key pressed and let the page answer it.</summary>
    /// <param name="frame">The frame the scene draws into.</param>
    /// <param name="modal">The window the host was asked to run.</param>
    /// <param name="key">The key that closes the page.</param>
    private static void Feed(ScreenBuffer frame, IModalScreen modal, Key key)
    {
        modal.Draw(frame);

        LocalEvent events = LocalEvent.Current;

        events.BeginFrame();
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        modal.Update(frame, 0.016);
    }

    /// <summary>Clicks the specifications cell of the panel, the left button path of the scene.</summary>
    /// <param name="scene">The editor scene.</param>
    private static void ClickSpecsButton(EditorScene scene)
    {
        scene.SetCursor(SpecsButtonCentre(scene));
        scene.SetLeftButton(true);
        scene.SetLeftButton(false);
    }

    /// <summary>
    /// Opens the editor with the fonts the page needs; <see langword="false"/> when the machine has no game
    /// data, the same way the smoke tests give up.
    /// </summary>
    /// <param name="scene">The scene, when it could be made.</param>
    /// <param name="frame">The frame the scene draws into.</param>
    private bool TryOpenEditor(out EditorScene scene, out ScreenBuffer frame)
    {
        scene = null!;
        frame = new ScreenBuffer(new Size(640, 480), new Palette());

        if (_assets is null || GameFonts.TryLoad(_assets) is not { } fonts)
        {
            return false;
        }

        GameAssets.Current = _assets;

        EditorScene? created = EditorScene.CreateNew(frame, _assets, fonts, MapSize, out _);

        if (created is null)
        {
            return false;
        }

        scene = created;

        return true;
    }

    /// <summary>Centre of the specifications button: the two rows sit at the bottom of the panel bay.</summary>
    /// <param name="scene">The editor scene.</param>
    private static Point SpecsButtonCentre(EditorScene scene)
    {
        Rect panel = scene.PanelArea;

        return new Point(
            panel.X + ((SpecsButtonIndex % 3) * ButtonWidth) + (ButtonWidth / 2),
            panel.Y + panel.Height - (2 * ButtonHeight) + ((SpecsButtonIndex / 3) * ButtonHeight) +
            (ButtonHeight / 2));
    }
}
