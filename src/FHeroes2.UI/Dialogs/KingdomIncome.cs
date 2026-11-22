using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The "Kingdom Income" box of <c>showKingdomIncome()</c> (<c>gui/ui_kingdom.cpp:32-44</c>): a title, the sentence
/// "Kingdom Income per day." and one <see cref="ResourceDialogElement"/> per resource the kingdom earns, gold first.
/// </summary>
/// <remarks>
/// <para>
/// The reference draws it through <c>Dialog::showMessage( header, body, buttons, funds )</c>
/// (<c>ui_dialog.cpp:523-545</c>), which wraps <see cref="MessageBox"/>, so the box here is a
/// <see cref="MessageBox"/> with elements. The two callers differ only in the buttons they pass: the marketplace's
/// right click uses <c>Dialog::ZERO</c> (a box that lives while the button is held, which is what
/// <see cref="MessageBox"/> already is), the town screen's "income" button uses <c>Dialog::OK</c>; the button variant
/// is not ported yet.
/// </para>
/// <para>
/// The order and the skipping of zero amounts are the reference's <c>getResourceDialogElements()</c>
/// (<c>ui_dialog.cpp:456-484</c>): gold, wood, mercury, ore, sulfur, crystal, gems, and a resource that earns nothing
/// gets no element at all.
/// </para>
/// <para>
/// The right click on an icon asks for the resource popup of <c>ResourceDialogElement::showPopup()</c>
/// (<c>ui_dialog.cpp:451-454</c>): the resource name over <see cref="ResourceText.Description"/>.
/// </para>
/// </remarks>
public static class KingdomIncome
{
    /// <summary>Title of the box, <c>_("Kingdom Income")</c>.</summary>
    public const string Header = "Kingdom Income";

    /// <summary>Body of the box, <c>_("Kingdom Income per day.")</c>.</summary>
    public const string Body = "Kingdom Income per day.";

    /// <summary>The resources the box shows an element for, in the reference's order.</summary>
    public static ReadOnlySpan<int> Order =>
    [
        Resource.Gold,
        Resource.Wood,
        Resource.Mercury,
        Resource.Ore,
        Resource.Sulfur,
        Resource.Crystal,
        Resource.Gems,
    ];

    /// <summary>Returns the non zero amounts of <paramref name="income"/>, in the order of <see cref="Order"/>.</summary>
    /// <param name="income">Daily income of the kingdom, <c>kingdom.GetIncome( INCOME_ALL )</c>.</param>
    /// <returns>Resource type and amount pairs; empty when nothing is earned.</returns>
    public static List<KeyValuePair<int, int>> Items(in Funds income)
    {
        List<KeyValuePair<int, int>> items = [];

        foreach (int resource in Order)
        {
            int amount = income.Get(resource);

            if (amount != 0)
            {
                items.Add(new KeyValuePair<int, int>(resource, amount));
            }
        }

        return items;
    }

    /// <summary>Composes the box for one day of <paramref name="income"/>.</summary>
    /// <param name="income">Daily income of the kingdom.</param>
    /// <param name="resourceIcons">Frames of <c>RESOURCE.ICN</c>, indexed by <see cref="ResourceIconIndex.Of"/>.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c> in the reference.</param>
    /// <param name="bodyFont">Font of the message, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="header">Translated title; <see cref="Header"/> when omitted.</param>
    /// <param name="body">Translated message; <see cref="Body"/> when omitted.</param>
    /// <param name="startY">Top of the frame, or a negative value to center it.</param>
    /// <param name="screen">Screen the box is centered on; 640x480 when omitted.</param>
    /// <returns>The box, with one element per non zero resource.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="resourceIcons"/> has fewer than <see cref="ResourceIconIndex.Count"/> frames.
    /// </exception>
    public static MessageBox Create(in Funds income, IReadOnlyList<Sprite> resourceIcons,
        IGlyphSource headerFont, IGlyphSource bodyFont, string header = Header, string body = Body, int startY = -1,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(resourceIcons);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        if (resourceIcons.Count < ResourceIconIndex.Count)
        {
            throw new ArgumentException(
                $"The resource artwork must have {ResourceIconIndex.Count} frames, it had {resourceIcons.Count}.",
                nameof(resourceIcons));
        }

        List<DialogElement> elements = [];

        foreach (KeyValuePair<int, int> item in Items(income))
        {
            elements.Add(new ResourceDialogElement(
                resourceIcons[ResourceIconIndex.Of(item.Key)],
                item.Value.ToString(CultureInfo.InvariantCulture),
                ResourceText.Of(item.Key),
                ResourceText.Description));
        }

        return MessageBox.Create(header, body, headerFont, bodyFont, elements, startY, screen);
    }
}
