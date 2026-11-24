using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;
using CastleTown = FHeroes2.Game.Castle.Castle;
using WorldMap = FHeroes2.Game.World;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Walking a hero across the map: <c>heroes/heroes_move.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file is one state machine and one bookkeeper. The state machine is the sprite index: a hero owns 45
/// walking frames (five directions × nine frames) plus a block of turning frames starting at 45, and every
/// call to <see cref="MoveStep"/> advances it by one. The bookkeeper is the route: when the sprite reaches the
/// last frame of a tile the hero actually moves, which pops the front step and subtracts its cost.
/// </para>
/// <para>
/// <b>Why the sprite index is the clock.</b> The reference has no timer of its own — the interface calls
/// <c>Move()</c> once per rendered frame, and the position within the tile is
/// <c>GetCurrentPixelOffset()</c>, derived from the sprite index. That is why <see cref="MoveStep"/> returns
/// "did the hero finish the step" instead of taking a delta time: the caller is the renderer, and the model
/// has to agree with it frame for frame.
/// </para>
/// <para>
/// <b>What is not here.</b> <c>playHeroWalkingSound()</c> (audio, and a table of <c>M82</c> ids),
/// <c>FadeOut()</c>/<c>FadeIn()</c> (they loop on the display and the event queue),
/// <c>_isInVisibleMapArea()</c> (it asks the game area for its visible tiles). The four calls that need a
/// subsystem are routed to <see cref="MovementHandler"/>.
/// </para>
/// <para>
/// <b>Port substitutions.</b> The reference reads the story books <c>world</c>, <c>Settings</c> and the
/// kingdom's control type; the port takes the <see cref="WorldMap"/> as a parameter, keeps
/// <see cref="IsControlHuman"/> on the hero, and takes the animation speed multiplier from
/// <see cref="AnimationSpeedMultiplier"/> instead of <c>Game::HumanHeroAnimSpeedMultiplier()</c>.
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>Walking frames per tile: eight steps and one standing frame, <c>heroFrameCountPerTile</c>.</summary>
    public const int FrameCountPerTile = 9;

    /// <summary>
    /// Index of the first turning frame, <c>_spriteIndex &gt;= 45</c> in the reference. A sprite index of 45
    /// or more means the hero is turning in place, not walking.
    /// </summary>
    public const int FirstTurningFrame = 45;

    /// <summary>Pixels the sprite travels per frame, <c>heroMoveStep</c>.</summary>
    private const int HeroMoveStep = 4;

    private readonly HeroPath _path = new();

    /// <summary>
    /// Frame of the hero's animation, <c>_spriteIndex</c> (<c>heroes.h:794</c>). Starts at 18, the first frame
    /// of the standing facing down block, which is the pose a hero has when he is placed on the map.
    /// </summary>
    private int _spriteIndex = 18;

    /// <summary>
    /// Facing of the hero, <c>_direction{ Direction::RIGHT }</c> (<c>heroes.h:792</c>). It goes together with
    /// <see cref="SpriteIndex"/>: 18 is the standing frame of the right-facing block, which is why every hero
    /// starts facing right.
    /// </summary>
    private Direction _direction = Direction.Right;

    /// <summary>
    /// Frames left before the turning animation advances, <c>_skippedFramesForAngleStep</c>
    /// (<c>heroes.h:795</c>).
    /// </summary>
    private int _skippedFramesForAngleStep;

    /// <summary>Pixel offset of the sprite inside its tile, <c>_offset</c> (<c>heroes.h:796</c>).</summary>
    private Point _offset;

    /// <summary>The route the hero is walking, <c>_path</c> (<c>heroes.h</c>, <c>Route::Path</c>).</summary>
    public HeroPath Path => _path;

    /// <summary>Shows or hides the route on the map, <c>Heroes::ShowPath()</c> (<c>heroes.h:518-521</c>).</summary>
    /// <param name="show">Whether the route should be drawn.</param>
    public void ShowPath(bool show)
    {
        if (show)
        {
            _path.Show();
        }
        else
        {
            _path.Hide();
        }
    }

    /// <summary>Whether the hero may still move, <c>isMoveEnabled()</c> (<c>heroes.h:633</c>).</summary>
    public bool IsMoveEnabled => _modes.HasFlag(HeroModes.MoveEnabled);

    /// <summary>
    /// Rebuilds the route to the destination the hero already has, <c>Heroes::calculatePath()</c>
    /// (<c>heroes.cpp:2158</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the route is rebuilt rather than kept</b>: what a step costs depends on what the hero can see and
    /// on the movement points left, and both change between turns. The reference calls this for every hero of a
    /// human kingdom at the start of its turn (<c>Kingdom::ActionBeforeTurn()</c>, <c>kingdom.cpp:184</c>), so a
    /// route the player laid out last turn is priced again before it is walked.
    /// </para>
    /// <para>
    /// The destination is read <b>before</b> the route is dropped, which is the reference's order: an empty route
    /// has no destination of its own, and the hero then keeps the one it was given. Both
    /// <see cref="HeroPath.IsValidForMovement(int)"/> checks are kept — the second one throws away a route that
    /// turned out to be unaffordable once priced.
    /// </para>
    /// </remarks>
    /// <param name="world">Map the hero stands on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    public void CalculatePath(WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int destination = _path.DestinationIndex;

        if (!_path.IsValidForMovement(world.Size))
        {
            _path.Reset();
        }

        if (destination < 0)
        {
            return;
        }

        _path.SetPath(world.GetPath(this, destination));

        if (!_path.IsValidForMovement(world.Size))
        {
            _path.Reset();
        }
    }

    /// <summary>
    /// Builds the route to a new destination, <c>Heroes::calculatePath( destinationIdx )</c> (<c>heroes.cpp:2158</c>).
    /// </summary>
    /// <param name="destinationIndex">The tile to walk to.</param>
    /// <param name="world">Map the hero stands on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The reference keeps no destination field of its own: <c>Path::GetDestinationIndex()</c> is the index of the
    /// last step, so asking for a new destination means replacing the whole route. An unreachable or unaffordable
    /// destination leaves an empty route, which is what the reference does too.
    /// </remarks>
    public void CalculatePath(int destinationIndex, WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (destinationIndex < 0 || destinationIndex >= world.Size)
        {
            _path.Reset();

            return;
        }

        _path.SetPath(world.GetPath(this, destinationIndex));

        if (!_path.IsValidForMovement(world.Size))
        {
            _path.Reset();
        }
    }

    /// <summary>
    /// Turns the movement flag on or off, <c>SetMove()</c>. Setting it on clears itself when the hero has no
    /// route, so that <see cref="Move"/> and the interface agree on whether there is anything to do.
    /// </summary>
    /// <param name="enable">Whether the hero may move.</param>
    public void SetMove(bool enable)
    {
        if (enable && _path.IsEmpty)
        {
            return;
        }

        if (enable)
        {
            _modes |= HeroModes.MoveEnabled;
        }
        else
        {
            _modes &= ~HeroModes.MoveEnabled;
        }
    }

    /// <summary>
    /// Whether the hero belongs to a player and is free to act, <c>isActive()</c>
    /// (<c>heroes.cpp:1949</c>): a valid hero, a player colour, not in a prison.
    /// </summary>
    public bool IsActive => Color != PlayerColor.None && !_modes.HasFlag(HeroModes.Jail);

    /// <summary>
    /// Whether a human drives this hero, <c>isControlHuman()</c>.
    /// </summary>
    /// <remarks>
    /// The reference asks the kingdom every time; the port copies the answer in when the rosters are attached
    /// (<c>World.AttachMapRosters</c>), because the control of a colour is fixed when the map is loaded. It decides
    /// whether walking sounds and the minimap are updated, i.e. exactly the things an AI hero skips.
    /// </remarks>
    public bool IsControlHuman { get; set; } = true;

    /// <summary>
    /// How much faster than the base 4-frame-per-pixel animation the interface runs, the port's stand-in for
    /// <c>Game::HumanHeroAnimSpeedMultiplier()</c>. One is the default speed.
    /// </summary>
    public int AnimationSpeedMultiplier { get; set; } = 1;

    /// <summary>
    /// Who gets told about fog, actions and the minimap. Defaults to
    /// <see cref="NullHeroMovementHandler"/>; see <see cref="IHeroMovementHandler"/>.
    /// </summary>
    public IHeroMovementHandler MovementHandler { get; set; } = NullHeroMovementHandler.Instance;

    /// <summary>Frame of the animation, <c>GetSpriteIndex()</c>/<c>SetSpriteIndex()</c>.</summary>
    public int SpriteIndex
    {
        get => _spriteIndex;
        set => _spriteIndex = value;
    }

    /// <summary>Facing of the hero, <c>GetDirection()</c>.</summary>
    public Direction Direction
    {
        get => _direction;
        set => _direction = value;
    }

    /// <summary>Pixel offset inside the tile, <c>GetOffset()</c>/<c>SetOffset()</c>.</summary>
    public Point Offset
    {
        get => _offset;
        set => _offset = value;
    }

    /// <summary>
    /// Subtracts the cost of a step, <c>Heroes::_applyMovementPenalty()</c> (<c>heroes_move.cpp:468</c>).
    /// </summary>
    /// <remarks>
    /// A step that costs more than what is left zeroes the points instead of going negative — that is the
    /// "last step" the pathfinder granted, and it is the only way a hero can end a day with no points on a
    /// tile he could not afford.
    /// </remarks>
    /// <param name="penalty">Cost of the step.</param>
    public void ApplyMovementPenalty(uint penalty)
    {
        if (penalty > _movePoints)
        {
            _movePoints = 0;

            return;
        }

        _movePoints -= penalty;
    }

    /// <summary>
    /// Advances the walking animation by one frame, <c>Heroes::_moveStep()</c>
    /// (<c>heroes_move.cpp:205</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The frame index decides what happens, and the three interesting frames are 0, 1 and 8 of the nine:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Frame 0 is the hero standing on the tile. If the next tile holds something he must walk up to but not
    /// onto — a guarded stack, a pickup, a flag — the step is taken and the route ends there, which is why
    /// the reference asserts the route had exactly one step left.
    /// </item>
    /// <item>
    /// Frame 1 clears the fog ahead of the hero: the reference scouts the tile he is about to enter, not the
    /// one he is on, so a hero never reveals the tile he is looking at before he steps into it.
    /// </item>
    /// <item>
    /// Frame 8 is the last frame of the tile: the sprite jumps back eight frames (to the standing pose of the
    /// new tile) and the hero actually moves — the route is popped, the penalty paid, the arrival announced.
    /// </item>
    /// </list>
    /// <para>
    /// The "skip the standing frame" shortcut at the end of the frame-8 branch is why a hero walking in a
    /// straight line animates faster than one who zigzags: if the next step continues in the same direction
    /// the standing frame is skipped, so the walker never pauses between tiles.
    /// </para>
    /// </remarks>
    /// <param name="jumpToNextTile">
    /// Take the whole step at once, as the interface does when the player clicks far away or when the hero is
    /// teleported by a script. Fog is still cleared first, and the "must not step on" tile still stops him.
    /// </param>
    /// <param name="world">The world the hero walks on.</param>
    /// <returns><see langword="true"/> when the hero finished the step.</returns>
    public bool MoveStep(bool jumpToNextTile, WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int heroIndex = MapIndex;
        MapGeometry geometry = world.Geometry!.Value;
        int nextStepIndex = geometry.GetDirectionIndex(heroIndex, _path.FrontDirection);

        void MakeStep(bool performMovement)
        {
            ApplyMovementPenalty(_path.FrontPenalty);

            if (!performMovement)
            {
                // The object cannot be stepped on: this was the last step of the route. The reference asserts
                // both facts; they hold by construction, because the pathfinder only stops a route at a tile
                // it cannot walk into.
                _path.PopFront();

                MovementHandler.Action(this, nextStepIndex);

                return;
            }

            MoveToDest(nextStepIndex, world);

            if (IsControlHuman)
            {
                MovementHandler.ScoutRadar(this);
            }

            MovementHandler.ActionNewPosition(this, true);

            _path.PopFront();

            // The hero may have been attacked and lost everything on the new tile, in which case there is
            // nobody left to act.
            if (IsActive)
            {
                MovementHandler.Action(this, nextStepIndex);
            }
        }

        if (jumpToNextTile)
        {
            if (IsNeedStayFrontObject(world, nextStepIndex))
            {
                MakeStep(false);
            }
            else
            {
                // Clear the fog before moving: the player must see the tile appear from under the hero.
                MovementHandler.Scout(this, nextStepIndex);

                MakeStep(true);
            }

            return true;
        }

        int currentHeroFrameIndex = _spriteIndex % FrameCountPerTile;

        if (currentHeroFrameIndex == 0)
        {
            if (IsNeedStayFrontObject(world, nextStepIndex))
            {
                MakeStep(false);

                return true;
            }

            // Walking sound would be played here; the port has no audio. See the class remarks.
        }
        else if (currentHeroFrameIndex == 1)
        {
            MovementHandler.Scout(this, nextStepIndex);
        }
        else if (currentHeroFrameIndex == 8)
        {
            _spriteIndex -= 8;

            MakeStep(true);

            // Walking into the same direction on the next tile: skip the standing frame.
            if (IsMoveEnabled && _direction == _path.FrontDirection && !IsNeedStayFrontObject(world, _path.FrontIndex))
            {
                ++_spriteIndex;
            }

            return true;
        }

        ++_spriteIndex;

        return false;
    }

    /// <summary>
    /// Turns the hero towards a direction without moving, <c>Heroes::_angleStep()</c>
    /// (<c>heroes_move.cpp:295</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The turning animation is a contiguous block of frames from 45 to 53 — the reference draws the eight
    /// in-between poses there and rewrites the sprite index into that block, then walks the index along it and
    /// finally rewrites it back to the standing frame of the new direction. The two switches are that mapping:
    /// the first picks the entry frame of the block, the second moves one frame and stops on the frame that
    /// belongs to the next direction.
    /// </para>
    /// <para>
    /// The skipped-frame counter exists because the block is four frames long and the interface can run at
    /// two or four times the base speed; without it a fast animation would flash the turn for less than a
    /// frame. <c>clamp( 4 / speedMultiplier, 1, 3 )</c> keeps the turn visible at every speed.
    /// </para>
    /// </remarks>
    /// <param name="targetDirection">Direction the hero must end up facing.</param>
    public void AngleStep(Direction targetDirection)
    {
        int speedMultiplier = Math.Max(AnimationSpeedMultiplier, 1);
        int framesToSkip = Math.Clamp(4 / speedMultiplier, 1, 3);

        ++_skippedFramesForAngleStep;

        if (_skippedFramesForAngleStep < framesToSkip)
        {
            return;
        }

        _skippedFramesForAngleStep = 0;

        bool clockwise = Directions.ShortDistanceClockWise(_direction, targetDirection);

        if (_spriteIndex < FirstTurningFrame && (_spriteIndex % FrameCountPerTile) == 0)
        {
            // Entering the turning block: pick the frame that comes right after the current one in the
            // direction of the turn.
            switch (_direction)
            {
                case Direction.Top:
                    _spriteIndex = 45;
                    break;
                case Direction.TopRight:
                    _spriteIndex = clockwise ? 47 : 46;
                    break;
                case Direction.TopLeft:
                    _spriteIndex = clockwise ? 46 : 47;
                    break;
                case Direction.Right:
                    _spriteIndex = clockwise ? 49 : 48;
                    break;
                case Direction.Left:
                    _spriteIndex = clockwise ? 48 : 49;
                    break;
                case Direction.BottomRight:
                    _spriteIndex = clockwise ? 51 : 50;
                    break;
                case Direction.BottomLeft:
                    _spriteIndex = clockwise ? 50 : 51;
                    break;
                case Direction.Bottom:
                    _spriteIndex = clockwise ? 52 : 53;
                    break;

                default:
                    break;
            }

            return;
        }

        // Inside the turning block: move one frame towards the next direction.
        switch (_direction)
        {
            case Direction.TopRight:
            case Direction.Right:
            case Direction.BottomRight:
                _spriteIndex += clockwise ? 1 : -1;
                break;

            case Direction.Top:
                ++_spriteIndex;
                break;

            case Direction.TopLeft:
            case Direction.Left:
            case Direction.BottomLeft:
                _spriteIndex += clockwise ? -1 : 1;
                break;

            case Direction.Bottom:
                --_spriteIndex;
                break;

            default:
                break;
        }

        Direction next = Direction.Unknown;

        switch (_direction)
        {
            case Direction.Top:
                next = clockwise ? Direction.TopRight : Direction.TopLeft;
                break;
            case Direction.TopRight:
                next = clockwise ? Direction.Right : Direction.Top;
                break;
            case Direction.TopLeft:
                next = clockwise ? Direction.Top : Direction.Left;
                break;
            case Direction.Right:
                next = clockwise ? Direction.BottomRight : Direction.TopRight;
                break;
            case Direction.Left:
                next = clockwise ? Direction.TopLeft : Direction.BottomLeft;
                break;
            case Direction.BottomRight:
                next = clockwise ? Direction.Bottom : Direction.Right;
                break;
            case Direction.BottomLeft:
                next = clockwise ? Direction.Left : Direction.Bottom;
                break;
            case Direction.Bottom:
                next = clockwise ? Direction.BottomLeft : Direction.BottomRight;
                break;

            default:
                break;
        }

        bool end = next switch
        {
            Direction.Top => _spriteIndex == 44,
            Direction.TopRight => _spriteIndex == (clockwise ? 47 : 46),
            Direction.TopLeft => _spriteIndex == (clockwise ? 46 : 47),
            Direction.Right => _spriteIndex == (clockwise ? 49 : 48),
            Direction.Left => _spriteIndex == (clockwise ? 48 : 49),
            Direction.BottomRight => _spriteIndex == (clockwise ? 51 : 50),
            Direction.BottomLeft => _spriteIndex == (clockwise ? 50 : 51),
            Direction.Bottom => _spriteIndex == 53,
            _ => false,
        };

        if (!end)
        {
            return;
        }

        // The turn is done: back to the standing frame of the new direction.
        switch (next)
        {
            case Direction.Top:
                _spriteIndex = 0;
                break;
            case Direction.Bottom:
                _spriteIndex = 36;
                break;
            case Direction.TopRight:
            case Direction.TopLeft:
                _spriteIndex = 9;
                break;
            case Direction.BottomRight:
            case Direction.BottomLeft:
                _spriteIndex = 27;
                break;
            case Direction.Right:
            case Direction.Left:
                _spriteIndex = 18;
                break;

            default:
                break;
        }

        _direction = next;
    }

    /// <summary>
    /// Pixel offset of the sprite inside its tile, <c>Heroes::getCurrentPixelOffset()</c>
    /// (<c>heroes_move.cpp:479</c>).
    /// </summary>
    /// <remarks>
    /// While the hero is turning the offset is zero — he stays in the middle of the tile. While walking, the
    /// eighth frame of a tile stands on the tile itself, so the offset only covers frames one to seven, which
    /// is why the frame index is decremented first and frame 0 returns the raw offset.
    /// </remarks>
    /// <returns>The offset in pixels.</returns>
    public Point GetCurrentPixelOffset()
    {
        if (_spriteIndex >= FirstTurningFrame)
        {
            return new Point(0, 0);
        }

        int frame = _spriteIndex % FrameCountPerTile;

        if (frame > 0)
        {
            --frame;
        }

        if (frame == 0)
        {
            return _offset;
        }

        int dx = 0;
        int dy = 0;

        if ((_direction & Directions.LeftColumn) != 0)
        {
            dx -= HeroMoveStep * frame;
        }
        else if ((_direction & Directions.RightColumn) != 0)
        {
            dx += HeroMoveStep * frame;
        }

        if ((_direction & Directions.TopRow) != 0)
        {
            dy -= HeroMoveStep * frame;
        }
        else if ((_direction & Directions.BottomRow) != 0)
        {
            dy += HeroMoveStep * frame;
        }

        return new Point(_offset.X + dx, _offset.Y + dy);
    }

    /// <summary>
    /// Walks one frame of the route, <c>Heroes::Move()</c> (<c>heroes_move.cpp:596</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller is the frame loop of the interface: one call per rendered frame, returning whether the hero
    /// is still active afterwards. Three things can happen:
    /// </para>
    /// <list type="bullet">
    /// <item>The hero faces the wrong way — turn in place, one frame per call.</item>
    /// <item>The hero faces the right way — walk, one frame per call.</item>
    /// <item>There is nothing to walk, or the hero has no movement points left — stop the movement flag.</item>
    /// </list>
    /// <para>
    /// The second condition of the guard is the interesting one: a hero is allowed to finish a step even when
    /// <see cref="IsMoveEnabled"/> is already false, as long as he is in the middle of one (any frame of the
    /// nine except the standing frame 0) or is turning. Otherwise the last movement point of the day would
    /// leave the sprite stuck halfway between two tiles.
    /// </para>
    /// </remarks>
    /// <param name="jumpToNextTile">Take the whole step at once instead of animating it.</param>
    /// <param name="world">The world the hero walks on.</param>
    /// <returns>Whether the hero is still active after this frame.</returns>
    public bool Move(bool jumpToNextTile, WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _modes &= ~HeroModes.Action;

        bool midStep = (_spriteIndex < FirstTurningFrame && (_spriteIndex % FrameCountPerTile) > 0)
                       || _spriteIndex >= FirstTurningFrame;

        if (!_path.IsValidForMovement(world.Geometry!.Value.TileCount) || (!IsMoveEnabled && !midStep))
        {
            SetMove(false);

            return false;
        }

        if (jumpToNextTile)
        {
            _direction = _path.FrontDirection;

            MoveStep(true, world);

            return IsActive;
        }

        Direction frontDirection = _path.FrontDirection;

        if (_direction != frontDirection)
        {
            // The hero is changing the direction of movement.
            AngleStep(frontDirection);
        }
        else
        {
            // An AI hero can appear from the fog in a frame that does not belong to his direction.
            SetValidDirectionSprite(world);

            if (MoveStep(jumpToNextTile, world))
            {
                return IsActive;
            }
        }

        return false;
    }

    /// <summary>
    /// The direction of the tile the hero is walking into, <c>Heroes::MovementDirection()</c>
    /// (<c>heroes_move.cpp:632</c>).
    /// </summary>
    /// <remarks>
    /// Not the same question as <see cref="Direction"/>: this is "which way is the hero's body actually
    /// going", which is zero while he stands still or turns and becomes a unit vector only for the frames of
    /// the walk. The interface uses it to compute the arrow of the route preview, so it has to be zero for
    /// the standing frames or the arrow would jump a tile ahead too early.
    /// </remarks>
    /// <param name="world">The world the hero stands on.</param>
    /// <returns>One of the eight unit vectors, or zero.</returns>
    public Point MovementDirection(WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int from = MapIndex;

        if (from == NoPosition)
        {
            return new Point(0, 0);
        }

        int to = world.Geometry!.Value.GetDirectionIndex(from, _path.FrontDirection);

        // GetDirectionIndex answers -1 for the unknown direction, which is what the reference tests.
        if (to < 0)
        {
            return new Point(0, 0);
        }

        if (_direction == Direction.Top)
        {
            if (_spriteIndex > 1 && _spriteIndex < 9)
            {
                return new Point(0, -1);
            }
        }
        else if (_direction is Direction.TopRight or Direction.TopLeft)
        {
            if (_spriteIndex > 9 + 1 && _spriteIndex < 18)
            {
                return new Point(_direction == Direction.TopRight ? 1 : -1, -1);
            }
        }
        else if (_direction is Direction.Right or Direction.Left)
        {
            if (_spriteIndex > 18 + 1 && _spriteIndex < 27)
            {
                return new Point(_direction == Direction.Right ? 1 : -1, 0);
            }
        }
        else if (_direction is Direction.BottomRight or Direction.BottomLeft)
        {
            if (_spriteIndex > 27 + 1 && _spriteIndex < 36)
            {
                return new Point(_direction == Direction.BottomRight ? 1 : -1, 1);
            }
        }
        else if (_direction == Direction.Bottom)
        {
            if (_spriteIndex > 36 + 1 && _spriteIndex < 45)
            {
                return new Point(0, 1);
            }
        }

        return new Point(0, 0);
    }

    /// <summary>
    /// Puts the sprite index into the block that belongs to the current facing, <c>_setValidDirectionSprite()</c>
    /// (<c>heroes_move.cpp:673</c>).
    /// </summary>
    /// <remarks>
    /// This is the repair pass for a hero whose facing and sprite disagree — an AI hero revealed from the fog
    /// on a frame that was never animated, or a hero restored from a save. The reference does nothing at all
    /// when the hero has no position or no next tile, because then there is no direction block to pick.
    /// </remarks>
    /// <param name="world">The world the hero stands on.</param>
    public void SetValidDirectionSprite(WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int from = MapIndex;

        if (from == NoPosition)
        {
            return;
        }

        // The reference gives up when there is no next tile; GetDirectionIndex answers -1 for the unknown
        // direction, which is exactly that case.
        if (world.Geometry!.Value.GetDirectionIndex(from, _path.FrontDirection) < 0)
        {
            return;
        }

        int blockStart = _direction switch
        {
            Direction.Top => 0,
            Direction.TopRight or Direction.TopLeft => 9,
            Direction.Right or Direction.Left => 18,
            Direction.BottomRight or Direction.BottomLeft => 27,
            Direction.Bottom => 36,
            _ => -1,
        };

        if (blockStart < 0)
        {
            return;
        }

        if (_spriteIndex < blockStart || _spriteIndex >= blockStart + FrameCountPerTile)
        {
            _spriteIndex = blockStart;
        }
    }

    /// <summary>
    /// Whether the hero must stop next to the tile instead of walking onto it,
    /// <c>isNeedStayFrontObject()</c> (<c>heroes_move.cpp:162</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_move.cpp:162-180</c>。三支，按参考的顺序判：
    /// </para>
    /// <list type="number">
    /// <item>
    /// 城：<b>不是友方的</b>、且城里有兵，就停在门口（友方的城直接走进去，空的城也直接走进去接管）。
    /// </item>
    /// <item>
    /// 船长的下一步是能上岸的陆地：停在水上，让动作层去问「下不下船」。
    /// </item>
    /// <item>
    /// 其余交给对象表：旗帜、宝物、守军都是走到跟前而不踩上去。
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="world">The world the hero walks on.</param>
    /// <param name="tileIndex">Tile the hero is about to enter.</param>
    /// <returns><see langword="true"/> when the hero must stop in front of it.</returns>
    public bool IsNeedStayFrontObject(WorldMap world, int tileIndex)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);

        if (tile.MainObjectType == Mp2ObjectType.Castle)
        {
            CastleTown? castle = world.GetCastleEntrance(tileIndex);

            if (castle is null)
            {
                return false;
            }

            return !IsFriends(castle.Color) && castle.GetActualArmy(world.GetHero(castle)).IsValid;
        }

        if (IsShipMaster && tile.IsSuitableForDisembarkation())
        {
            return true;
        }

        return Mp2ObjectTypes.IsNeedStayFront(tile.MainObjectType);
    }

    /// <summary>
    /// Teleports the hero to a tile without walking, <c>Heroes::Move2Dest()</c> (<c>heroes.cpp:2049</c>).
    /// </summary>
    /// <remarks>
    /// Two tile updates and one index change, in that order: the old tile loses its occupant before the index
    /// changes and the new tile gains it after, so a tile never holds a hero whose index points elsewhere.
    /// A move to the tile he is already on is a no-op, which is what keeps a route that ends where it starts
    /// from flickering the hero off the map for one frame.
    /// </remarks>
    /// <param name="destinationIndex">Tile to move to.</param>
    /// <param name="world">The world the hero stands on.</param>
    public void MoveToDest(int destinationIndex, WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int currentIndex = MapIndex;

        if (destinationIndex == currentIndex)
        {
            return;
        }

        world.GetTile(currentIndex).SetHero(world, null);
        SetMapIndex(destinationIndex);
        world.GetTile(destinationIndex).SetHero(world, this);
    }

    /// <summary>
    /// Whether the hero is surrounded by water, <c>Heroes::isInDeepOcean()</c> (<c>heroes_move.cpp:190</c>).
    /// </summary>
    /// <remarks>
    /// Deep ocean means no land in the eight neighbouring tiles — the question the AI asks before it puts a
    /// hero in a boat. The half-tile rule matters here: once the hero has walked more than half of the
    /// current tile he is, for the purpose of the question, already on the next one.
    /// </remarks>
    /// <param name="world">The world the hero stands on.</param>
    /// <returns><see langword="true"/> when every neighbour is water.</returns>
    public bool IsInDeepOcean(WorldMap world)
    {
        ArgumentNullException.ThrowIfNull(world);

        MapGeometry geometry = world.Geometry!.Value;

        // Nine steps per tile, so more than four frames means more than half of it.
        bool isHeroMovedHalfOfCell = _spriteIndex < FirstTurningFrame && (_spriteIndex % FrameCountPerTile) > 4;

        int tileIndex = isHeroMovedHalfOfCell && geometry.IsValidDirection(MapIndex, _direction)
            ? geometry.GetDirectionIndex(MapIndex, _direction)
            : MapIndex;

        foreach (int nearbyIndex in geometry.GetAroundIndexes(tileIndex))
        {
            if (!world.GetTile(nearbyIndex).IsWater)
            {
                return false;
            }
        }

        return true;
    }
}
