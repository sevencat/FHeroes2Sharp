using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The behaviour of <see cref="HeroDialog"/>, the port of <c>Heroes::OpenDialog()</c>
/// (<c>heroes_dialog.cpp</c>).
/// </summary>
public sealed class HeroDialogTests
{
    private const byte BackgroundColor = 17;
    private const byte PortraitColor = 42;
    private const byte ExitColor = 91;

    [Fact]
    public void GetTitle_NamesTheHero()
    {
        Assert.Equal(
            "Lord Kilburn the Knight (Level 7)",
            HeroDialog.GetTitle("Lord Kilburn", MapRace.Knight, 7));
    }

    [Fact]
    public void GetTitle_FallsBackToTheRandomHeroLine()
    {
        // Without a name and without a race there is nothing to name, which is what the editor stores before the
        // first turn of a random hero.
        Assert.Equal("Random hero (Level 3)", HeroDialog.GetTitle(string.Empty, MapRace.Random, 3));
    }

    [Fact]
    public void GetTitle_FallsBackToTheRandomRaceLine()
    {
        Assert.Equal(
            "Random Sorceress hero (Level 5)",
            HeroDialog.GetTitle(string.Empty, MapRace.Sorceress, 5));
    }

    [Fact]
    public void Layout_MatchesTheReferenceAnchors()
    {
        HeroDialogLayout layout = new();

        Assert.Equal(new Rect(0, 0, 640, 480), layout.Area);
        Assert.Equal(new Rect(49, 31, 101, 93), layout.PortraitArea);
        Assert.Equal(new Rect(60, 1, 519, 17), layout.TitleArea);
        Assert.Equal(new Point(156, 31), layout.PrimarySkillsOffset);
        Assert.Equal(new Point(49, 130), layout.CrestArea.Position);
        Assert.Equal(new Point(156, 130), layout.ArmyOffset);
        Assert.Equal(new Point(3, 233), layout.SecondarySkillsOffset);
        Assert.Equal(new Point(51, 308), layout.ArtifactsOffset);
        Assert.Equal(new Point(22, 460), layout.StatusBarOffset);
        Assert.Equal(new Point(9, 378), layout.DismissOffset);
        Assert.Equal(new Point(602, 378), layout.ExitOffset);
        Assert.Equal(new Point(0, 460), layout.PreviousHeroOffset);
        Assert.Equal(new Point(618, 460), layout.NextHeroOffset);
    }

    [Fact]
    public void Layout_MovesTheEditorIndicatorsUp()
    {
        HeroDialogLayout layout = new();

        // The editor has no formation icons, so the reference moves the morale and luck pair down ten pixels
        // (heroes_dialog.cpp:204) and lifts the experience and spell point pair by ten (:243, :258).
        Assert.Equal(layout.LuckOffset(isEditor: false).Y + 10, layout.LuckOffset(isEditor: true).Y);
        Assert.Equal(layout.MoraleOffset(isEditor: false).Y + 10, layout.MoraleOffset(isEditor: true).Y);
        Assert.Equal(layout.ExperienceOffset(isEditor: false).Y - 10, layout.ExperienceOffset(isEditor: true).Y);
        Assert.Equal(layout.SpellPointsOffset(isEditor: false).Y - 10, layout.SpellPointsOffset(isEditor: true).Y);
    }

    [Fact]
    public void Layout_ShiftsWithTheOffset()
    {
        Point offset = new(10, 20);
        HeroDialogLayout layout = new(offset);

        Assert.Equal(new Rect(59, 51, 101, 93), layout.PortraitArea);
        Assert.Equal(new Point(166, 150), layout.ArmyOffset);
        Assert.Equal(new Point(612, 398), layout.ExitOffset);
        Assert.Equal(new Rect(10, 20, 640, 480), layout.Area);
    }

