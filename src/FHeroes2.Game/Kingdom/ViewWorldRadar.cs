using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using CastleType = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The radar panel of the View World window, <c>Radar::RedrawObjects</c> and
/// <c>Radar::QueueEventProcessingForWorldView</c> of <c>interface_radar.cpp:283-534</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考里世界视图的雷达是 <c>Radar</c> 的一个副本（`RadarType::ViewWorld`），ROI 固定为整张地图，
/// `_zoom` 沿用冒险地图雷达的那一个。端口把这一份单独拎出来：它不看 <c>Mp2Map</c>，而是走 <see cref="World"/>，
/// 所以复制的不是雷达本身，而是它「按模式着色」的那一半。
/// </para>
/// <para>
/// 着色规则（`RedrawObjects`）：迷雾内的格子按模式决定要不要画，可见格先看城镇（城镇的占地格都算城镇色），
/// 再看道路、地面、山与树（地面色 + 3）；英雄、矿、灯塔、炼金实验室、锯木场用所属颜色，宝物与资源是灰色。
/// </para>
/// </remarks>
public sealed class ViewWorldRadar
{
    private readonly World _world;
    private readonly byte[] _colors;

    private PixelBuffer _image;

    /// <summary>Creates the radar of <paramref name="world"/>; the panel is black until the first redraw.</summary>
    /// <param name="world">The world to render.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The world has no tiles.</exception>
    public ViewWorldRadar(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.Width <= 0 || world.Height <= 0)
        {
            throw new ArgumentException("The world has no tiles.", nameof(world));
        }

