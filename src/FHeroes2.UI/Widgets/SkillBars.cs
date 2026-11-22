using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Heroes;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The artwork the two skill bars of <c>gui/skill_bar.cpp</c> paint, the <c>Assets::getImage()</c> lookups of
/// <c>PrimarySkillsBar</c> and <c>SecondarySkillsBar</c>.
/// </summary>
/// <remarks>
/// The reference reads the frames straight out of the global archive. The port passes them in, the same choice
/// <see cref="IArmyBarSprites"/> makes, so that a screen decides once which bundle it paints from.
/// </remarks>
public interface ISkillBarSprites
{
    /// <summary>One plate of the full size primary bar, <c>PRIMSKIL</c> (<c>skill_bar.cpp:137</c>).</summary>
    /// <param name="skill">The skill whose plate is wanted.</param>
    /// <returns>The 73x70 plate, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetPrimarySkillFrame(PrimarySkill skill);

    /// <summary>
    /// The window the mini primary icons are cut out of, <c>SWAPWIN</c> frame 0 (<c>skill_bar.cpp:93</c>).
    /// </summary>
    /// <returns>The frame, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetSwapWindowFrame();

    /// <summary>
    /// The plate of one full size secondary skill, <c>SECSKILL</c> (<c>skill_bar.cpp:351</c>).
    /// </summary>
    /// <param name="skill">The skill whose plate is wanted.</param>
    /// <returns>The plate, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetSecondarySkillFrame(SecondarySkill skill);

    /// <summary>The icon of one mini secondary skill, <c>MINISS</c> (<c>skill_bar.cpp:351</c>).</summary>
    /// <param name="skill">The skill whose icon is wanted.</param>
    /// <returns>The icon, or <see langword="null"/> when it is missing.</returns>
    Sprite? GetMiniSecondarySkillFrame(SecondarySkill skill);

    /// <summary>The empty plate of a full size secondary skill slot, <c>SECSKILL</c> frame 0.</summary>
    Sprite? SecondarySkillPlate { get; }

    /// <summary>The empty slot of a mini bar, <see cref="SkillBarArtwork.CreateMiniPlate"/>.</summary>
    Sprite? MiniPlate { get; }
}

/// <summary>
/// The one piece of artwork the reference builds by hand, <c>GetBarBackgroundSprite()</c>
/// (<c>skill_bar.cpp:41-51</c>).
/// </summary>
public static class SkillBarArtwork
{
    /// <summary>Side of a mini slot, <c>icon.resize( 34, 34 )</c>.</summary>
    public const int MiniPlateSize = 34;

    /// <summary>
    /// Builds the empty mini slot out of the hero screen's icon frame, <c>GetBarBackgroundSprite()</c>.
    /// </summary>
    /// <param name="hsiconsFrame">Frame 0 of <c>HSICONS</c>.</param>
    /// <param name="borderColor">The palette index of the frame, <c>GetColorId( 0xD0, 0xC0, 0x48 )</c> in the reference.</param>
    /// <returns>The 34x34 plate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hsiconsFrame"/> is <see langword="null"/>.</exception>
    public static Sprite CreateMiniPlate(Sprite hsiconsFrame, byte borderColor)
    {
        ArgumentNullException.ThrowIfNull(hsiconsFrame);

        Sprite plate = new(MiniPlateSize, MiniPlateSize);
        ImageOps.DrawBorder(plate.Image, borderColor);
        ImageOps.Blit(plate.Image, new Point(1, 1), hsiconsFrame.Image, new Rect(26, 21, 32, 32));

        return plate;
    }
}

