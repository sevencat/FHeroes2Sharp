using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Interface;

/// <summary>
/// Picking the square a Dimension Door lands in, <c>Interface::AdventureMap::GetDimensionDoorDestination</c>
/// (<c>game/game_interface.cpp:175-323</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把地图借去当选择器：雷达区换成次元门的控制面板（<c>VIEWDDOR</c>/<c>EVIWDDOR</c>）加一个 EXIT，地图上以
/// 英雄为中心压暗一块方形之外的画面，框内的格子可以点，点下的那一格就是落点。循环一直跑到点中或 EXIT。
/// </para>
/// <para>
/// 隐藏界面模式下四个面板先收起来（地图铺满屏幕），框外压暗用 <see cref="InvertedShadow"/>；普通模式下框外的
/// 像素按 <c>InvertedFadeWithPalette</c> 的节奏淡出。两者的稳定态都是「框内原样、框外发灰」，差别只在头几帧，
/// 所以这里共用同一套重画，淡出只在进入时跑一遍。
/// </para>
/// <para>
/// 光标按参考切三个主题：框内可点是 HERO_MOVE，其余是 POINTER，地图与雷达之外是 WAR_NONE。
/// </para>
/// </remarks>
public sealed partial class AdventureMap
{
    /// <summary>
    /// Runs the square picker of a Dimension Door until the player picks a tile or gives up.
    /// </summary>
    /// <param name="from">The hero's tile index, the centre of the square.</param>
    /// <param name="distance">Spell points spent, which is the number of tiles the door reaches.</param>
    /// <param name="isWater">Whether the hero is sailing, <c>isHeroWaterPath</c>.</param>
    /// <returns>The tile picked, or -1 when the player cancelled.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The distance is negative.</exception>
    internal int SelectDimensionDoorDestination(int from, int distance, bool isWater)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        LocalEvent input = LocalEvent.Current;

        if (input.Engine is not { } engine || _sprites is not { } sprites)
        {
            GameLog.Warn("The Dimension Door picker needs the frame pump and the map artwork; cancelling.");

            return -1;
        }

        bool evil = _isCurrentInterfaceEvil;
        IcnType panelAsset = evil ? IcnType.Eviwddor : IcnType.Viewddor;
        IcnType exitAsset = evil ? IcnType.ButtonExitPuzzleDimDoorEvil : IcnType.ButtonExitPuzzleDimDoorGood;

        if (sprites.GetSprite(panelAsset, 0) is not { } panel ||
            sprites.GetSprite(exitAsset, 0) is not { } exitReleased ||
            sprites.GetSprite(exitAsset, 1) is not { } exitPressed)
        {
            GameLog.Warn("The Dimension Door picker needs VIEWDDOR/EVIWDDOR and the EXIT button; cancelling.");

            return -1;
        }

        Rect radarArea = Radar.RenderArea;
        Rect radarRect = Radar.GetRect;
        ButtonSprite exitButton = new(exitReleased, exitPressed,
            offsetX: radarArea.X + 32, offsetY: radarArea.Y + radarArea.Height - 37);

        Settings settings = Settings.Get();
        bool hideInterface = settings.IsHideInterface;
        bool iconsShown = settings.IsShowIcons;
        bool buttonsShown = settings.IsShowButtons;
        bool statusShown = settings.IsShowStatus;
        bool controlShown = settings.ShowControlPanel;

        if (hideInterface)
        {
            // 参考先把面板收起来再重画，这样框外的压暗覆盖整块地图而不是被面板挡住。
            settings.SetShowIcons(false);
            settings.SetShowButtons(false);
            settings.SetShowStatus(false);
            settings.SetShowControlPanel(false);
            RedrawInterface(Redraw.All);
        }

        World world = World.Current;
        Point heroTile = new(from % world.Width, from / world.Width);
        Point heroPosition = GameArea.GetRelativeTilePosition(heroTile) +
                             new Point(GameBorder.TileWidth / 2, GameBorder.TileWidth / 2);
        Rect spellArea = DimensionDoorArea.Compute(heroPosition, GameArea.Roi, distance, hideInterface);

        int picked = -1;
        int frame = 0;

        try
        {
            DrawMap();

            while (input.HandleEvents())
            {
                bool exitHeld = input.IsMouseLeftButtonPressedAndHeldInArea(exitButton.Area);

                if (input.MouseClickLeft(exitButton.Area) || input.IsKeyPressed(Key.Escape))
                {
                    break;
                }

                if (input.IsMouseCursorPosInArea(radarArea))
                {
                    CursorState.Current?.SetThemes(CursorTheme.Pointer);
                }
                else if (input.IsMouseCursorPosInArea(GameArea.Roi))
                {
                    int target = GameArea.GetValidTileIdFromPoint(input.CursorPosition);

                    if (!DimensionDoorArea.CanSelect(spellArea, input.CursorPosition, target, world, isWater))
                    {
                        CursorState.Current?.SetThemes(CursorTheme.Pointer);
                    }
                    else if (input.MouseClickLeft())
                    {
                        picked = target;

                        break;
                    }
                    else if (input.IsMouseRightButtonPressedInArea(GameArea.Roi))
                    {
                        if (world.GetTile(target) is { } tile)
                        {
                            _dialogs.ShowQuickInfo(tile, input);
                        }
                    }
                    else
                    {
                        CursorState.Current?.SetThemes(CursorTheme.HeroMove);
                    }
                }
                else if (!input.IsMouseCursorPosInArea(radarRect))
                {
                    CursorState.Current?.SetThemes(CursorTheme.CombatNone);
                }

                // 头几帧按 InvertedFadeWithPalette 的节奏把框外压暗，之后地图照常动、压暗跟着重画。
                if (frame < DimensionDoorFadeFrames)
                {
                    InvertedShadow.Apply(_display, GameArea.Roi, spellArea, PaletteId.Gray, 1);
                    engine.Render(_display.Bounds);
                    ++frame;

                    continue;
                }

                DrawMap();

                if (!exitHeld)
                {
                    engine.Sleep(DimensionDoorFrameDelayMs);
                }
            }
        }
        finally
        {
            if (hideInterface)
            {
                settings.SetShowIcons(iconsShown);
                settings.SetShowButtons(buttonsShown);
                settings.SetShowStatus(statusShown);
                settings.SetShowControlPanel(controlShown);
            }

            RedrawInterface(Redraw.All);
            CursorState.Current?.SetThemes(CursorTheme.Pointer);
        }

        return picked;

        void DrawMap()
        {
            // 地图连同雷达与面板一起重画，然后把控制面板压在雷达区上、EXIT 压在面板上。
            RedrawInterface(Redraw.GameArea | Redraw.Radar);

            InvertedShadow.Apply(_display, GameArea.Roi, spellArea, PaletteId.Gray, 9);
            ImageOps.BlitSprite(_display, radarArea.Position, panel);
            exitButton.DrawOnState(input.IsMouseLeftButtonPressedAndHeldInArea(exitButton.Area), _display);

            engine.Render(_display.Bounds);
        }
    }

    /// <summary>Frames the frame-out of the area outside the square takes, <c>300 ms / 9 PAL::InvertedFadeWithPalette</c>.</summary>
    private const int DimensionDoorFadeFrames = 9;

    /// <summary>Delay between two map frames of the picker, <c>Game::validateCustomAnimationDelay()</c>.</summary>
    private const ulong DimensionDoorFrameDelayMs = 40;
}
