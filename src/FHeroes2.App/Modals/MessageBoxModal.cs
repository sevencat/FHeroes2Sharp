using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// A message box run by <see cref="GameHost.RunModal"/>, the port of <c>Dialog::Message</c> and
/// <c>Dialog::YES_NO</c> (<c>ui_dialog.cpp</c>): it draws on top of the scene, closes on the button the
/// player presses and hands that button back as <see cref="Answer"/>.
/// </summary>
/// <remarks>
/// 参考的 <c>Dialog::Message</c> 自己转事件循环、关掉才返回，<c>Dialog::YES_NO</c> 再折成「是不是 YES」。
/// 这里只把 <see cref="MessageBox"/> 接到宿主那层循环上；「哪个按钮算关掉」由调用方按 <see cref="Answer"/>
/// 判断，和参考的 <c>answer == DialogButton::Yes</c> 一一对应。
/// </remarks>
internal sealed class MessageBoxModal : IModalScreen
{
    private readonly MessageBox _box;

    /// <summary>Wraps an already built message box.</summary>
    /// <param name="box">Box to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
    public MessageBoxModal(MessageBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        _box = box;
    }

    /// <summary>The button that closed the box; <see cref="DialogButton.None"/> while it is still open.</summary>
    public DialogButton Answer { get; private set; }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>
    /// Builds a box with <paramref name="buttons"/> and wraps it, <c>Dialog::Message</c> and
    /// <c>Dialog::YES_NO</c> in one call.
    /// </summary>
    /// <param name="header">Title of the message; empty for none.</param>
    /// <param name="body">Message text; empty for none.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">Font of the message, <c>FontType::normalWhite()</c>.</param>
    /// <param name="buttons">Buttons on the bottom edge, e.g. <see cref="DialogButton.Ok"/>.</param>
    /// <param name="artwork">Source of the button sprites.</param>
    /// <returns>The box, ready for <see cref="GameHost.RunModal"/>.</returns>
    /// <exception cref="ArgumentNullException">A font, the artwork, the header or the body is <see langword="null"/>.</exception>
    public static MessageBoxModal Create(string header, string body, IGlyphSource headerFont, IGlyphSource bodyFont,
        DialogButton buttons, IDialogButtonArtworkSource artwork) =>
        new(MessageBox.Create(header, body, headerFont, bodyFont, buttons: buttons, buttonArtwork: artwork));

    /// <inheritdoc />
    public void SetCursor(Point? position) => _box.SetCursor(position);

    /// <inheritdoc />
    public void SetLeftButton(bool down) => Finish(_box.SetLeftButton(down));

    /// <summary>Ignored: a message box has no right button, the reference drops the right click too.</summary>
    /// <param name="down">Whether the button is now held.</param>
    public void SetRightButton(bool down)
    {
    }

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => Finish(_box.ProcessHotKey(hotKey));

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame) => _box.Draw(frame.Pixels);

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds) =>
        _box.Update(frame.Pixels, TimeSpan.FromSeconds(elapsedSeconds));

    /// <summary>Closes the box as soon as a button reports an answer.</summary>
    private void Finish(DialogButton answer)
    {
        if (answer == DialogButton.None)
        {
            return;
        }

        Answer = answer;
        IsFinished = true;
    }
}
