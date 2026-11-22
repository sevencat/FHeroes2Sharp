using System.Diagnostics;
using System.Globalization;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Which split or join hot key is held, the port of the <c>Game::HotKeyEvent</c> queries the army bar makes
/// (<c>army_bar.cpp:123</c>).
/// </summary>
/// <remarks>
/// The reference asks the global hot-key state several times per frame; the port takes one answer because only the
/// first held key matters — the checks are an else-if chain in that same order. A screen maps its keyboard onto this
/// value and hands it to <see cref="ArmyBar.ProcessEvents(LocalEvent, ArmyBarHotKey)"/>.
/// </remarks>
public enum ArmyBarHotKey
{
    /// <summary>No split or join key is held.</summary>
    None,

    /// <summary>Split the stack by one, <c>ARMY_SPLIT_STACK_BY_ONE</c>.</summary>
    SplitStackByOne,

    /// <summary>Join the stacks of the same kind, <c>ARMY_JOIN_STACKS</c>.</summary>
    JoinStacks,

    /// <summary>Split the stack over every free slot, <c>ARMY_SPLIT_STACK_BY_HALF</c>.</summary>
    SplitStackByHalf,
}

/// <summary>
/// The creature artwork an army bar paints, the two <c>Assets::getImage()</c> lookups of
/// <c>ArmyBar::RedrawItem()</c> (<c>army_bar.cpp:275</c>) and the two of <c>renderMonsterFrame()</c>
/// (<c>ui_monster.cpp:36</c>).
/// </summary>
public interface IArmyBarSprites : IMonsterFrameSprites
{
    /// <summary>A mini monster, <c>MONS32</c>.</summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The 32x32 sprite, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetMiniMonster(MonsterType monster);
}

/// <summary>
/// A dictionary-backed <see cref="IArmyBarSprites"/> for tests and for a caller that loads the frames itself.
/// </summary>
public sealed class ArmyBarSpriteSet : IArmyBarSprites
{
    /// <summary>The mini monsters, keyed by creature.</summary>
    public Dictionary<MonsterType, Sprite> MiniMonsters { get; } = [];

    /// <summary>The portraits, keyed by creature.</summary>
    public Dictionary<MonsterType, Sprite> MonsterFrames { get; } = [];

    /// <summary>The plates, keyed by race.</summary>
    public Dictionary<MapRace, Sprite> RaceFrames { get; } = [];

    /// <inheritdoc/>
    public Sprite? GetMiniMonster(MonsterType monster) =>
        MiniMonsters.TryGetValue(monster, out Sprite? sprite) ? sprite : null;

    /// <inheritdoc/>
    public Sprite? GetRaceFrame(MapRace race) => RaceFrames.TryGetValue(race, out Sprite? sprite) ? sprite : null;

    /// <inheritdoc/>
    public Sprite? GetMonsterFrame(MonsterType monster) =>
        MonsterFrames.TryGetValue(monster, out Sprite? sprite) ? sprite : null;
}

/// <summary>
/// The editor's "add troop" request, the port of <c>Dialog::selectMonster()</c> plus <c>Dialog::SelectCount()</c>
/// (<c>army_bar.cpp:476-487</c>).
/// </summary>
/// <remarks>
/// The reference blocks in the two dialogs and, when they are answered, fills the slot itself. The port hands the
/// slot and the creature it would suggest out; the caller shows the pickers and calls <c>Troop.Set()</c> on the slot.
/// </remarks>
/// <param name="Troop">The empty slot to fill.</param>
/// <param name="SuggestedMonster">The level-one creature of the army's race, or <see cref="MonsterType.Unknown"/>.</param>
public readonly record struct ArmyBarAddTroop(Troop Troop, MonsterType SuggestedMonster);