/// <summary>
/// The four primary skills of a hero, <c>PrimarySkillsBar</c> (<c>gui/skill_bar.cpp:54-305</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sizes.</b> The small form is the 34x34 grid of the hero screen's bottom panel: an <c>HSICONS</c>-derived slot, a
/// 32x32 icon cut out of <c>SWAPWIN</c> and the value printed underneath. The full form is the 73x70 <c>PRIMSKIL</c>
/// plate with the skill name on top and the value at the bottom.
/// </para>
/// <para>
/// <b>No editor.</b> The reference's <c>isEditMode</c> and <c>allowSkillReset</c>, with the darkened "Default value"
/// plate and the two dialogs behind them, belong to the editor screens, which the port has not reached yet. The port
/// only draws what the game shows.
/// </para>
/// <para>
/// <b>No input.</b> The clicks of the reference open a message box or, in the editor, a number picker. A screen drives
/// those itself from the same <see cref="Layout"/>; this class paints only.
/// </para>
/// </remarks>
public sealed class PrimarySkillsBar : ItemsBar<PrimarySkill>
{
    /// <summary>The skills of the bar, in the order the reference lays them out.</summary>
    public static readonly PrimarySkill[] DefaultSkills =
        [PrimarySkill.Attack, PrimarySkill.Defense, PrimarySkill.Power, PrimarySkill.Knowledge];

    /// <summary>
    /// Where the mini icons are cut from <c>SWAPWIN</c> frame 0, <c>fheroes2::Copy( backSprite, 217, 52 + 33 * i, ... )</c>
    /// (<c>skill_bar.cpp:101-119</c>).
    /// </summary>
    private static readonly Point MiniIconOrigin = new(217, 52);

    /// <summary>The step between two mini icons, <c>52 → 85 → 118 → 151</c>.</summary>
    private const int MiniIconStep = 33;

    /// <summary>Side of a mini icon, <c>const int ww = 32</c>.</summary>
    private const int MiniIconSize = 32;

    private readonly Hero? _hero;
    private readonly ISkillBarSprites _sprites;
    private readonly Sprite? _miniPlate;

    private Point _textOffset;

    /// <summary>Creates a bar, <c>PrimarySkillsBar::PrimarySkillsBar()</c> (<c>skill_bar.cpp:54-70</c>).</summary>
    /// <param name="hero">The hero whose values are printed; <see langword="null"/> leaves the numbers out.</param>
    /// <param name="useSmallSize">Whether the 34x34 mini form is wanted, <c>useSmallSize</c>.</param>
    /// <param name="sprites">Where the plates and icons come from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sprites"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The artwork the chosen size needs is missing.</exception>
    public PrimarySkillsBar(Hero? hero, bool useSmallSize, ISkillBarSprites sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        _hero = hero;
        _sprites = sprites;
        UseSmallSize = useSmallSize;

        if (useSmallSize)
        {
            _miniPlate = sprites.MiniPlate
                ?? throw new InvalidOperationException("The mini primary skill bar needs the 34x34 slot plate.");

            SetSingleItemSize(new Size(_miniPlate.Width, _miniPlate.Height));
        }
        else
        {
            Sprite frame = sprites.GetPrimarySkillFrame(PrimarySkill.Attack)
                ?? throw new InvalidOperationException("The primary skill bar needs the PRIMSKIL plates.");

            SetSingleItemSize(new Size(frame.Width, frame.Height));
        }

        // The reference fills the bar with ATTACK..KNOWLEDGE in its constructor (SetContent( _content )).
        SetContent(DefaultSkills);
    }

    /// <summary>Whether the mini form is painted, <c>_useSmallSize</c>.</summary>
    public bool UseSmallSize { get; }

    /// <summary>The skills in layout order.</summary>
    public IReadOnlyList<PrimarySkill> Skills => DefaultSkills;

    /// <summary>The offset the value text is shifted by, <c>SetTextOff()</c> (<c>skill_bar.cpp:72</c>).</summary>
    /// <param name="x">Horizontal shift, 20 for the hero dialog.</param>
    /// <param name="y">Vertical shift, -13 for the hero dialog.</param>
    public void SetTextOff(int x, int y) => _textOffset = new Point(x, y);

    /// <summary>Paints the plate behind a slot, <c>RedrawBackground()</c> (<c>skill_bar.cpp:77</c>).</summary>
    /// <param name="position">Where the background goes.</param>
    /// <param name="destination">The frame to paint into.</param>
    /// <remarks>The full size bar has no background of its own: its plates are painted per item.</remarks>
    public override void RedrawBackground(Rect position, PixelBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (_miniPlate is not null)
        {
            ImageOps.BlitSprite(destination, position.Position, _miniPlate);
        }
    }

