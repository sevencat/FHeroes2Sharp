using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork an editor list window is assembled from: the rumors window, <c>openRumorWindow()</c>
/// (<c>editor_rumor_window.cpp:159-196</c>), and the daily events window, <c>openDailyEventsWindow()</c>
/// (<c>editor_daily_events_window.cpp:180-217</c>).
/// </summary>
/// <remarks>
/// <para>
/// 两个窗口要的是同一套素材：窗口本身是标准窗，另外三样——滚动条（<c>SCROLL</c>/<c>SCROLLE</c> 的 0..4 帧）、
/// 滚动条的槽（<c>ADVBORD</c> 0 号帧）、标准 OKAY/CANCEL（<c>SYSTEM</c> 1..4）。
/// </para>
/// <para>
/// 增/改/删三个小按钮是 <c>CELLWIN</c>（邪恶界面 <c>CELLWINE</c>）的 13..18 帧，两帧一对。
/// </para>
/// </remarks>
public sealed class EditorListArtwork
{
    /// <summary>Released OKAY frame of <c>SYSTEM.ICN</c>.</summary>
    public const int OkayReleasedFrame = 1;

    /// <summary>Pressed OKAY frame of <c>SYSTEM.ICN</c>.</summary>
    public const int OkayPressedFrame = 2;

    /// <summary>Released CANCEL frame of <c>SYSTEM.ICN</c>.</summary>
    public const int CancelReleasedFrame = 3;

    /// <summary>Pressed CANCEL frame of <c>SYSTEM.ICN</c>.</summary>
    public const int CancelPressedFrame = 4;

    /// <summary>Frame of the released "scroll up" button, <c>SCROLL</c> 0.</summary>
    public const int ScrollUpReleasedFrame = 0;

    /// <summary>Frame of the pressed "scroll up" button, <c>SCROLL</c> 1.</summary>
    public const int ScrollUpPressedFrame = 1;

    /// <summary>Frame of the released "scroll down" button, <c>SCROLL</c> 2.</summary>
    public const int ScrollDownReleasedFrame = 2;

    /// <summary>Frame of the pressed "scroll down" button, <c>SCROLL</c> 3.</summary>
    public const int ScrollDownPressedFrame = 3;

    /// <summary>Frame of the slider itself, <c>SCROLL</c> 4.</summary>
    public const int ScrollSliderFrame = 4;

    /// <summary>How many <c>SCROLL</c> frames the window needs.</summary>
    public const int MinimumScrollFrames = 5;

    /// <summary>Released frame of the "add" button, <c>CELLWIN</c> 13.</summary>
    public const int AddReleasedFrame = 13;

    /// <summary>Pressed frame of the "add" button, <c>CELLWIN</c> 14.</summary>
    public const int AddPressedFrame = 14;

    /// <summary>Released frame of the "edit" button, <c>CELLWIN</c> 15.</summary>
    public const int EditReleasedFrame = 15;

    /// <summary>Pressed frame of the "edit" button, <c>CELLWIN</c> 16.</summary>
    public const int EditPressedFrame = 16;

    /// <summary>Released frame of the "delete" button, <c>CELLWIN</c> 17.</summary>
    public const int DeleteReleasedFrame = 17;

    /// <summary>Pressed frame of the "delete" button, <c>CELLWIN</c> 18.</summary>
    public const int DeletePressedFrame = 18;

    /// <summary>One past the highest <c>CELLWIN</c> frame the window needs.</summary>
    public const int MinimumCellwinFrames = DeletePressedFrame + 1;

    /// <summary>The frames of <c>SCROLL</c>/<c>SCROLLE</c> the buttons and the slider are cut from.</summary>
    public IReadOnlyList<Sprite> ScrollBarFrames { get; private init; } = [];

    /// <summary>The scroll bar groove, <c>ADVBORD</c> 0 through <c>renderScrollbarBackground()</c>.</summary>
    public Sprite ScrollBarBackground { get; private init; } = null!;

    /// <summary>The OKAY button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Okay { get; private init; }

    /// <summary>The CANCEL button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Cancel { get; private init; }

    /// <summary>The "add" button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Add { get; private init; }

    /// <summary>The "edit" button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Edit { get; private init; }

    /// <summary>The "delete" button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Delete { get; private init; }

    /// <summary>
    /// Width the three small buttons are laid out with, <c>buttonImage.width()</c>
    /// (<c>editor_rumor_window.cpp:186</c>、<c>editor_daily_events_window.cpp:207</c>).
    /// </summary>
    public int ButtonWidth => Add.Released.Width;

    /// <summary>Whether every sprite the window needs was found.</summary>
    public bool IsComplete =>
        ScrollBarFrames.Count >= MinimumScrollFrames && ScrollBarBackground is not null;

    /// <summary>
    /// Reads the artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used.</param>
    /// <returns>The artwork, or <see langword="null"/> when a file it needs is missing.</returns>
    public static EditorListArtwork? TryCreate(bool isEvilInterface)
    {
        string scrollFile = GameAssets.GetFileName(isEvilInterface ? IcnType.Scrolle : IcnType.Scroll);
        string boardFile = GameAssets.GetFileName(isEvilInterface ? IcnType.Advborde : IcnType.Advbord);
        string systemFile = GameAssets.GetFileName(IcnType.System);
        string cellwinFile = GameAssets.GetFileName(isEvilInterface ? IcnType.CellwinEvil : IcnType.Cellwin);

        List<Sprite> scroll = [];

        for (int frame = 0; frame < MinimumScrollFrames; ++frame)
        {
            if (GameAssets.GetImage(scrollFile, frame) is { } sprite)
            {
                scroll.Add(sprite);
            }
        }

        Sprite? board = GameAssets.GetImage(boardFile, 0);

        Sprite? okayReleased = GameAssets.GetImage(systemFile, OkayReleasedFrame);
        Sprite? okayPressed = GameAssets.GetImage(systemFile, OkayPressedFrame);
        Sprite? cancelReleased = GameAssets.GetImage(systemFile, CancelReleasedFrame);
        Sprite? cancelPressed = GameAssets.GetImage(systemFile, CancelPressedFrame);

        Sprite? addReleased = GameAssets.GetImage(cellwinFile, AddReleasedFrame);
        Sprite? addPressed = GameAssets.GetImage(cellwinFile, AddPressedFrame);
        Sprite? editReleased = GameAssets.GetImage(cellwinFile, EditReleasedFrame);
        Sprite? editPressed = GameAssets.GetImage(cellwinFile, EditPressedFrame);
        Sprite? deleteReleased = GameAssets.GetImage(cellwinFile, DeleteReleasedFrame);
        Sprite? deletePressed = GameAssets.GetImage(cellwinFile, DeletePressedFrame);

        if (scroll.Count < MinimumScrollFrames || board is null || okayReleased is null || okayPressed is null ||
            cancelReleased is null || cancelPressed is null || addReleased is null || addPressed is null ||
            editReleased is null || editPressed is null || deleteReleased is null || deletePressed is null)
        {
            return null;
        }

        return new EditorListArtwork
        {
            ScrollBarFrames = scroll,
            ScrollBarBackground = board,
            Okay = (okayReleased, okayPressed),
            Cancel = (cancelReleased, cancelPressed),
            Add = (addReleased, addPressed),
            Edit = (editReleased, editPressed),
            Delete = (deleteReleased, deletePressed)
        };
    }
}
