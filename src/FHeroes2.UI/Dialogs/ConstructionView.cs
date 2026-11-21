using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>What one click of the construction window asks its owner to do.</summary>
public enum ConstructionTarget
{
    /// <summary>Nothing under the mouse, or a cell that cannot be opened.</summary>
    None,

    /// <summary>Leave the construction window, <c>castle_town.cpp:600-602</c>.</summary>
    Exit,

    /// <summary>The "previous town" arrow, <c>castle_town.cpp:612-620</c>.</summary>
    PreviousTown,

    /// <summary>The "next town" arrow, <c>castle_town.cpp:622-630</c>.</summary>
    NextTown,

    /// <summary>The resource panel, which opens the income window, <c>castle_town.cpp:606-610</c>.</summary>
    Income,

    /// <summary>The first hero slot, <c>castle_town.cpp:660-667</c>.</summary>
    FirstHero,

    /// <summary>The second hero slot, <c>castle_town.cpp:669-678</c>.</summary>
    SecondHero,

    /// <summary>The "spread formation" icon, <c>castle_town.cpp:728-732</c>.</summary>
    SpreadFormation,

    /// <summary>The "grouped formation" icon, <c>castle_town.cpp:733-737</c>.</summary>
    GroupedFormation,

    /// <summary>One of the nineteen building cells.</summary>
    Building,
}

/// <summary>
/// One hit of the construction window: the target and, for a building cell, its index into
/// <see cref="ConstructionView.Cells"/>.
/// </summary>
/// <param name="Target">What is under the mouse.</param>
/// <param name="CellIndex">Index into <see cref="ConstructionView.Cells"/>, or <c>-1</c>.</param>
public readonly record struct ConstructionHit(ConstructionTarget Target, int CellIndex)
{
    /// <summary>No hit at all.</summary>
    public static ConstructionHit None => new(ConstructionTarget.None, -1);

    /// <summary>Whether the hit is <see cref="ConstructionTarget.None"/>.</summary>
    public bool IsNone => Target == ConstructionTarget.None;
}

/// <summary>
/// The construction window's own frames <em>plus</em> the parts of the castle screen it does not show: the
/// hero portraits and the two recruit marks. Everything here is decided by the owner of the window.
/// </summary>
public sealed class ConstructionFrame
{
    /// <summary>The kingdom's treasury, drawn in the resource panel.</summary>
    public Funds Funds { get; init; }

    /// <summary>The world, only used to pick the ground tiles of the town picture.</summary>
    public World? World { get; init; }

    /// <summary>The first hero that can be hired, or <see langword="null"/>.</summary>
    public Sprite? FirstHeroPortrait { get; init; }

    /// <summary>The second hero that can be hired, or <see langword="null"/>.</summary>
    public Sprite? SecondHeroPortrait { get; init; }

    /// <summary>Whether the first hero may be hired right now, <c>allow_buy_hero1</c>.</summary>
    public bool CanRecruitFirstHero { get; init; }

    /// <summary>Whether the second hero may be hired right now, <c>allow_buy_hero2</c>.</summary>
    public bool CanRecruitSecondHero { get; init; }

    /// <summary>The status line; the owner passes the date line when nothing is hovered.</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>Whether EXIT is held down.</summary>
    public bool ExitPressed { get; init; }

    /// <summary>Whether the "previous town" arrow is held down.</summary>
    public bool PreviousPressed { get; init; }

    /// <summary>Whether the "next town" arrow is held down.</summary>
    public bool NextPressed { get; init; }
}

/// <summary>
/// The construction window: the nineteen building cells, the town picture, the captain's statistics, the two
/// hero slots, the status bar and the resource panel, <c>Castle::_openConstructionDialog()</c>
/// (<c>castle_town.cpp:186-480</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window is one full 640×480 screen. The drawing order is the reference's: background, stone plate while
/// there is no captain, town picture, name, the nineteen cells, the captain's statistics and formation icons,
/// the two hero slots, the status bar, EXIT and the resource panel, and finally the status text.
/// </para>
/// <para>
/// <b>Known gap</b>: clicking a hero slot asks for <c>recruitHeroDialog()</c>, which needs the hero dialogue that
/// has not been ported yet; the window reports the click and the caller decides what to do with it.
/// </para>
/// </remarks>
public sealed class ConstructionView
{
    /// <summary>The right-click help of the EXIT button, <c>_( "Exit this menu." )</c>.</summary>
    public const string ExitHelpText = "Exit this menu.";

