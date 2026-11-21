using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// One cell of the construction window, <c>BuildingInfo::Redraw()</c>/<c>RedrawCaptain()</c>
/// (<c>buildinginfo.cpp:318-412</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一个格子的画法固定三层：<c>BLDGXTRA</c> 的相框、该种族的建筑图标、右下角的状态标记；名字写在相框底部那条
/// 文字底上（能建是 <c>CASLXTRA</c> 1，不能建是 2），文字底压住 58 像素以下那一行。
/// </para>
/// <para>
/// 船长的格子不一样：它的相框是船长图本身，右下角换成 83/79 这组比重更靠里的锚点。
/// </para>
/// </remarks>
public sealed class ConstructionCell
{
    /// <summary>The width the name is truncated to, <c>fitToOneRow( 125 )</c>.</summary>
    public const int NameMaxWidth = 125;

    /// <summary>The left edge of the name's centring point inside a cell, <c>area.x + 68</c>.</summary>
    private const int NameCenterX = 68;

    /// <summary>The top edge of the name, <c>area.y + 61</c>.</summary>
    private const int NameTop = 61;

    /// <summary>The top edge of the caption strip, <c>area.y + 58</c>.</summary>
    private const int CaptionTop = 58;

    /// <summary>Where the grayed-out caption of a disabled cell starts, <c>( 6, 59 )</c>.</summary>
    private static readonly Point GrayedCaptionOffset = new(6, 59);

    /// <summary>The size of that grayed-out caption, 125×12.</summary>
    private static readonly Size GrayedCaptionSize = new(125, 12);

    private readonly IGlyphSource _smallFont;

    /// <summary>Wraps one building of the window.</summary>
    /// <param name="info">The building and its position inside the window.</param>
    /// <param name="smallFont">The font of the cell's name.</param>
    public ConstructionCell(BuildingInfo info, IGlyphSource smallFont)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(smallFont);