        _world = world;
        _colors = new byte[world.Width * world.Height];
        _image = new PixelBuffer(MapFrame.PanelSize, MapFrame.PanelSize);
    }

    /// <summary>Width of the map in tiles.</summary>
    public int Width => _world.Width;

    /// <summary>Height of the map in tiles.</summary>
    public int Height => _world.Height;

    /// <summary>The 144x144 panel of the last <see cref="RedrawObjects"/> call.</summary>
    public PixelBuffer Image => _image;

    /// <summary>The colour the last redraw gave one tile, before it was scaled onto the panel.</summary>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <returns>A palette index.</returns>
    public byte ColorAt(int x, int y) => _colors[(y * Width) + x];

    /// <summary>
    /// Colours the whole map for a View World mode, <c>Radar::RedrawObjects</c> (<c>interface_radar.cpp:283-419</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 ROI 在整个世界视图期间都是整张地图，所以这里没有 ROI 参数，也省掉了「只在整图重画时才清黑」的分支：
    /// 每次都从头填。<paramref name="friends"/> 是 <c>Players::FriendColors()</c>，即当前玩家的盟友集合。
    /// </remarks>
    /// <param name="friends">The colours whose fog the display may see through.</param>
    /// <param name="mode">What the window asks to see.</param>
    public void RedrawObjects(PlayerColor friends, ViewWorldMode mode)
    {
        bool revealAll = mode == ViewWorldMode.ViewAll;
        bool revealMines = revealAll || mode == ViewWorldMode.ViewMines;
        bool revealHeroes = revealAll || mode == ViewWorldMode.ViewHeroes;
        bool revealTowns = revealAll || mode == ViewWorldMode.ViewTowns;
        bool revealArtifacts = revealAll || mode == ViewWorldMode.ViewArtifacts;
        bool revealResources = revealAll || mode == ViewWorldMode.ViewResources;
        bool revealOnlyVisible = revealAll || mode == ViewWorldMode.OnlyVisible;

        Array.Fill(_colors, RadarPanel.BlackColor);

        for (int y = 0; y < Height; ++y)
        {
            for (int x = 0; x < Width; ++x)
            {
                Tile tile = _world.GetTile(x, y);
                bool visibleTile = revealAll || !tile.IsFog(friends);

                byte fillColor = RadarPanel.BlackColor;

                // Only the "visible only" and hero modes look through a hero standing on the tile; the other
                // modes want the hero itself, which is what the cached type holds.
                Mp2ObjectType objectType = tile.GetMainObjectType(_world, revealOnlyVisible || revealHeroes);
                Mp2ObjectType baseType = Mp2ObjectTypes.NonActionType(objectType);

                switch (baseType)
                {
                case Mp2ObjectType.Hero:
                    if (visibleTile || revealHeroes)
                    {
                        Hero? hero = _world.Heroes.GetAtMapIndex(tile.Index);

                        if (hero is not null)
                        {
                            fillColor = RadarPanel.GetKingdomColor(hero.Color);
                            break;
                        }
                    }

                    continue;

                case Mp2ObjectType.Lighthouse:
                case Mp2ObjectType.AlchemistLab:
                case Mp2ObjectType.Mine:
                case Mp2ObjectType.Sawmill:
                    if (visibleTile || revealMines)
                    {
                        // A multi tile object may be stored without the action bit: its owner then sits on
                        // the main tile of the object, not on the part the loop happens to be on.
                        int mainTileIndex = Mp2ObjectTypes.IsActionObject(objectType)
                            ? tile.Index
                            : Tile.GetIndexOfMainTile(tile, _world);

                        if (mainTileIndex >= 0)
                        {
                            fillColor = RadarPanel.GetKingdomColor(_world.ColorCapturedObject(mainTileIndex));
                            break;
                        }
                    }

                    continue;

                case Mp2ObjectType.Artifact:
                    if (visibleTile || revealArtifacts)
                    {
                        fillColor = RadarPanel.GrayColor;
                        break;
                    }

                    continue;

                case Mp2ObjectType.Resource:
                    if (visibleTile || revealResources)
                    {
                        fillColor = RadarPanel.GrayColor;
                        break;
                    }

                    continue;

                default:
                    if (visibleTile)
                    {
                        // Towns can be partially covered by other non action objects, so the town table is
                        // asked first: it knows every tile of the sprite the town occupies.
                        CastleType? castle = _world.Castles.Get(x, y);

                        if (castle is not null)
                        {
                            fillColor = RadarPanel.GetKingdomColor(castle.Color);
                        }
                        else if (tile.IsRoad)
                        {
                            fillColor = RadarPanel.RoadColor;
                        }
                        else
                        {
                            fillColor = RadarPanel.GetGroundColor(tile.Ground);

                            if (baseType is Mp2ObjectType.Mountains or Mp2ObjectType.Trees)
                            {
                                fillColor = (byte)(fillColor + RadarPanel.HillShadeOffset);
                            }
                        }
                    }
                    else if (revealTowns)
                    {
                        CastleType? castle = _world.Castles.Get(x, y);

                        if (castle is not null)
                        {
                            fillColor = RadarPanel.GetKingdomColor(castle.Color);
                        }
                    }
                    else
                    {
                        // The panel is already black, nothing to paint.
                        continue;
                    }

                    break;
                }

                _colors[(y * Width) + x] = fillColor;
            }
        }

        _image = RadarPanel.ScaleColors(_colors, Width, Height);
    }

    /// <summary>
    /// The frame that marks <paramref name="visibleTiles"/> on the panel, in panel pixels relative to the
    /// top left corner; empty when the whole map is visible.
    /// </summary>
    /// <param name="visibleTiles">The tiles the window shows, in world tiles.</param>
    /// <returns>The frame, or an empty rectangle.</returns>
    public Rect CursorArea(in Rect visibleTiles) => RadarPanel.CursorArea(visibleTiles, Width, Height);

    /// <summary>
    /// Draws the panel at <paramref name="origin"/> followed by the frame of <paramref name="visibleTiles"/>,
    /// <c>Radar::RedrawForViewWorld</c> (<c>interface_radar.cpp:253-267</c>).
    /// </summary>
    /// <param name="target">The screen buffer.</param>
    /// <param name="origin">Top left corner of the panel on the screen, <c>(536, 16)</c> for 640x480.</param>
    /// <param name="visibleTiles">The tiles the window shows.</param>
    public void Draw(PixelBuffer target, in Point origin, in Rect visibleTiles)
    {
        ImageOps.Blit(target, origin, _image, _image.Bounds);

        Rect cursor = CursorArea(visibleTiles);
        if (cursor.IsEmpty)
        {
            return;
        }

        RadarPanel.DrawCursorFrame(target, new Rect(origin.X + cursor.X, origin.Y + cursor.Y, cursor.Width, cursor.Height));
    }

    /// <summary>
    /// Moves the view so that the tile under <paramref name="panelPoint"/> is in its centre,
    /// <c>Radar::QueueEventProcessingForWorldView</c> (<c>interface_radar.cpp:501-534</c>).
    /// </summary>
    /// <param name="zoom">The view to move.</param>
    /// <param name="panelPoint">A point of the panel relative to its top left corner.</param>
    /// <returns><see langword="true"/> if the centre moved.</returns>
    /// <remarks>
    /// 参考先拿「像素单位的上左角」和「tile 单位的上左角」比了一下再决定要不要调 <c>ChangeCenter</c>，两个量单位不同，
    /// 这一比永远不等；端口直接调用，中心是否真的动了由 <see cref="ViewWorldZoom.ChangeCenter"/> 回答。
    /// </remarks>
    public bool CenterOn(ViewWorldZoom zoom, in Point panelPoint)
    {
        ArgumentNullException.ThrowIfNull(zoom);

        Point tile = new(
            panelPoint.X * Width / MapFrame.PanelSize,
            panelPoint.Y * Height / MapFrame.PanelSize);

        const int halfTile = ViewWorldZoomLevels.TileWidthPx / 2;

        return zoom.ChangeCenter(new Point(
            (tile.X * ViewWorldZoomLevels.TileWidthPx) - halfTile,
            (tile.Y * ViewWorldZoomLevels.TileWidthPx) - halfTile));
    }
}
