using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The sphinx editor, <c>Editor::openSphinxWindow()</c> (<c>editor_sphinx_window.cpp</c>), on the host's frame pump.
/// </summary>
/// <remarks>
/// <para>
/// 参考的窗口在循环里就地开五扇子窗：谜题与答案走 <c>Dialog::inputString</c>（<c>:314-345</c>）、神器走
/// <c>Dialog::selectArtifact</c>（<c>:262-289</c>）、资源数量走 <c>Dialog::SelectCount</c>（<c>:290-312</c>）、
/// 重复答案与各处右键说明走消息框。端口把这五类请求各摞一层在自己的栈上，关掉之后回填进窗口：
/// <see cref="EditorSphinxWindow.SetRiddle"/> / <see cref="EditorSphinxWindow.AddAnswer"/> /
/// <see cref="EditorSphinxWindow.ReplaceAnswer"/> / <see cref="EditorSphinxWindow.SetArtifact"/> /
/// <see cref="EditorSphinxWindow.SetResourceCount"/>。
/// </para>
/// <para>
/// 「加答案」与「改答案」用的是同一个输入框，区别只在回填调哪个方法；这一点由 <see cref="_pendingAnswer"/>
/// 记着，关窗时才知道该加还是该改。
/// </para>
/// </remarks>
internal sealed class EditorSphinxModal : IModalScreen
{
    /// <summary>Header of the riddle box, <c>_( "Riddle:" )</c> of <c>editor_sphinx_window.cpp:316</c>.</summary>
    public const string RiddleBody = "Riddle:";

    /// <summary>Header of the answer box, <c>_( "Answer:" )</c> of <c>:329</c>.</summary>
    public const string AnswerBody = "Answer:";

    /// <summary>Longest riddle the author may type, <c>Maps::Map_Format::messageCharLimit</c>.</summary>
    public const int RiddleCharLimit = 999;

    private readonly EditorSphinxWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private EditKind _pending = EditKind.Nothing;
    private int _pendingResource = -1;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorSphinxModal(EditorSphinxWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public EditorSphinxWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorListOutcome.None"/> while it is still open.</summary>
    public EditorListOutcome Outcome => _window.Result;

    /// <summary>The metadata the window edits, or <see langword="null"/> when the author cancelled.</summary>
    public SphinxMetadata? AcceptedMetadata =>
        _window.Result == EditorListOutcome.Accepted ? _window.ToMetadata() : null;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>Whether an inner window is open.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <summary>What the input box on top of the stack is for.</summary>
    private enum EditKind
    {
        /// <summary>Nothing is being edited.</summary>
        Nothing,

        /// <summary>The riddle text.</summary>
        Riddle,

        /// <summary>A new answer.</summary>
        AddAnswer,

        /// <summary>The selected answer.</summary>
        EditAnswer,
    }

    /// <inheritdoc />
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <inheritdoc />
    public void SetRightButton(bool down) => Top?.SetRightButton(down);

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Top?.HotKey(hotKey);

    /// <inheritdoc />
    public void PressKey(Key key, KeyModifier modifier) => Top?.PressKey(key, modifier);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels, LocalEvent.Current);

        Top?.Draw(frame);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (IsFinished)
        {
            return;
        }

        // 内层窗还开着：这一帧只归它，窗口既不读事件也不改元数据。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
                Resolve(inner);
            }

            return;
        }

        EditorListOutcome outcome = _window.Process(LocalEvent.Current);

        if (outcome != EditorListOutcome.None)
        {
            IsFinished = true;

            return;
        }

        OpenRequestedWindows();

        if (_window.Popup is { } popup && _host.TryCreateMessageBox(popup.Header, popup.Body) is { } box)
        {
            _inner.Push(box);
        }
    }

    /// <summary>Opens the sub-window the window asked for this frame; at most one per frame.</summary>
    private void OpenRequestedWindows()
    {
        if (_window.RiddleRequested)
        {
            OpenInput(EditKind.Riddle, RiddleBody, _window.Riddle, RiddleCharLimit, isMultiLine: true);

            return;
        }

        if (_window.AnswerAddRequested)
        {
            OpenInput(EditKind.AddAnswer, AnswerBody, string.Empty, EditorSphinxWindow.LongestAnswer,
                isMultiLine: false);

            return;
        }

        if (_window.AnswerEditRequested)
        {
            OpenInput(EditKind.EditAnswer, AnswerBody, _window.SelectedAnswer ?? string.Empty,
                EditorSphinxWindow.LongestAnswer, isMultiLine: false);

            return;
        }

        if (_window.ArtifactRequested)
        {
            if (_host.TryCreateArtifactSelect(_window.Artifact) is { } picker)
            {
                _pending = EditKind.Nothing;
                _inner.Push(picker);
            }

            return;
        }

        if (_window.ResourceCountRequest is { } resource)
        {
            if (_host.TryCreateSelectCount(resource.Dialog, resource.Value) is { } count)
            {
                _pendingResource = resource.ResourceType;
                _inner.Push(count);
            }
        }
    }

    /// <summary>Opens a text input box for the riddle or an answer.</summary>
    /// <param name="kind">What the box is for.</param>
    /// <param name="body">Its header, <see cref="RiddleBody"/> or <see cref="AnswerBody"/>.</param>
    /// <param name="initial">The text it starts with.</param>
    /// <param name="limit">Longest text it takes.</param>
    /// <param name="isMultiLine">Whether the field scrolls over several lines.</param>
    private void OpenInput(EditKind kind, string body, string initial, int limit, bool isMultiLine)
    {
        InputStringDialog dialog = InputStringDialog.Create(string.Empty, body, limit, isMultiLine, initial);

        if (_host.TryCreateInput(dialog) is not { } input)
        {
            GameLog.Warn("The sphinx needs a text box, and it cannot be built.");

            return;
        }

        _pending = kind;
        _inner.Push(input);
    }

    /// <summary>Feeds the answer of a closed inner window back into the sphinx.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        switch (closed)
        {
            case InputStringModal input:
            {
                EditKind kind = _pending;
                _pending = EditKind.Nothing;

                if (input.Window.Result is not { } text)
                {
                    return;
                }

                switch (kind)
                {
                    case EditKind.Riddle:
                        _window.SetRiddle(text);

                        break;

                    case EditKind.AddAnswer:
                        _ = _window.AddAnswer(text);

                        break;

                    case EditKind.EditAnswer:
                        _ = _window.ReplaceAnswer(text);

                        break;

                    default:
                        break;
                }

                return;
            }

            case ArtifactSelectModal picker:
            {
                ArtifactType artifact = picker.SelectedArtifact;

                if (artifact != ArtifactType.Unknown)
                {
                    _window.SetArtifact(artifact);
                }

                return;
            }

            case SelectCountModal count:
            {
                int resourceType = _pendingResource;
                _pendingResource = -1;

                if (count.Result is { } value)
                {
                    _window.SetResourceCount(resourceType, value);
                }

                return;
            }

            default:
                return;
        }
    }
}
