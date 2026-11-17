using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Picking a resource to give away, <c>Dialog::MakeGiftResource()</c> (<c>dialog.h:132</c>,
/// <c>dialog_giftresources.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference returns the index of the picked resource, <c>-1</c> when the window was closed; the port answers
/// with <see langword="null"/> for that. The index is the <c>Resource::</c> one, i.e. the same order the
/// <see cref="FHeroes2.Game.Resources.Resource"/> constants use, so it is returned as an <see cref="int"/> rather
/// than as a resource type - there is no such type, resources are plain constants.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class GiftResourcesDialog
{
    private GiftResourcesDialog(GameKingdom kingdom)
    {
        Kingdom = kingdom;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::MakeGiftResource( Kingdom &amp; )</c>.
    /// </summary>
    /// <param name="kingdom">The kingdom that gives the resource away.</param>
    /// <returns>The model.</returns>
    public static GiftResourcesDialog Create(GameKingdom kingdom) => new(kingdom);

    /// <summary>The kingdom that gives the resource away.</summary>
    public GameKingdom Kingdom { get; }

    /// <summary>
    /// The result of the dialog: index of the picked resource, or <see langword="null"/> when it was closed.
    /// </summary>
    /// <remarks>The reference returns <c>-1</c> for "closed"; the port uses <see langword="null"/>.</remarks>
    public int? Result { get; private set; }

    /// <summary>
    /// Records the picked resource.
    /// </summary>
    /// <param name="resourceIndex">Index of the resource, or <see langword="null"/> when the dialog was closed.</param>
    public void Choose(int? resourceIndex)
    {
        Result = resourceIndex is >= 0 and < 7 ? resourceIndex : null;
    }
}
