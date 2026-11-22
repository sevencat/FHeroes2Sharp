using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The artwork the artifact bar paints, the <c>Assets::getImage()</c> lookups of <c>ArtifactsBar</c>
/// (<c>resource/artifact.cpp:1104-1183</c>).
/// </summary>
/// <remarks>
/// As for the other bars, the reference reads the frames out of the global archive and the port passes them in.
/// </remarks>
public interface IArtifactsBarSprites
{
    /// <summary>The empty slot of a mini bar, <see cref="SkillBarArtwork.CreateMiniPlate"/>.</summary>
    /// <remarks>
    /// The reference builds the very same 34x34 plate in <c>ArtifactsBar</c> and <c>SecondarySkillsBar</c>: <c>HSICONS</c>
    /// frame 0 cut at (26,21) with a gold frame. One plate serves both bars.
    /// </remarks>
    Sprite? MiniPlate { get; }

    /// <summary>The empty slot of the full size bar, <c>ARTIFACT</c> frame 0.</summary>
    Sprite? EmptySlot { get; }

    /// <summary>The 32x32 icon of a mini bar, <c>ARTFX</c> (<c>Artifact::IndexSprite32()</c>).</summary>
    /// <param name="frame">The frame index, see <see cref="ArtifactTypes.MiniSpriteIndex"/>.</param>
    /// <returns>The icon, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetMiniArtifact(uint frame);

    /// <summary>The 64x64 icon of the full size bar, <c>ARTIFACT</c> (<c>Artifact::IndexSprite64()</c>).</summary>
    /// <param name="frame">The frame index, see <see cref="ArtifactTypes.FullSpriteIndex"/>.</param>
    /// <returns>The icon, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetArtifact(uint frame);
}

/// <summary>
/// The fourteen artifacts of a hero, <c>ArtifactsBar</c> (<c>resource/artifact.cpp:1104-1183</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sizes.</b> The mini form is the 34x34 grid the hero screen and the kingdom overview use: a gold plate, the 32x32
/// <c>ARTFX</c> icon one pixel in, and a thin cursor frame. The full form is the 64x64 <c>ARTIFACT</c> slot with a
/// three pixel thick cursor frame hanging three pixels outside it.
/// </para>
/// <para>
/// <b>No input.</b> The clicks of the reference open the artifact description, swap two artifacts of the bag, hand the
/// magic book to the spell book screen, or — in the editor — let the player pick an artifact or a spell scroll's spell.
/// Every one of those needs dialogs the port does not have yet, so this class paints only; <see cref="Select"/> is the
/// seam the future input port drives, and it is what the cursor frame follows.
/// </para>
/// <para>
/// <b>No status bar.</b> The reference passes a <c>StatusBar*</c> just to print "you cannot trade the magic book"; a
/// painter has nothing to print.
/// </para>
/// </remarks>
public sealed class ArtifactsBar : ItemsActionBar<ArtifactType>
{
    /// <summary>Side of a mini slot, <c>backsf.resize( 32 + 2, 32 + 2 )</c>.</summary>
    public const int MiniSlotSize = 34;

    /// <summary>Side of a full size slot, <c>ARTIFACT</c> frame 0.</summary>
    public const int FullSlotSize = 64;

    /// <summary>The palette index of the mini cursor frame, <c>fheroes2::DrawBorder( spcursor, 214 )</c>.</summary>
    public const byte MiniCursorColor = 214;

    /// <summary>The palette index of the outer and inner line of the full cursor, <c>190</c>.</summary>
    public const byte FullCursorColor = 190;

    /// <summary>The palette index of the middle line of the full cursor, <c>180</c>.</summary>
    public const byte FullCursorMiddleColor = 180;

    /// <summary>Side of the full cursor, <c>spcursor.resize( 70, 70 )</c>.</summary>
    private const int FullCursorSize = 70;

    /// <summary>
    /// How far the full cursor sticks out of its slot, <c>spcursor.setPosition( pos.x - 3, pos.y - 3 )</c>.
    /// </summary>
    private const int FullCursorOffset = 3;

