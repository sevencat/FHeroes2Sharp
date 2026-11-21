using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The sprites the army info window is made of, the <c>ICN::VIEWARMY</c> frames the reference loads before it
/// opens the window: the window with its frame and the amount box already painted in, and its drop shadow.
/// </summary>
/// <remarks>
/// <para>
/// Frame 0 is the 520 by 256 window; frame 7 is the shadow, whose own anchors place it a few pixels right and below
/// the window — <see cref="ArmyInfoLayout.Create"/> is given the difference rather than a guess. The evil interface
/// reads the same two frames from <c>ICN::VIEWARME</c>, which is why <see cref="GetAssetIcn"/> takes the flag.
/// </para>
/// <para>
/// <b>Not here</b>: the three buttons. They are ordinary small buttons read from their own files
/// (<c>BUTTON_SMALL_EXIT_GOOD</c> and its siblings) and arrive as <see cref="DialogButtonArtwork"/>, so the window
/// artwork stays one archive and the buttons can be swapped by whoever opens the dialog.
/// </para>
/// </remarks>
public sealed class ArmyInfoArtwork
{
    /// <summary>The frame of the window itself, amount box painted in.</summary>
    public const int BackgroundFrame = 0;

    /// <summary>The frame of the drop shadow.</summary>
    public const int ShadowFrame = 7;

    /// <summary>How many frames the archive must have for <see cref="TryCreate"/> to accept it.</summary>
    public const int RequiredSpriteCount = ShadowFrame + 1;

    private ArmyInfoArtwork(
        Sprite background,
        Sprite shadow,
        bool evilInterface,
        DialogButtonArtwork? exitButton,
        DialogButtonArtwork? upgradeButton,
        DialogButtonArtwork? dismissButton)
    {
        Background = background;
        Shadow = shadow;
        EvilInterface = evilInterface;
        ExitButton = exitButton;
        UpgradeButton = upgradeButton;
        DismissButton = dismissButton;
    }

    /// <summary>The 520 by 256 window, frame 0.</summary>
    public Sprite Background { get; }

    /// <summary>The drop shadow, frame 7.</summary>
    public Sprite Shadow { get; }

    /// <summary>
    /// Whether these frames came from <c>ICN::VIEWARME</c>. The layout needs it: the evil window is pushed down three
    /// pixels and measured from nine pixels to the right (<c>dialog_armyinfo.cpp:534-550</c>).
    /// </summary>
    public bool EvilInterface { get; }

    /// <summary>The button that closes the window.</summary>
    public DialogButtonArtwork? ExitButton { get; }

    /// <summary>The upgrade button, absent when the dialog has no upgrade.</summary>
    public DialogButtonArtwork? UpgradeButton { get; }

    /// <summary>The dismiss button, absent when the dialog has no dismiss.</summary>
    public DialogButtonArtwork? DismissButton { get; }

    /// <summary>
    /// The archive a window is read from, <c>isEvilInterfaceEnabled() ? ICN::VIEWARME : ICN::VIEWARMY</c>
    /// (<c>dialog_armyinfo.cpp:523-524</c>).
    /// </summary>
    /// <param name="evilInterface">Whether the evil interface is in use.</param>
    /// <returns>The ICN to load.</returns>
    public static IcnType GetAssetIcn(bool evilInterface) =>
        evilInterface ? IcnType.Viewarme : IcnType.Viewarmy;

    /// <summary>
    /// Takes the two frames out of the archive.
    /// </summary>
    /// <param name="sprites">The frames of <see cref="GetAssetIcn"/>.</param>
    /// <param name="evilInterface">Whether the frames are the evil interface's.</param>
    /// <param name="exitButton">The exit button's two frames, or <see langword="null"/>.</param>
    /// <param name="upgradeButton">The upgrade button's two frames, or <see langword="null"/>.</param>
    /// <param name="dismissButton">The dismiss button's two frames, or <see langword="null"/>.</param>
    /// <returns>The artwork, or <see langword="null"/> when the window archive is too short.</returns>
    public static ArmyInfoArtwork? TryCreate(
        IReadOnlyList<Sprite>? sprites,
        bool evilInterface = false,
        DialogButtonArtwork? exitButton = null,
        DialogButtonArtwork? upgradeButton = null,
        DialogButtonArtwork? dismissButton = null)
    {
        if (sprites is null || sprites.Count < RequiredSpriteCount)
        {
            return null;
        }

        return new ArmyInfoArtwork(
            sprites[BackgroundFrame],
            sprites[ShadowFrame],
            evilInterface,
            exitButton,
            upgradeButton,
            dismissButton);
    }

    /// <summary>
    /// Where the shadow goes, the anchor difference the layout needs: the shadow sprite is not stored at
    /// <c>( 0, 0 )</c> relative to the window.
    /// </summary>
    /// <returns>The shadow's anchors, relative to the window's.</returns>
    public Point GetShadowShift() => new(Shadow.OffsetX - Background.OffsetX, Shadow.OffsetY - Background.OffsetY);
}
