using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

[Collection(GlobalArchiveCollection.Name)]
public sealed class NewGameMenuViewTests
{
    private const byte Canvas = 3;
    private const byte Button = 50;

    private static readonly Size ButtonSize = new(100, 30);

    // A mode button column of five 100x30 buttons 10 pixels apart, room for the cancel button under
    // them, and a window of 152x273 centred on the 640x480 screen, gives an active area at (244,103)
    // and a column at (269,125).
    private static readonly Point Column = new(269, 125);

    [Fact]
    public void TryCreate_ReturnsNullWithoutTheFrames()
    {
        Assert.Null(NewGameMenuView.TryCreate(null, true, true));
        Assert.Null(NewGameMenuView.TryCreate(CreateFrames(NewGameMenuView.FrameCount - 1), true, true));
    }

    [Fact]
    public void TryCreate_LaysTheModeButtonsInOneColumn()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(NewGameMenuView.ModeCount, view.ModeButtons.Count);

        for (int index = 0; index < NewGameMenuView.ModeCount; ++index)
        {
            Assert.Equal(new Point(Column.X, Column.Y + (index * (ButtonSize.Height + 10))),
                TopLeftOf(view.ModeButtons[index]));
        }
    }

    [Fact]
    public void TryCreate_PutsTheCancelButtonUnderTheColumn()
    {
        NewGameMenuView view = CreateView();

        // 103 * 2 + 273 - 125 - 30, the mirror of the distance the first button keeps from the top.
        Assert.Equal(new Point(Column.X, 324), TopLeftOf(view.CancelButton));
    }

    [Fact]
    public void TryCreate_StacksTheHotSeatAndCampaignButtonsOnTheFirstTwoModes()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(TopLeftOf(view.ModeButtons[0]), TopLeftOf(view.HotSeatButton));
        Assert.NotNull(view.CampaignButtons);
        Assert.Equal(TopLeftOf(view.ModeButtons[0]), TopLeftOf(view.CampaignButtons![0]));
        Assert.Equal(TopLeftOf(view.ModeButtons[1]), TopLeftOf(view.CampaignButtons[1]));

        for (int index = 0; index < NewGameMenuView.PlayerCountButtonsCount; ++index)
        {
            Assert.Equal(new Point(Column.X, Column.Y + (index * (ButtonSize.Height + 10))),
                TopLeftOf(view.PlayerCountButtons[index]));
        }
    }

    [Fact]
    public void TryCreate_LeavesTheCampaignChoiceOutWithoutPriceOfLoyalty()
    {
        NewGameMenuView view = NewGameMenuView.TryCreate(CreateFrames(), true, false)!;

        Assert.Null(view.CampaignButtons);
        Assert.True(view.ModeButtons[1].IsEnabled);
    }

    [Fact]
    public void TryCreate_DisablesTheCampaignModeWithoutSuccessionWars()
    {
        NewGameMenuView view = NewGameMenuView.TryCreate(CreateFrames(), false, false)!;

        Assert.False(view.ModeButtons[1].IsEnabled);
        Assert.True(view.ModeButtons[0].IsEnabled);
    }

    [Fact]
    public void TryCreate_StartsOnTheModePage()
    {
        NewGameMenuView view = CreateView();

        Assert.False(view.IsChoosingCampaign);
        Assert.False(view.IsChoosingHotSeat);
        Assert.False(view.IsChoosingPlayerCount);
        Assert.False(view.HotSeatButton.IsEnabled);
        Assert.False(view.CampaignButtons![0].IsEnabled);
    }

    [Fact]
    public void SetLeftButton_AnswersTheStandardGame()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(NewGameMenuAction.None, view.SetLeftButton(true, CenterOf(view.ModeButtons[0])).Action);
        Assert.Equal(NewGameMenuAction.StandardGame,
            view.SetLeftButton(false, CenterOf(view.ModeButtons[0])).Action);
    }

    [Fact]
    public void SetLeftButton_AnswersTheBattleOnlyAndTheSettings()
    {
        Assert.Equal(NewGameMenuAction.BattleOnly, Click(CreateView(), 3).Action);
        Assert.Equal(NewGameMenuAction.GameSettings, Click(CreateView(), 4).Action);
    }

    [Fact]
    public void SetLeftButton_IgnoresAReleaseOutsideTheButton()
    {
        NewGameMenuView view = CreateView();

        view.SetLeftButton(true, CenterOf(view.ModeButtons[0]));

        Assert.Equal(NewGameMenuAction.None,
            view.SetLeftButton(false, CenterOf(view.ModeButtons[1])).Action);
    }

    [Fact]
    public void SetLeftButton_ShowsTheCampaignChoiceWhenPriceOfLoyaltyIsPresent()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(NewGameMenuAction.None, Click(view, 1).Action);
        Assert.True(view.IsChoosingCampaign);
        Assert.False(view.ModeButtons[0].IsEnabled);
        Assert.True(view.CampaignButtons![0].IsEnabled);
        Assert.True(view.CampaignButtons[1].IsEnabled);
    }

    [Fact]
    public void SetLeftButton_AnswersTheSuccessionWarsCampaignWithoutPriceOfLoyalty()
    {
        NewGameMenuView view = NewGameMenuView.TryCreate(CreateFrames(), true, false)!;

        // Without the expansion the campaign button skips the choice and goes straight to the original.
        Assert.Equal(NewGameMenuAction.SuccessionWarsCampaign, Click(view, 1).Action);
        Assert.False(view.IsChoosingCampaign);
    }

    [Fact]
    public void SetLeftButton_AnswersTheChoiceOfCampaign()
    {
        NewGameMenuView original = CreateView();
        Click(original, 1);

        Assert.Equal(NewGameMenuAction.SuccessionWarsCampaign, Click(original, 0).Action);

        NewGameMenuView expansion = CreateView();
        Click(expansion, 1);

        Assert.Equal(NewGameMenuAction.PriceOfLoyaltyCampaign, Click(expansion, 1).Action);
    }

    [Fact]
    public void SetLeftButton_WalksFromTheMultiplayerGameToThePlayerCount()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(NewGameMenuAction.None, Click(view, 2).Action);
        Assert.True(view.IsChoosingHotSeat);
        Assert.True(view.HotSeatButton.IsEnabled);

        Assert.Equal(NewGameMenuAction.None, Click(view, 0).Action);
        Assert.True(view.IsChoosingPlayerCount);
        Assert.False(view.HotSeatButton.IsEnabled);
        Assert.True(view.PlayerCountButtons[0].IsEnabled);

        Assert.Equal(new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 4), Click(view, 2));
    }

    [Fact]
    public void SetLeftButton_AnswersCancelOnEveryPage()
    {
        NewGameMenuView modes = CreateView();

        Assert.Equal(NewGameMenuAction.Cancel, ClickCancel(modes).Action);

        NewGameMenuView campaigns = CreateView();
        Click(campaigns, 1);

        Assert.Equal(NewGameMenuAction.Cancel, ClickCancel(campaigns).Action);
    }

    [Fact]
    public void SetLeftButton_IgnoresTheCampaignModeWhileItIsDisabled()
    {
        NewGameMenuView view = NewGameMenuView.TryCreate(CreateFrames(), false, true)!;

        Assert.Equal(NewGameMenuAction.None, Click(view, 1).Action);
        Assert.False(view.IsChoosingCampaign);
    }

    [Fact]
    public void SetHotKey_AnswersTheModeKeysOnTheModePageOnly()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(NewGameMenuAction.StandardGame,
            view.SetHotKey(NewGameMenuHotKey.Standard).Action);
        Assert.Equal(NewGameMenuAction.BattleOnly,
            view.SetHotKey(NewGameMenuHotKey.BattleOnly).Action);
        Assert.Equal(NewGameMenuAction.GameSettings,
            view.SetHotKey(NewGameMenuHotKey.Settings).Action);
        Assert.Equal(NewGameMenuAction.Cancel, view.SetHotKey(NewGameMenuHotKey.Cancel).Action);

        // The hot seat and the player count keys need their own page.
        Assert.Equal(NewGameMenuAction.None, view.SetHotKey(NewGameMenuHotKey.HotSeat).Action);
        Assert.Equal(NewGameMenuAction.None, view.SetHotKey(NewGameMenuHotKey.TwoPlayers).Action);
    }

    [Fact]
    public void SetHotKey_SwitchesToTheCampaignChoice()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(NewGameMenuAction.None, view.SetHotKey(NewGameMenuHotKey.Campaign).Action);
        Assert.True(view.IsChoosingCampaign);

        Assert.Equal(NewGameMenuAction.PriceOfLoyaltyCampaign,
            view.SetHotKey(NewGameMenuHotKey.ExpansionCampaign).Action);
        Assert.Equal(NewGameMenuAction.SuccessionWarsCampaign,
            view.SetHotKey(NewGameMenuHotKey.OriginalCampaign).Action);
    }

    [Fact]
    public void SetHotKey_AnswersThePlayerCountKeys()
    {
        NewGameMenuView view = CreateView();

        view.SetHotKey(NewGameMenuHotKey.Multiplayer);
        view.SetHotKey(NewGameMenuHotKey.HotSeat);

        Assert.True(view.IsChoosingPlayerCount);
        Assert.Equal(new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 2),
            view.SetHotKey(NewGameMenuHotKey.TwoPlayers));
        Assert.Equal(new NewGameMenuChoice(NewGameMenuAction.PlayerCount, 6),
            view.SetHotKey(NewGameMenuHotKey.SixPlayers));
    }

    [Fact]
    public void ProcessRightClick_NamesTheModeBlocks()
    {
        NewGameMenuView view = CreateView();

        Assert.Equal(NewGameMenuHelpTarget.StandardGame,
            view.ProcessRightClick(CenterOf(view.ModeButtons[0])).Target);
        Assert.Equal(NewGameMenuHelpTarget.CampaignGame,
            view.ProcessRightClick(CenterOf(view.ModeButtons[1])).Target);
        Assert.Equal(NewGameMenuHelpTarget.MultiplayerGame,
            view.ProcessRightClick(CenterOf(view.ModeButtons[2])).Target);
        Assert.Equal(NewGameMenuHelpTarget.BattleOnly,
            view.ProcessRightClick(CenterOf(view.ModeButtons[3])).Target);
        Assert.Equal(NewGameMenuHelpTarget.GameSettings,
            view.ProcessRightClick(CenterOf(view.ModeButtons[4])).Target);
        Assert.Equal(NewGameMenuHelpTarget.Cancel,
            view.ProcessRightClick(CenterOf(view.CancelButton)).Target);
        Assert.Equal(NewGameMenuHelpTarget.None, view.ProcessRightClick(new Point(0, 0)).Target);
    }

    [Fact]
    public void ProcessRightClick_LeavesTheDisabledCampaignModeSilent()
    {
        NewGameMenuView view = NewGameMenuView.TryCreate(CreateFrames(), false, false)!;

        Assert.Equal(NewGameMenuHelpTarget.MultiplayerGame,
            view.ProcessRightClick(CenterOf(view.ModeButtons[2])).Target);
        Assert.NotEqual(NewGameMenuHelpTarget.CampaignGame,
            view.ProcessRightClick(CenterOf(view.ModeButtons[1])).Target);
    }

    [Fact]
    public void ProcessRightClick_NamesTheCampaignAndPlayerCountBlocks()
    {
        NewGameMenuView view = CreateView();
        Click(view, 1);

        Assert.Equal(NewGameMenuHelpTarget.SuccessionWarsCampaign,
            view.ProcessRightClick(CenterOf(view.CampaignButtons![0])).Target);
        Assert.Equal(NewGameMenuHelpTarget.PriceOfLoyaltyCampaign,
            view.ProcessRightClick(CenterOf(view.CampaignButtons[1])).Target);

        NewGameMenuView count = CreateView();
        Click(count, 2);
        Click(count, 0);

        NewGameMenuHelpRequest request = count.ProcessRightClick(CenterOf(count.PlayerCountButtons[3]));

        Assert.Equal(NewGameMenuHelpTarget.PlayerCount, request.Target);
        Assert.Equal(5, request.PlayerCount);
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        Assert.Throws<ArgumentNullException>(() => CreateView().Draw(null!));
    }

    [Fact]
    public void Draw_PaintsTheButtonsOfTheShownPageAndTheCancelButton()
    {
        NewGameMenuView view = CreateView();
        PixelBuffer canvas = new(640, 480);
        canvas.Fill(Canvas);

        // The stone frame of the window comes from the archive, so the game data has to be open.
        using AssetBundle? assets = OpenArchives();

        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            view.Draw(canvas);
        }
        finally
        {
            GameAssets.Current = null;
        }

        Assert.Equal(Button, canvas[Column.X + 50, Column.Y + 15]);
        Assert.Equal(Button, canvas[Column.X + 50, 324 + 15]);
        Assert.Equal(Canvas, canvas[0, 0]);
    }

    private static NewGameMenuView CreateView() => NewGameMenuView.TryCreate(CreateFrames(), true, true)!;

    /// <summary>Answers one click on a mode, hot seat, campaign or player count button of the shown page.</summary>
    private static NewGameMenuChoice Click(NewGameMenuView view, int index)
    {
        Widgets.ButtonBase button = view.VisibleButtons[index];

        return ClickAt(view, TopLeftOf(button), button.Area.Size);
    }

    private static NewGameMenuChoice ClickCancel(NewGameMenuView view) =>
        ClickAt(view, TopLeftOf(view.CancelButton), view.CancelButton.Area.Size);

    private static NewGameMenuChoice ClickAt(NewGameMenuView view, Point topLeft, Size size)
    {
        Point point = new(topLeft.X + (size.Width / 2), topLeft.Y + (size.Height / 2));

        view.SetLeftButton(true, point);

        return view.SetLeftButton(false, point);
    }

    private static Point CenterOf(Widgets.ButtonBase button) =>
        new(button.Area.X + (button.Area.Width / 2), button.Area.Y + (button.Area.Height / 2));

    private static Sprite[] CreateFrames(int count = NewGameMenuView.FrameCount)
    {
        Sprite[] sprites = new Sprite[count];

        for (int index = 0; index < count; ++index)
        {
            PixelBuffer image = new(ButtonSize.Width, ButtonSize.Height);
            image.Fill(Button);
            sprites[index] = new Sprite(image, 0, 0);
        }

        return sprites;
    }

    /// <summary>Opens the game data the stone frame of the window is read from.</summary>
    /// <returns>The archives, or <see langword="null"/> when the machine has no game data.</returns>
    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static Point TopLeftOf(Widgets.ButtonBase button) => new(button.Area.X, button.Area.Y);
}