/// <summary>
/// What one frame of input asked an army bar to do.
/// </summary>
/// <param name="Handled">Whether the frame's input was spent on the bar.</param>
/// <param name="Split">The split question to put to the player, or <see langword="null"/>.</param>
/// <param name="Info">The unit information window to show, or <see langword="null"/>.</param>
/// <param name="AddTroop">The editor's add-troop request, or <see langword="null"/>.</param>
public readonly record struct ArmyBarEvent(
    bool Handled,
    ArmyBarSplit? Split = null,
    ArmyInfoDialog? Info = null,
    ArmyBarAddTroop? AddTroop = null);

/// <summary>
/// The army bar of <c>src/fheroes2/army/army_bar.cpp</c>: five slots that show a stack each, let the player
/// merge, swap and split them, and open the unit information window.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is here</b>: the painting (<see cref="Redraw"/>, <see cref="RedrawBackground"/>, <see cref="RedrawItem"/>),
/// the cursor messages, the mouse handling of one bar and of a pair of bars (<c>ArmyBar.Input.cs</c>), and the split
/// questions the reference's anonymous-namespace helpers ask (<see cref="ArmyBarSplit"/> and
/// <c>ArmyBar.Redistribute.cs</c>).
/// </para>
/// <para>
/// <b>What is not here</b>: nothing is loaded from an archive. The reference calls
/// <c>Assets::getImage( ICN::STRIP, ... )</c> and <c>Assets::getImage( ICN::MONS32, ... )</c>; the port passes the
/// sprites in — the background and the cursor through <see cref="SetBackground"/> or
/// <see cref="SetStripBackground"/>, the creature artwork through <see cref="IArmyBarSprites"/> on every
/// <see cref="Redraw"/>. The background colours of <see cref="SetBackground"/> are palette indices, because the
/// reference's <c>fheroes2::GetColorId()</c> needs the game palette, which the core does not have.
/// </para>
/// <para>
/// <b>Dialogs</b>: the reference blocks in three of them. Splitting becomes a returned <see cref="ArmyBarSplit"/> and
/// <see cref="ApplySplit"/>; the unit information window becomes a returned <see cref="ArmyInfoDialog"/> and
/// <see cref="ApplyInfoAnswer"/>; the editor's monster chooser becomes a returned <see cref="ArmyBarAddTroop"/>.
/// Everything a screen has to answer is therefore in <see cref="ArmyBarEvent"/> and no method blocks.
/// </para>
/// <para>
/// <b>Hot keys</b>: the reference asks the global hot-key state (<c>Game::HotKeyHoldEvent</c>); the port takes the
/// answer as <see cref="ArmyBarHotKey"/> because which key is down is the screen's business.
/// </para>
/// </remarks>
public sealed partial class ArmyBar : ItemsActionBar<Troop>
{
    /// <summary>The size of a mini monster slot, <c>{ 43, 43 }</c> (<c>army_bar.cpp:212</c>).</summary>
    public const int MiniMonsterItemSize = 43;

    /// <summary>The palette index of the selection frame (<c>army_bar.cpp:197</c>).</summary>
    public const byte CursorColor = 214;

    /// <summary>
    /// <c>Dialog::BUTTONS</c> (<c>dialog.h:65</c>): the four button flags an information window starts with before
    /// the upgrade and dismiss flags are added.
    /// </summary>
    internal const DialogButton InfoButtons =
        DialogButton.Yes | DialogButton.Ok | DialogButton.No | DialogButton.Cancel;

    private Army? _army;
    private Sprite? _background;
    private Sprite? _darkenedBackground;
    private Sprite? _cursor;
    private IArmyBarSprites? _sprites;

