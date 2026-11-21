using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

// The port still carries the self drawn panel of the old scene (FHeroes2.Game.AdventureMap.ButtonsPanel) next to the
// ported one; the tests of the ported panel pin the alias so the two do not collide.
using ButtonsPanel = FHeroes2.UI.Interface.ButtonsPanel;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="ButtonsPanel"/>, <c>Interface::ButtonsPanel</c> (<c>gui/interface_buttons.cpp:34-293</c>).
/// </summary>
public sealed class ButtonsPanelTests
{
    private const int BlockX = 300;
    private const int BlockY = 400;

    private static readonly AdventureButtonStatus EverythingEnabled =
        new(HeroMovementIcon.Walk, IsMovementEnabled: true, IsSpellEnabled: true, IsNextHeroEnabled: true,
            HasValidPath: true);

    [Fact]
    public void SetPos_LaysOutTwoRowsOfFour()
    {
        ButtonsPanel panel = CreatePanel();

        panel.SetPos(BlockX, BlockY);

        Assert.Equal(new Rect(BlockX, BlockY, 144, 72), panel.Area);
        Assert.Equal(new Rect(BlockX, BlockY, 36, 36), panel.NextHeroRect);
        Assert.Equal(new Rect(BlockX + 36, BlockY, 36, 36), panel.HeroMovementRect);
        Assert.Equal(new Rect(BlockX + 72, BlockY, 36, 36), panel.KingdomRect);
        Assert.Equal(new Rect(BlockX + 108, BlockY, 36, 36), panel.SpellRect);
        Assert.Equal(new Rect(BlockX, BlockY + 36, 36, 36), panel.EndTurnRect);
        Assert.Equal(new Rect(BlockX + 36, BlockY + 36, 36, 36), panel.AdventureRect);
        Assert.Equal(new Rect(BlockX + 72, BlockY + 36, 36, 36), panel.FileRect);
        Assert.Equal(new Rect(BlockX + 108, BlockY + 36, 36, 36), panel.SystemRect);
    }

    [Fact]
    public void AreaOf_MatchesTheButtonAreas()
    {
        ButtonsPanel panel = CreatePanel();
        panel.SetPos(BlockX, BlockY);

        foreach (AdventureButton button in Enum.GetValues<AdventureButton>())
        {
            Rect area = panel.AreaOf(button);

            Assert.Equal(36, area.Width);
            Assert.Equal(36, area.Height);
            Assert.True(area.X >= BlockX && area.X <= BlockX + 108, $"{button} sticks out to the right.");
            Assert.True(area.Y >= BlockY && area.Y <= BlockY + 36, $"{button} sticks out downwards.");
        }
    }

    [Fact]
    public void Constructor_RejectsAShortFrameList()
    {
        PixelBuffer display = new(800, 600);

        Assert.Throws<ArgumentException>(() =>
            new ButtonsPanel(display, new Rect(0, 0, 144, 72), CreateFrames(15)));
    }

    [Fact]
    public void SetButtonStatus_ShowsTheWalkFrames()
    {
        ButtonsPanel panel = CreatePanel();

        panel.SetButtonStatus(new AdventureButtonStatus(HeroMovementIcon.Walk, true, true, true));

        Assert.Same(Frames[ButtonsPanel.WalkFrame], panel.Buttons[(int)AdventureButton.HeroMovement]!.Released);
    }

    [Fact]
    public void SetButtonStatus_ShowsTheActionFramesWhenTheFileCarriesThem()
    {
        ButtonsPanel panel = CreatePanel(Frames20);

        panel.SetButtonStatus(new AdventureButtonStatus(HeroMovementIcon.Action, true, true, true));

        Assert.Same(Frames20[ButtonsPanel.ActionFrame], panel.Buttons[(int)AdventureButton.HeroMovement]!.Released);
    }

    [Fact]
    public void SetButtonStatus_FallsBackToTheWalkFramesWithoutTheActionArt()
    {
        ButtonsPanel panel = CreatePanel();

        // The retail ADVBTNS.ICN stops at frame 15, so 16..19 are missing and the panel keeps the walk glyph.
        panel.SetButtonStatus(new AdventureButtonStatus(HeroMovementIcon.Action, true, true, true));

        Assert.Same(Frames[2], panel.Buttons[(int)AdventureButton.HeroMovement]!.Released);
    }

