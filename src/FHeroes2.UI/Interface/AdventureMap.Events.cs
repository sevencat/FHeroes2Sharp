using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Audio;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.UI.Interface;

/// <summary>
/// The events the adventure screen answers key presses and button clicks with, <c>Interface::AdventureMap::Event*()</c>
/// and the three movement helpers above them (<c>gui/interface_events.cpp:77-641</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Deviations.</b> The reference opens dialogs and message boxes itself, through the global <c>Dialog::</c>
/// functions and through <c>fheroes2::showStandardTextMessage()</c>, both of which block until the player answers.
/// The port has no blocking dialogs (a dialog is an object its owner feeds events to), so every such call ends up in
/// <see cref="AdventureMapDialogs"/>, which runs the window loop itself. The same applies to <c>redraw()</c>: the
/// events request a repaint with <see cref="BaseInterface.SetRedraw"/> and let the frame loop paint, instead of
/// painting right away.
/// </para>
/// <para>
/// The titles and bodies of the messages are the reference's <c>_()</c> strings. The port has no translation layer
/// yet, so they are the English originals.
/// </para>
/// </remarks>
public sealed partial class AdventureMap
{
    /// <summary>
    /// Lays a route to a tile, or starts walking the route the hero already has, <c>ShowPathOrStartMoveHero()</c>
    /// (<c>interface_events.cpp:77-100</c>).
    /// </summary>
    /// <param name="hero">The hero to move.</param>
    /// <param name="destinationIndex">The tile to walk to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public void ShowPathOrStartMoveHero(Hero hero, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;

        if (!GetGeometry().IsValidAbsIndex(destinationIndex))
        {
            return;
        }

        HeroPath path = hero.Path;

