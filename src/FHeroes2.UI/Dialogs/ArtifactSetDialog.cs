using FHeroes2.Game.Artifacts;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// "The set has been assembled", <c>Dialog::ArtifactSetAssembled()</c> (<c>dialog.h:137</c>,
/// <c>dialog_artifact.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window is opened when the last artifact of a set is picked up: it shows the assembled artifact and the parts
/// that were combined to make it. It has no answer, and the reference takes the set data by reference only because
/// the animation of the parts needs to know them - the port takes the immutable <see cref="ArtifactSet"/>.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs only; layout comes with the widget layer.
/// </para>
/// </remarks>
public sealed class ArtifactSetDialog
{
    private ArtifactSetDialog(ArtifactSet set)
    {
        Set = set;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::ArtifactSetAssembled( ArtifactSetData &amp; )</c>.
    /// </summary>
    /// <param name="set">The set that was assembled.</param>
    /// <returns>The model.</returns>
    public static ArtifactSetDialog Create(ArtifactSet set) => new(set);

    /// <summary>The set that was assembled: the artifact and the parts it was made of.</summary>
    public ArtifactSet Set { get; }
}
