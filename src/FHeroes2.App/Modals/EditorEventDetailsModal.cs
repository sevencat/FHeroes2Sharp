using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The adventure map event editor, <c>Editor::openEventDetailsWindow()</c>
/// (<c>editor_event_details_window.cpp</c>), on the host's frame pump.
/// </summary>
/// <remarks>
/// <para>
/// 参考的窗口在循环里就地开五扇子窗：消息 <c>Dialog::inputString</c>（<c>:271-289</c>）、神器
/// <c>Dialog::selectArtifact</c>（<c>:290-320</c>）、资源与经验各一扇 <c>Dialog::SelectCount</c>
/// （<c>:321-341</c>、<c>:415-440</c>）、副技选择窗（<c>:342-414</c>）。端口把五类请求各摞一层在自己的栈上，
/// 关掉之后回填进窗口的 `Set*` 方法。
/// </para>
/// <para>
/// 「副技」那一支参考用的是 <c>openSecondarySkillSelectionWindow( …, 1, … )</c> 的**单选**形态；端口的窗口暴露的是
/// <c>SetSecondarySkill( skill, level )</c>，所以宿主拿到选中集的第一项就够。
/// </para>
/// </remarks>
internal sealed class EditorEventDetailsModal : IModalScreen
{
    /// <summary>Header of the message box, <c>_( "Input event message text:" )</c> of <c>:272</c>.</summary>
    public const string MessageBody = "Input event message text:";

    private readonly EditorEventDetailsWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private int _pendingResource = -1;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="host">Where the inner windows are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorEventDetailsModal(EditorEventDetailsWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public EditorEventDetailsWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorListOutcome.None"/> while it is still open.</summary>
    public EditorListOutcome Outcome => _window.Result;

    /// <summary>The metadata the window edits, or <see langword="null"/> when the author cancelled.</summary>
    /// <remarks>
    /// 参考把 <c>newEventData</c> 传引用进来、窗口就地改，所以调用方手里那份就是最新的；端口的窗口持有同一份
    /// <see cref="AdventureMapEventMetadata"/>，这里直接交出去即可。
    /// </remarks>
    public AdventureMapEventMetadata? AcceptedMetadata =>
        _window.Result == EditorListOutcome.Accepted ? _window.Event : null;

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>Whether an inner window is open.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

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

        // 内层窗还开着：这一帧只归它。
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
        if (_window.MessageRequested)
        {
            InputStringDialog dialog = InputStringDialog.Create(string.Empty, MessageBody,
                EditorEventDetailsWindow.MessageCharLimit, isMultiLine: true, _window.Event.Message);

            if (_host.TryCreateInput(dialog) is { } input)
            {
                _inner.Push(input);
            }

            return;
        }

        if (_window.ArtifactRequested)
        {
            if (_host.TryCreateArtifactSelect(_window.Event.Artifact is >= 0
                    ? (ArtifactType)_window.Event.Artifact
                    : ArtifactType.Unknown) is { } picker)
            {
                _inner.Push(picker);
            }

            return;
        }

        if (_window.SecondarySkillRequested)
        {
            if (_host.TryCreateSecondarySkillSelection("Skill:",
                    (SkillLevel)Math.Max(1, (int)_window.Event.SecondarySkillLevel), []) is { } skill)
            {
                _inner.Push(skill);
            }

            return;
        }

        if (_window.ExperienceRequest is { } experience)
        {
            if (_host.TryCreateSelectCount(experience.Dialog, experience.Value) is { } count)
            {
                _pendingResource = -1;
                _inner.Push(count);
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

    /// <summary>Feeds the answer of a closed inner window back into the event.</summary>
    /// <param name="closed">The window that just finished.</param>
    private void Resolve(IModalScreen closed)
    {
        switch (closed)
        {
            case InputStringModal { Window.Result: { } text }:
                _window.SetMessage(text);

                return;

            case ArtifactSelectModal picker when picker.SelectedArtifact != ArtifactType.Unknown:
                _window.SetArtifact(picker.SelectedArtifact);

                return;

            case EditorSecondarySkillSelectionModal skills when skills.Outcome ==
                EditorSecondarySkillSelectionOutcome.Accepted:
            {
                IReadOnlyList<SecondarySkill> chosen = skills.SelectedSkills;

                if (chosen.Count > 0)
                {
                    _window.SetSecondarySkill(chosen[0], SkillLevel.Basic);
                }
                else
                {
                    _window.ClearSecondarySkill();
                }

                return;
            }

            case SelectCountModal count:
            {
                int resourceType = _pendingResource;
                _pendingResource = -1;

                if (count.Result is not { } value)
                {
                    return;
                }

                if (resourceType < 0)
                {
                    _window.SetExperience(value);
                }
                else
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