    /// <summary>The status line of the EXIT button, <c>_( "Exit Castle Options" )</c>.</summary>
    public const string ExitStatusText = "Exit Castle Options";

    /// <summary>The status line of the "spread" icon, <c>castle_town.cpp:584</c>.</summary>
    public const string SpreadFormationStatusText = "Set garrison combat formation to 'Spread'";

    /// <summary>The status line of the "grouped" icon, <c>castle_town.cpp:587</c>.</summary>
    public const string GroupedFormationStatusText = "Set garrison combat formation to 'Grouped'";

    /// <summary>The right-click help title of the "spread" icon, <c>castle_town.cpp:739</c>.</summary>
    public const string SpreadFormationHelpTitle = "Spread Formation";

    /// <summary>The description behind the "spread" icon, <c>castle_town.cpp:344-345</c>.</summary>
    public const string SpreadFormationHelpText =
        "'Spread' combat formation spreads the castle's units from the top to the bottom of the battlefield, "
        + "with at least one empty space between each unit.";

    /// <summary>The right-click help title of the "grouped" icon, <c>castle_town.cpp:742</c>.</summary>
    public const string GroupedFormationHelpTitle = "Grouped Formation";

    /// <summary>The description behind the "grouped" icon, <c>castle_town.cpp:346-347</c>.</summary>
    public const string GroupedFormationHelpText =
        "'Grouped' combat formation bunches the castle's units together in the center of the castle's side "
        + "of the battlefield.";

    private readonly ConstructionArtwork _artwork;
    private readonly IGlyphSource _font;
    private readonly IReadOnlyList<ConstructionCell> _cells;

    private ConstructionView(Castle castle, ConstructionArtwork artwork, IGlyphSource font, in Funds treasury)
    {
        Castle = castle;
        _artwork = artwork;
        _font = font;

        List<ConstructionCell> cells = new(ConstructionLayout.SlotCount);
        foreach (ConstructionSlot slot in ConstructionLayout.GetSlots(castle))
        {
            BuildingInfo info = new(castle, slot.Building, treasury);
            info.SetPosition(slot.Offset);
            cells.Add(new ConstructionCell(info, font));
        }

        _cells = cells;

        // 参考 :447-479 把状态条、两个箭头与 EXIT 当成城堡屏同一套控件来摆；这里借用城堡屏的布局，
        // 但没有旗帜，所以只塞一个 1×1 的占位尺寸（CrestArea 在建造窗口里不用）。
        Layout = CastleDialogLayout.Create(
            new Point(0, 0),
            new Size(artwork.StatusBar.Width, artwork.StatusBar.Height),
            new Size(1, 1),
            new Size(artwork.PreviousButton.Width, artwork.PreviousButton.Height));
    }

    /// <summary>The town the window shows.</summary>
    public Castle Castle { get; }

    /// <summary>The status bar, arrows, EXIT, resource panel and their hit areas.</summary>
    public CastleDialogLayout Layout { get; }

    /// <summary>The nineteen cells, in the reference's order; the last one is the captain's.</summary>
    public IReadOnlyList<ConstructionCell> Cells => _cells;

    /// <summary>Whether the captain's quarters stand, <c>isBuild( BUILD_CAPTAIN )</c>.</summary>
    public bool CaptainBuilt => Castle.IsBuild(BuildingType.Captain);

    /// <summary>Where the "spread formation" icon is, <c>castle_town.cpp:342</c>.</summary>
    public Rect SpreadFormationArea => new(
        ConstructionLayout.SpreadFormationOffset.X,
        ConstructionLayout.SpreadFormationOffset.Y,
        _artwork.SpreadFormation.Width,
        _artwork.SpreadFormation.Height);

    /// <summary>Where the "grouped formation" icon is, <c>castle_town.cpp:343</c>.</summary>
    public Rect GroupedFormationArea => new(
        ConstructionLayout.GroupedFormationOffset.X,
        ConstructionLayout.GroupedFormationOffset.Y,
        _artwork.GroupedFormation.Width,
        _artwork.GroupedFormation.Height);

