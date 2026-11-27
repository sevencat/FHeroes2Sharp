using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The single-spell picker, <c>Dialog::selectSpell()</c> (<c>dialog_selectitems.cpp:1117-1141</c>), as a modal.
/// </summary>
/// <remarks>
/// <para>
/// 窗口自己读 <see cref="LocalEvent.Current"/>，所以这个类只把每帧喂给它，关掉之后把结果交出去——与
/// <see cref="EditorSpellSelectionModal"/> 的外层一个路子。
/// </para>
/// <para>
/// EXIT 与空列表都算「没选」：参考只认 <c>OK</c> 且列表里有一项的时候才回填（<c>:1140</c>），否则返回
/// <c>Spell::NONE</c>。
/// </para>
/// </remarks>
internal sealed class SpellSelectModal : IModalScreen
{
    private readonly SpellSelectionWindow _window;

    private ButtonHotKey? _hotKey;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The picker to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public SpellSelectModal(SpellSelectionWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
    }

    /// <summary>The picker being run.</summary>
    public SpellSelectionWindow Window => _window;

    /// <summary>
    /// The spell the author picked; <see cref="SpellType.None"/> when the picker was cancelled or had nothing to
    /// offer, the <c>result == OK && listbox.IsValid()</c> test of <c>:1140</c>.
    /// </summary>
    public SpellType SelectedSpell =>
        _window.Result == DialogButton.Ok ? _window.SelectedSpell : SpellType.None;

    /// <inheritdoc />
    public bool IsFinished => _window.IsClosed;

    /// <summary>The picker does not hunt for a cursor: it reads the frame's event itself.</summary>
    /// <param name="position">Ignored.</param>
    public void SetCursor(Point? position)
    {
    }

    /// <summary>The picker does not take clicks from the host: it reads the frame's event itself.</summary>
    /// <param name="down">Ignored.</param>
    public void SetLeftButton(bool down)
    {
    }

    /// <summary>The picker does not take clicks from the host: it reads the frame's event itself.</summary>
    /// <param name="down">Ignored.</param>
    public void SetRightButton(bool down)
    {
    }

    /// <summary>Keeps the hot key for the next frame, the way the reference's loop tests it.</summary>
    /// <param name="hotKey">The hot key of this frame.</param>
    public void HotKey(ButtonHotKey hotKey) => _hotKey = hotKey;

    /// <summary>The picker has no text entry: keys are read by the list box itself.</summary>
    /// <param name="key">Ignored.</param>
    /// <param name="modifier">Ignored.</param>
    public void PressKey(Key key, KeyModifier modifier)
    {
    }

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (IsFinished)
        {
            _hotKey = null;

            return;
        }

        _window.ProcessEvents(LocalEvent.Current, _hotKey);

        _hotKey = null;
    }
}