    /// <summary>
    /// Creates a bar, <c>ArmyBar::ArmyBar()</c> (<c>army_bar.cpp:189</c>).
    /// </summary>
    /// <remarks>
    /// The reference remembers the army and, for the mini sprite mode, calls
    /// <c>SetBackground( { 43, 43 }, GetColorId( 0, 45, 0 ) )</c>. The port cannot resolve that colour (see the
    /// type's remarks), so a mini bar draws its background only after <see cref="SetBackground"/>; its selection
    /// frame, which needs no colour but <see cref="CursorColor"/>, exists at once.
    /// <para>
    /// A bar that draws the full <c>STRIP</c> frames needs <see cref="SetStripBackground"/> before its first
    /// <see cref="Redraw"/>, because those frames also give a slot its size.
    /// </para>
    /// </remarks>
    /// <param name="army">The army shown; <see langword="null"/> for an empty bar.</param>
    /// <param name="miniSprites">Whether slots are 32x32 mini monsters instead of full portraits.</param>
    /// <param name="readOnly">Whether the bar refuses every change, as in a shop.</param>
    /// <param name="canChange">Whether the editor may add a troop to an empty slot.</param>
    /// <param name="saveLastTroop">Whether the last stack of an army may be moved away.</param>
    public ArmyBar(Army? army, bool miniSprites, bool readOnly, bool canChange = false, bool saveLastTroop = true)
    {
        _army = army;
        UseMiniSprites = miniSprites;
        IsReadOnly = readOnly;
        CanChange = canChange;
        SaveLastTroop = saveLastTroop;

        if (miniSprites)
        {
            Size size = new(MiniMonsterItemSize, MiniMonsterItemSize);
            SetSingleItemSize(size);
            _cursor = CreateCursor(size);
        }

        SetContentForArmy();
    }

    /// <summary>Whether the bar shows mini monsters, <c>use_mini_sprite</c>.</summary>
    public bool UseMiniSprites { get; }

    /// <summary>Whether the bar refuses every change, <c>read_only</c>.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Whether the editor may add a troop to an empty slot, <c>can_change</c>.</summary>
    public bool CanChange { get; }

    /// <summary>Whether the last stack of an army may be moved away, <c>_saveLastTroop</c>.</summary>
    public bool SaveLastTroop { get; }

    /// <summary>
    /// The vertical offset the unit information window opens at, <c>_troopWindowOffsetY</c> (<c>army_bar.h:101</c>).
    /// </summary>
    /// <remarks>
    /// A screen that shares the screen estate with another window shifts the information window with it. The port's
    /// <see cref="ArmyInfoDialog"/> does its own layout, so the value is remembered for a caller that lays the window
    /// out around the bar; it is not read by this class.
    /// </remarks>
    public int TroopWindowOffsetY { get; set; }

    /// <summary>The message to show under the cursor, <c>msg</c>.</summary>
    /// <remarks>Cleared at the start of every <see cref="ProcessEvents(LocalEvent, ArmyBarHotKey)"/> call.</remarks>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The selected slot, or <c>-1</c>.</summary>
    /// <remarks>
    /// <c>ItemsActionBar::GetSelectedIndex()</c> answers the item count when nothing is selected, because it measures
    /// the distance to <c>items.end()</c>; the bar's own callers expect <c>-1</c>, so the negative case is folded here.
    /// </remarks>
    public int SelectedIndex => IsSelected ? GetSelectedIndex() : -1;

    /// <summary>The army shown, <c>_army</c>.</summary>
    public Army? Army => _army;

    /// <summary>How many slots the bar acts on: the army's five, or none without an army.</summary>
    public int ItemsCount => ItemCount;

    /// <summary>
    /// Replaces the army, <c>ArmyBar::SetArmy()</c> (<c>army_bar.cpp:222</c>).
    /// </summary>
    /// <param name="army">The army to show; <see langword="null"/> empties the bar.</param>
    public void SetArmy(Army? army)
    {
        if (_army is not null && IsSelected)
        {
            ResetSelected();
        }

        _army = army;
        ResetSelected();
        SetContentForArmy();
    }

    /// <summary>
    /// Hands the army's five slots to the base class, <c>SetContent( _army-&gt;GetTroops() )</c>.
    /// </summary>
    /// <remarks>
    /// A slot is the army's own <see cref="Troop"/> object, not a copy, so a troop that is merged or emptied anywhere
    /// else is painted as it is now. Without an army the bar has no items at all, which is what makes
    /// <see cref="ItemsCount"/> zero and every hit test answer "nothing".
    /// </remarks>
    private void SetContentForArmy()
    {
        if (_army is null)
        {
            SetContent(Array.Empty<Troop>());

            return;
        }

        Troop[] troops = new Troop[Army.MaximumTroopCount];

        for (int i = 0; i < troops.Length; ++i)
        {
            troops[i] = _army.GetTroop(i);
        }

        SetContent(troops);
    }