    /// <summary>
    /// Builds the window for one town, <c>castle_town.cpp:186-480</c>.
    /// </summary>
    /// <param name="castle">The town.</param>
    /// <param name="artwork">The frames of the window.</param>
    /// <param name="smallFont">The font of the names and of the status line.</param>
    /// <param name="treasury">The kingdom's treasury, which decides which cells lack resources.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ConstructionView Create(Castle castle, ConstructionArtwork artwork, IGlyphSource smallFont, in Funds treasury)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(smallFont);

        return new ConstructionView(castle, artwork, smallFont, treasury);
    }

    /// <summary>
    /// What is under the mouse, in the reference's priority order: EXIT, the arrows, the resource panel, the
    /// building cells, the hero slots and finally the formation icons.
    /// </summary>
    /// <param name="cursor">The mouse, or <see langword="null"/> when it is outside the window.</param>
    /// <param name="forClick">
    /// Whether the answer decides a click. A cell that cannot be built lets the click fall through to what is
    /// drawn on top of it — the captain's cell covers part of the "spread" icon, and the reference only reacts to
    /// the icon because <c>BuildingInfo::QueueEventProcessing()</c> does nothing for a built cell
    /// (<c>buildinginfo.cpp:419-437</c>).
    /// </param>
    /// <returns>The hit; <see cref="ConstructionHit.None"/> when nothing is under the mouse.</returns>
    public ConstructionHit HitTest(Point? cursor, bool forClick = false)
    {
        if (cursor is not { } point)
        {
            return ConstructionHit.None;
        }

        if (Layout.GetExitButtonArea(_artwork.ExitButton).Contains(point))
        {
            return new ConstructionHit(ConstructionTarget.Exit, -1);
        }

        if (Layout.PreviousCastleButton.Contains(point))
        {
            return new ConstructionHit(ConstructionTarget.PreviousTown, -1);
        }

        if (Layout.NextCastleButton.Contains(point))
        {
            return new ConstructionHit(ConstructionTarget.NextTown, -1);
        }

        if (Layout.ResourceHitArea.Contains(point))
        {
            return new ConstructionHit(ConstructionTarget.Income, -1);
        }

        for (int i = 0; i < _cells.Count; ++i)
        {
            if (!_cells[i].Area.Contains(point))
            {
                continue;
            }

            if (!forClick || CanOpenCell(i))
            {
                return new ConstructionHit(ConstructionTarget.Building, i);
            }
        }

        if (ConstructionLayout.FirstHeroArea.Contains(point))
        {
            return new ConstructionHit(ConstructionTarget.FirstHero, -1);
        }

        if (ConstructionLayout.SecondHeroArea.Contains(point))
        {
            return new ConstructionHit(ConstructionTarget.SecondHero, -1);
        }

        if (CaptainBuilt)
        {
            if (SpreadFormationArea.Contains(point))
            {
                return new ConstructionHit(ConstructionTarget.SpreadFormation, -1);
            }

            if (GroupedFormationArea.Contains(point))
            {
                return new ConstructionHit(ConstructionTarget.GroupedFormation, -1);
            }
        }

        return ConstructionHit.None;
    }

    /// <summary>
    /// Whether a left click on a cell opens the price dialog, <c>BuildingInfo::QueueEventProcessing()</c>
    /// (<c>buildinginfo.cpp:392-403</c>): only what can be built now or would be built if the treasury allowed it.
    /// </summary>
    /// <param name="cellIndex">Index into <see cref="Cells"/>.</param>
    /// <returns><see langword="true"/> when a left click does something.</returns>
    public bool CanOpenCell(int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= _cells.Count)
        {
            return false;
        }

        ConstructionCell cell = _cells[cellIndex];

        return cell.Info.Type != BuildingType.Nothing
            && cell.Info.Status is BuildingStatus.AllowBuild or BuildingStatus.LackResources;
    }

    /// <summary>
    /// The line the status bar shows for a spot of the window, <c>castle_town.cpp:489-560</c>.
    /// </summary>
    /// <param name="cursor">The mouse, or <see langword="null"/>.</param>
    /// <param name="firstHero">The first hireable hero, or <see langword="null"/>.</param>
    /// <param name="secondHero">The second hireable hero, or <see langword="null"/>.</param>
    /// <param name="canRecruitFirst">Whether the first hero may be hired.</param>
    /// <param name="canRecruitSecond">Whether the second hero may be hired.</param>
    /// <param name="canSwitchTown">Whether the two town arrows are enabled.</param>
    /// <returns>The message, or an empty string when the caller should show its own default.</returns>
    public string GetStatusMessage(
        Point? cursor,
        Hero? firstHero,
        Hero? secondHero,
        bool canRecruitFirst,
        bool canRecruitSecond,
        bool canSwitchTown)
    {
        if (cursor is not { } point)
        {
            return string.Empty;
        }

        foreach (ConstructionCell cell in _cells)
        {
            if (!cell.Area.Contains(point))
            {
                continue;
            }

            return cell.Info.Type == BuildingType.Nothing ? string.Empty : cell.Info.GetConditionDescription();
        }

        if (firstHero is not null && ConstructionLayout.FirstHeroArea.Contains(point))
        {
            return DescribeHero(firstHero, canRecruitFirst);
        }

        if (secondHero is not null && ConstructionLayout.SecondHeroArea.Contains(point))
        {
            return DescribeHero(secondHero, canRecruitSecond);
        }

        if (CaptainBuilt)
        {
            if (SpreadFormationArea.Contains(point))
            {
                return SpreadFormationStatusText;
            }

            if (GroupedFormationArea.Contains(point))
            {
                return GroupedFormationStatusText;
            }
        }

        if (Layout.GetExitButtonArea(_artwork.ExitButton).Contains(point))
        {
            return ExitStatusText;
        }

        if (Layout.ResourceHitArea.Contains(point))
        {
            return "Show Income";
        }

        if (canSwitchTown && Layout.PreviousCastleButton.Contains(point))
        {
            return "Show previous town";
        }

        if (canSwitchTown && Layout.NextCastleButton.Contains(point))
        {
            return "Show next town";
        }

        return string.Empty;
    }

    /// <summary>
    /// Paints the whole window.
    /// </summary>
    /// <param name="target">The frame the window is drawn into.</param>
    /// <param name="frame">The parts that change from frame to frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="frame"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target, ConstructionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(frame);

        ImageOps.BlitSprite(target, new Point(0, 0), _artwork.Background);

        if (!CaptainBuilt)
        {
            // STONEBAK is a whole 640×480 screen in the original resources, so the plate is cut out of it at the
            // same coordinates it is pasted at (castle_town.cpp:204-208).
            Rect plate = ConstructionLayout.CaptainPlateArea;
            ImageOps.Blit(target, new Point(plate.X, plate.Y), _artwork.CaptainPlate.Image, plate);
        }

        CastleImagePainter.Draw(target, Castle, ConstructionLayout.CastleImageOffset, frame.World);

        DrawTownName(target);

        foreach (ConstructionCell cell in _cells)
        {
            cell.Draw(target, _artwork);
        }

        if (CaptainBuilt)
        {
            DrawCaptainStatistics(target);
            DrawFormation(target);
        }

        DrawHeroes(target, frame);
        DrawStatusBar(target, frame);

        Sprite exitFrame = frame.ExitPressed ? _artwork.ExitPressed : _artwork.ExitButton;
        ResourcePanel.Draw(target, frame.Funds, new Point(0, 0), _font, _artwork.ResourceIcons, exitFrame);
    }

    private static string DescribeHero(Hero hero, bool canRecruit) =>
        $"{(canRecruit ? "Recruit" : "Cannot recruit")} {hero.Name} the {RaceNames.Get(hero.Race)}";

    private void DrawTownName(PixelBuffer target)
    {
        string name = Castle.Name;
        int width = GlyphText.Measure(_font, name);

        GlyphText.DrawLine(
            _font,
            target,
            new Point(ConstructionLayout.NameCenterX - (width / 2), ConstructionLayout.NameTop),
            name);
    }

    /// <summary>The four primary statistics, <c>castle_town.cpp:355-368</c>.</summary>
    private void DrawCaptainStatistics(PixelBuffer target)
    {
        Captain captain = Castle.Captain;
        IReadOnlyList<(string Name, int Value)> stats =
        [
            (PrimarySkillNames.Get(PrimarySkill.Attack), captain.GetAttack()),
            (PrimarySkillNames.Get(PrimarySkill.Defense), captain.GetDefense()),
            (PrimarySkillNames.Get(PrimarySkill.Power), captain.GetPower()),
            (PrimarySkillNames.Get(PrimarySkill.Knowledge), captain.GetKnowledge()),
        ];

        for (int i = 0; i < stats.Count; ++i)
        {
            Point at = new(
                ConstructionLayout.CaptainSkillOffset.X,
                ConstructionLayout.CaptainSkillOffset.Y + (i * ConstructionLayout.CaptainSkillLineHeight));

            GlyphText.DrawLine(_font, target, at, stats[i].Name);

            string value = stats[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            GlyphText.DrawLine(
                _font,
                target,
                new Point(at.X + ConstructionLayout.CaptainSkillValueOffset, at.Y),
                value);
        }
    }

    /// <summary>The two formation icons and the frame around the one in use, <c>castle_town.cpp:370-394</c>.</summary>
    private void DrawFormation(PixelBuffer target)
    {
        ImageOps.BlitSprite(target, ConstructionLayout.SpreadFormationOffset, _artwork.SpreadFormation);
        ImageOps.BlitSprite(target, ConstructionLayout.GroupedFormationOffset, _artwork.GroupedFormation);

        // The reference's movable cursor sits one pixel up and left of the icon it marks (:348-349).
        Point selected = Castle.Army.IsSpreadFormation
            ? ConstructionLayout.SpreadFormationOffset
            : ConstructionLayout.GroupedFormationOffset;

        ImageOps.BlitSprite(target, new Point(selected.X - 1, selected.Y - 1), _artwork.FormationCursor);
    }

    /// <summary>The two hero slots and the cross over the ones that cannot be hired, <c>castle_town.cpp:400-444</c>.</summary>
    private void DrawHeroes(PixelBuffer target, ConstructionFrame frame)
    {
        DrawHero(
            target,
            ConstructionLayout.FirstHeroArea,
            frame.FirstHeroPortrait,
            frame.CanRecruitFirstHero);

        DrawHero(
            target,
            ConstructionLayout.SecondHeroArea,
            frame.SecondHeroPortrait,
            frame.CanRecruitSecondHero);
    }

    private void DrawHero(PixelBuffer target, Rect area, Sprite? portrait, bool canRecruit)
    {
        ImageOps.BlitSprite(target, new Point(area.X, area.Y), portrait ?? _artwork.EmptyHero);

        if (canRecruit)
        {
            return;
        }

        // 参考 :422-427 把「不能招」的叉按在右下角：x + 102 - 4 + 1 - w，y + h - 2 - h_mark。
        ConstructionCell.DrawMark(
            target,
            new Point(area.Right - 4 + 1 - _artwork.DeniedMark.Width, area.Bottom - 2 - _artwork.DeniedMark.Height),
            _artwork.DeniedMark,
            grayed: true);
    }

    private void DrawStatusBar(PixelBuffer target, ConstructionFrame frame)
    {
        ImageOps.BlitSprite(
            target,
            new Point(Layout.PreviousCastleButton.X, Layout.PreviousCastleButton.Y),
            frame.PreviousPressed ? _artwork.PreviousPressed : _artwork.PreviousButton);

        ImageOps.BlitSprite(target, new Point(Layout.StatusBarArea.X, Layout.StatusBarArea.Y), _artwork.StatusBar);

        ImageOps.BlitSprite(
            target,
            new Point(Layout.NextCastleButton.X, Layout.NextCastleButton.Y),
            frame.NextPressed ? _artwork.NextPressed : _artwork.NextButton);

        if (frame.StatusText.Length == 0)
        {
            return;
        }

        string text = BuildDialogView.FitToOneRow(_font, frame.StatusText, Layout.StatusTextArea.Width);
        int width = GlyphText.Measure(_font, text);

        Point position = new(
            Layout.StatusTextArea.X + ((Layout.StatusTextArea.Width - width) / 2),
            Layout.StatusTextArea.Y + ((Layout.StatusTextArea.Height - _font.LineHeight) / 2));

        GlyphText.DrawLine(_font, target, position, text, Layout.StatusTextArea);
    }
}
