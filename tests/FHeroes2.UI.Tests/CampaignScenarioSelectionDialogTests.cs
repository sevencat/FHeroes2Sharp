using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Campaign;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The campaign selection screen of <c>SelectCampaignScenario()</c> (<c>game_campaign.cpp:1284-1710</c>): the bonus
/// column, the two faces of the okay button, the help areas of the right button and the state the screen starts in.
/// </summary>
/// <remarks>
/// 战役存档是进程级单例（<c>CampaignSaveData.Get()</c>），每个测试自己 <c>Reset()</c> 再摆出要测的那一幕；
/// xUnit 在同一个类里串行跑，所以这里不需要额外的并行开关。
/// </remarks>
public sealed class CampaignScenarioSelectionDialogTests
{
    private const byte BackgroundColour = 210;
    private const byte TrackColour = 211;
    private const byte HeaderColour = 212;
    private const byte ButtonColourBase = 220;
    private const byte IconColourBase = 230;

    private const int ScreenWidth = 640;
    private const int ScreenHeight = 480;
    private const int ButtonWidth = 60;
    private const int ButtonHeight = 20;

    /// <summary>The screen top left corner: the background is as big as the screen, so this is (0, 0).</summary>
    private static readonly Point Top = new(0, 0);

    [Fact]
    public void ANewScenario_PreselectsTheFirstBonus()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);

        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        Assert.True(dialog.Bonuses.Count > 1);
        Assert.Equal(0, dialog.SelectedBonusIndex);
    }

    [Fact]
    public void ARestartingScenario_PreselectsTheBonusStoredInTheSave()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignSaveData.Get().SetCurrentScenarioInfo(scenario.ScenarioInfo, 2);

        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario, allowToRestart: true);

        Assert.Equal(2, dialog.SelectedBonusIndex);
    }

    [Fact]
    public void ARestartingScenarioWithABonusOutOfRange_PreselectsNothing()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignSaveData.Get().SetCurrentScenarioInfo(scenario.ScenarioInfo, 7);

        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario, allowToRestart: true);

        Assert.Equal(-1, dialog.SelectedBonusIndex);
    }

    [Fact]
    public void TheBonusRows_AreStackedTwentyTwoPixelsApart()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);

        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        IReadOnlyList<Rect> areas = dialog.BonusAreas;

        Assert.Equal(dialog.Bonuses.Count, areas.Count);

        for (int i = 0; i < areas.Count; ++i)
        {
            Assert.Equal(Top.X + 590, areas[i].X);
            Assert.Equal(Top.Y + 199 + (22 * i), areas[i].Y);
        }
    }

    [Fact]
    public void Drawing_ShowsTheOkayButtonForANewScenario()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);

        var target = new PixelBuffer(ScreenWidth, ScreenHeight);
        CreateDialog(scenario).Draw(target);

        // 「确定」是四帧一组里的第 4 帧（未按下），与「重开」的第 2 帧占同一个位置。
        Assert.Equal(ButtonColourBase + 4, target.Row(431 + (ButtonHeight / 2))[376 + 10]);
    }

    [Fact]
    public void Drawing_ShowsTheRestartButtonForARunningScenario()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignSaveData.Get().SetCurrentScenarioInfo(scenario.ScenarioInfo, 0);

        var target = new PixelBuffer(ScreenWidth, ScreenHeight);
        CreateDialog(scenario, allowToRestart: true).Draw(target);

        Assert.Equal(ButtonColourBase + 2, target.Row(431 + (ButtonHeight / 2))[376 + 10]);
    }

    [Fact]
    public void Drawing_PaintsOnlyTheSelectedBonusWithThePressedFrame()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);

        var target = new PixelBuffer(ScreenWidth, ScreenHeight);
        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        dialog.Draw(target);
        Assert.Equal(IconColourBase + 8, target.Row(Top.Y + 199 + 2)[Top.X + 590 + 2]);

        Assert.True(dialog.SelectBonus(1));

        dialog.Draw(target);

        // 第 0 行换成了「从背景挖下来的凹槽」，第 1 行才用按下的贴片。
        Assert.Equal(BackgroundColour, target.Row(Top.Y + 199 + 2)[Top.X + 590 + 2]);
        Assert.Equal(IconColourBase + 8, target.Row(Top.Y + 199 + 22 + 2)[Top.X + 590 + 2]);
    }

    [Fact]
    public void TheOkayButton_IsNotClickableWhenTheMapIsMissing()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);

        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario, isMapFilePresent: false);

        Assert.Equal(
            CampaignSelectionAction.None,
            dialog.ProcessLeftClick(new LocalEvent(), new CampaignSelectionHotKeys(Okay: true)));
    }

    [Fact]
    public void TheHotKeys_AskForTheSameActionsAsTheButtons()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        var events = new LocalEvent();

        Assert.Equal(CampaignSelectionAction.Cancel, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(Cancel: true)));
        Assert.Equal(CampaignSelectionAction.ViewIntro, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(ViewIntro: true)));
        Assert.Equal(CampaignSelectionAction.ChooseDifficulty, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(Difficulty: true)));
        Assert.Equal(CampaignSelectionAction.StartScenario, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(Okay: true)));

        Assert.Equal(CampaignSelectionAction.None, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(Bonus: 2)));
        Assert.Equal(2, dialog.SelectedBonusIndex);
    }

    [Fact]
    public void APickedBonus_SurvivesFramesWithoutHotKeys()
    {
        // CampaignSelectionHotKeys 是 record struct，default(T) 是全零、也就是 Bonus == 0。少一句
        // 「Bonus >= 0」的守卫，每一个没有热键的帧都会当成「第一项被按下」，玩家选好的第二项被立刻打回去。
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        var events = new LocalEvent();

        Assert.Equal(CampaignSelectionAction.None, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(Bonus: 1)));
        Assert.Equal(1, dialog.SelectedBonusIndex);

        Assert.Equal(CampaignSelectionAction.None, dialog.ProcessLeftClick(events, default));
        Assert.Equal(1, dialog.SelectedBonusIndex);

        Assert.Equal(CampaignSelectionAction.None, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys()));
        Assert.Equal(1, dialog.SelectedBonusIndex);
    }

    [Fact]
    public void ARestartingScenario_StartsInsteadOfOkayingWhenTheOkayButtonIsHidden()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignSaveData.Get().SetCurrentScenarioInfo(scenario.ScenarioInfo, 0);
        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario, allowToRestart: true);

        var events = new LocalEvent();

        Assert.Equal(CampaignSelectionAction.None, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(Okay: true)));
        Assert.Equal(CampaignSelectionAction.StartScenario, dialog.ProcessLeftClick(events, new CampaignSelectionHotKeys(Restart: true)));
    }

    [Fact]
    public void TheRightButton_ReportsTheBonusUnderTheCursor()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        CampaignSelectionHelpRequest request = dialog.ProcessRightClick(RightPress(new Point(Top.X + 420, Top.Y + 225)));

        Assert.Equal(CampaignSelectionHelpTarget.Bonus, request.Target);
        Assert.Equal(1, request.Index);
    }

    [Fact]
    public void TheRightButton_ReportsTheAwardUnderTheCursor()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.Reset();

        // 罗兰战役只有第二关起才有奖励可拿，第一关打完什么都不给。
        ScenarioData scenario = AwardingScenario(CampaignId.Roland);
        saveData.SetCurrentScenarioInfo(scenario.ScenarioInfo, 0);
        saveData.AddCurrentMapToFinished();
        saveData.AddCampaignAward(CampaignAwardData.GetCampaignAwardData(scenario.ScenarioInfo)[0].Id);

        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        Assert.Single(dialog.ObtainedAwards);

        CampaignSelectionHelpRequest request = dialog.ProcessRightClick(RightPress(new Point(Top.X + 420, Top.Y + 95)));

        Assert.Equal(CampaignSelectionHelpTarget.Awards, request.Target);
        Assert.Equal(0, request.Index);
    }

    [Fact]
    public void TheRightButton_ReportsTheDaysAndTheTitleColumns()
    {
        CampaignSaveData.Get().Reset();
        ScenarioData scenario = FirstScenario(CampaignId.Roland);
        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        Assert.Equal(
            CampaignSelectionHelpTarget.Days,
            dialog.ProcessRightClick(RightPress(new Point(Top.X + 420, Top.Y + 30))).Target);
        Assert.Equal(
            CampaignSelectionHelpTarget.ScenarioTitle,
            dialog.ProcessRightClick(RightPress(new Point(Top.X + 250, Top.Y + 90))).Target);
    }

    [Fact]
    public void TheObtainedAwards_AreNotShownForABetrayalScenario()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.Reset();

        IReadOnlyList<ScenarioData> scenarios = CampaignData.GetCampaignData(CampaignId.Roland).AllScenarios;
        ScenarioData awardingScenario = AwardingScenario(CampaignId.Roland);

        saveData.SetCurrentScenarioInfo(awardingScenario.ScenarioInfo, 0);
        saveData.AddCurrentMapToFinished();
        saveData.AddCampaignAward(CampaignAwardData.GetCampaignAwardData(awardingScenario.ScenarioInfo)[0].Id);

        // 先确认这一幕本来是有奖励可显示的，否则下面的 Empty 说明不了任何事。
        Assert.NotEmpty(CreateDialog(awardingScenario).ObtainedAwards);

        ScenarioInfoId betrayal = FindBetrayalScenario(scenarios);

        saveData.SetCurrentScenarioInfo(betrayal, 0);

        Assert.Empty(CreateDialog(awardingScenario).ObtainedAwards);
    }

    [Fact]
    public void TheTitleHelpArea_IsTallerForTheTwoOldCampaigns()
    {
        CampaignSaveData.Get().Reset();

        Rect oldCampaign = CreateDialog(FirstScenario(CampaignId.Roland)).ScenarioTitleArea;
        Rect newCampaign = CreateDialog(FirstScenario(CampaignId.PriceOfLoyalty)).ScenarioTitleArea;

        Assert.Equal(new Rect(Top.X + 198, Top.Y + 84, 200, 25), oldCampaign);
        Assert.Equal(new Rect(Top.X + 198, Top.Y + 84, 200, 22), newCampaign);
    }

    [Fact]
    public void ClickingAnotherAvailableScenario_RecordsIt()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.Reset();

        ScenarioData scenario = FirstScenario(CampaignId.Roland);

        // 打完第一关后只有第二关可选，而这一屏还停在第一关：点第二关的图标应该记住它。
        saveData.SetCurrentScenarioInfo(scenario.ScenarioInfo, 0);
        saveData.AddCurrentMapToFinished();

        CampaignScenarioSelectionDialog dialog = CreateDialog(scenario);

        // 罗兰战役的第二关在格坐标 (2, 1)，一个格子是 37 × 42 像素。
        var events = new LocalEvent();
        Point icon = new(Top.X + (2 * 37) + 5, Top.Y + 42 + 5);

        events.OnMouseMotion(icon);
        events.OnMouseButton(true, MouseButtonType.Left, icon);
        events.OnMouseButton(false, MouseButtonType.Left, icon);

        Assert.Equal(CampaignSelectionAction.SelectOtherScenario, dialog.ProcessLeftClick(events));
        Assert.Equal(new ScenarioInfoId(CampaignId.Roland, 1), saveData.CurrentScenarioInfoId);
    }

    private static ScenarioData FirstScenario(int campaignId) => CampaignData.GetCampaignData(campaignId).AllScenarios[0];

    /// <summary>The first scenario of the campaign that hands out an award.</summary>
    private static ScenarioData AwardingScenario(int campaignId)
    {
        foreach (ScenarioData scenario in CampaignData.GetCampaignData(campaignId).AllScenarios)
        {
            if (CampaignAwardData.GetCampaignAwardData(scenario.ScenarioInfo).Count > 0)
            {
                return scenario;
            }
        }

        throw new InvalidOperationException($"The campaign {campaignId} hands out no award at all.");
    }

    private static ScenarioInfoId FindBetrayalScenario(IReadOnlyList<ScenarioData> scenarios)
    {
        for (int i = 0; i < scenarios.Count; ++i)
        {
            ScenarioInfoId info = scenarios[i].ScenarioInfo;

            if (CampaignData.IsBetrayalScenario(info))
            {
                return info;
            }
        }

        throw new InvalidOperationException("The Roland campaign has no betrayal scenario.");
    }

    private static CampaignScenarioSelectionDialog CreateDialog(
        ScenarioData scenario,
        bool isMapFilePresent = true,
        bool allowToRestart = false)
    {
        return new CampaignScenarioSelectionDialog(
            new FakeArtwork(),
            CampaignSaveData.Get(),
            CampaignData.GetCampaignData(scenario.ScenarioInfo.CampaignId),
            scenario,
            new BlockFont(lineHeight: 10, advance: 6, colour: 200),
            new Size(ScreenWidth, ScreenHeight),
            isMapFilePresent,
            allowToRestart);
    }

    private static LocalEvent RightPress(Point position)
    {
        var events = new LocalEvent();

        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Right, position);

        return events;
    }

    /// <summary>Solid colour pictures: the size and the colour of every frame tell the test what was drawn.</summary>
    private sealed class FakeArtwork : ICampaignScenarioArtwork
    {
        public Sprite GetIcon(IcnType icnId, int frame)
        {
            (int width, int height, byte colour) = icnId switch
            {
                IcnType.Campbkgg or IcnType.Campbkge or IcnType.XCmpbkg => (ScreenWidth, ScreenHeight, BackgroundColour),
                IcnType.GoodCampaignButtons or IcnType.EvilCampaignButtons or IcnType.PolCampaignButtons =>
                    (ButtonWidth, ButtonHeight, (byte)(ButtonColourBase + frame)),
                IcnType.XCmpext when frame >= 15 => (200, 20, HeaderColour),
                _ when IsTrackIcn(icnId) => (ScreenWidth, ScreenHeight, TrackColour),
                _ => (22, 22, (byte)(IconColourBase + frame)),
            };

            var image = new PixelBuffer(width, height);
            image.Fill(colour);

            return new Sprite(image, 0, 0);
        }

        private static bool IsTrackIcn(IcnType icnId) =>
            icnId is IcnType.Ctrack00 or IcnType.Ctrack01 or IcnType.Ctrack02 or IcnType.Ctrack03 or IcnType.Ctrack04
                or IcnType.Ctrack05 or IcnType.Ctrack06 or IcnType.XTrack1 or IcnType.XTrack2 or IcnType.XTrack3
                or IcnType.XTrack4;
    }

    /// <summary>A glyph source whose glyphs are solid blocks.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte colour)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;

            var image = new PixelBuffer(advance, lineHeight);
            image.Fill(colour);
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
}