        Info = info;
        _smallFont = smallFont;
    }

    /// <summary>The building shown in this cell.</summary>
    public BuildingInfo Info { get; }

    /// <summary>Where the cell is inside the window.</summary>
    public Rect Area => Info.Area;

    /// <summary>The name written under the cell, already fitted into 125 pixels.</summary>
    public string Name => BuildDialogView.FitToOneRow(_smallFont, Info.Name, NameMaxWidth);

    /// <summary>Whether this cell is the captain's.</summary>
    public bool IsCaptain => Info.Type == BuildingType.Captain;

    /// <summary>
    /// Paints the cell, <c>BuildingInfo::Redraw()</c>.
    /// </summary>
    /// <param name="target">The frame the window is drawn into.</param>
    /// <param name="artwork">The frames of the window.</param>
    public void Draw(PixelBuffer target, ConstructionArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(artwork);

        if (IsCaptain)
        {
            DrawCaptain(target, artwork);

            return;
        }

        Rect area = Area;

        ImageOps.BlitSprite(target, new Point(area.X, area.Y), artwork.CellFrame);

        if (Info.Status is BuildingStatus.BuildDisabled or BuildingStatus.ShipyardNotAllowed)
        {
            DrawGrayedCaption(target, artwork, area);
        }

        if (Info.Type == BuildingType.Nothing)
        {
            // Nothing is ever built here, so the whole cell is replaced (including the caption strip).
            ImageOps.BlitSprite(target, new Point(area.X, area.Y), artwork.EmptyCell);

            return;
        }

        if (artwork.GetBuildingIcon(Info.Type) is { } icon)
        {
            ImageOps.BlitSprite(target, new Point(area.X + 1, area.Y + 1), icon);
        }

        DrawIndicator(target, artwork, area, Info.Status);

        if (Info.Status == BuildingStatus.AlreadyBuilt)
        {
            return;
        }

        Sprite caption = Info.Status == BuildingStatus.AllowBuild ? artwork.BuildableStrip : artwork.DeniedStrip;
        ImageOps.BlitSprite(target, new Point(area.X, area.Y + CaptionTop), caption);

        string name = Name;
        int width = GlyphText.Measure(_smallFont, name);
        GlyphText.DrawLine(
            _smallFont,
            target,
            new Point(area.X + NameCenterX - (width / 2), area.Y + NameTop),
            name);
    }

    /// <summary>The captain's cell, <c>RedrawCaptain()</c> (<c>buildinginfo.cpp:318-347</c>).</summary>
    private void DrawCaptain(PixelBuffer target, ConstructionArtwork artwork)
    {
        Rect area = Area;

        if (Info.Status == BuildingStatus.AlreadyBuilt)
        {
            ImageOps.BlitSprite(target, new Point(area.X, area.Y), artwork.CaptainBuilt);

            Point flagOffset = ConstructionLayout.GetCaptainFlagOffset(Info.Castle.Race);
            ImageOps.BlitSprite(
                target,
                new Point(area.X + flagOffset.X, area.Y + flagOffset.Y),
                artwork.Flag);
        }
        else
        {
            ImageOps.BlitSprite(target, new Point(area.X, area.Y), artwork.CaptainEmpty);
        }

        DrawIndicator(target, artwork, area, Info.Status, captain: true);
    }

    /// <summary>The tick, the coin or the cross in the corner of a cell.</summary>
    private static void DrawIndicator(PixelBuffer target, ConstructionArtwork artwork, Rect area, BuildingStatus status, bool captain = false)
    {
        // The captain's cell is 135×70 as every other one, but the reference anchors its marker on 83/79
        // because the picture is not a frame.
        int right = captain ? area.X + 83 : area.X + artwork.CellFrame.Width;
        int bottom = captain ? area.Y + 79 : area.Y + 58;

        switch (status)
        {
            case BuildingStatus.AlreadyBuilt:
                DrawMark(target, new Point(right - 5 - artwork.BuiltMark.Width, bottom - 2 - artwork.BuiltMark.Height), artwork.BuiltMark);

                break;

            case BuildingStatus.BuildDisabled:
            case BuildingStatus.ShipyardNotAllowed:
                DrawMark(
                    target,
                    new Point(right - 5 + 1 - artwork.DeniedMark.Width, bottom - 2 - artwork.DeniedMark.Height),
                    artwork.DeniedMark,
                    grayed: true);

                break;

            case BuildingStatus.LackResources:
                DrawMark(
                    target,
                    new Point(right - 5 + 1 - artwork.UnaffordableMark.Width, bottom - 3 - artwork.UnaffordableMark.Height),
                    artwork.UnaffordableMark);

                break;

            case BuildingStatus.AllowBuild:
                break;

            default:
                // RequiresBuild and everything else uses the plain cross.
                DrawMark(
                    target,
                    new Point(right - 5 + 1 - artwork.DeniedMark.Width, bottom - 2 - artwork.DeniedMark.Height),
                    artwork.DeniedMark);

                break;
        }
    }

    internal static void DrawMark(PixelBuffer target, Point point, Sprite mark, bool grayed = false)
    {
        if (!grayed)
        {
            ImageOps.BlitSprite(target, point, mark);

            return;
        }

        // A disabled cell's cross is flattened twice, exactly like the reference: gray, then darkened.
        PixelBuffer buffer = mark.Image.Clone();
        ImageOps.ApplyPalette(buffer, PaletteId.Gray);
        ImageOps.ApplyPalette(buffer, PaletteId.Darkening);

        ImageOps.Blit(target, point, buffer, buffer.Bounds);
    }

    /// <summary>The grayed-out caption of a cell that is never offered, <c>buildinginfo.cpp:361-367</c>.</summary>
    private static void DrawGrayedCaption(PixelBuffer target, ConstructionArtwork artwork, Rect area)
    {
        PixelBuffer strip = new(GrayedCaptionSize.Width, GrayedCaptionSize.Height);
        strip.Fill(0);

        ImageOps.Blit(
            strip,
            new Point(0, 0),
            artwork.CellFrame.Image,
            new Rect(GrayedCaptionOffset.X, GrayedCaptionOffset.Y, GrayedCaptionSize.Width, GrayedCaptionSize.Height));

        ImageOps.ApplyPalette(strip, PaletteId.Gray);
        ImageOps.ApplyPalette(strip, PaletteId.Darkening);

        ImageOps.Blit(
            target,
            new Point(area.X + GrayedCaptionOffset.X, area.Y + GrayedCaptionOffset.Y),
            strip,
            strip.Bounds);
    }
}
