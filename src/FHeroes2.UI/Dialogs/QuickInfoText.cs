using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The text of the right button quick info, <c>getQuickInfoText()</c> of
/// <c>src/fheroes2/dialog/dialog_quickinfo.cpp:455-595</c>, for the parts of the switch whose data the
/// port already reads.
/// </summary>
/// <remarks>
/// <para>
/// The reference has a branch per object family, and every branch needs state the port does not model
/// yet: the garrison of a guarded object (<c>getTroopFromTile</c>), what the kingdom has visited, the
/// spell of a shrine, the resource of a mine's overlay, the number of creatures of a dwelling, the
/// requirement of the tree of knowledge, the colour of a barrier. What is left here is the skeleton of
/// the switch plus the three branches that need nothing but the tile itself:
/// </para>
/// <list type="bullet">
/// <item><description>bare ground, <c>OBJ_NONE</c>, <c>OBJ_EVENT</c> and <c>OBJ_COAST</c>: the ground or
/// the coast, as <c>showGroundInfo()</c> does without the digging and penalty lines;</description></item>
/// <item><description>a resource pile, whose sprite names the resource;</description></item>
/// <item><description>everything else: the object name of <c>MP2::StringObject()</c>, which is exactly
/// the <c>default</c> branch of the reference.</description></item>
/// </list>
/// <para>
/// <b>Not ported</b>, in the order the reference reaches them: the fog test of
/// <c>Dialog::QuickInfo()</c> ("Uncharted Territory") — the caller knows the fog and can pass the text
/// in; <c>isCaptureObjectProtected()</c> and "guarded by" lines; the monster count of
/// <c>OBJ_MONSTER</c>; the digging lines of <c>showGroundInfo()</c>
/// (<c>isSuitableForUltimateArtifact()</c> and the pathfinding penalty) and its road name (the port has
/// <see cref="Tile.IsRoad"/> but not the two lines that follow it); the visited state and the extra
/// lines of the shrine, dwelling, mine, barrier, tent, tree of knowledge and visiting objects.
/// </para>
/// </remarks>
public static class QuickInfoText
{
    /// <summary>Name the reference gives a tile the player has not charted yet.</summary>
    public const string UnchartedTerritory = "Uncharted Territory";

    /// <summary>Name the reference gives a road tile.</summary>
    public const string Road = "Road";

    /// <summary>
    /// The quick info of a tile, <c>getQuickInfoText( tile )</c>.
    /// </summary>
    /// <param name="tile">Tile under the cursor.</param>
    /// <returns>The text to show in the popup.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static string ForTile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        // The reference calls getMainObjectType( false ), which keeps the object a hero stands on.
        Mp2ObjectType objectType = tile.MainObjectType;

        return objectType switch
        {
            Mp2ObjectType.Coast or Mp2ObjectType.Event or Mp2ObjectType.None => DescribeGround(tile),
            Mp2ObjectType.Resource => DescribeResourcePile(tile),
            _ => Mp2ObjectName.Of(objectType),
        };
    }

    /// <summary>
    /// The first half of <c>showGroundInfo()</c>: the coast, the road or the ground.
    /// </summary>
    /// <param name="tile">Tile to describe.</param>
    /// <returns>The name of the surface.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static string DescribeGround(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        if (tile.MainObjectType == Mp2ObjectType.Coast)
        {
            return Mp2ObjectName.Of(Mp2ObjectType.Coast);
        }

        return tile.IsRoad ? Road : tile.Ground.ToString();
    }

    /// <summary>
    /// The <c>OBJ_RESOURCE</c> branch: the sprite of the pile names the resource.
    /// </summary>
    /// <param name="tile">Tile of the pile.</param>
    /// <returns>The resource name, capitalized the way <c>Resource::String()</c> prints it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tile"/> is <see langword="null"/>.</exception>
    public static string DescribeResourcePile(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        Mp2ResourceType resource = Mp2ObjectData.ResourceFromSpriteIndex(tile.MainObjectPart.IcnIndex);
        string name = Mp2ObjectData.Name(resource);

        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }
}
