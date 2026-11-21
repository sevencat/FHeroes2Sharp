using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>What the thieves' guild report asks its caller to do.</summary>
public enum ThievesGuildAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>The EXIT button was pressed; the caller closes the report.</summary>
    Exit,

    /// <summary>EXIT was right clicked; the caller shows the "Exit this menu." message.</summary>
    ExitHelp,
}

/// <summary>The two text faces of the thieves' guild report.</summary>
/// <param name="SmallWhite">The four primary statistics and the personalities.</param>
/// <param name="NormalWhite">Every label, the title, the ordinals and the colour names.</param>
public sealed record ThievesGuildFonts(IGlyphSource SmallWhite, IGlyphSource NormalWhite);

/// <summary>
/// The thieves' guild and oracle ranking, <c>Dialog::ThievesGuild()</c>
/// (<c>dialog_thievesguild.cpp:242-486</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is a modal loop that blits the report once, then only repaints the exit button. The port composes the
/// whole report every frame: <see cref="Draw"/> reads <see cref="ThievesGuildReport"/>, and
/// <see cref="ProcessEvents"/> only has to watch the exit button.
/// </para>
/// <para>
/// <b>Deviations from the reference</b>:
/// </para>
/// <list type="bullet">
/// <item>
/// The reference reads ten different lambdas from <c>world</c> while it draws. The port reads the same numbers up front
/// into a <see cref="ThievesGuildReport"/> and can therefore be tested without a world.
/// </item>
/// <item>
/// The exit button is <c>BUTTON_GUILDWELL_EXIT</c> in the reference and does not exist in the retail
/// <c>H2X1_1N.ICN</c>; the caller passes the two frames of <c>WELLXTRA</c> instead, the same ones the well and the
/// mage guild use.
/// </item>
/// <item>
/// The reference restores the castle screen with an <c>ImageRestorer</c>; the port redraws the scene behind the report
/// and needs no restorer.
/// </item>
/// </list>
/// </remarks>
public sealed class ThievesGuildView
{
    /// <summary>What the exit button's help message says.</summary>
    public const string ExitHelpText = "Exit this menu.";

    private readonly ThievesGuildReport _report;
    private readonly ThievesGuildArtwork _artwork;
    private readonly ThievesGuildFonts _fonts;
    private readonly ButtonSprite _exitButton;

    private ThievesGuildView(
        ThievesGuildReport report,
        ThievesGuildArtwork artwork,
        ThievesGuildLayout layout,
        ThievesGuildFonts fonts)
    {
        _report = report;
        _artwork = artwork;
        _fonts = fonts;
        Layout = layout;
        _exitButton = new ButtonSprite(artwork.ExitReleased, artwork.ExitPressed);
        _exitButton.SetPosition(layout.Exit.X, layout.Exit.Y);
    }

    /// <summary>Where every part of the report goes.</summary>
    public ThievesGuildLayout Layout { get; }

    /// <summary>The report being shown.</summary>
    public ThievesGuildReport Report => _report;

    /// <summary>The one button of the report.</summary>
    public ButtonBase ExitButton => _exitButton;

    /// <summary>Builds the report.</summary>
    /// <param name="report">The ranking to show.</param>
    /// <param name="artwork">The sprites.</param>
    /// <param name="fonts">The two text faces.</param>
    /// <param name="screen">Screen the report is centred on; the default is a 640 by 480 screen.</param>
    /// <returns>The report, or <see langword="null"/> when the artwork is unusable.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ThievesGuildView? TryCreate(
        ThievesGuildReport report,
        ThievesGuildArtwork? artwork,
        ThievesGuildFonts fonts,
        Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(fonts);

        if (artwork is null
            || report.Players.Count == 0
            || artwork.Flags.Count < ThievesGuildArtwork.FlagFrames
            || artwork.Background.Width < ThievesGuildLayout.DialogWidth
            || artwork.Background.Height < ThievesGuildLayout.DialogHeight
            || artwork.Bar.Width <= ThievesGuildLayout.DialogWidth / 2)
        {
            return null;
        }

        Size area = screen ?? new Size(ThievesGuildLayout.DialogWidth, ThievesGuildLayout.DialogHeight);