        if (path.DestinationIndex != destinationIndex)
        {
            // Calculate and show the hero's path.
            hero.CalculatePath(destinationIndex, world);

            GameArea.SetRedraw();
            SetRedraw(Redraw.Buttons);
        }
        else if (path.IsValidForMovement(world.Size) && hero.MayStillMove(world, false, true))
        {
            // Start the hero's movement.
            StartHeroMove(hero);
        }
    }

    /// <summary>
    /// Routes the hero one tile in a direction, the arrow-key follower of the mouse, <c>MoveHeroFromArrowKeys()</c>
    /// (<c>interface_events.cpp:102-118</c>).
    /// </summary>
    /// <param name="hero">The hero to move.</param>
    /// <param name="direction">The direction of the key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public void MoveHeroFromArrowKeys(Hero hero, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;
        MapGeometry geometry = GetGeometry();
        int heroIndex = hero.MapIndex;

        if (!geometry.IsValidDirection(heroIndex, direction))
        {
            return;
        }

        int destinationIndex = geometry.GetDirectionIndex(heroIndex, direction);
        Tile tile = world.GetTile(destinationIndex);

        if (!tile.IsPassableFrom(world, Direction.Center, hero.IsShipMaster, false, hero.Color))
        {
            return;
        }

        ShowPathOrStartMoveHero(hero, destinationIndex);
    }

    /// <summary>
    /// Makes a hero walk the route it has, <c>_startHeroMove()</c> (<c>interface_events.cpp:120-131</c>).
    /// </summary>
    /// <param name="hero">The hero to wake up.</param>
    private void StartHeroMove(Hero hero)
    {
        SetFocus(hero, true);
        RedrawFocus();

        hero.SetMove(true);

        // The animation delay is passed so that the hero starts moving immediately and no second click is handled
        // while the first step is being animated.
        GameDelays.PassAnimationDelay(DelayType.CurrentHeroDelay);
    }

    /// <summary>
    /// Moves the focus to the hero standing on a tile, <c>EventSwitchFocusedHero()</c>
    /// (<c>interface_events.cpp:133-141</c>).
    /// </summary>
    /// <param name="tileIndex">The tile the player clicked on.</param>
    public void EventSwitchFocusedHero(int tileIndex)
    {
        World world = World.Current;

        if (!GetGeometry().IsValidAbsIndex(tileIndex))
        {
            return;
        }

        Hero? selectedHero = world.GetTile(tileIndex).GetHero(world);

        if (selectedHero == null || ReferenceEquals(selectedHero, InterfaceFocus.GetFocusHeroes()))
        {
            return;
        }

        if (Settings.Get().Players.Current is not { } player || selectedHero.Color != player.Color)
        {
            return;
        }

        SetFocus(selectedHero, false);
        RedrawFocus();
    }

    /// <summary>
    /// Focuses the next hero that can still move, <c>EventNextHero()</c> (<c>interface_events.cpp:143-179</c>).
    /// </summary>
    public void EventNextHero()
    {
        if (!TryGetCurrentKingdom(out Kingdom kingdom))
        {
            return;
        }

        World world = World.Current;
        IReadOnlyList<Hero> heroes = kingdom.Heroes;

        if (heroes.Count == 0)
        {
            return;
        }

        if (InterfaceFocus.GetFocusHeroes() is { } focused)
        {
            // The search starts after the focused hero and wraps around, so that a kingdom whose only moveable hero
            // is the focused one stops on it instead of looking forever. A focused hero that is not in the kingdom
            // list anymore (it just lost a battle) starts the search at the first hero, which is the slot after the
            // last one.
            int focusedIndex = heroes.Count - 1;

            for (int i = 0; i < heroes.Count; ++i)
            {
                if (ReferenceEquals(heroes[i], focused))
                {
                    focusedIndex = i;

                    break;
                }
            }

            for (int step = 1; step <= heroes.Count; ++step)
            {
                Hero candidate = heroes[(focusedIndex + step) % heroes.Count];

                if (candidate.MayStillMove(world, true, false))
                {
                    SetFocus(candidate, false);

                    break;
                }
            }
        }
        else
        {
            foreach (Hero hero in heroes)
            {
                if (hero.MayStillMove(world, true, false))
                {
                    SetFocus(hero, false);

                    break;
                }
            }
        }

        RedrawFocus();
    }

    /// <summary>
    /// Walks the focused hero, or repeats the action under it, <c>EventHeroMovement()</c>
    /// (<c>interface_events.cpp:181-198</c>).
    /// </summary>
    /// <returns><see cref="AdventureEventResult.Cancel"/> unless the action under the hero decided the scenario.</returns>
    public AdventureEventResult EventHeroMovement()
    {
        World world = World.Current;

        if (InterfaceFocus.GetFocusHeroes() is not { } hero)
        {
            return AdventureEventResult.Cancel;
        }

        if (hero.Path.IsValidForMovement(world.Size) && hero.MayStillMove(world, false, true))
        {
            StartHeroMove(hero);
        }
        else if (Mp2ObjectTypes.IsRevisitAllowedForObject(hero.ObjectTypeUnderHero, hero.IsShipMaster))
        {
            return EventDefaultAction();
        }
        else if (hero.Path.IsValidForMovement(world.Size))
        {
            _dialogs.ShowMessage(
                "Hero Movement Points",
                "The hero's army cannot move anymore today. The movement points will be refilled tomorrow after you end your turn.");
        }

        return AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Drops the route of the focused hero, <c>EventResetHeroPath()</c> (<c>interface_events.cpp:200-210</c>).
    /// </summary>
    public void EventResetHeroPath()
    {
        if (InterfaceFocus.GetFocusHeroes() is not { } hero)
        {
            return;
        }

        hero.Path.Reset();

        SetRedraw(Redraw.GameArea | Redraw.Buttons);
    }

    /// <summary>
    /// Opens the kingdom overview, <c>EventKingdomInfo()</c> (<c>interface_events.cpp:212-218</c>).
    /// </summary>
    public void EventKingdomInfo()
    {
        _dialogs.OpenKingdomOverview();

        _iconsPanel.SetRedraw();
    }

    /// <summary>
    /// Opens the spell book of the focused hero and casts what the player picks, <c>EventCastSpell()</c>
    /// (<c>interface_events.cpp:220-242</c>).
    /// </summary>
    public void EventCastSpell()
    {
        if (InterfaceFocus.GetFocusHeroes() is not { } hero)
        {
            return;
        }

        // Center on the hero before opening the spell book.
        GameArea.SetCenter(GetCenterOf(hero.MapIndex));
        SetRedraw(Redraw.GameArea | Redraw.RadarCursor);

        if (!_dialogs.OpenSpellBook(hero, out Game.Spells.Spell spell))
        {
            return;
        }

        hero.ActionSpellCast(spell, World.Current);

        // The spell consumes spell points (and maybe movement points) and can move the hero, so the terrain music
        // and the ambient sounds have to be worked out again.
        ResetFocus((int)GameFocus.Heroes, true);
        RedrawFocus();
    }

    /// <summary>
    /// Ends the turn of the current player, asking first while heroes can still move, <c>EventEndTurn()</c>
    /// (<c>interface_events.cpp:244-261</c>).
    /// </summary>
    /// <returns><see cref="AdventureEventResult.EndTurn"/>, or <see cref="AdventureEventResult.Cancel"/>.</returns>
    public AdventureEventResult EventEndTurn()
    {
        if (!TryGetCurrentKingdom(out Kingdom kingdom))
        {
            return AdventureEventResult.Cancel;
        }

        World world = World.Current;

        if (!kingdom.HeroesMayStillMove(world)
            || _dialogs.ShowYesNo("End Turn", "One or more heroes may still move, are you sure you want to end your turn?"))
        {
            return AdventureEventResult.EndTurn;
        }

        return AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Opens the adventure options dialog and runs what was picked, <c>EventAdventureDialog()</c>
    /// (<c>interface_events.cpp:263-285</c>).
    /// </summary>
    /// <returns>The result of the dig, if digging was picked.</returns>
    public AdventureEventResult EventAdventureDialog()
    {
        switch (_dialogs.ShowAdventureOptions(InterfaceFocus.GetFocusType() == GameFocus.Heroes))
        {
        case AdventureOption.World:
            EventViewWorld();

            break;

        case AdventureOption.Puzzle:
            EventPuzzleMaps();

            break;

        case AdventureOption.Info:
            return EventScenarioInformation();

        case AdventureOption.Dig:
            return EventDigArtifact();

        default:
            break;
        }

        return AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Opens the world map, <c>EventViewWorld()</c> (<c>interface_events.cpp:287-290</c>).
    /// </summary>
    /// <remarks>
    /// 参考把当前玩家色与 <c>OnlyVisible</c> 交给 <c>ViewWorld::ViewWorldWindow()</c>，窗口阻塞到玩家关掉；缩放档写回设置
    /// （<c>conf.SetViewWorldZoomLevel</c>）留给场景。视图还没接线时（<see cref="AttachView"/> 没被调用）什么都不做。
    /// </remarks>
    public void EventViewWorld()
    {
        if (_view is not { } view)
        {
            return;
        }

        _viewWorldZoom = _dialogs.OpenViewWorld(ViewWorldMode.OnlyVisible, view, _viewWorldZoom, _viewport);
    }

    /// <summary>
    /// Opens the file options dialog, <c>EventFileDialog()</c> (<c>interface_events.cpp:292-295</c>).
    /// </summary>
    /// <returns>What the file dialog decided.</returns>
    public AdventureEventResult EventFileDialog() => _dialogs.ShowFileOptions();

    /// <summary>
    /// Opens the system options dialog, <c>EventSystemDialog()</c> (<c>interface_events.cpp:297-300</c>).
    /// </summary>
    /// <remarks>
    /// 参考只有一句 <c>fheroes2::showSystemOptionsDialog()</c>；<see cref="ShowSystemOptions"/> 上的两个可选参数是给
    /// 场景注入语言选择器与环境音混音器用的，屏幕自己不开这两个子流程。
    /// </remarks>
    public void EventSystemDialog() => ShowSystemOptions();

    /// <summary>
    /// Focuses the next castle, or the first one when a hero is focused, <c>EventNextTown()</c>
    /// (<c>interface_events.cpp:302-320</c>).
    /// </summary>
    public void EventNextTown()
    {
        if (!TryGetCurrentKingdom(out Kingdom kingdom))
        {
            return;
        }

        IReadOnlyList<CastleTown> castles = kingdom.Castles;

        if (castles.Count == 0)
        {
            return;
        }

        if (InterfaceFocus.GetFocusCastle() is { } focused)
        {
            int next = 0;

            for (int i = 0; i < castles.Count; ++i)
            {
                if (ReferenceEquals(castles[i], focused))
                {
                    next = (i + 1) % castles.Count;

                    break;
                }
            }

            SetFocus(castles[next]);
        }
        else
        {
            ResetFocus((int)GameFocus.Castle, false);
        }

        RedrawFocus();
    }

    /// <summary>
    /// Asks whether the restarted game is really wanted, <c>EventNewGame()</c> (<c>interface_events.cpp:322-327</c>).
    /// </summary>
    /// <returns><see cref="AdventureEventResult.NewGame"/>, or <see cref="AdventureEventResult.Cancel"/>.</returns>
    public AdventureEventResult EventNewGame()
    {
        return _dialogs.ShowYesNo("", "Are you sure you want to restart? (Your current game will be lost.)")
                   ? AdventureEventResult.NewGame
                   : AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Asks for a file name and saves the game, <c>EventSaveGame()</c> (<c>interface_events.cpp:329-350</c>).
    /// </summary>
    /// <returns>Always <see cref="AdventureEventResult.Cancel"/>: saving does not leave the map.</returns>
    /// <remarks>
    /// 选择窗口、覆盖确认、写盘和结果提示都已在 <see cref="AdventureMapDialogs.SaveGame"/> 里，和参考在
    /// <c>Interface::AdventureMap</c> 内部完成同样的事；这里只把结果翻译成事件返回值。
    /// </remarks>
    public AdventureEventResult EventSaveGame() => _dialogs.SaveGame();

    /// <summary>
    /// Asks whether loading really is wanted, <c>EventLoadGame()</c> (<c>interface_events.cpp:352-359</c>).
    /// </summary>
    /// <returns><see cref="AdventureEventResult.LoadGame"/>, or <see cref="AdventureEventResult.Cancel"/>.</returns>
    public AdventureEventResult EventLoadGame()
    {
        return _dialogs.ShowYesNo("", "Are you sure you want to load a new game? (Your current game will be lost.)")
                   ? AdventureEventResult.LoadGame
                   : AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Opens the puzzle map, <c>EventPuzzleMaps()</c> (<c>interface_events.cpp:361-364</c>).
    /// </summary>
    public void EventPuzzleMaps() => _dialogs.ShowPuzzleMap(GameArea, Radar.Area);

    /// <summary>
    /// Opens the scenario information, <c>EventScenarioInformation()</c> (<c>interface_events.cpp:366-396</c>).
    /// </summary>
    /// <returns>Always <see cref="AdventureEventResult.Cancel"/>.</returns>
    /// <remarks>
    /// The reference has a campaign branch that fades out, selects a scenario
    /// (<c>Game::SelectCampaignScenario()</c>) and either restores the map or leaves for the campaign screen. Campaign
    /// selection is not ported, so the port only does the non-campaign part — the scenario information — and the
    /// branch has no way to ask for another mode yet.
    /// </remarks>
    public AdventureEventResult EventScenarioInformation()
    {
        // 战役局在参考里走的是「淡出 → 选关 → 淡入」，选关界面还没搬，端口在战役局里也照样只开这一张信息表。
        _dialogs.ShowGameInfo();

        return AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Puts the focused hero to sleep or wakes it up, <c>EventSwitchHeroSleeping()</c>
    /// (<c>interface_events.cpp:398-408</c>).
    /// </summary>
    public void EventSwitchHeroSleeping()
    {
        if (InterfaceFocus.GetFocusHeroes() is not { } hero)
        {
            return;
        }

        if (hero.Modes.HasFlag(HeroModes.Sleeper))
        {
            hero.ResetModes(HeroModes.Sleeper);
        }
        else
        {
            hero.SetModes(HeroModes.Sleeper);
        }

        SetRedraw(Redraw.Heroes);
        SetRedraw(Redraw.Buttons);
    }

    /// <summary>
    /// Digs for the ultimate artifact, <c>EventDigArtifact()</c> (<c>interface_events.cpp:410-479</c>).
    /// </summary>
    /// <returns>The game-over evaluation after a successful dig.</returns>
    public AdventureEventResult EventDigArtifact()
    {
        World world = World.Current;

        if (InterfaceFocus.GetFocusHeroes() is not { } hero)
        {
            return AdventureEventResult.Cancel;
        }

        if (hero.IsShipMaster)
        {
            _dialogs.ShowMessage("", "Try looking on land!!!");

            return AdventureEventResult.Cancel;
        }

        if (hero.BagArtifacts.IsFull)
        {
            _dialogs.ShowMessage(
                "",
                "Searching for the Ultimate Artifact is fruitless. Your hero could not carry it even if he found it - all his artifact slots are full.");

            return AdventureEventResult.Cancel;
        }

        if (hero.GetMaxMovePoints() > hero.MovePoints)
        {
            _dialogs.ShowMessage("", "Digging for artifacts requires a whole day, try again tomorrow.");

            return AdventureEventResult.Cancel;
        }

        // The original editor allows to put the ultimate artifact on an invalid tile, so the tile index is checked.
        var ultimateArtifact = world.UltimateArtifact;
        int heroIndex = hero.MapIndex;

        if (!world.GetTile(heroIndex).IsSuitableForUltimateArtifact(world)
            && (!ultimateArtifact.IsAt(heroIndex) || ultimateArtifact.IsFound))
        {
            _dialogs.ShowMessage("", "Try searching on clear ground.");

            return AdventureEventResult.Cancel;
        }

        _audioManager?.PlaySound(M82Sound.Digsound);

        hero.ResetMovePoints();
        hero.Path.Reset();

        void Announce()
        {
            hero.PickupArtifact(ultimateArtifact.Artifact, world);

            _dialogs.ShowMessage("Congratulations!", $"After spending many hours digging here, you have uncovered the {ultimateArtifact.Name}.");
        }

        if (world.DiggingForUltimateArtifact(GetCenterOf(heroIndex)))
        {
            // 没有声卡时参考的 Music/Mixer 调用全是空操作，号角与「先停地形音乐再还回去」都没有意义，直接跳过。
            if (_audioManager is null)
            {
                Announce();
            }
            else
            {
                // The fanfare interrupts the terrain music and has to give it back afterwards.
                using (new MusicRestorer(_audioManager))
                {
                    if (Settings.Get().IsMusicTypeMidi)
                    {
                        // The MIDI soundtrack has no fanfare track, so the treasure sound stands in for it.
                        _audioManager.PlaySound(M82Sound.Treasure);
                    }
                    else
                    {
                        _audioManager.PlayMusicAsync(MusicTrack.UltimateArtifact, MusicPlaybackMode.PlayOnce);
                    }

                    Announce();
                }
            }
        }
        else
        {
            _dialogs.ShowMessage("", "Nothing here. Where could it be?");
        }

        SetRedraw(Redraw.Heroes | Redraw.Buttons);

        // The dig can be the action that decides the scenario.
        return _dialogs.CheckGameOver();
    }

    /// <summary>
    /// Runs the object action under the focused hero, or opens the focused castle, <c>EventDefaultAction()</c>
    /// (<c>interface_events.cpp:481-506</c>).
    /// </summary>
    /// <returns>The game-over evaluation when the action decided the scenario.</returns>
    public AdventureEventResult EventDefaultAction()
    {
        World world = World.Current;

        if (InterfaceFocus.GetFocusHeroes() is { } hero)
        {
            if (Mp2ObjectTypes.IsRevisitAllowedForObject(hero.ObjectTypeUnderHero, hero.IsShipMaster))
            {
                // Heroes::Action() is the hero's own action handler; the port's HeroActions takes the hero it works
                // on, so the call builds one for the action.
                new HeroActions(hero, world.Kingdoms).Action(hero.MapIndex, world);

                // The action object can change the hero's status (a well, stables) or move it (a whirlpool, the
                // stone liths).
                ResetFocus((int)GameFocus.Heroes, true);
                RedrawFocus();

                if (hero.Modes.HasFlag(HeroModes.Action))
                {
                    hero.ResetModes(HeroModes.Action);

                    return _dialogs.CheckGameOver();
                }
            }
        }
        else if (InterfaceFocus.GetFocusCastle() is { } castle)
        {
            _dialogs.OpenCastleDialog(castle);
        }

        return AdventureEventResult.Cancel;
    }

    /// <summary>
    /// Opens the dialog of whatever is focused, <c>EventOpenFocus()</c> (<c>interface_events.cpp:508-516</c>).
    /// </summary>
    public void EventOpenFocus()
    {
        if (InterfaceFocus.GetFocusHeroes() is { } hero)
        {
            _dialogs.OpenHeroesDialog(hero);
        }
        else if (InterfaceFocus.GetFocusCastle() is { } castle)
        {
            _dialogs.OpenCastleDialog(castle);
        }
    }

    /// <summary>
    /// Shows or hides the radar, <c>EventSwitchShowRadar()</c> (<c>interface_events.cpp:518-532</c>).
    /// </summary>
    /// <remarks>The five show/hide events only do anything while the hide-interface mode is on, like the reference.</remarks>
    public void EventSwitchShowRadar()
    {
        Settings settings = Settings.Get();

        if (!settings.IsHideInterface)
        {
            return;
        }

        if (settings.IsShowRadar)
        {
            settings.SetShowRadar(false);
            GameArea.SetRedraw();
        }
        else
        {
            settings.SetShowRadar(true);
            SetRedraw(Redraw.Radar);
        }
    }

    /// <summary>
    /// Shows or hides the button bar, <c>EventSwitchShowButtons()</c> (<c>interface_events.cpp:534-548</c>).
    /// </summary>
    public void EventSwitchShowButtons()
    {
        Settings settings = Settings.Get();

        if (!settings.IsHideInterface)
        {
            return;
        }

        if (settings.IsShowButtons)
        {
            settings.SetShowButtons(false);
            GameArea.SetRedraw();
        }
        else
        {
            settings.SetShowButtons(true);
            SetRedraw(Redraw.Buttons);
        }
    }

    /// <summary>
    /// Shows or hides the status panel, <c>EventSwitchShowStatus()</c> (<c>interface_events.cpp:550-564</c>).
    /// </summary>
    public void EventSwitchShowStatus()
    {
        Settings settings = Settings.Get();

        if (!settings.IsHideInterface)
        {
            return;
        }

        if (settings.IsShowStatus)
        {
            settings.SetShowStatus(false);
            GameArea.SetRedraw();
        }
        else
        {
            settings.SetShowStatus(true);
            _statusPanel.SetRedraw();
        }
    }

    /// <summary>
    /// Shows or hides the icons bar, <c>EventSwitchShowIcons()</c> (<c>interface_events.cpp:566-580</c>).
    /// </summary>
    public void EventSwitchShowIcons()
    {
        Settings settings = Settings.Get();

        if (!settings.IsHideInterface)
        {
            return;
        }

        if (settings.IsShowIcons)
        {
            settings.SetShowIcons(false);
            GameArea.SetRedraw();
        }
        else
        {
            settings.SetShowIcons(true);
            _iconsPanel.SetRedraw();
        }
    }

    /// <summary>
    /// Shows or hides the control panel, <c>EventSwitchShowControlPanel()</c> (<c>interface_events.cpp:582-590</c>).
    /// </summary>
    public void EventSwitchShowControlPanel()
    {
        Settings settings = Settings.Get();

        if (!settings.IsHideInterface)
        {
            return;
        }

        settings.SetShowControlPanel(!settings.ShowControlPanel);
        GameArea.SetRedraw();
    }

    /// <summary>
    /// Moves the focused hero with an arrow key, or scrolls the map when no hero can move, <c>EventKeyArrowPress()</c>
    /// (<c>interface_events.cpp:592-641</c>).
    /// </summary>
    /// <param name="direction">The direction of the key.</param>
    public void EventKeyArrowPress(Direction direction)
    {
        if (InterfaceFocus.GetFocusHeroes() is { } hero)
        {
            MoveHeroFromArrowKeys(hero, direction);

            return;
        }

        if (GameArea.IsDragScroll())
        {
            return;
        }

        if (Settings.Get().ScrollSpeed == AdventureMapScrollSpeed.None)
        {
            // Scrolling is disabled.
            return;
        }

        switch (direction)
        {
        case Direction.TopLeft:
            GameArea.SetScroll(ScrollingType.Top);
            GameArea.SetScroll(ScrollingType.Left);

            break;

        case Direction.Top:
            GameArea.SetScroll(ScrollingType.Top);

            break;

        case Direction.TopRight:
            GameArea.SetScroll(ScrollingType.Top);
            GameArea.SetScroll(ScrollingType.Right);

            break;

        case Direction.Right:
            GameArea.SetScroll(ScrollingType.Right);

            break;

        case Direction.BottomRight:
            GameArea.SetScroll(ScrollingType.Bottom);
            GameArea.SetScroll(ScrollingType.Right);

            break;

        case Direction.Bottom:
            GameArea.SetScroll(ScrollingType.Bottom);

            break;

        case Direction.BottomLeft:
            GameArea.SetScroll(ScrollingType.Bottom);
            GameArea.SetScroll(ScrollingType.Left);

            break;

        case Direction.Left:
            GameArea.SetScroll(ScrollingType.Left);

            break;

        default:
            break;
        }
    }

    /// <summary>The realm of the player whose turn it is, or <see langword="false"/> when there is none.</summary>
    private static bool TryGetCurrentKingdom(out Kingdom kingdom)
    {
        if (Settings.Get().Players.Current is { } player)
        {
            kingdom = World.Current.Kingdoms.GetKingdom(player.Color);

            return true;
        }

        kingdom = null!;

        return false;
    }

    /// <summary>The geometry of the current map, <c>Maps::isValidAbsIndex()</c> and friends' owner.</summary>
    private static MapGeometry GetGeometry()
    {
        return World.Current.Geometry
               ?? throw new InvalidOperationException("The adventure screen needs a map of non-zero size.");
    }
}
