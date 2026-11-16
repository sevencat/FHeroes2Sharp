using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// An object located by its <b>point on the map</b> instead of by its tile index,
/// <c>MapPosition</c> (<c>src/fheroes2/maps/position.h:33-74</c> and <c>position.cpp:29-37</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference stores a <c>fheroes2::Point</c> and converts to and from the tile index through the
/// global map size (<c>Maps::GetIndexFromAbsPoint()</c>, <c>Maps::GetPoint()</c>,
/// <c>Maps::isValidAbsIndex()</c>). The port has no global map, so both conversions take the
/// <see cref="MapGeometry"/> they need — the same trade every other <c>maps</c> type makes in the port.
/// </para>
/// <para>
/// <b>The subclasses of the reference stay index-first in the port.</b> <c>MapBaseObject</c>,
/// <c>ActionObject</c> and <c>ObjectInfo</c> derive from this class, but a map object in the port is
/// placed by tile index while the map is being read, and the geometry of the world does not exist yet at
/// that point — see <see cref="MapObject"/>, which keeps <see cref="MapObject.TileIndex"/> for that
/// reason. This class therefore lands on its own: it is what the editor and the save format need when
/// they talk about a position without an index.
/// </para>
/// <para>
/// The reference deletes the copy constructor and the assignment operator; nothing in the port can
/// express that, and nothing depends on it.
/// </para>
/// </remarks>
public class MapPosition
{
    /// <summary>
    /// The empty position, <c>center{ -1, -1 }</c> (<c>position.h:73</c>).
    /// </summary>
    public static readonly Point Empty = new(-1, -1);

    /// <summary>
    /// Creates a position that is not on the map, <c>MapPosition() = default</c> (<c>position.h:35</c>).
    /// </summary>
    public MapPosition()
    {
    }

    /// <summary>
    /// Creates a position on <paramref name="center"/>, <c>MapPosition( const fheroes2::Point & )</c>
    /// (<c>position.h:38</c>).
    /// </summary>
    /// <param name="center">Absolute point on the map.</param>
    public MapPosition(Point center) => Center = center;

    /// <summary>
    /// The absolute point of the object, <c>center</c> (<c>position.h:73</c>).
    /// </summary>
    /// <remarks>
    /// The reference keeps this member protected and offers <c>GetCenter()</c>/<c>SetCenter()</c>;
    /// the port uses one property, which is what those two accessors do.
    /// </remarks>
    public Point Center { get; set; } = Empty;

    /// <summary>
    /// The tile index of <see cref="Center"/>, <c>MapPosition::GetIndex()</c> (<c>position.cpp:29</c>).
    /// </summary>
    /// <remarks>
    /// <c>Maps::GetIndexFromAbsPoint()</c> does not validate: a point outside the map yields an index
    /// that is simply not on the map, and <see cref="Empty"/> yields <c>-1</c> for its negative
    /// coordinates. Callers that care ask <see cref="MapGeometry.IsValidAbsIndex"/>.
    /// </remarks>
    /// <param name="geometry">Geometry of the map the point belongs to.</param>
    /// <returns>The tile index of the centre point.</returns>
    public int GetIndex(MapGeometry geometry) => geometry.GetIndexFromAbsPoint(Center);

    /// <summary>
    /// Moves the object onto a tile, <c>MapPosition::SetIndex()</c> (<c>position.cpp:34</c>).
    /// </summary>
    /// <remarks>
    /// An index outside the map clears the position to <see cref="Empty"/> instead of throwing, which is
    /// how the reference turns a missing object into an object that no map point matches.
    /// </remarks>
    /// <param name="index">Index of the tile to move onto.</param>
    /// <param name="geometry">Geometry of the map the index belongs to.</param>
    public void SetIndex(int index, MapGeometry geometry) =>
        Center = geometry.IsValidAbsIndex(index) ? geometry.GetPoint(index) : Empty;

    /// <summary>
    /// Whether <paramref name="point"/> is the point of this object,
    /// <c>MapPosition::isPosition()</c> (<c>position.h:64</c>).
    /// </summary>
    /// <remarks>
    /// Virtual because the reference's <c>ActionObject</c> widens the hit area of a large object to its
    /// whole tile neighbourhood; everything else is happy with the centre point.
    /// </remarks>
    /// <param name="point">Absolute point to test.</param>
    /// <returns><see langword="true"/> when the point belongs to the object.</returns>
    public virtual bool IsPosition(Point point) => point == Center;

    /// <inheritdoc/>
    public override string ToString() => $"{Center}";
}
