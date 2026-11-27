using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork an <see cref="ItemSelectionWindow"/> needs, i.e. everything the reference loads from
/// <c>ICN::SCROLL</c>, <c>ICN::SCROLLE</c>, <c>ICN::ADVBORD</c> and the small okay/cancel buttons in its
/// constructor (<c>dialog_selectitems.cpp:909-1057</c>).
/// </summary>
/// <remarks>
/// The reference reads the archive inside the dialog. The port keeps the widget layer free of the archive
/// and hands the frames in, the same way <see cref="KingdomOverviewArtwork"/> does: the caller decides
/// between the good and the evil interface, and tests can build the sprites without an AGG file.
/// </remarks>
public sealed class ItemSelectionWindowArtwork
{
    /// <summary>
    /// The groove of the scroll bar, frame 0 of <c>ICN::ADVBORD</c> —
    /// <see cref="StandardWindow.RenderScrollbarBackground"/> slices it.
    /// </summary>
    public required Sprite ScrollBarBackground { get; init; }

    /// <summary>The step-up button, frame 0 of <c>ICN::SCROLL</c> or <c>ICN::SCROLLE</c>.</summary>
    public required Sprite ScrollUpReleased { get; init; }

    /// <summary>The pressed step-up button, frame 1 of the same ICN.</summary>
    public required Sprite ScrollUpPressed { get; init; }

    /// <summary>The step-down button, frame 2 of the same ICN.</summary>
    public required Sprite ScrollDownReleased { get; init; }

    /// <summary>The pressed step-down button, frame 3 of the same ICN.</summary>
    public required Sprite ScrollDownPressed { get; init; }

    /// <summary>The thumb before it is stretched to the list length, frame 4 of the same ICN.</summary>
    public required Sprite ScrollSlider { get; init; }

    /// <summary>The okay button, <c>ICN::BUTTON_SMALL_OKAY_GOOD</c> frames 0 and 1 in the reference.</summary>
    public required DialogButtonArtwork Okay { get; init; }

    /// <summary>The cancel button, <c>ICN::BUTTON_SMALL_CANCEL_GOOD</c> frames 0 and 1 in the reference.</summary>
    public required DialogButtonArtwork Cancel { get; init; }

    /// <summary>
    /// The "enable all" button of a multi-selection list, <c>ICN::BUTTON_TOGGLE_ALL_ON_*</c> frames 0 and 1
    /// (<c>dialog_selectitems.cpp:1074</c>); <see langword="null"/> when a window offers no such button.
    /// </summary>
    /// <remarks>
    /// 这两张图在归档里没有 <c>.ICN</c>，是空板子加 h2d 图案合成的（<c>ButtonFactory.MakeToggleAllButton</c>）。
    /// </remarks>
    public (Sprite Released, Sprite Pressed)? ToggleAllOn { get; init; }

    /// <summary>
    /// The "disable all" button of a multi-selection list, <c>ICN::BUTTON_TOGGLE_ALL_OFF_*</c> frames 0 and 1
    /// (<c>dialog_selectitems.cpp:1078</c>); <see langword="null"/> when a window offers no such button.
    /// </summary>
    public (Sprite Released, Sprite Pressed)? ToggleAllOff { get; init; }

    /// <summary>
    /// Loads the artwork the way <c>ItemSelectionWindow::ItemSelectionWindow()</c> does
    /// (<c>dialog_selectitems.cpp:909-957</c>): the scroll bar of <c>SCROLL</c> or <c>SCROLLE</c>, the groove of
    /// <c>ADVBORD</c> and the small okay/cancel buttons of the good or the evil interface.
    /// </summary>
    /// <param name="sprites">Where the frames come from.</param>
    /// <param name="evilInterface">Whether the evil interface is in effect.</param>
    /// <returns>The artwork, or <see langword="null"/> when a frame is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sprites"/> is <see langword="null"/>.</exception>
    public static ItemSelectionWindowArtwork? TryCreate(IViewWorldSpriteSource sprites, bool evilInterface)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        IcnType scrollAsset = evilInterface ? IcnType.Scrolle : IcnType.Scroll;
        IcnType okayAsset = evilInterface ? IcnType.ButtonSmallOkayEvil : IcnType.ButtonSmallOkayGood;
        IcnType cancelAsset = evilInterface ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood;

        if (sprites.GetSprite(IcnType.Advbord, 0) is not { } background ||
            sprites.GetSprite(scrollAsset, 0) is not { } upReleased ||
            sprites.GetSprite(scrollAsset, 1) is not { } upPressed ||
            sprites.GetSprite(scrollAsset, 2) is not { } downReleased ||
            sprites.GetSprite(scrollAsset, 3) is not { } downPressed ||
            sprites.GetSprite(scrollAsset, 4) is not { } slider)
        {
            return null;
        }

        if (sprites.GetSprite(okayAsset, 0) is not { } okayReleased ||
            sprites.GetSprite(okayAsset, 1) is not { } okayPressed ||
            sprites.GetSprite(cancelAsset, 0) is not { } cancelReleased ||
            sprites.GetSprite(cancelAsset, 1) is not { } cancelPressed)
        {
            return null;
        }

        // 两颗「全开/全关」是合成的：BUTTON_TOGGLE_ALL_* 在归档里没有 .ICN，图案在 h2d 资源里，底子铺 STONEBAK
        // （与二级技能选择窗同一条路，ADR-311）。取不到就留空，多选窗因此不画它们，窗口仍然可用。
        (Sprite Released, Sprite Pressed)? toggleOn = null;
        (Sprite Released, Sprite Pressed)? toggleOff = null;

        if (sprites.GetSprite(evilInterface ? IcnType.StonebakEvil : IcnType.Stonebak, 0) is { } stone)
        {
            toggleOn = ButtonFactory.MakeToggleAllButton(stone, evilInterface, enableAll: true);
            toggleOff = ButtonFactory.MakeToggleAllButton(stone, evilInterface, enableAll: false);
        }

        return new ItemSelectionWindowArtwork
        {
            ScrollBarBackground = background,
            ScrollUpReleased = upReleased,
            ScrollUpPressed = upPressed,
            ScrollDownReleased = downReleased,
            ScrollDownPressed = downPressed,
            ScrollSlider = slider,
            Okay = new DialogButtonArtwork(okayReleased, okayPressed),
            Cancel = new DialogButtonArtwork(cancelReleased, cancelPressed),
            ToggleAllOn = toggleOn,
            ToggleAllOff = toggleOff,
        };
    }
}