    [Fact]
    public void SetButtonStatus_TogglesTheThreeConditionalButtons()
    {
        ButtonsPanel panel = CreatePanel();

        panel.SetButtonStatus(new AdventureButtonStatus(HeroMovementIcon.Reset, false, false, false));

        Assert.False(panel.IsEnabled(AdventureButton.NextHero));
        Assert.False(panel.IsEnabled(AdventureButton.HeroMovement));
        Assert.False(panel.IsEnabled(AdventureButton.Spell));

        // The five unconditional buttons never go dark.
        Assert.True(panel.IsEnabled(AdventureButton.Kingdom));
        Assert.True(panel.IsEnabled(AdventureButton.EndTurn));
        Assert.True(panel.IsEnabled(AdventureButton.Adventure));
        Assert.True(panel.IsEnabled(AdventureButton.File));
        Assert.True(panel.IsEnabled(AdventureButton.System));

        panel.SetButtonStatus(EverythingEnabled);

        Assert.True(panel.IsEnabled(AdventureButton.NextHero));
        Assert.True(panel.IsEnabled(AdventureButton.HeroMovement));
        Assert.True(panel.IsEnabled(AdventureButton.Spell));
        Assert.Equal(EverythingEnabled, panel.ButtonStatus);
    }

    [Theory]
    [InlineData(AdventureButton.NextHero)]
    [InlineData(AdventureButton.HeroMovement)]
    [InlineData(AdventureButton.Kingdom)]
    [InlineData(AdventureButton.Spell)]
    [InlineData(AdventureButton.EndTurn)]
    [InlineData(AdventureButton.Adventure)]
    [InlineData(AdventureButton.File)]
    [InlineData(AdventureButton.System)]
    public void QueueEventProcessing_ReportsAClickOnEveryButton(AdventureButton button)
    {
        ButtonsPanel panel = CreateEnabledPanel();
        Point centre = CentreOf(panel.AreaOf(button));

        LocalEvent events = ClickAt(centre);

        Assert.Equal(
            new ButtonsPanelAction(button, ButtonsPanelInput.Click),
            panel.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_IgnoresDisabledButtons()
    {
        ButtonsPanel panel = CreatePanel();
        panel.SetPos(BlockX, BlockY);
        panel.SetButtonStatus(new AdventureButtonStatus(HeroMovementIcon.Reset, false, false, false));

        Assert.Null(panel.QueueEventProcessing(ClickAt(CentreOf(panel.NextHeroRect))));
        Assert.Null(panel.QueueEventProcessing(ClickAt(CentreOf(panel.HeroMovementRect))));
        Assert.Null(panel.QueueEventProcessing(ClickAt(CentreOf(panel.SpellRect))));

        Assert.Equal(
            new ButtonsPanelAction(AdventureButton.Kingdom, ButtonsPanelInput.Click),
            panel.QueueEventProcessing(ClickAt(CentreOf(panel.KingdomRect))));
    }

    [Fact]
    public void QueueEventProcessing_IgnoresAClickOutsideTheBlock()
    {
        ButtonsPanel panel = CreateEnabledPanel();

        Assert.Null(panel.QueueEventProcessing(ClickAt(new Point(BlockX - 20, BlockY - 20))));
    }

    [Fact]
    public void QueueEventProcessing_ReportsARightPressForTheTooltip()
    {
        ButtonsPanel panel = CreateEnabledPanel();

        LocalEvent events = new();
        Point centre = CentreOf(panel.EndTurnRect);
        events.OnMouseButton(true, MouseButtonType.Right, centre);

        Assert.Equal(
            new ButtonsPanelAction(AdventureButton.EndTurn, ButtonsPanelInput.RightClick),
            panel.QueueEventProcessing(events));
    }

    [Fact]
    public void QueueEventProcessing_DoesNotFireALongPressOnAPlainPress()
    {
        ButtonsPanel panel = CreateEnabledPanel();

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, CentreOf(panel.HeroMovementRect));

        // The press is not a click and the long press delay has not passed yet.
        Assert.Null(panel.QueueEventProcessing(events));
    }

    [Fact]
    public void Redraw_PaintsAllEightButtons()
    {
        ButtonsPanel panel = CreateEnabledPanel();
        panel.SetButtonStatus(EverythingEnabled);

        PixelBuffer target = new(800, 600);
        target.Fill(0);

        panel.Redraw(target);

        foreach (AdventureButton button in Enum.GetValues<AdventureButton>())
        {
            Rect area = panel.AreaOf(button);
            byte expected = button == AdventureButton.HeroMovement
                ? (byte)(ButtonsPanel.WalkFrame + 1)
                : (byte)(((int)button * 2) + 1);

            Assert.Equal(expected, target[area.X, area.Y]);
        }
    }

    [Fact]
    public void Redraw_DarkensTheDisabledSpellButton()
    {
        ButtonsPanel panel = CreateEnabledPanel();
        panel.SetButtonStatus(new AdventureButtonStatus(HeroMovementIcon.Walk, true, false, true));

        PixelBuffer target = new(800, 600);
        target.Fill(0);

        panel.Redraw(target);

        Rect spell = panel.SpellRect;
        byte plainColor = (byte)(((int)AdventureButton.Spell * 2) + 1);

        Assert.NotEqual(plainColor, target[spell.X, spell.Y]);
    }

    [Fact]
    public void UpdatePressState_DrawsThePressedFrame()
    {
        ButtonsPanel panel = CreateEnabledPanel();
        PixelBuffer target = new(800, 600);
        target.Fill(0);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, CentreOf(panel.KingdomRect));

        Assert.True(panel.UpdatePressState(events, target));
        Assert.Equal(
            (byte)(((int)AdventureButton.Kingdom * 2) + 2),
            target[panel.KingdomRect.X, panel.KingdomRect.Y]);

        // Nothing changed on the second look at the same press.
        Assert.False(panel.UpdatePressState(events, target));
    }

