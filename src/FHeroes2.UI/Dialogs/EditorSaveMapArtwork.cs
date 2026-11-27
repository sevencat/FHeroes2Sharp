using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork the editor's save map window is assembled from, <c>mapSaveSelectFile()</c>
/// (<c>editor_save_map_window.cpp:181-303</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口本身是标准窗（底纹由 <c>StandardWindow</c> 铺），另外要三样：滚动条（<c>SCROLL</c>/<c>SCROLLE</c> 的六帧）、
/// 滚动条的槽（<c>ADVBORD</c> 0 号帧，参考 <c>renderScrollbarBackground</c>）、标准 OKAY/CANCEL（<c>SYSTEM</c> 1..4）。
/// </para>
/// <para>
/// 列表每行的两张小图标来自 <c>REQUESTS</c>：19..24 是玩家数，25..29 是地图大小（<c>:144-151</c>）。
/// 左下角还有一个虚拟键盘按钮（<c>BUTTON_VIRTUAL_KEYBOARD_*</c>，<c>:298-308</c>）。
/// </para>
/// </remarks>
public sealed class EditorSaveMapArtwork
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

    /// <summary>First frame of the "how many players" icons, <c>REQUESTS</c> 19 (<c>:144</c>).</summary>
    public const int PlayerCountFirstFrame = 19;

    /// <summary>First frame of the "map size" icons, <c>REQUESTS</c> 25 (<c>:148</c>).</summary>
    public const int MapSizeFirstFrame = 25;

    private readonly Dictionary<int, Sprite> _requestsIcons = [];

    /// <summary>The frames of <c>SCROLL</c>/<c>SCROLLE</c> the buttons and the slider are cut from.</summary>
    public IReadOnlyList<Sprite> ScrollBarFrames { get; private init; } = [];

    /// <summary>The scroll bar groove, <c>ADVBORD</c> 0 through <c>renderScrollbarBackground()</c>.</summary>
    public Sprite ScrollBarBackground { get; private init; } = null!;

    /// <summary>The OKAY button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Okay { get; private init; }

    /// <summary>The CANCEL button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Cancel { get; private init; }

    /// <summary>The virtual keyboard button, released and pressed (<c>:298-302</c>).</summary>
    public (Sprite Released, Sprite Pressed)? VirtualKeyboard { get; private init; }

    /// <summary>Whether every sprite the window needs was found.</summary>
    public bool IsComplete => ScrollBarFrames.Count >= MinimumScrollFrames;

    /// <summary>
    /// Reads the artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used.</param>
    /// <returns>The artwork, or <see langword="null"/> when a file it needs is missing.</returns>
    public static EditorSaveMapArtwork? TryCreate(bool isEvilInterface)
    {
        string scrollFile = GameAssets.GetFileName(isEvilInterface ? IcnType.Scrolle : IcnType.Scroll);
        string boardFile = GameAssets.GetFileName(isEvilInterface ? IcnType.Advborde : IcnType.Advbord);
        string systemFile = GameAssets.GetFileName(IcnType.System);

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

        // 虚拟键盘键在归档里没有自己的 .ICN，是空板子加 h2d 图案合成的（game_assets.cpp:5465-5498）。
        (Sprite Released, Sprite Pressed)? keyboard = ButtonFactory.MakeVirtualKeyboardButton(isEvilInterface);

        if (scroll.Count < MinimumScrollFrames || board is null || okayReleased is null || okayPressed is null ||
            cancelReleased is null || cancelPressed is null)
        {
            return null;
        }

        return new EditorSaveMapArtwork
        {
            ScrollBarFrames = scroll,
            ScrollBarBackground = board,
            Okay = (okayReleased, okayPressed),
            Cancel = (cancelReleased, cancelPressed),
            VirtualKeyboard = keyboard
        };
    }

    /// <summary>
    /// A <c>REQUESTS</c> icon, <c>Assets::getImage( ICN::REQUESTS, index )</c>.
    /// </summary>
    /// <param name="frame">Frame number, 19..24 for players and 26..29 for size.</param>
    /// <returns>The icon, or <see langword="null"/> when the archive lacks that frame.</returns>
    public Sprite? GetRequestsIcon(int frame)
    {
        if (_requestsIcons.TryGetValue(frame, out Sprite? cached))
        {
            return cached;
        }

        Sprite? icon = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Requests), frame);

        if (icon is not null)
        {
            _requestsIcons[frame] = icon;
        }

        return icon;
    }
}
