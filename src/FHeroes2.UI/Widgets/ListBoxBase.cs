using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The part of a list a window can talk to without knowing what the list holds, a port of
/// <c>Interface::ListBasic</c> (<c>src/fheroes2/gui/interface_list.h:35-52</c>).
/// </summary>
/// <remarks>
/// <para>
/// The kingdom overview owns two lists — heroes and castles — and switches between them with one pointer
/// (<c>kingdom_overview.cpp:808</c>), so the shared half cannot be generic. The reference solves it with this base
/// class; the port keeps it for the same reason.
/// </para>
/// <para>
/// <see cref="CurrentId"/> is the reference's protected <c>_currentId</c>, exposed because the dialog has to read it
/// after the loop; it is <c>-1</c> for «nothing selected». <see cref="TopId"/> is the first entry that is visible and
/// is what the dialog stores between openings.
/// </para>
/// </remarks>
public abstract class ListBoxBase
{
    /// <summary>Index of the selected entry, or <c>-1</c>, <c>_currentId</c> of the reference.</summary>
    public int CurrentId { get; protected set; } = -1;

    /// <summary>Index of the first visible entry, or <c>-1</c>, <c>_topId</c> of the reference.</summary>
    public int TopId { get; protected set; } = -1;

    /// <summary>Whether the list has to be painted again, <c>IsNeedRedraw()</c>.</summary>
    public abstract bool IsNeedRedraw { get; }

    /// <summary>Paints the whole list, <c>Redraw()</c>.</summary>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public abstract void Redraw(PixelBuffer output);

    /// <summary>Consumes one frame of input, <c>QueueEventProcessing()</c>.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns><see langword="true"/> when the list changed and has to be painted again.</returns>
    public abstract bool QueueEventProcessing(LocalEvent events);
}