    /// <summary>
    /// Builds the background and the selection frame of a mini bar, <c>ArmyBar::SetBackground()</c>
    /// (<c>army_bar.cpp:205</c>).
    /// </summary>
    /// <remarks>
    /// The reference fills the slot with <c>GetColorId( 0, 45, 0 )</c> and frames it with
    /// <c>GetColorId( 0xD0, 0xC0, 0x48 )</c>; both are palette indices, and the port has no RGB palette in the core,
    /// so the caller passes them. The selection frame is always <see cref="CursorColor"/>.
    /// </remarks>
    /// <param name="size">The slot size, 43x43 in the reference.</param>
    /// <param name="fillColor">The palette index the slot is filled with.</param>
    /// <param name="borderColor">The palette index of the slot's one-pixel border.</param>
    public void SetBackground(Size size, byte fillColor, byte borderColor)
    {
        Debug.Assert(UseMiniSprites, "The reference only builds this background for a mini sprite bar.");

        SetSingleItemSize(size);

        Sprite background = new(size.Width, size.Height);
        ImageOps.FillSprite(background, background.Image.Bounds, fillColor);
        ImageOps.DrawBorder(background.Image, borderColor);

        _background = background;
        _cursor = CreateCursor(size);
    }

    /// <summary>
    /// Sets the full-size slot artwork, the <c>STRIP</c> frames 2 and 1 of the reference's constructor.
    /// </summary>
    /// <remarks>
    /// The reference copies frame 2 into every slot's background (<c>army_bar.cpp:260</c>) and lays frame 1 over the
    /// selected one (<c>army_bar.cpp:300</c>). The background also gives the slot its size, which is why this method
    /// replaces <see cref="SetBackground"/> for a bar without mini sprites.
    /// </remarks>
    /// <param name="background">The slot background, <c>STRIP</c> frame 2.</param>
    /// <param name="cursor">The selection frame, <c>STRIP</c> frame 1.</param>
    public void SetStripBackground(Sprite background, Sprite cursor)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(cursor);

        Debug.Assert(!UseMiniSprites, "A mini sprite bar draws its own background.");

        _background = background;
        _cursor = cursor;
        SetSingleItemSize(new Size(background.Width, background.Height));