    private readonly IArtifactsBarSprites _sprites;
    private readonly Hero _hero;
    private readonly Sprite? _miniPlate;
    private readonly Sprite _cursor;

    /// <summary>Creates a bar, <c>ArtifactsBar::ArtifactsBar()</c> (<c>artifact.cpp:1104-1138</c>).</summary>
    /// <param name="hero">The hero whose bag is shown.</param>
    /// <param name="mini">Whether the 34x34 mini form is wanted, <c>mini</c>.</param>
    /// <param name="readOnly">Whether a click may not exchange artifacts, <c>ro</c>.</param>
    /// <param name="canChange">Whether the player may pick and clear artifacts, the editor switch <c>change</c>.</param>
    /// <param name="allowOpeningMagicBook">
    /// Whether a click on the magic book may open the spell book, <c>allowOpeningMagicBook</c>.
    /// </param>
    /// <param name="sprites">Where the plates and icons come from.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hero"/> or <paramref name="sprites"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The artwork the chosen size needs is missing.</exception>
    public ArtifactsBar(Hero hero, bool mini, bool readOnly, bool canChange, bool allowOpeningMagicBook,
        IArtifactsBarSprites sprites)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(sprites);

        _hero = hero;
        _sprites = sprites;
        UseMiniSprites = mini;
        IsReadOnly = readOnly;
        CanChange = canChange;
        AllowOpeningMagicBook = allowOpeningMagicBook;

        if (mini)
        {
            _miniPlate = sprites.MiniPlate
                ?? throw new InvalidOperationException("The mini artifact bar needs the 34x34 slot plate.");

            SetSingleItemSize(new Size(_miniPlate.Width, _miniPlate.Height));

            _cursor = CreateCursor(new Size(MiniSlotSize, MiniSlotSize), [(new Rect(0, 0, MiniSlotSize, MiniSlotSize), MiniCursorColor)]);
        }
        else
        {
            Sprite slot = sprites.EmptySlot
                ?? throw new InvalidOperationException("The full artifact bar needs the ARTIFACT slot plate.");

            SetSingleItemSize(new Size(slot.Width, slot.Height));

            // Three nested frames, two lighter lines around one darker: fheroes2::DrawRect( ..., 190/180/190 ).
            _cursor = CreateCursor(new Size(FullCursorSize, FullCursorSize), [
                (new Rect(0, 0, FullCursorSize, FullCursorSize), FullCursorColor),
                (new Rect(1, 1, FullCursorSize - 2, FullCursorSize - 2), FullCursorMiddleColor),
                (new Rect(2, 2, FullCursorSize - 4, FullCursorSize - 4), FullCursorColor),
            ]);
        }

