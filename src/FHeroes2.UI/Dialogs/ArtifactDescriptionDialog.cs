using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The box that describes one artifact, <c>ArtifactDialogElement::showPopup()</c> of the reference
/// (<c>ui_dialog.cpp:666-676</c>): the name as the yellow title, the description as the white body, the artifact in
/// its frame under the text, and a single OKAY to close it.
/// </summary>
/// <remarks>
/// 与 <see cref="SpellDescriptionDialog"/> 同一套写法：参考那一行是
/// <c>showStandardTextMessage( name, description, buttons, { this } )</c>，排版全交给 <see cref="MessageBox.Create"/>。
/// 元素是 <see cref="ArtifactDialogElement"/>，它要「框 + 图标」两张图：框是 <c>RESOURCE.ICN</c> 第 7 帧，图标是
/// <c>ARTIFACT.ICN</c> 的全尺寸帧。
/// </remarks>
public static class ArtifactDescriptionDialog
{
    /// <summary>Frame of <c>RESOURCE.ICN</c> that frames the icon, the <c>7</c> of <c>ui_dialog.cpp:669</c>.</summary>
    public const int FrameIndex = 7;

    /// <summary>Composes the description box of an artifact.</summary>
    /// <param name="artifact">The artifact to describe.</param>
    /// <param name="frame">The frame the icon sits in, <c>RESOURCE.ICN</c> frame 7.</param>
    /// <param name="icon">Its icon, a frame of <c>ARTIFACT.ICN</c>, <c>ArtifactTypes.FullSpriteIndex</c>.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">Font of the description, <c>FontType::normalWhite()</c>.</param>
    /// <param name="buttonArtwork">Source of the OKAY button's sprites.</param>
    /// <param name="extraParameter">
    /// The spell id of a spell scroll, which fills the <c>%{spell}</c> of its description; <see cref="SpellType.None"/>
    /// for every other artifact.
    /// </param>
    /// <param name="screen">Screen the box is centred on; 640x480 when omitted.</param>
    /// <returns>The composed box, ready for <see cref="MessageBox.Draw"/> or a modal wrapper.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="frame"/>, <paramref name="icon"/>, a font or <paramref name="buttonArtwork"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static MessageBox Create(ArtifactType artifact, Sprite frame, Sprite icon, IGlyphSource headerFont,
        IGlyphSource bodyFont, IDialogButtonArtworkSource buttonArtwork, int extraParameter = (int)SpellType.None,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(buttonArtwork);

        ArtifactData data = ArtifactTable.Get(artifact);
        string name = data.Name;
        string description = data.GetDescription(extraParameter);

        return MessageBox.Create(name, description, headerFont, bodyFont,
            [new ArtifactDialogElement(frame, icon, name, description)], screen: screen, buttons: DialogButton.Ok,
            buttonArtwork: buttonArtwork);
    }
}