    /// <summary>Paints one skill, <c>RedrawItem()</c> (<c>skill_bar.cpp:84-193</c>).</summary>
    /// <param name="skill">The skill to paint.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="destination">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="skill"/> is not one of the four.</exception>
    public override void RedrawItem(PrimarySkill skill, Rect position, PixelBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (skill is PrimarySkill.Unknown or < PrimarySkill.Attack or > PrimarySkill.Knowledge)
        {
            throw new ArgumentOutOfRangeException(nameof(skill), skill, "Only the four primary skills have a plate.");
        }

        if (UseSmallSize)
        {
            RedrawMiniItem(skill, position, destination);
        }
        else
        {
            RedrawFullItem(skill, position, destination);
        }
    }

    /// <summary>
    /// Prints the four skill names beside a pair of mini bars, <c>fheroes2::RedrawPrimarySkillInfo()</c>
    /// (<c>skill_bar.cpp:431-457</c>).
    /// </summary>
    /// <param name="position">Top left corner of the panel the bars sit in.</param>
    /// <param name="destination">The frame to paint into.</param>
    /// <param name="first">The left bar, or <see langword="null"/>.</param>
    /// <param name="second">The right bar, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public static void DrawSkillNames(Point position, PixelBuffer destination, PrimarySkillsBar? first,
        PrimarySkillsBar? second)
    {
        ArgumentNullException.ThrowIfNull(destination);

        // The reference centres the labels on x + 320 and steps them by 33 pixels, starting 62 pixels down.
        const int labelCenterX = 320;
        const int firstLabelY = 62;
        const int labelStepY = 33;

        for (int i = 0; i < DefaultSkills.Length; ++i)
        {
            Text text = new(PrimarySkillNames.Get(DefaultSkills[i]), FontType.SmallWhite());
            text.Draw(position.X + labelCenterX - (text.Width() / 2), position.Y + firstLabelY + (i * labelStepY),
                destination);
        }

        first?.Redraw(destination);
        second?.Redraw(destination);
    }

    /// <summary>The value a skill shows, the <c>GetAttack()</c>…<c>GetKnowledge()</c> calls of the reference.</summary>
    /// <param name="skill">The skill.</param>
    /// <returns>The value, or <c>-1</c> when there is no hero to ask.</returns>
    private int SkillValue(PrimarySkill skill) => _hero is null
        ? -1
        : skill switch
        {
            PrimarySkill.Attack => _hero.GetAttack(),
            PrimarySkill.Defense => _hero.GetDefense(),
            PrimarySkill.Power => _hero.GetPower(),
            PrimarySkill.Knowledge => _hero.GetKnowledge(),
            _ => -1,
        };

    /// <summary>Paints one slot of the mini form, <c>skill_bar.cpp:92-135</c>.</summary>
    /// <param name="skill">The skill.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="destination">The frame to paint into.</param>
    private void RedrawMiniItem(PrimarySkill skill, Rect position, PixelBuffer destination)
    {
        Sprite icon = _sprites.GetSwapWindowFrame()
            ?? throw new InvalidOperationException("The mini primary skill bar needs SWAPWIN.");

        Point iconPosition = new(
            position.X + ((position.Width - MiniIconSize) / 2),
            position.Y + ((position.Height - MiniIconSize) / 2));

        // fheroes2::Copy( backSprite, 217, 52 + 33 * ( skill - 1 ), dstsf, ..., 32, 32 ).
        int iconIndex = (int)skill - (int)PrimarySkill.Attack;
        Rect iconSource = new(
            MiniIconOrigin.X,
            MiniIconOrigin.Y + (iconIndex * MiniIconStep),
            MiniIconSize,
            MiniIconSize);

        // BlitSprite() takes sprite local coordinates, so the origin of the crop is taken off the destination.
        ImageOps.BlitSprite(
            destination,
            new Point(iconPosition.X - iconSource.X, iconPosition.Y - iconSource.Y),
            icon,
            iconSource);

        int value = SkillValue(skill);

        if (value < 0)
        {
            return;
        }

        Text text = new(value.ToString(System.Globalization.CultureInfo.InvariantCulture), FontType.SmallWhite());
        text.Draw(
            position.X + ((position.Width + _textOffset.X - text.Width()) / 2),
            position.Y + position.Height + _textOffset.Y + 2,
            destination);
    }

    /// <summary>Paints one slot of the full form, <c>skill_bar.cpp:136-192</c>.</summary>
    /// <param name="skill">The skill.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="destination">The frame to paint into.</param>
    private void RedrawFullItem(PrimarySkill skill, Rect position, PixelBuffer destination)
    {
        Sprite plate = _sprites.GetPrimarySkillFrame(skill)
            ?? throw new InvalidOperationException($"The PRIMSKIL plate of {skill} is missing.");

        Point platePosition = new(
            position.X + ((position.Width - plate.Width) / 2),
            position.Y + ((position.Height - plate.Height) / 2));

        // fheroes2::Copy( sprite, 0, 0, dstsf, ..., pos.width, pos.height ): the slot's size crops the plate.
        ImageOps.BlitSprite(destination, platePosition, plate, new Rect(0, 0, position.Width, position.Height));

        Text name = new(PrimarySkillNames.Get(skill), FontType.SmallWhite());
        name.DrawInRoi(position.X + ((position.Width - name.Width()) / 2), position.Y + 6, destination, position);

        int value = SkillValue(skill);

        if (value < 0)
        {
            return;
        }

        Text valueText = new(value.ToString(System.Globalization.CultureInfo.InvariantCulture), FontType.NormalWhite());
        valueText.DrawInRoi(position.X, position.Y + position.Height - valueText.Height(), destination, position);
    }
}