        // The reference hands the bag itself to the base class in its constructor.
        SetContent(Artifacts);
    }

    /// <summary>Whether the mini form is painted, <c>use_mini_sprite</c>.</summary>
    public bool UseMiniSprites { get; }

    /// <summary>Whether a click may not exchange artifacts, <c>read_only</c>.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Whether the player may pick and clear artifacts, <c>can_change</c>.</summary>
    public bool CanChange { get; }

    /// <summary>Whether a click on the magic book may open the spell book, <c>_allowOpeningMagicBook</c>.</summary>
    public bool AllowOpeningMagicBook { get; }

    /// <summary>The bag the bar shows, <c>hero->GetBagArtifacts()</c>.</summary>
    public IReadOnlyList<ArtifactType> Artifacts => _hero.BagArtifacts.Slots;

    /// <summary>How many slots the bar shows, <c>BagArtifacts::maxCapacity</c>.</summary>
    public int ItemsCount => ItemCount;

    /// <summary>The slot the cursor frames, <c>ItemsActionBar::GetSelectedIndex()</c>; <c>-1</c> for none.</summary>
    /// <remarks>
    /// <c>ItemsActionBar::GetSelectedIndex()</c> answers the item count when nothing is selected, because it measures
    /// the distance to <c>items.end()</c>; the bar's own callers expect <c>-1</c>, so the negative case is folded here.
    /// </remarks>
    public int SelectedIndex => IsSelected ? GetSelectedIndex() : -1;

    /// <summary>Paints the empty slot, <c>RedrawBackground()</c> (<c>artifact.cpp:1152-1158</c>).</summary>
    /// <param name="position">Where the plate goes.</param>
    /// <param name="destination">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public override void RedrawBackground(Rect position, PixelBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        Sprite? plate = UseMiniSprites ? _miniPlate : _sprites.EmptySlot;

        if (plate is null)
        {
            throw new InvalidOperationException("The artifact bar is missing its empty slot plate.");
        }

        ImageOps.BlitSprite(destination, position.Position, plate);
    }

    /// <summary>Paints one artifact, <c>RedrawItem()</c> (<c>artifact.cpp:1160-1183</c>).</summary>
    /// <param name="artifact">The artifact; an empty slot paints nothing.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="selected">Whether the cursor frames this slot.</param>
    /// <param name="destination">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The icon of the artifact is missing.</exception>
    public override void RedrawItem(ArtifactType artifact, Rect position, bool selected, PixelBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!ArtifactTypes.IsValid(artifact))
        {
            // "if ( art.isValid() )": an empty slot keeps the plate that RedrawBackground() painted.
            return;
        }

        if (UseMiniSprites)
        {
            Sprite icon = _sprites.GetMiniArtifact(ArtifactTypes.MiniSpriteIndex(artifact))
                ?? throw new InvalidOperationException($"The ARTFX icon of {artifact} is missing.");

            Point iconPosition = new(position.X + 1, position.Y + 1);

            // The reference clears the icon's rectangle first, so that a transparent pixel shows the plate and not
            // whatever the previous frame left behind.
            ImageOps.FillRect(destination, new Rect(iconPosition, new Size(icon.Width, icon.Height)), 0);
            ImageOps.BlitSprite(destination, iconPosition, icon);

            if (selected)
            {
                ImageOps.BlitSprite(destination, position.Position, _cursor);
            }

            return;
        }

        Sprite full = _sprites.GetArtifact(ArtifactTypes.FullSpriteIndex(artifact))
            ?? throw new InvalidOperationException($"The ARTIFACT icon of {artifact} is missing.");

        ImageOps.FillRect(destination, new Rect(position.X, position.Y, full.Width, full.Height), 0);
        ImageOps.BlitSprite(destination, position.Position, full);

        if (selected)
        {
            ImageOps.BlitSprite(
                destination,
                new Point(position.X - FullCursorOffset, position.Y - FullCursorOffset),
                _cursor);
        }
    }

    /// <summary>Builds a cursor that holds nothing but the frames of <paramref name="frames"/>.</summary>
    /// <remarks>
    /// The reference builds <c>spcursor</c> with <c>reset()</c> and then <c>DrawBorder</c>/<c>DrawRect</c>
    /// (<c>artifact.cpp:1124-1136</c>), so everything but the frame stays transparent. The port draws four lines per
    /// frame because <see cref="ImageOps.DrawLine(Sprite, Point, Point, byte)"/> also clears the transform layer along
    /// them, which the <see cref="PixelBuffer"/> primitives do not do.
    /// </remarks>
    /// <param name="size">Side of the cursor.</param>
    /// <param name="frames">The frames, outermost first.</param>
    /// <returns>The cursor.</returns>
    private static Sprite CreateCursor(Size size, IReadOnlyList<(Rect Frame, byte Color)> frames)
    {
        Sprite cursor = new(size.Width, size.Height);
        cursor.Reset();

        foreach ((Rect frame, byte color) in frames)
        {
            int right = frame.Right - 1;
            int bottom = frame.Bottom - 1;

            ImageOps.DrawLine(cursor, new Point(frame.X, frame.Y), new Point(right, frame.Y), color);
            ImageOps.DrawLine(cursor, new Point(frame.X, bottom), new Point(right, bottom), color);
            ImageOps.DrawLine(cursor, new Point(frame.X, frame.Y + 1), new Point(frame.X, bottom - 1), color);
            ImageOps.DrawLine(cursor, new Point(right, frame.Y + 1), new Point(right, bottom - 1), color);
        }

        return cursor;
    }
}
