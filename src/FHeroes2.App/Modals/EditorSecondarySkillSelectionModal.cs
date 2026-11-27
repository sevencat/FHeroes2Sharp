using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The secondary skill picker, <c>Editor::openSecondarySkillSelectionWindow()</c>, as a modal.
/// </summary>
/// <remarks>
/// <para>
/// 参考那扇窗自己转循环（<c>editor_secondary_skill_selection.cpp</c>）：点格子切换、两颗 toggle 全开全关、
/// 右键弹四处说明。端口的 <see cref="EditorSecondarySkillSelectionWindow"/> 与
/// <see cref="EditorSecondarySkillSelectionInput"/> 已经把这三样拆开，这一层只负责每帧喂一次、把说明框摞到
/// 自己的栈上（参考在循环里就地开窗），关掉之后把 <see cref="SelectedSkills"/> 交出去。
/// </para>
/// <para>
/// 「全开」在参考里交回空列表（游戏读空列表为「按默认」），窗口自己的
/// <see cref="EditorSecondarySkillSelectionWindow.GetSelectedSkills"/> 已经照这个口径折过。
/// </para>
/// </remarks>
internal sealed class EditorSecondarySkillSelectionModal : IModalScreen
{
    private readonly EditorSecondarySkillSelectionWindow _window;
    private readonly EditorSecondarySkillSelectionInput _input;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The window to run.</param>
    /// <param name="input">The state machine fed by the window's events.</param>
    /// <param name="host">Where the message boxes are built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorSecondarySkillSelectionModal(EditorSecondarySkillSelectionWindow window,
        EditorSecondarySkillSelectionInput input, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _input = input;
        _host = host;
    }

    /// <summary>The window being run.</summary>
    public EditorSecondarySkillSelectionWindow Window => _window;

    /// <summary>How the author left the window; <see cref="EditorSecondarySkillSelectionOutcome.None"/> while open.</summary>
    public EditorSecondarySkillSelectionOutcome Outcome { get; private set; }

    /// <summary>The skills to keep; empty both when cancelled and when everything is enabled.</summary>
    public IReadOnlyList<SecondarySkill> SelectedSkills =>
        Outcome == EditorSecondarySkillSelectionOutcome.Accepted ? _window.GetSelectedSkills() : [];

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>Whether a hint box is stacked above the window.</summary>
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

        _window.Paint(frame.Pixels, LocalEvent.Current);

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

        // 说明框还开着：这一帧只归它，窗口既不读事件也不改选中。
        if (Top is { } inner)
        {
            inner.Update(frame, elapsedSeconds);

            if (inner.IsFinished)
            {
                _ = _inner.Pop();
            }

            return;
        }

        EditorSecondarySkillSelectionOutcome outcome = _input.Process(LocalEvent.Current);

        if (outcome != EditorSecondarySkillSelectionOutcome.None)
        {
            Outcome = outcome;
            IsFinished = true;

            return;
        }

        if (_input.Request is { IsNone: false } request)
        {
            if (_host.TryCreateMessageBox(request.Title, request.Body) is { } box)
            {
                _inner.Push(box);
            }
            else
            {
                GameLog.Warn("The secondary skill picker has a hint to show, and the box is missing.");
            }
        }
    }
}
