using FHeroes2.App.Scenes;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The creature picker, <c>Dialog::selectMonster()</c> (<c>dialog_selectitems.cpp:1204-1229</c>), as a modal.
/// </summary>
/// <remarks>
/// <para>
/// 窗口自己读 <see cref="LocalEvent.Current"/>，所以这个类只把每帧喂给它，关掉之后把结果交出去——与
/// <see cref="SpellSelectModal"/> 一个路子。
/// </para>
/// <para>
/// 右键一行在参考里开的是 <c>Dialog::ArmyInfo( Troop( monster, 0 ), Dialog::ZERO )</c>（<c>:157-166</c>），
/// 也就是单位说明窗。它是自己的模态，所以摞在这个类自己的栈上：栈上有窗时这一帧只归它，关掉再回到列表。
/// </para>
/// </remarks>
internal sealed class MonsterSelectModal : IModalScreen
{
    private readonly MonsterSelectionWindow _window;
    private readonly EditorSceneHost _host;
    private readonly Stack<IModalScreen> _inner = new();

    private ButtonHotKey? _hotKey;

    /// <summary>Wraps the picker so the frame pump can run it.</summary>
    /// <param name="window">The picker to run.</param>
    /// <param name="host">Where the unit information window is built from.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MonsterSelectModal(MonsterSelectionWindow window, EditorSceneHost host)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(host);

        _window = window;
        _host = host;
    }

    /// <summary>The picker being run.</summary>
    public MonsterSelectionWindow Window => _window;

    /// <summary>
    /// The creature the author picked; <see cref="MonsterType.Unknown"/> when the picker was cancelled or had
    /// nothing to offer, the <c>result == OK && listbox.IsValid()</c> test of <c>:1228</c>.
    /// </summary>
    public MonsterType SelectedMonster =>
        _window.Result == DialogButton.Ok ? _window.SelectedMonster : MonsterType.Unknown;

    /// <inheritdoc />
    public bool IsFinished => _window.IsClosed;

    /// <summary>Whether the unit information window of a right clicked row is open.</summary>
    internal bool HasInnerWindow => _inner.Count > 0;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    internal IModalScreen? TopWindow => Top;

    /// <summary>The inner window on top of the stack, or <see langword="null"/> when none is open.</summary>
    private IModalScreen? Top => _inner.Count > 0 ? _inner.Peek() : null;

    /// <summary>The picker does not hunt for a cursor: it reads the frame's event itself.</summary>
    /// <param name="position">Ignored unless a unit information window is open.</param>
    public void SetCursor(Point? position) => Top?.SetCursor(position);

    /// <summary>The picker does not take clicks from the host: it reads the frame's event itself.</summary>
    /// <param name="down">Ignored unless a unit information window is open.</param>
    public void SetLeftButton(bool down) => Top?.SetLeftButton(down);

    /// <summary>The picker does not take clicks from the host: it reads the frame's event itself.</summary>
    /// <param name="down">Ignored unless a unit information window is open.</param>
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
    /// <param name="key">Ignored unless a unit information window is open.</param>
    /// <param name="modifier">Ignored unless a unit information window is open.</param>
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

        // 右键说明：参考在这一行开的是整扇单位信息窗，不是浮窗。
        if (_window.TakeInfoRequest() is { } monster)
        {
            if (_host.TryCreateArmyInfo(ArmyInfoDialog.Create(new Troop(monster, 0))) is { } info)
            {
                _inner.Push(info);
            }
            else
            {
                GameLog.Warn($"The creature picker asked for the window of {monster}, and it cannot be built.");
            }
        }
    }
}
