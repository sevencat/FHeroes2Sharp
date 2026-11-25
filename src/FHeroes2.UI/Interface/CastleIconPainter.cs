using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The 46x22 icon of a castle on the mini map row of icons, <c>ICN::LOCATORS</c> at the frame the race and the built
/// castle gate pick, with the <c>ICN::CSLMARKER</c> badge that says whether the town can be built in today.
/// </summary>
/// <remarks>
/// <para>
/// The adventure map's icon panel and the kingdom overview's town list show the same picture, so the drawing lives here
/// and the panel delegates. The reference has the same split: <c>Interface::redrawCastleIcon()</c> for the overview and
/// the panel's own code calling <c>fheroes2::AGG::GetICN</c> for the icons row.
/// </para>
/// <para>
/// 参考的 <c>fheroes2::drawCastleIcon()</c>（<c>gui/ui_castle.cpp:222-240</c>）除图标本身还会在图标右上角
/// （<c>offset.x + 40</c>）叠一个 <c>ICN::CSLMARKER</c>：帧 0 表示「今天已经建过」，帧 1 表示「可以建但钱不够」。
/// 端口把建筑状态作为参数传进来，因为 <c>Castle</c> 自己不持有王国资金。
/// </para>
/// </remarks>
public static class CastleIconPainter
{
    /// <summary>How far right of the icon the build marker sits, <c>offset.x + 40</c> of the reference.</summary>
    public const int MarkerOffsetX = 40;

    /// <summary>Draws the icon of a castle, <c>fheroes2::drawCastleIcon()</c>.</summary>
    /// <param name="castle">The castle to draw.</param>
    /// <param name="sprites">Where <c>LOCATORS</c> (or <c>LOCATORE</c>) and <c>CSLMARKER</c> come from.</param>
    /// <param name="position">
    /// Top left corner the icon is copied to, the reference's <c>offset</c>. Note that the icon frame of
    /// <c>LOCATORS</c> carries its own ( 5, 5 ) offset, which <c>drawCastleIcon()</c> ignores: the caller already
    /// passes the final position, as <c>Interface::redrawCastleIcon( castle, ox + 5, oy + 5 )</c> does.
    /// </param>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="evilInterface">Whether the black letterbox of <c>LOCATORE</c> is wanted.</param>
    /// <param name="status">
    /// The verdict of <see cref="Castle.GetAllBuildingStatus"/>, which decides the marker; the default draws no
    /// marker, like the reference's <c>UNKNOWN_COND</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="castle"/>, <paramref name="sprites"/> or <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    public static void Draw(Castle castle, IViewWorldSpriteSource sprites, Point position, PixelBuffer output,
        bool evilInterface = false, BuildingStatus status = BuildingStatus.UnknownCondition)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(output);

        if (IconBar.CastleFrame(castle.Race, castle.IsCastle) is not { } frame)
        {
            return;
        }

        if (sprites.GetSprite(evilInterface ? IcnType.Locatore : IcnType.Locators, frame) is not { } icon)
        {
            return;
        }

        ImageOps.BlitSprite(output, position, icon);
        DrawBuildMarker(sprites, position, output, status);
    }

    private static void DrawBuildMarker(IViewWorldSpriteSource sprites, Point iconPosition, PixelBuffer output,
        BuildingStatus status)
    {
        int markerFrame;

        switch (status)
        {
            case BuildingStatus.NotToday:
                markerFrame = 0;
                break;

            case BuildingStatus.RequiresBuild:
                markerFrame = 1;
                break;

            default:
                return;
        }

        if (sprites.GetSprite(IcnType.Cslmarker, markerFrame) is not { } marker)
        {
            return;
        }

        ImageOps.BlitSprite(output, new Point(iconPosition.X + MarkerOffsetX, iconPosition.Y), marker);
    }
}