/// <summary>
/// The secondary skills of a hero, <c>SecondarySkillsBar</c> (<c>gui/skill_bar.cpp:307-427</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sizes.</b> The mini form is the 34x34 slot with a <c>MINISS</c> icon and the level in its corner; the full form
/// is the <c>SECSKILL</c> plate with the skill name on top and the level at the bottom.
/// </para>
/// <para>
/// <b>No input and no editor.</b> Clicking a skill shows its description window and, in the editor, lets the player
/// pick or clear it. Both are the screen's business here, exactly as for <see cref="PrimarySkillsBar"/>.
/// </para>
/// </remarks>
public sealed class SecondarySkillsBar : ItemsBar<SecondarySkillLevel>
{
    /// <summary>The vertical offset of the level text in the full form, <c>pos.y + 53</c>.</summary>
    private const int FullLevelTextY = 53;

    /// <summary>The vertical offset of the name text in the full form, <c>pos.y + 5</c>.</summary>
    private const int FullNameTextY = 5;

    private readonly ISkillBarSprites _sprites;
    private readonly Hero _hero;
    private readonly Sprite? _miniPlate;

    /// <summary>Creates a bar, <c>SecondarySkillsBar::SecondarySkillsBar()</c> (<c>skill_bar.cpp:307-321</c>).</summary>
    /// <param name="hero">The hero whose skills are shown, <c>_hero</c>.</param>
    /// <param name="sprites">Where the plates and icons come from.</param>
    /// <param name="mini">Whether the 34x34 mini form is wanted, <c>mini</c>.</param>
    /// <param name="necromancyBonus">
    /// The extra levels the necromancy text adds, <c>Skill::Level::StringWithBonus( _hero, skill )</c>. The reference
    /// reads it from the world; the port takes it from the screen that knows the kingdom.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> or <paramref name="sprites"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The artwork the chosen size needs is missing.</exception>
    public SecondarySkillsBar(Hero hero, ISkillBarSprites sprites, bool mini = true, uint necromancyBonus = 0)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(sprites);