        ThievesGuildLayout layout = new(
            new Rect(0, 0, area.Width, area.Height),
            new Size(artwork.Bar.Width, artwork.Bar.Height),
            new Size(artwork.ExitReleased.Width, artwork.ExitReleased.Height),
            fonts.NormalWhite.LineHeight);

        return new ThievesGuildView(report, artwork, layout, fonts);
    }

    /// <summary>Paints the whole report, <c>dialog_thievesguild.cpp:276-446</c>.</summary>
    /// <param name="target">The buffer to draw on, usually the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        IReadOnlyList<ThievesGuildPlayer> players = _report.Players;

        ImageOps.Blit(target, Layout.Dialog.Position, _artwork.Background.Image, _artwork.Background.Image.Bounds);

        for (int column = 0; column < players.Count; ++column)
        {
            DrawCentred(_fonts.NormalWhite, target, Layout.OrdinalCentre(column), ThievesGuildRanking.Ordinal(column));
        }

        ImageOps.Blit(target, Layout.BarLeft.Position, _artwork.Bar.Image, new Rect(
            ThievesGuildLayout.BarLeftSourceX,
            0,
            Layout.BarLeft.Width,
            Layout.BarHeight));
        ImageOps.Blit(target, Layout.BarRight.Position, _artwork.Bar.Image, new Rect(
            Layout.BarRightSourceX,
            0,
            Layout.BarRight.Width,
            Layout.BarHeight));

        GlyphText.DrawLine(_fonts.NormalWhite, target, Layout.TitleAnchor(GlyphText.Measure(_fonts.NormalWhite, _report.Title)), _report.Title);

        foreach (ThievesGuildRow row in ThievesGuildRows.All)
        {
            string label = ThievesGuildRows.Label(row);
            GlyphText.DrawLine(
                _fonts.NormalWhite,
                target,
                Layout.RowLabelAnchor(row, GlyphText.Measure(_fonts.NormalWhite, label)),
                label);

            if (_report.IsRevealed(row))
            {
                DrawFlags(target, row);
            }
        }

        for (int column = 0; column < players.Count; ++column)
        {
            DrawCentred(_fonts.NormalWhite, target, Layout.ColorNameCentre(column), PlayerColors.GetName(players[column].Color));
        }

        DrawLabel(target, "Best Hero:", Layout.Dialog.X + ThievesGuildLayout.TextOffsetX, Layout.Dialog.Y + ThievesGuildLayout.BestHeroOffsetY);

        if (_report.ShowsBestHero)
        {
            DrawHeroes(target, players);
        }

        DrawLabel(target, "Best Hero Stats:", Layout.Dialog.X + ThievesGuildLayout.TextOffsetX, Layout.Dialog.Y + ThievesGuildLayout.BestHeroStatsOffsetY);
        DrawLabel(target, "Personality:", Layout.Dialog.X + ThievesGuildLayout.TextOffsetX, Layout.Dialog.Y + ThievesGuildLayout.PersonalityLabelOffsetY);

        if (_report.ShowsPersonality)
        {
            DrawPersonalities(target, players);
        }

        DrawLabel(target, "Best Monster:", Layout.Dialog.X + ThievesGuildLayout.TextOffsetX, Layout.Dialog.Y + ThievesGuildLayout.BestMonsterOffsetY);

        if (_report.ShowsBestMonster)
        {
            DrawMonsters(target, players);
        }

        _exitButton.Draw(target);
    }

    /// <summary>Reads the exit button, <c>dialog_thievesguild.cpp:448-473</c>.</summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>The action that leaves the report, if any.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public ThievesGuildAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _exitButton.UpdatePressState(events);

        if (events.MouseClickLeft(_exitButton.Area))
        {
            return ThievesGuildAction.Exit;
        }

        if (events.IsMouseRightButtonPressedInArea(_exitButton.Area))
        {
            return ThievesGuildAction.ExitHelp;
        }

        return ThievesGuildAction.None;
    }

    private static void DrawCentred(IGlyphSource font, PixelBuffer target, in Point centre, string text) =>
        GlyphText.DrawLine(font, target, ThievesGuildLayout.Centre(centre, GlyphText.Measure(font, text)), text);

    private void DrawLabel(PixelBuffer target, string text, int right, int top) =>
        GlyphText.DrawLine(_fonts.NormalWhite, target, new Point(right - GlyphText.Measure(_fonts.NormalWhite, text), top), text);

    private void DrawFlags(PixelBuffer target, ThievesGuildRow row)
    {
        IReadOnlyList<ThievesGuildGroup> groups = _report.Ranking(row);
        Point anchor = Layout.FlagsAnchor(row);
        int flagWidth = _artwork.Flags[0].Width;
        int shown = Math.Min(_report.Players.Count, groups.Count);

        for (int group = 0; group < shown; ++group)
        {
            IReadOnlyList<PlayerColor> colors = PlayerColors.ToList(groups[group].Colors);
            int x = anchor.X + (group * ThievesGuildLayout.StepX) - ((colors.Count * flagWidth) / 2) + 3;

            foreach (PlayerColor color in colors)
            {
                ImageOps.BlitSprite(target, new Point(x, anchor.Y - 4), _artwork.Flags[PlayerColors.IndexOf(color)]);
                x += flagWidth;
            }
        }
    }

    private void DrawHeroes(PixelBuffer target, IReadOnlyList<ThievesGuildPlayer> players)
    {
        for (int column = 0; column < players.Count; ++column)
        {
            if (players[column].BestHero is not { } hero)
            {
                continue;
            }

            Point centre = Layout.HeroIconCentre(column);

            ImageOps.BlitSprite(
                target,
                new Point(centre.X - (_artwork.HeroWindow.Width / 2), centre.Y - 4),
                _artwork.HeroWindow);

            int frame = hero.Id - 1;

            if (frame >= 0 && frame < _artwork.Portraits.Count)
            {
                Sprite portrait = _artwork.Portraits[frame];
                ImageOps.Blit(target, new Point(centre.X - (portrait.Width / 2), centre.Y), portrait.Image, portrait.Image.Bounds);
            }

            if (_report.ShowsHeroStats)
            {
                DrawHeroStats(target, hero, Layout.HeroStatsAnchor(column));
            }
        }
    }

    private void DrawHeroStats(PixelBuffer target, ThievesGuildHero hero, in Point origin)
    {
        DrawStatistic(target, "Att.", hero.Attack, 0, origin);
        DrawStatistic(target, "Def.", hero.Defense, 1, origin);
        DrawStatistic(target, "Power", hero.Power, 2, origin);
        DrawStatistic(target, "Knowl", hero.Knowledge, 3, origin);
    }

    private void DrawStatistic(PixelBuffer target, string label, int value, int line, in Point origin)
    {
        Point at = new(origin.X, origin.Y + (line * ThievesGuildLayout.HeroStatsLineStepY));
        GlyphText.DrawLine(_fonts.SmallWhite, target, at, label);

        string text = value.ToString(CultureInfo.InvariantCulture);
        GlyphText.DrawLine(
            _fonts.SmallWhite,
            target,
            new Point(
                origin.X + ThievesGuildLayout.HeroStatsValueOffsetX - GlyphText.Measure(_fonts.SmallWhite, text),
                at.Y),
            text);
    }

    private void DrawPersonalities(PixelBuffer target, IReadOnlyList<ThievesGuildPlayer> players)
    {
        for (int column = 0; column < players.Count; ++column)
        {
            DrawCentred(_fonts.SmallWhite, target, Layout.PersonalityCentre(column), players[column].Personality);
        }
    }

    private void DrawMonsters(PixelBuffer target, IReadOnlyList<ThievesGuildPlayer> players)
    {
        int first = (int)MonsterType.Peasant;

        for (int column = 0; column < players.Count; ++column)
        {
            if (players[column].StrongestMonster is not { } monster)
            {
                continue;
            }

            int frame = (int)monster - first;

            if (frame < 0 || frame >= _artwork.Monsters.Count)
            {
                continue;
            }

            Point centre = Layout.MonsterCentre(column);
            Sprite sprite = _artwork.Monsters[frame];

            ImageOps.BlitSprite(
                target,
                new Point(centre.X - (sprite.Width / 2), centre.Y - (sprite.Height / 2)),
                sprite);
        }
    }
}