        // The editor's empty army shows the slot dark with "Default troop" over it (army_bar.cpp:252-257). The
        // reference darkens the frame every time it paints; the table is static, so the port darkens it once.
        Sprite darkened = new(background.Width, background.Height);
        ImageOps.Blit(darkened.Image, Point.Zero, background.Image, background.Image.Bounds);
        ImageOps.ApplyPalette(darkened.Image, PaletteId.Darkening);
        _darkenedBackground = darkened;
    }

    /// <summary>
    /// Paints the whole bar, <c>ArmyBar::Redraw()</c> (<c>army_bar.cpp:311</c>).
    /// </summary>
    /// <param name="output">The frame buffer.</param>
    /// <param name="sprites">The creature artwork; <see langword="null"/> draws backgrounds and counts only.</param>
    /// <remarks>
    /// <c>ItemsBar::Redraw()</c> walks the slots and asks for a background and then an item, so the artwork of this
    /// frame only has to be remembered before the base class runs.
    /// </remarks>
    public void Redraw(PixelBuffer output, IArmyBarSprites? sprites = null)
    {
        ArgumentNullException.ThrowIfNull(output);

        _sprites = sprites;

        base.Redraw(output);
    }

    /// <summary>
    /// Paints one slot's background, <c>ArmyBar::RedrawBackground()</c> (<c>army_bar.cpp:244</c>).
    /// </summary>
    /// <param name="area">The slot.</param>
    /// <param name="output">The frame buffer.</param>
    public override void RedrawBackground(Rect area, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (UseMiniSprites)
        {
            if (_background is not null)
            {
                ImageOps.BlitSprite(output, new Point(area.X, area.Y), _background);
            }

            return;
        }

        if (_background is null)
        {
            return;
        }

        // A map army without a single troop gets the default army of its race at the game start; the editor says so
        // instead of leaving the slot blank (army_bar.cpp:252-257).
        if (CanChange && !SaveLastTroop && _army is { } army && army.OccupiedSlotCount == 0 &&
            _darkenedBackground is not null)
        {
            ImageOps.BlitSprite(output, new Point(area.X, area.Y), _darkenedBackground);

            Text text = new("Default\ntroop", FontType.NormalWhite());
            text.DrawInRoi(area.X, area.Y + (area.Height / 2) - 17, area.Width, output, area);

            return;
        }

        ImageOps.BlitSprite(output, new Point(area.X, area.Y), _background);
    }

    /// <summary>
    /// Paints one slot's monster and count, <c>ArmyBar::RedrawItem()</c> (<c>army_bar.cpp:265</c>).
    /// </summary>
    /// <param name="index">The slot.</param>
    /// <param name="area">Its rectangle.</param>
    /// <param name="selected">Whether it is selected.</param>
    /// <param name="output">The frame buffer.</param>
    /// <param name="sprites">The creature artwork.</param>
    public void RedrawItem(int index, Rect area, bool selected, PixelBuffer output, IArmyBarSprites? sprites)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (index < 0 || index >= ItemsCount)
        {
            return;
        }

        DrawTroop(_army!.GetTroop(index), area, selected, output, sprites);
    }

    /// <summary>
    /// Paints one slot's monster and count, <c>ItemsActionBar::RedrawItem()</c> (<c>army_bar.cpp:265</c>).
    /// </summary>
    /// <param name="troop">The slot's troop.</param>
    /// <param name="area">Its rectangle.</param>
    /// <param name="selected">Whether it is selected.</param>
    /// <param name="output">The frame buffer.</param>
    public override void RedrawItem(Troop troop, Rect area, bool selected, PixelBuffer output) =>
        DrawTroop(troop, area, selected, output, _sprites);

    private void DrawTroop(Troop troop, Rect area, bool selected, PixelBuffer output, IArmyBarSprites? sprites)
    {
        if (!troop.IsValid)
        {
            return;
        }

        string count = troop.Count.ToString(CultureInfo.InvariantCulture);

        if (UseMiniSprites)
        {
            Sprite? mini = sprites?.GetMiniMonster(troop.Monster);

            if (mini is not null)
            {
                // The sprite stands on the slot's floor, centred horizontally; only what does not fit is cut away
                // (army_bar.cpp:276-289).
                Rect source = mini.Image.Bounds;

                if (mini.Width > area.Width)
                {
                    source = new Rect((mini.Width - area.Width) / 2, source.Y, area.Width, source.Height);
                }

                if (mini.Height > area.Height)
                {
                    source = new Rect(source.X, (mini.Height - area.Height) / 2, source.Width, area.Height);
                }

                ImageOps.BlitSprite(
                    output,
                    new Point(area.X + ((area.Width - mini.Width) / 2), area.Y + area.Height - mini.Height - 1),
                    mini,
                    source);
            }

            Text text = new(count, FontType.SmallWhite());
            text.Draw(area.X + area.Width - text.Width() - 3, area.Y + area.Height - text.Height() + 2, output);
        }
        else
        {
            // renderMonsterFrame(): the race plate, then the creature portrait at its ICN offset (ui_monster.cpp:36).
            if (sprites is not null)
            {
                MonsterFrameRenderer.Render(troop.Monster, output, new Point(area.X, area.Y), sprites);
            }

            Text text = new(count, FontType.NormalWhite());
            text.Draw(area.X + area.Width - text.Width() - 3, area.Y + area.Height - text.Height() + 1, output);
        }

        if (selected && _cursor is not null)
        {
            ImageOps.BlitSprite(output, new Point(area.X, area.Y), _cursor);
        }
    }
}
