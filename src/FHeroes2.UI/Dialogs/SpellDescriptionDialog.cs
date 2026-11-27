using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Spells;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The box that describes one spell, <c>SpellDialogElement::showPopup()</c> of the reference
/// (<c>ui_dialog.cpp:543-546</c>): the name as the yellow title, the description as the white body, the spell
/// itself as the element under the text, and a single OKAY to close it.
/// </summary>
/// <remarks>
/// <para>
/// 参考那一行是 <c>showStandardTextMessage( _spell.GetName(), getSpellDescription( _spell, _hero ), buttons, { this } )</c>，
/// 也就是「标题 + 正文 + 自己这个元素」。英雄为空时 <c>getSpellDescription()</c> 直接取法术表里的说明，端口对应的是
/// <see cref="Spell.GetDescription"/>；元素是 <see cref="SpellDialogElement"/>，图标与名字都在里面。
/// </para>
/// <para>
/// 排版全交给 <see cref="MessageBox.Create"/>：它照 <c>showMessage()</c> 把元素单独排成一行放在文字下面，框高跟着长。
/// 这个类只负责把参考那四个参数凑齐，所以调用方（编辑器宿主）拿到的就是一个能直接跑的
/// <see cref="MessageBox"/>。
/// </para>
/// </remarks>
public static class SpellDescriptionDialog
{
    /// <summary>Composes the description box of <paramref name="spell"/>.</summary>
    /// <param name="spell">The spell to describe.</param>
    /// <param name="icon">Its icon, a frame of <c>ICN::SPELLS</c>, <c>Assets::getImage( ICN::SPELLS, spell.IndexSprite() )</c>.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c>.</param>
    /// <param name="bodyFont">Font of the description, <c>FontType::normalWhite()</c>.</param>
    /// <param name="labelFont">Font of the name under the icon, <c>FontType::smallWhite()</c>.</param>
    /// <param name="buttonArtwork">Source of the OKAY button's sprites.</param>
    /// <param name="screen">Screen the box is centred on; 640x480 when omitted.</param>
    /// <returns>The composed box, ready for <see cref="MessageBox.Draw"/> or a modal wrapper.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="icon"/>, a font or <paramref name="buttonArtwork"/> is <see langword="null"/>.
    /// </exception>
    public static MessageBox Create(Spell spell, Sprite icon, IGlyphSource headerFont, IGlyphSource bodyFont,
        IGlyphSource labelFont, IDialogButtonArtworkSource buttonArtwork, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);
        ArgumentNullException.ThrowIfNull(labelFont);
        ArgumentNullException.ThrowIfNull(buttonArtwork);

        return MessageBox.Create(spell.GetName(), spell.GetDescription(), headerFont, bodyFont,
            [new SpellDialogElement(icon, spell, 0, labelFont)], screen: screen, buttons: DialogButton.Ok,
            buttonArtwork: buttonArtwork);
    }
}
