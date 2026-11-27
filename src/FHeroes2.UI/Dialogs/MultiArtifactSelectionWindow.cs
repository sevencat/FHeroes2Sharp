using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The "pick any number of these" artifact list, <c>MultiArtifactSelection</c>
/// (<c>dialog_selectitems.cpp:169-267</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在 <c>SelectEnumArtifact</c> 之上叠两层：<c>MultiItemSelectionBase</c> 管选中集，本类管交互——单击与双击都是
/// 「切换这一行」（所以双击不关窗），两颗 toggle 按钮全开/全关，图标没被选中时套一层灰度调色板
/// （<c>PAL::PaletteType::GRAY</c>）。OKAY 在选中集为空时灰掉。
/// </para>
/// <para>
/// 交出的结果是 <see cref="SelectedArtifacts"/>，也就是参考的 <c>getSelected()</c>。
/// </para>
/// </remarks>
public sealed class MultiArtifactSelectionWindow : ArtifactSelectionWindow
{
    private readonly MultiItemSelection _selection;

    /// <summary>
    /// Lays out the window over a candidate list and a selection, the reference's <c>setup()</c>
    /// (<c>dialog_selectitems.cpp:179-190</c>).
    /// </summary>
    /// <param name="allowed">The artifacts the list offers.</param>
    /// <param name="selected">The artifacts already picked; empty means every one of them is picked.</param>
    /// <param name="icons">The <c>ARTFX</c> mini icons.</param>
    /// <param name="dialogSize">Size of the active area, normally <see cref="ArtifactSelectDialog.DialogSize"/>.</param>
    /// <param name="artwork">
    /// Frames of the scroll bar, the two buttons and the two toggle buttons; a window without the last pair shows no
    /// toggle buttons at all.
    /// </param>
    /// <param name="titleFont">Yellow font.</param>
    /// <param name="font">White font.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MultiArtifactSelectionWindow(IReadOnlyList<int> allowed, IReadOnlyList<int> selected,
        IArtifactIcons icons, Size dialogSize, ItemSelectionWindowArtwork artwork, IGlyphSource titleFont,
        IGlyphSource font, Size? screen = null)
        : base(allowed, icons, dialogSize, artwork, titleFont, font, screen)
    {
        _selection = new MultiItemSelection(allowed, selected);

        EnableToggleButtons();
        SetOkayEnabled(!_selection.IsEmpty);
    }

    /// <summary>The artifacts the author picked, <c>getSelected()</c>.</summary>
    public IReadOnlyList<int> SelectedArtifacts => _selection.GetSelected();

    /// <summary>The selection set itself, for a caller that wants to test one row.</summary>
    public MultiItemSelection Selection => _selection;

    /// <summary>
    /// A double click toggles the row instead of answering OKAY, <c>MultiArtifactSelection::isDoubleClicked()</c>
    /// (<c>dialog_selectitems.cpp:249-252</c>).
    /// </summary>
    protected override bool DoubleClickAnswersOkay => false;

    /// <summary>
    /// Picks or unpicks the clicked row, the <c>ActionListSingleClick</c> / <c>ActionListDoubleClick</c> of
    /// <c>:192-202</c>.
    /// </summary>
    /// <param name="item">The clicked row.</param>
    public override void ActionListSingleClick(ItemSelectionEntry item) => Toggle(item);

    /// <inheritdoc cref="ActionListSingleClick(ItemSelectionEntry)"/>
    /// <param name="item">The clicked row.</param>
    public override void ActionListDoubleClick(ItemSelectionEntry item) => Toggle(item);

    /// <summary>Picks every row, <c>onToggleOn()</c> (<c>:204-215</c>).</summary>
    protected override void OnToggleOn()
    {
        _selection.SelectAll();
        SetOkayEnabled(true);
    }

    /// <summary>Unpicks every row, <c>onToggleOff()</c> (<c>:217-223</c>).</summary>
    protected override void OnToggleOff()
    {
        _selection.Clear();
        SetOkayEnabled(false);
    }

    /// <summary>
    /// Greys the icon of an unpicked artifact, <c>MultiArtifactSelection::getImage()</c> (<c>:257-267</c>).
    /// </summary>
    /// <param name="icon">The mini icon.</param>
    /// <param name="item">The row.</param>
    /// <returns>The icon, greyed out when the row is not picked.</returns>
    protected override Sprite DecorateIcon(Sprite icon, ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(icon);

        if (_selection.IsSelected(item.Id))
        {
            return icon;
        }

        Sprite greyed = icon.Clone();
        ImageOps.ApplyPalette(greyed.Image, FHeroes2.Core.Imaging.PaletteId.Gray);

        return greyed;
    }

    /// <summary>Flips one row and keeps the OKAY button in step, <c>updateStatus()</c> (<c>:240-247</c>).</summary>
    /// <param name="item">The clicked row.</param>
    private void Toggle(ItemSelectionEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _ = _selection.Toggle(item.Id);
        SetOkayEnabled(!_selection.IsEmpty);
    }
}
