using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The interface language picker on the host's frame pump, <c>fheroes2::selectLanguage( ..., isGameLanguage = true )</c>
/// of <c>ui_language.cpp</c>: the editor's Language option (<c>editor_options.cpp:283-296</c>) runs it.
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="LanguagePickerModal"/>（地图规格窗里的「附加语言」）不是一回事：那一份的选项来自
/// <see cref="SupportedLanguage"/> 枚举，选出来的是地图要翻译成哪些语言；这一份的选项是数据目录里真正装了译文目录的
/// 那几个 <see cref="Localization.GameText"/>，选出来的是整个界面用哪一份译文。
/// </para>
/// <para>
/// <see cref="LanguageSelectionDialog"/> 是 <c>SetCursor</c> / <c>SetLeftButton</c> 那一套，不读 <see cref="LocalEvent"/>，
/// 所以摞在栈上当内层窗正合适。
/// </para>
/// </remarks>
internal sealed class GameLanguagePickerModal : IModalScreen
{
    private readonly LanguageSelectionDialog _dialog;
    private DialogButton _answer = DialogButton.None;

    /// <summary>Wraps the picker.</summary>
    /// <param name="dialog">The picker to run.</param>
    /// <exception cref="ArgumentNullException">The picker is <see langword="null"/>.</exception>
    public GameLanguagePickerModal(LanguageSelectionDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        _dialog = dialog;
    }

    /// <summary>The picker being run.</summary>
    public LanguageSelectionDialog Dialog => _dialog;

    /// <summary>Whether the author confirmed the pick instead of dropping it.</summary>
    public bool Accepted => _answer == DialogButton.Ok;

    /// <summary>The index of the highlighted row in the list the caller built the picker from.</summary>
    public int SelectedIndex => _dialog.SelectedIndex;

    /// <inheritdoc />
    public bool IsFinished => _answer is DialogButton.Ok or DialogButton.Cancel;

    /// <inheritdoc />
    public void SetCursor(Point? position) => _ = _dialog.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Answer(_dialog.SetLeftButton(down));

    /// <inheritdoc />
    public void SetRightButton(bool down) => _ = down;

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Answer(_dialog.ProcessHotKey(hotKey));

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _dialog.Draw(frame.Pixels);
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _ = elapsedSeconds;

        // 选择器没有动画，重绘由宿主每帧整屏做。
    }

    /// <summary>Keeps the answer of the button row; OK and CANCEL close the picker.</summary>
    /// <param name="button">What the button row reported.</param>
    private void Answer(DialogButton button)
    {
        if (button is DialogButton.Ok or DialogButton.Cancel)
        {
            _answer = button;
        }
    }
}