    [Fact]
    public void Draw_PaintsTheBackgroundAndThePortrait()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);

        dialog.Draw(display);

        // The extension is a small corner piece over the background (heroes_dialog.cpp:97), so the middle of the
        // dialog shows the background alone, and the portrait sits on its own anchor.
        Assert.Equal(BackgroundColor, display[320, 240]);
        Assert.Equal(PortraitColor, display[49, 31]);
    }

    [Fact]
    public void Draw_IsRepeatable()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);

        dialog.Draw(display);
        PixelBuffer first = display.Clone();
        dialog.Draw(display);

        Assert.Equal(first.Pixels, display.Pixels);
    }

    [Fact]
    public void ProcessEvents_ClosesOnTheExitButton()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        LocalEvent events = new();

        Click(events, Centre(dialog.ExitArea));

        dialog.ProcessEvents(events);

        Assert.Equal(HeroDialogResult.Cancel, dialog.Result);
    }

    [Fact]
    public void ProcessEvents_ClosesOnTheCloseHotKey()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);

        dialog.ProcessEvents(new LocalEvent(), new HeroDialogHotKeys(CloseWindow: true));

        Assert.Equal(HeroDialogResult.Cancel, dialog.Result);
    }

    [Fact]
    public void ProcessEvents_WalksToTheNextHero()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);

        dialog.ProcessEvents(new LocalEvent(), new HeroDialogHotKeys(NextHero: true));

        Assert.Equal(HeroDialogResult.Next, dialog.Result);
    }

    [Fact]
    public void ProcessEvents_WalksToThePreviousHero()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);

        dialog.ProcessEvents(new LocalEvent(), new HeroDialogHotKeys(PreviousHero: true));

        Assert.Equal(HeroDialogResult.Previous, dialog.Result);
    }

    [Fact]
    public void ProcessEvents_IgnoresTheSwitchWhenThereIsOneHero()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display, canSwitchHeroes: false);
        LocalEvent events = new();

        Click(events, Centre(dialog.NextHeroArea));

        dialog.ProcessEvents(events, new HeroDialogHotKeys(NextHero: true));

        Assert.Equal(HeroDialogResult.None, dialog.Result);
    }

    [Fact]
    public void ProcessEvents_AsksToDismiss()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        LocalEvent events = new();

        Click(events, Centre(dialog.DismissArea));

        dialog.ProcessEvents(events);

        Assert.True(dialog.Requests.Dismiss);
        Assert.Equal(HeroDialogResult.None, dialog.Result);
    }

    [Fact]
    public void ProcessEvents_RefusesToDismissInACastle()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display, inCastle: true);
        LocalEvent events = new();

        Move(events, Centre(dialog.DismissArea));
        dialog.ProcessEvents(events);

        Assert.False(dialog.Requests.Dismiss);
        Assert.Equal("You cannot dismiss a hero in a castle", dialog.Message);

        Click(events, Centre(dialog.DismissArea));
        dialog.ProcessEvents(events);

        Assert.False(dialog.Requests.Dismiss);
    }

    [Fact]
    public void ProcessEvents_SaysWhyTheScenarioForbidsTheDismissal()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display, dismissProhibited: true);
        LocalEvent events = new();

        Move(events, Centre(dialog.DismissArea));
        dialog.ProcessEvents(events);

        Assert.Equal(
            $"Dismissal of {dialog.Hero.Name} the {MapRaces.Name(dialog.Hero.Race)} is prohibited by scenario",
            dialog.Message);
    }

    [Fact]
    public void ProcessEvents_SwitchesTheFormation()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        LocalEvent events = new();

        Assert.False(dialog.Hero.Army.IsSpreadFormation);

        Click(events, Centre(dialog.SpreadFormationArea));
        bool redraw = dialog.ProcessEvents(events);

        Assert.True(dialog.Hero.Army.IsSpreadFormation);
        Assert.True(redraw);

        Click(events, Centre(dialog.GroupedFormationArea));
        dialog.ProcessEvents(events);

        Assert.False(dialog.Hero.Army.IsSpreadFormation);
    }

    [Fact]
    public void ProcessEvents_KeepsTheFormationOfAReadOnlyScreen()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display, readOnly: true);
        LocalEvent events = new();

        Click(events, Centre(dialog.SpreadFormationArea));
        dialog.ProcessEvents(events);

        Assert.False(dialog.Hero.Army.IsSpreadFormation);
    }

    [Fact]
    public void ProcessEvents_AsksForTheQuickInformation()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        LocalEvent events = new();

        RightClick(events, Centre(dialog.Layout.PortraitArea));
        dialog.ProcessEvents(events);

        Assert.True(dialog.Requests.ShowQuickInfo);
    }

    [Fact]
    public void ProcessEvents_ExplainsTheTwoFormations()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        LocalEvent events = new();

        RightClick(events, Centre(dialog.SpreadFormationArea));
        dialog.ProcessEvents(events);

        Assert.Equal("Spread Formation", dialog.Requests.Popup?.Header);

        RightClick(events, Centre(dialog.GroupedFormationArea));
        dialog.ProcessEvents(events);

        Assert.Equal("Grouped Formation", dialog.Requests.Popup?.Header);
    }

    [Fact]
    public void ProcessEvents_ShowsTheStatusBarTitleWhenNothingIsUnderTheCursor()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        LocalEvent events = new();

        Move(events, new Point(1, 1));
        dialog.ProcessEvents(events);

        Assert.Equal(HeroDialog.DefaultMessage, dialog.Message);
    }

    [Fact]
    public void Show_ThrowsWhenNoEngineIsAttached()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);

        Assert.Throws<InvalidOperationException>(() =>
            dialog.Show(display, new LocalEvent(() => 0)));
    }

    [Fact]
    public void Show_ReturnsCancelOnEscape()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);

        LocalEvent events = new(() => 0);
        FakeEngine engine = new()
        {
            OnFrame = (state, frame) =>
                state.OnKeyboard(Key.Escape, KeyModifier.None, KeyboardEventState.Down),
        };

        events.Engine = engine;

        Assert.Equal(HeroDialogResult.Cancel, dialog.Show(display, events));
    }

    [Fact]
    public void Show_ReturnsNoneWhenAFrameNeedsTheCaller()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        Point point = Centre(dialog.SpreadFormationArea);

        LocalEvent events = new(() => 0);
        FakeEngine engine = new()
        {
            OnFrame = (state, frame) => RightClick(state, point),
        };

        events.Engine = engine;

        // The help popup is not part of the screen, so the loop hands the frame back instead of drawing it.
        Assert.Equal(HeroDialogResult.None, dialog.Show(display, events));
        Assert.Equal("Spread Formation", dialog.Requests.Popup?.Header);
    }

    [Fact]
    public void Show_ReturnsCancelWhenTheEngineStops()
    {
        PixelBuffer display = new(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight);
        HeroDialog dialog = CreateDialog(display);
        LocalEvent events = new(() => 0) { Engine = new FakeEngine { FramesBeforeStop = 0 } };

        Assert.Equal(HeroDialogResult.Cancel, dialog.Show(display, events));
    }

    /// <summary>An event engine that plays a scripted frame at a time and stops after a few of them.</summary>
    private sealed class FakeEngine : IEventEngine
    {
        public Rect ScreenArea => new(0, 0, 640, 480);

        public int FramesBeforeStop { get; init; } = 8;

        public int Frames { get; private set; }

        public Action<LocalEvent, int>? OnFrame { get; set; }

        public bool HandleEvents(LocalEvent state, bool allowExit, out bool displayRefreshRequired)
        {
            displayRefreshRequired = false;

            if (Frames >= FramesBeforeStop)
            {
                return false;
            }

            OnFrame?.Invoke(state, Frames++);

            return true;
        }

        public void Render(Rect area)
        {
        }

        public void Sleep(ulong milliseconds)
        {
        }
    }

    private static HeroDialog CreateDialog(
        PixelBuffer display,
        bool canSwitchHeroes = true,
        bool readOnly = false,
        bool inCastle = false,
        bool dismissProhibited = false)
    {
        _ = display;

        Hero hero = new(Hero.LordKilburn, MapRace.Knight);

        return new HeroDialog(
            hero,
            CreateArtwork(),
            CreateArmySprites(hero.Race),
            new StubArtifactsBarSprites(),
            new StubSkillBarSprites(),
            Font(),
            Font(),
            canSwitchHeroes: canSwitchHeroes,
            readOnly: readOnly,
            inCastle: inCastle,
            dismissProhibited: dismissProhibited);
    }

    private static HeroDialogArtwork CreateArtwork() =>
        new()
        {
            Background = CreateSprite(HeroDialogLayout.DialogWidth, HeroDialogLayout.DialogHeight, BackgroundColor),
            BackgroundExtension = CreateSprite(16, 16, 3),
            SpreadFormation = CreateSprite(12, 12, 1),
            GroupedFormation = CreateSprite(12, 12, 2),
            FormationCursor = CreateSprite(14, 14, 3),
            StatusBarPlate = CreateSprite(600, 20, 4),
            PreviousHeroReleased = CreateSprite(22, 40, 5),
            PreviousHeroPressed = CreateSprite(22, 40, 6),
            NextHeroReleased = CreateSprite(22, 40, 7),
            NextHeroPressed = CreateSprite(22, 40, 8),
            DismissReleased = CreateSprite(24, 52, 9),
            DismissPressed = CreateSprite(24, 52, 10),
            DismissDisabled = CreateSprite(24, 52, 11),
            ExitReleased = CreateSprite(24, 52, ExitColor),
            ExitPressed = CreateSprite(24, 52, 12),
            Indicators = CreateIndicators(),
            Portrait = CreateSprite(101, 93, PortraitColor),
            Crest = CreateSprite(101, 93, 13),
            ArmySlot = CreateSprite(64, 64, 14),
            ArmyCursor = CreateSprite(64, 64, 15),
        };

    private static HeroIndicatorArtwork CreateIndicators() =>
        new()
        {
            LuckGood = CreateSprite(35, 26, 1),
            LuckBad = CreateSprite(35, 26, 1),
            LuckNeutral = CreateSprite(35, 26, 1),
            MoraleGood = CreateSprite(35, 26, 1),
            MoraleBad = CreateSprite(35, 26, 1),
            MoraleNeutral = CreateSprite(35, 26, 1),
            Experience = CreateSprite(35, 26, 1),
            SpellPoints = CreateSprite(35, 26, 1),
        };

    private static ArmyBarSpriteSet CreateArmySprites(MapRace race)
    {
        ArmyBarSpriteSet sprites = new();
        sprites.RaceFrames[race] = CreateSprite(64, 64, 16);
        sprites.RaceFrames[MapRace.Knight] = CreateSprite(64, 64, 16);

        return sprites;
    }

    private static Sprite CreateSprite(int width, int height, byte color)
    {
        PixelBuffer image = new(width, height);
        image.Fill(color);

        return new Sprite(image, 0, 0);
    }

    private static Point Centre(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static void Move(LocalEvent events, Point position) => events.OnMouseMotion(position);

    private static void Click(LocalEvent events, Point position)
    {
        events.OnMouseButton(true, MouseButtonType.Left, position);
        events.OnMouseButton(false, MouseButtonType.Left, position);
    }

    /// <summary>
    /// The right press; the release is not part of the frame the dialogs look at
    /// (<see cref="LocalEvent.IsMouseRightButtonPressedInArea"/>).
    /// </summary>
    private static void RightClick(LocalEvent events, Point position)
    {
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);
    }

    private static IGlyphSource Font() => new BlockFont(8, 6, 200);

    /// <summary>A font of identical solid glyphs, so text measurement is predictable.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte color)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight { get; }

        public int SpaceWidth { get; }

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }

    /// <summary>The bag's lookups, backed by single frames.</summary>
    private sealed class StubArtifactsBarSprites : IArtifactsBarSprites
    {
        public Sprite? MiniPlate => CreateSprite(34, 34, 20);

        public Sprite? EmptySlot => CreateSprite(65, 65, 21);

        public Sprite? GetArtifact(uint frame) => null;

        public Sprite? GetMiniArtifact(uint frame) => null;
    }

    /// <summary>The two skill bars' lookups, backed by single frames.</summary>
    private sealed class StubSkillBarSprites : ISkillBarSprites
    {
        public Sprite? SecondarySkillPlate => CreateSprite(34, 34, 22);

        public Sprite? MiniPlate => CreateSprite(34, 34, 23);

        public Sprite? GetPrimarySkillFrame(PrimarySkill skill) => CreateSprite(73, 70, 24);

        public Sprite? GetSwapWindowFrame() => CreateSprite(73, 70, 25);

        public Sprite? GetSecondarySkillFrame(SecondarySkill skill) => CreateSprite(34, 34, 26);

        public Sprite? GetMiniSecondarySkillFrame(SecondarySkill skill) => CreateSprite(32, 32, 27);
    }
}