    [Fact]
    public void UpdatePressState_DrawsTheReleasedFrameOnRelease()
    {
        ButtonsPanel panel = CreateEnabledPanel();
        PixelBuffer target = new(800, 600);

        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, CentreOf(panel.FileRect));
        panel.UpdatePressState(events, target);

        events.OnMouseButton(false, MouseButtonType.Left, CentreOf(panel.FileRect));

        Assert.True(panel.UpdatePressState(events, target));
        Assert.Equal(
            (byte)(((int)AdventureButton.File * 2) + 1),
            target[panel.FileRect.X, panel.FileRect.Y]);
    }

    [Fact]
    public void SavePosition_IsNotPortedYet()
    {
        ButtonsPanel panel = CreatePanel();

        Assert.Throws<NotImplementedException>(panel.SavePosition);
    }

    [Fact]
    public void Create_ReturnsNullWithoutArchive()
    {
        PixelBuffer display = new(800, 600);

        Assert.Null(ButtonsPanel.Create(null, false, display, new Rect(0, 0, 144, 72)));
        Assert.Null(ButtonsPanel.Create(null, true, display, new Rect(0, 0, 144, 72)));
    }

    [Fact]
    public void Create_ReadsTheButtonSetFromTheArchive()
    {
        if (AssetLocator.Resolve() is not { } directory)
        {
            return;
        }

        using AssetBundle? assets = AssetBundle.Open(directory);
        if (assets is null)
        {
            return;
        }

        PixelBuffer display = new(800, 600);
        ButtonsPanel? good = ButtonsPanel.Create(assets, false, display, new Rect(BlockX, BlockY, 144, 72));
        ButtonsPanel? evil = ButtonsPanel.Create(assets, true, display, new Rect(BlockX, BlockY, 144, 72));

        Assert.NotNull(good);
        Assert.NotNull(evil);

        // Every frame of the retail file is a 36x36 button.
        good!.SetButtonStatus(EverythingEnabled);
        Assert.Equal(new Rect(BlockX, BlockY, 36, 36), good.NextHeroRect);
        Assert.True(good.IsEnabled(AdventureButton.System));
    }

    private static ButtonsPanel CreatePanel(IReadOnlyList<Sprite>? frames = null)
    {
        ButtonsPanel panel = new(new PixelBuffer(800, 600), new Rect(0, 0, 144, 72), frames ?? Frames);
        panel.SetPos(BlockX, BlockY);

        return panel;
    }

    private static ButtonsPanel CreateEnabledPanel()
    {
        ButtonsPanel panel = CreatePanel();
        panel.SetButtonStatus(EverythingEnabled);

        return panel;
    }

    private static Point CentreOf(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static LocalEvent ClickAt(Point point)
    {
        LocalEvent events = new();
        events.OnMouseButton(true, MouseButtonType.Left, point);
        events.OnMouseButton(false, MouseButtonType.Left, point);

        return events;
    }

    /// <summary>
    /// Sixteen frames, frame <c>i</c> painted with palette index <c>i + 1</c>, which is what the retail
    /// <c>ADVBTNS.ICN</c> holds: one released and one pressed frame per button.
    /// </summary>
    private static readonly Sprite[] Frames = CreateFrames(16);

    /// <summary>Twenty frames, as the extended asset packs carry: walk (2,3), action (16,17) and idle (18,19).</summary>
    private static readonly Sprite[] Frames20 = CreateFrames(20);

    private static Sprite[] CreateFrames(int count)
    {
        Sprite[] frames = new Sprite[count];

        for (int i = 0; i < count; ++i)
        {
            PixelBuffer image = new(36, 36);
            image.Fill((byte)(i + 1));
            frames[i] = new Sprite(image);
        }

        return frames;
    }
}
