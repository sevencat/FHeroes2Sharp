using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The single-artifact picker, <c>Dialog::selectArtifact()</c> (<c>dialog_selectitems.cpp:1162-1194</c>), as a modal.
/// </summary>
/// <remarks>
/// 与 <see cref="SpellSelectModal"/> 同构：窗口自己读 <see cref="LocalEvent.Current"/>，关掉之后把结果交出去。
/// 右键一行的说明框（<c>ArtifactDialogElement::showPopup()</c>）是整扇窗，所以摞在这个类自己的栈上。
/// </remarks>
internal sealed class ArtifactSelectModal : IModalScreen
{
    private readonly ArtifactSelectionWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private ButtonHotKey? _hotKey;

    /// <summary>Wraps the picker so the frame pump can run it.</summary>
    /// <param name="window">The picker to run.</param>
    /// <param name="host">Where the description box is built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ArtifactSelectModal(ArtifactSelectionWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The picker being run.</summary>
    public ArtifactSelectionWindow Window => _window;

    /// <summary>
    /// The artifact the author picked; <see cref="ArtifactType.Unknown"/> when the picker was cancelled or had nothing
    /// to offer.
    /// </summary>
    public ArtifactType SelectedArtifact =>
        _window.Result == DialogButton.Ok ? _window.SelectedArtifact : ArtifactType.Unknown;

    /// <inheritdoc />
    public bool IsFinished => _window.IsClosed;

    /// <summary>Whether the description box of a right clicked row is open.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <summary>The picker does not hunt for a cursor: it reads the frame's event itself.</summary>
    /// <param name="position">Ignored unless a description box is open.</param>
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <summary>The picker does not take clicks from the host: it reads the frame's event itself.</summary>
    /// <param name="down">Ignored unless a description box is open.</param>
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <summary>The picker does not take clicks from the host: it reads the frame's event itself.</summary>
    /// <param name="down">Ignored unless a description box is open.</param>
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <summary>Keeps the hot key for the next frame, the way the reference's loop tests it.</summary>
    /// <param name="hotKey">The hot key of this frame.</param>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (Top is { } inner)
        {
            inner.HotKey(hotKey);

            return;
        }

        _hotKey = hotKey;
    }

    /// <summary>The picker has no text entry: keys are read by the list box itself.</summary>
    /// <param name="key">Ignored unless a description box is open.</param>
    /// <param name="modifier">Ignored unless a description box is open.</param>
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels);

        Top?.Draw(frame);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        if (IsFinished)
        {
            _hotKey = null;

            return;
        }

        _window.ProcessEvents(LocalEvent.Current, _hotKey);

        _hotKey = null;

        if (_window.TakeInfoRequest() is { } artifact)
        {
            if (_host.TryCreateArtifactInfo(artifact) is { } info)
            {
                _inner.Push(info);
            }
            else
            {
                GameLog.Warn($"The artifact picker asked for the description of {artifact}, and it cannot be built.");
            }
        }
    }
}