        _hero = hero;
        _sprites = sprites;
        UseMiniSprites = mini;
        NecromancyBonus = necromancyBonus;

        if (mini)
        {
            _miniPlate = sprites.MiniPlate
                ?? throw new InvalidOperationException("The mini secondary skill bar needs the 34x34 slot plate.");

            SetSingleItemSize(new Size(_miniPlate.Width, _miniPlate.Height));
        }
        else
        {
            Sprite plate = sprites.SecondarySkillPlate
                ?? throw new InvalidOperationException("The full secondary skill bar needs the SECSKILL plate.");

            SetSingleItemSize(new Size(plate.Width, plate.Height));
        }

        // The reference hands the hero's own skill array to the base class in its constructor.
        SetContent(hero.SecondarySkills.Skills);
    }

    /// <summary>Whether the mini form is painted, <c>use_mini_sprite</c>.</summary>
    public bool UseMiniSprites { get; }

    /// <summary>The levels the necromancy text adds, <c>_hero</c>'s bonus already resolved by the caller.</summary>
    public uint NecromancyBonus { get; }

    /// <summary>The skills of the hero, in the order the bar lays them out.</summary>
    public IReadOnlyList<SecondarySkillLevel> Skills => _hero.SecondarySkills.Skills;

    /// <summary>Paints the plate behind a slot, <c>RedrawBackground()</c> (<c>skill_bar.cpp:323-341</c>).</summary>
    /// <param name="position">Where the background goes.</param>
    /// <param name="destination">The frame to paint into.</param>
    /// <remarks>
    /// The reference darkens the plate and prints "Default skill" when the editor holds no skill at all. That branch is
    /// part of the editor, which the port has not reached.
    /// </remarks>
    public override void RedrawBackground(Rect position, PixelBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (_miniPlate is not null)
        {
            ImageOps.BlitSprite(destination, position.Position, _miniPlate);

            return;
        }

        Sprite plate = _sprites.SecondarySkillPlate
            ?? throw new InvalidOperationException("The full secondary skill bar needs the SECSKILL plate.");

        ImageOps.BlitSprite(destination, position.Position, plate);
    }

    /// <summary>Paints one skill, <c>RedrawItem()</c> (<c>skill_bar.cpp:343-373</c>).</summary>
    /// <param name="skill">The skill to paint; an invalid one paints nothing.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="destination">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public override void RedrawItem(SecondarySkillLevel skill, Rect position, PixelBuffer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!skill.IsValid)
        {
            // "If the skill is invalid nothing we need to do."
            return;
        }

        Sprite icon = (UseMiniSprites
            ? _sprites.GetMiniSecondarySkillFrame(skill.Skill)
            : _sprites.GetSecondarySkillFrame(skill.Skill))
            ?? throw new InvalidOperationException($"The {skill.Skill} artwork is missing.");

        Point iconPosition = new(
            position.X + ((position.Width - icon.Width) / 2),
            position.Y + ((position.Height - icon.Height) / 2));

        ImageOps.BlitSprite(destination, iconPosition, icon, new Rect(0, 0, position.Width, position.Height));

        if (UseMiniSprites)
        {
            Text level = new(((int)skill.Level).ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontType.SmallWhite());
            level.Draw(position.X + position.Width - level.Width() - 3, position.Y + position.Height - 10, destination);

            return;
        }

        Text name = new(SecondarySkills.GetName(skill.Skill), FontType.SmallWhite());

        if (name.Width() > icon.Width + 1)
        {
            name.FitToOneRow(position.Width);
        }

        name.DrawInRoi(position.X + ((position.Width - name.Width()) / 2) - 1, position.Y + FullNameTextY, destination,
            position);

        Text levelText = new(SecondarySkills.GetLevelNameWithBonus(skill, NecromancyBonus), FontType.SmallWhite());

        if (levelText.Width() > icon.Width + 1)
        {
            levelText.FitToOneRow(position.Width);
        }

        levelText.DrawInRoi(position.X + ((position.Width - levelText.Width()) / 2) - 1, position.Y + FullLevelTextY,
            destination, position);
    }
}
