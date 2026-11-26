using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Campaign;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the player asked for on the campaign selection screen, the return values of
/// <c>SelectCampaignScenario()</c> (<c>game_campaign.cpp:1284-1710</c>).
/// </summary>
public enum CampaignSelectionAction
{
    /// <summary>Nothing happened this frame.</summary>
    None,

    /// <summary>Start (or restart) the selected scenario; the caller loads the map.</summary>
    StartScenario,

    /// <summary>Show the campaign's intro movie, <c>playPreviousScenarioVideo()</c> and <c>playCurrentScenarioVideo()</c>.</summary>
    ViewIntro,

    /// <summary>Pick the difficulty, <c>setCampaignDifficulty()</c>.</summary>
    ChooseDifficulty,

    /// <summary>Leave to wherever the screen was entered from.</summary>
    Cancel,

    /// <summary>Another scenario was clicked; the caller reopens the screen for it.</summary>
    SelectOtherScenario,
}

/// <summary>
/// Which block of the screen the right mouse button points at, the <c>le.MouseClickRight()</c> chain of
/// <c>SelectCampaignScenario()</c> (<c>game_campaign.cpp:1558-1587, 1690-1697</c>).
/// </summary>
/// <remarks>
/// 参考在这里直接弹消息框；端口把「哪一块被点了」交回宿主，因为弹窗需要字体与美术资源，那是宿主的装配活。
/// </remarks>
public enum CampaignSelectionHelpTarget
{
    /// <summary>Nothing was hit.</summary>
    None,

    /// <summary>A bonus option; the index is on the request.</summary>
    Bonus,

    /// <summary>One obtained award; the index is on the request.</summary>
    Awards,

    /// <summary>The days spent text.</summary>
    Days,

    /// <summary>The scenario title.</summary>
    ScenarioTitle,

    /// <summary>The Cancel button.</summary>
    CancelButton,

    /// <summary>The Okay button.</summary>
    OkayButton,

    /// <summary>The Restart button.</summary>
    RestartButton,

    /// <summary>The View Intro button.</summary>
    ViewIntroButton,

    /// <summary>The Difficulty button.</summary>
    DifficultyButton,
}

/// <summary>
/// What the right mouse button points at, see <see cref="CampaignSelectionHelpTarget"/>.
/// </summary>
/// <param name="Target">The block.</param>
/// <param name="Index">
/// Which row of <see cref="CampaignSelectionHelpTarget.Bonus"/> or <see cref="CampaignSelectionHelpTarget.Awards"/> was
/// hit, or -1 for a block that has no rows.
/// </param>
public readonly record struct CampaignSelectionHelpRequest(CampaignSelectionHelpTarget Target, int Index = -1);

/// <summary>
/// The hot keys the selection screen listens to besides the pointer, the <c>Game::HotKeyPressEvent()</c>
/// lookups of the reference loop.
/// </summary>
/// <remarks>
/// 端口还没有热键表，宿主导航层把这一帧的按键解码成这几个答案再交进来，与 <c>WellHotKeys</c> 同一套做法。
/// </remarks>
/// <param name="Cancel"><c>DEFAULT_CANCEL</c>: Escape.</param>
/// <param name="Okay"><c>DEFAULT_OKAY</c>: Enter or Space.</param>
/// <param name="Restart"><c>CAMPAIGN_RESTART_SCENARIO</c>.</param>
/// <param name="ViewIntro"><c>CAMPAIGN_VIEW_INTRO</c>.</param>
/// <param name="Difficulty"><c>CAMPAIGN_SELECT_DIFFICULTY</c>.</param>
/// <param name="Bonus">
/// The pressed <c>CAMPAIGN_SELECT_*_BONUS</c> as an index, or <see langword="null"/> when none was pressed.
/// </param>
/// <remarks>
/// <para>
/// <paramref name="Bonus"/> 必须是可空整数：这是 record struct，<c>default(T)</c> 给的是「所有字段全零」，
/// 而不是构造参数的默认值——用 <c>int Bonus = -1</c> 的话零值就成了 0，也就是每一帧都假装第一项奖励被按下，
/// 玩家选好的第二、三项会被立刻打回去。可空类型的零值正好是 <see langword="null"/>，「没按」与「按第一项」才分得开。
/// </para>
/// </remarks>
public readonly record struct CampaignSelectionHotKeys(
    bool Cancel = false,
    bool Okay = false,
    bool Restart = false,
    bool ViewIntro = false,
    bool Difficulty = false,
    int? Bonus = null);

/// <summary>
/// The full screen campaign selection screen, <c>SelectCampaignScenario()</c>
/// (<c>game_campaign.cpp:1284-1710</c>).
/// </summary>
/// <remarks>
/// <para>
/// 一屏里有五样东西：背景图（与资料片的标题头）、五个按钮（看片 / 重开 / 确定 / 取消 / 难度）、右边的奖励单选、
/// 左边的关卡轨道、一堆文字（天数、关卡名、序号、描述、奖励选项、已得奖励）。参考把这些全写在一个函数里，直接往
/// <c>Display</c> 上画并跑自己的事件循环；端口按本层惯例拆成「画一遍」「更新按钮态」「处理左键」「处理右键」，
/// 循环、过场淡入淡出、影片与音乐留在宿主。
/// </para>
/// <para>
/// 奖励单选在参考里是 <c>OptionButtonGroup</c>：一组互斥按钮，按下的那个一直保持按下态。端口没有这个类型，这里用
/// 一个小数组维护「当前选中项」——按下图是 <c>CAMPXTRG</c> 第 8 帧，未按下图是从背景图上按同样的 anchor 挖下来的
/// 贴片，于是未选中的行看起来是「凹进去的空槽」。
/// </para>
/// </remarks>
public sealed class CampaignScenarioSelectionDialog
{
    private const int BackgroundMargin = 30;
    private const int OptionButtonOffsetX = 590;
    private const int OptionButtonOffsetY = 199;
    private const int OptionButtonStep = 22;
    private const int ButtonOffsetY = 431;
    private const int AwardTextWidth = 180;
    private const int AwardInitialOffsetY = 102;
    private const int BonusTextWidth = 160;

    /// <summary>Direction of the button drop shadows, <c>{ -5, 5 }</c> of the reference.</summary>
    private static readonly Point ButtonShadowOffset = new(-5, 5);

    private readonly CampaignSaveData _saveData;
    private readonly CampaignData _campaignData;
    private readonly ScenarioData _scenario;
    private readonly IReadOnlyList<ScenarioBonusData> _bonuses;
    private readonly IGlyphSource _font;
    private readonly Sprite _background;
    private readonly Sprite? _header;
    private readonly Point _top;
    private readonly CampaignScenarioTrack _track;
    private readonly bool _allowToRestart;

    private readonly ButtonSprite _viewIntroButton;
    private readonly ButtonSprite _restartButton;
    private readonly ButtonSprite _okButton;
    private readonly ButtonSprite _cancelButton;
    private readonly ButtonSprite _difficultyButton;

    private readonly Sprite _bonusPressedIcon;
    private readonly List<(Sprite Icon, Rect Area)> _bonusOptions = [];

    /// <summary>Frame 0 of the button set, whose silhouette every button of the row casts.</summary>
    private readonly Sprite _buttonShadowSprite;

    /// <summary>The four places the button row is placed at, see <see cref="Draw"/>.</summary>
    private readonly Point[] _buttonShadows;

    private int _selectedBonusIndex;

    /// <summary>
    /// Builds the selection screen of one campaign.
    /// </summary>
    /// <param name="artwork">Where the pictures come from.</param>
    /// <param name="saveData">The campaign save.</param>
    /// <param name="campaignData">The campaign being played.</param>
    /// <param name="scenario">The scenario the screen shows.</param>
    /// <param name="font">The normal font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="screenSize">The size of the screen the window is centred on, <c>Display::instance()</c>.</param>
    /// <param name="isMapFilePresent">Whether the scenario's map file exists, <c>ScenarioData::isMapFilePresent()</c>.</param>
    /// <param name="allowToRestart">True when the running scenario may be restarted, <c>allowToRestart</c>.</param>
    public CampaignScenarioSelectionDialog(
        ICampaignScenarioArtwork artwork,
        CampaignSaveData saveData,
        CampaignData campaignData,
        ScenarioData scenario,
        IGlyphSource font,
        Size screenSize,
        bool isMapFilePresent,
        bool allowToRestart)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(campaignData);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(font);

        _saveData = saveData;
        _campaignData = campaignData;
        _scenario = scenario;
        _font = font;
        _allowToRestart = allowToRestart;
        _bonuses = scenario.Bonuses;

        _background = LoadBackground(artwork, campaignData.Id);
        _top = new Point((screenSize.Width - _background.Width) / 2, (screenSize.Height - _background.Height) / 2);
        _header = TryLoadHeader(artwork, campaignData.Id);

        // 重开时必须已经选过一个奖励；新开局默认选第一个。
        _selectedBonusIndex = allowToRestart
            ? (saveData.CurrentScenarioBonusId >= 0 && saveData.CurrentScenarioBonusId < _bonuses.Count
                ? saveData.CurrentScenarioBonusId
                : -1)
            : (_bonuses.Count > 0 ? 0 : -1);

        _track = CampaignScenarioTrack.Create(
            artwork,
            saveData,
            campaignData,
            _top,
            chosenScenarioId: allowToRestart ? scenario.ScenarioId : -1);

        // 参考在画完轨道后把「当前这一关」的按钮按下去：重开时它就在可选列表里，新开局时一般不在，等于没按。
        _track.Press(_scenario.ScenarioInfo);

        // 五个按钮的 x 由三个等宽间隙推出来，间隙就是「VIEW INTRO 右边缘」到「CANCEL 左边缘」的剩余空间除以三。
        IcnType buttonSet = GetButtonSet(campaignData.Id);
        Sprite viewIntroFrame = artwork.GetIcon(buttonSet, 0);
        Sprite cancelFrame = artwork.GetIcon(buttonSet, 6);
        Sprite difficultyFrame = artwork.GetIcon(buttonSet, 8);
        Sprite okayOrRestartFrame = artwork.GetIcon(buttonSet, allowToRestart ? 2 : 4);

        int viewIntroPlacement = _top.X + BackgroundMargin;
        int endOfViewIntro = viewIntroPlacement + viewIntroFrame.Width;
        int cancelPlacement = _top.X + _background.Width - BackgroundMargin - cancelFrame.Width;
        int space = cancelPlacement - endOfViewIntro;
        int middleMargin = (space - difficultyFrame.Width - okayOrRestartFrame.Width) / 3;
        int difficultyPlacement = endOfViewIntro + middleMargin;
        int okOrRestartPlacement = difficultyPlacement + difficultyFrame.Width + middleMargin;
        int buttonY = _top.Y + ButtonOffsetY;

        _viewIntroButton = new ButtonSprite(viewIntroFrame, artwork.GetIcon(buttonSet, 1), null, viewIntroPlacement, buttonY);
        _cancelButton = new ButtonSprite(cancelFrame, artwork.GetIcon(buttonSet, 7), null, cancelPlacement, buttonY);
        _difficultyButton = new ButtonSprite(difficultyFrame, artwork.GetIcon(buttonSet, 9), null, difficultyPlacement, buttonY);
        _restartButton = new ButtonSprite(okayOrRestartFrame, artwork.GetIcon(buttonSet, 3), null, okOrRestartPlacement, buttonY);
        _okButton = new ButtonSprite(artwork.GetIcon(buttonSet, 4), artwork.GetIcon(buttonSet, 5), null, okOrRestartPlacement, buttonY);

        // 两个按钮占同一个位置，永远只出现一个：重开时只能重开，否则只有「确定」。地图文件不在时变灰但仍可见。
        SetAlternateButton(_restartButton, used: allowToRestart, isMapFilePresent);
        SetAlternateButton(_okButton, used: !allowToRestart, isMapFilePresent);

        _bonusPressedIcon = artwork.GetIcon(IcnType.Campxtrg, 8);

        BuildBonusOptions(_background, _bonusPressedIcon);

        // 参考给这一排四个按钮打同一张影子（帧 0 的轮廓），摆放点各用各的，见 game_campaign.cpp:1365-1368。
        _buttonShadowSprite = viewIntroFrame;
        _buttonShadows =
        [
            new Point(viewIntroPlacement, buttonY),
            new Point(okOrRestartPlacement, buttonY),
            new Point(cancelPlacement, buttonY),
            new Point(difficultyPlacement, buttonY),
        ];
    }

    /// <summary>Where the window is anchored, <c>top</c>.</summary>
    public Point Top => _top;

    /// <summary>The scenario the screen shows.</summary>
    public ScenarioInfoId ScenarioInfoId => _scenario.ScenarioInfo;

    /// <summary>The bonus the player picked, or -1 when none is picked.</summary>
    public int SelectedBonusIndex => _selectedBonusIndex;

    /// <summary>The bonus options on screen, the ones <see cref="SelectedBonusIndex"/> indexes into.</summary>
    public IReadOnlyList<ScenarioBonusData> Bonuses => _bonuses;

    /// <summary>
    /// The awards the campaign has handed out so far, the ones <see cref="CampaignSelectionHelpTarget.Awards"/>
    /// indexes into; empty for the betrayal scenarios, which show none.
    /// </summary>
    /// <remarks>
    /// A campaign that has not chosen its first scenario yet cannot own anything, and the save data asserts on an
    /// unknown campaign, so that state is answered here rather than asked down.
    /// </remarks>
    public IReadOnlyList<CampaignAwardData> ObtainedAwards =>
        IsCurrentScenarioBetrayal || _saveData.CurrentScenarioInfoId == ScenarioInfoId.NotStarted
            ? Array.Empty<CampaignAwardData>()
            : _saveData.GetObtainedCampaignAwards();

    /// <summary>The press areas of the bonus options, in bonus order.</summary>
    public IReadOnlyList<Rect> BonusAreas => [.. _bonusOptions.Select(option => option.Area)];

    /// <summary>The click area of the scenario title, whose size differs between the old and new campaigns.</summary>
    public Rect ScenarioTitleArea
    {
        get
        {
            bool oldCampaign = _campaignData.Id is CampaignId.Roland or CampaignId.Archibald;

            return new Rect(_top.X + 198, _top.Y + 84, 200, oldCampaign ? 25 : 22);
        }
    }

    /// <summary>The area of the days spent text, <c>areaDaysSpent</c>.</summary>
    public Rect DaysArea => new(_top.X + 413, _top.Y + 27, 201, 25);

    /// <summary>Draws the whole screen.</summary>
    /// <param name="target">The screen.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, _top, _background);

        if (_header is not null)
        {
            ImageOps.BlitSprite(target, new Point(_top.X + 24, _top.Y + 25), _header);
        }

        _track.Draw(target);

        DrawTexts(target);
        DrawBonusOptions(target);

        // 参考在画按钮前先给按钮下方的背景打一层投影，按钮看起来是浮起来的。
        foreach (Point shadow in _buttonShadows)
        {
            ImageOps.AddGradientShadow(_buttonShadowSprite, target, shadow, ButtonShadowOffset);
        }

        _viewIntroButton.Draw(target);
        _difficultyButton.Draw(target);
        _restartButton.Draw(target);
        _okButton.Draw(target);
        _cancelButton.Draw(target);

        _track.DrawButtons(target);
    }

    /// <summary>Refreshes the pressed state of the buttons from the mouse, <c>button.drawOnState( le )</c>.</summary>
    /// <param name="events">The current frame's input.</param>
    public void PressButtons(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _viewIntroButton.UpdatePressState(events);
        _difficultyButton.UpdatePressState(events);
        _cancelButton.UpdatePressState(events);

        if (_restartButton.IsVisible && _restartButton.IsEnabled)
        {
            _restartButton.UpdatePressState(events);
        }

        if (_okButton.IsVisible && _okButton.IsEnabled)
        {
            _okButton.UpdatePressState(events);
        }
    }

    /// <summary>
    /// Handles a left press, the <c>le.MouseClickLeft()</c> chain of <c>SelectCampaignScenario()</c>
    /// (<c>game_campaign.cpp:1524-1545, 1588-1673</c>).
    /// </summary>
    /// <param name="events">The current frame's input.</param>
    /// <param name="hotKeys">The hot keys pressed this frame.</param>
    /// <returns>What the player asked for.</returns>
    public CampaignSelectionAction ProcessLeftClick(LocalEvent events, CampaignSelectionHotKeys hotKeys = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        // 奖励单选在「按下」时就生效，不等抬起——参考用的就是 isMouseLeftButtonPressedInArea。
        //
        // hotKeys.Bonus 与 i 比较时 null 自然不成立（见 CampaignSelectionHotKeys 的说明），
        // 所以「没按奖励键」的帧不会碰选中态。
        for (int i = 0; i < _bonusOptions.Count; ++i)
        {
            if ((events.IsMouseLeftButtonPressed && BonusClickArea(i).Contains(events.CursorPosition))
                || hotKeys.Bonus == i)
            {
                SelectBonus(i);
                break;
            }
        }

        if (events.MouseClickLeft(_cancelButton.ReleasedArea) || hotKeys.Cancel)
        {
            return CampaignSelectionAction.Cancel;
        }

        if (events.MouseClickLeft(_viewIntroButton.ReleasedArea) || hotKeys.ViewIntro)
        {
            return CampaignSelectionAction.ViewIntro;
        }

        if (events.MouseClickLeft(_difficultyButton.ReleasedArea) || hotKeys.Difficulty)
        {
            return CampaignSelectionAction.ChooseDifficulty;
        }

        bool restartClicked = _restartButton.IsVisible && _restartButton.IsEnabled
            && (events.MouseClickLeft(_restartButton.ReleasedArea) || hotKeys.Restart);
        bool okayClicked = _okButton.IsVisible && _okButton.IsEnabled
            && (events.MouseClickLeft(_okButton.ReleasedArea) || hotKeys.Okay);

        if (restartClicked || okayClicked)
        {
            return CampaignSelectionAction.StartScenario;
        }

        // 点另一个可进入的关卡：记下它，让宿主重开这一屏。
        if (_track.HitTest(events.CursorPosition) is { } otherScenario && otherScenario != _scenario.ScenarioInfo)
        {
            _saveData.SetCurrentScenarioInfo(otherScenario);
            return CampaignSelectionAction.SelectOtherScenario;
        }

        return CampaignSelectionAction.None;
    }

    /// <summary>
    /// Tells which block a right press hits, the <c>le.MouseClickRight()</c> chain of
    /// <c>SelectCampaignScenario()</c> (<c>game_campaign.cpp:1558-1587, 1690-1697</c>).
    /// </summary>
    /// <param name="events">The current frame's input.</param>
    /// <returns>What was hit.</returns>
    public CampaignSelectionHelpRequest ProcessRightClick(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!events.IsMouseRightButtonPressed)
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.None);
        }

        Point cursor = events.CursorPosition;

        for (int i = 0; i < _bonusOptions.Count; ++i)
        {
            if (BonusHelpArea(i).Contains(cursor))
            {
                return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.Bonus, i);
            }
        }

        IReadOnlyList<CampaignAwardData> awards = ObtainedAwards;

        for (int i = 0; i < awards.Count && i < 4; ++i)
        {
            if (AwardHelpArea(i, awards.Count).Contains(cursor))
            {
                return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.Awards, i);
            }
        }

        if (_cancelButton.ReleasedArea.Contains(cursor))
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.CancelButton);
        }

        if (_okButton.IsVisible && _okButton.ReleasedArea.Contains(cursor))
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.OkayButton);
        }

        if (_viewIntroButton.ReleasedArea.Contains(cursor))
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.ViewIntroButton);
        }

        if (_difficultyButton.ReleasedArea.Contains(cursor))
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.DifficultyButton);
        }

        if (_restartButton.IsVisible && _restartButton.ReleasedArea.Contains(cursor))
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.RestartButton);
        }

        if (DaysArea.Contains(cursor))
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.Days);
        }

        if (ScenarioTitleArea.Contains(cursor))
        {
            return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.ScenarioTitle);
        }

        return new CampaignSelectionHelpRequest(CampaignSelectionHelpTarget.None);
    }

    /// <summary>
    /// Picks a bonus option, the <c>optionButtonGroup.press( i )</c> loop.
    /// </summary>
    /// <param name="index">The index of the option.</param>
    /// <returns>True when the selection changed.</returns>
    public bool SelectBonus(int index)
    {
        if (index < 0 || index >= _bonusOptions.Count || index == _selectedBonusIndex)
        {
            return false;
        }

        _selectedBonusIndex = index;
        return true;
    }

    /// <summary>
    /// Whether the save sits on a betrayal scenario. A campaign that has not picked its first scenario yet
    /// still reads <see cref="ScenarioInfoId.NotStarted"/>, which is not a scenario at all — the reference looks it
    /// up regardless, but the port's campaign table asserts on unknown campaigns.
    /// </summary>
    private bool IsCurrentScenarioBetrayal =>
        _saveData.CurrentScenarioInfoId != ScenarioInfoId.NotStarted
        && CampaignData.IsBetrayalScenario(_saveData.CurrentScenarioInfoId);

    /// <summary>
    /// The press area of one bonus option: the button itself plus the 170 pixels to its left, so that the
    /// bonus name counts as part of the row, <c>choiceArea</c> of the reference.
    /// </summary>
    private Rect BonusClickArea(int index)
    {
        Rect area = _bonusOptions[index].Area;

        return new Rect(area.X - 170, area.Y, area.Width + 170, area.Height);
    }

    /// <summary>
    /// The help area of one obtained award row, <c>displayScenarioAwardsPopupWindow()</c>
    /// (<c>game_campaign.cpp:492</c>): rows are 22 pixels apart, or 16 once there are four.
    /// </summary>
    private Rect AwardHelpArea(int index, int awardCount)
    {
        int lineStep = awardCount > 3 ? 16 : 22;

        return new Rect(_top.X + 414, _top.Y + 100 - (lineStep / 2) + (lineStep * index), 200, lineStep);
    }

    private Rect BonusHelpArea(int index) => new(_top.X + 414, _top.Y + 198 + OptionButtonStep * index, 200, OptionButtonStep);

    private static Sprite LoadBackground(ICampaignScenarioArtwork artwork, int campaignId) => campaignId switch
    {
        CampaignId.Roland => artwork.GetIcon(IcnType.Campbkgg, 0),
        CampaignId.Archibald => artwork.GetIcon(IcnType.Campbkge, 0),
        _ => artwork.GetIcon(IcnType.XCmpbkg, 0),
    };

    private static Sprite? TryLoadHeader(ICampaignScenarioArtwork artwork, int campaignId)
    {
        // 罗兰与阿基巴德的背景图里自带战役名，资料片才另贴一条标题头。
        int frame = campaignId switch
        {
            CampaignId.PriceOfLoyalty => 15,
            CampaignId.Descendants => 16,
            CampaignId.WizardsIsle => 17,
            CampaignId.VoyageHome => 18,
            _ => -1,
        };

        return frame < 0 ? null : artwork.GetIcon(IcnType.XCmpext, frame);
    }

    /// <summary>The ten frame button set of the campaign, <c>getCampaignButtonSetId()</c>.</summary>
    /// <remarks>
    /// 公开是因为这套按钮不在游戏数据里，得由调用方（App 层）先现做出来再交进来；
    /// 它需要知道做哪一套，正好是这一函数的结果。
    /// </remarks>
    public static IcnType GetButtonSet(int campaignId) => campaignId switch
    {
        CampaignId.Roland => IcnType.GoodCampaignButtons,
        CampaignId.Archibald => IcnType.EvilCampaignButtons,
        _ => IcnType.PolCampaignButtons,
    };

    private static void SetAlternateButton(ButtonSprite button, bool used, bool isMapFilePresent)
    {
        if (!used)
        {
            button.Hide();
            button.Disable();
            return;
        }

        button.Show();

        if (isMapFilePresent)
        {
            button.Enable();
        }
        else
        {
            button.Disable();
        }
    }

    private void BuildBonusOptions(Sprite background, Sprite pressedIcon)
    {
        // 未选中的行是「从背景图上按按钮 anchor 挖下来的一块」，看起来就是背景本身的一个凹槽。
        // 参考只挖第一行那一块（game_campaign.cpp:1382 的 Copy 在循环外），之后每一行都贴同一张，
        // 所以在背景有渐变时各行的凹槽长得一样；这里保持参考的行为。
        var hole = new Sprite(pressedIcon.Width, pressedIcon.Height, pressedIcon.OffsetX, pressedIcon.OffsetY);

        ImageOps.Copy(
            background,
            new Rect(OptionButtonOffsetX + pressedIcon.OffsetX, OptionButtonOffsetY + pressedIcon.OffsetY, hole.Width, hole.Height),
            hole,
            new Point(0, 0));

        for (int i = 0; i < _bonuses.Count; ++i)
        {
            // 按钮 anchor 是「格子左上角 + 贴片自己的偏移」，与 ButtonBase.ReleasedArea 的算法一致。
            var position = new Point(
                _top.X + OptionButtonOffsetX + pressedIcon.OffsetX,
                _top.Y + OptionButtonOffsetY + OptionButtonStep * i + pressedIcon.OffsetY);

            _bonusOptions.Add((hole, new Rect(position, new Size(pressedIcon.Width, pressedIcon.Height))));
        }
    }

    private void DrawTexts(PixelBuffer target)
    {
        DrawDays(target);
        DrawScenarioDescription(target);
        DrawObtainedAwards(target);
    }

    private void DrawDays(PixelBuffer target)
    {
        string text = _saveData.DaysPassed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int width = GlyphText.Measure(_font, text);

        // 这个数字水平居中在 582 这个位置上，不是居中在某个框里。
        GlyphText.DrawLine(_font, target, new Point(_top.X + 582 - width / 2, _top.Y + 33), text);
    }

    private void DrawScenarioDescription(PixelBuffer target)
    {
        string name = GlyphText.FitToOneRow(_font, _scenario.ScenarioName, 200);
        int nameWidth = GlyphText.Measure(_font, name);
        GlyphText.DrawLine(_font, target, new Point(_top.X + 197 + (200 - nameWidth) / 2, _top.Y + 99 - _font.LineHeight / 2), name);

        // 叛变关不属于任何一段正式战役，参考固定按第五关显示。
        int scenarioId = CampaignData.IsBetrayalScenario(_scenario.ScenarioInfo)
            ? CampaignScenarioTrack.BetrayalScenarioId + 1
            : _scenario.ScenarioId + 1;
        string count = scenarioId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int countWidth = GlyphText.Measure(_font, count);
        GlyphText.DrawLine(_font, target, new Point(_top.X + 172 - countWidth / 2, _top.Y + 99 - _font.LineHeight / 2), count);

        // setUniformVerticalAlignment( false )：逐行左对齐，不做垂直居中。
        GlyphText.DrawMultiline(_font, target, new Point(_top.X + 34, _top.Y + 134), _scenario.Description, 356, center: false);

        // 奖励选项名是左对齐到 x = 425 的（参考用 Text::draw( x, y ) 的三参版本），只有已得奖励才居中。
        for (int i = 0; i < _bonuses.Count; ++i)
        {
            string choice = GlyphText.FitToOneRow(_font, _bonuses[i].GetName(), BonusTextWidth);

            GlyphText.DrawLine(
                _font,
                target,
                new Point(_top.X + 425, _top.Y + 211 + OptionButtonStep * i - _font.LineHeight / 2),
                choice);
        }
    }

    private void DrawBonusOptions(PixelBuffer target)
    {
        for (int i = 0; i < _bonusOptions.Count; ++i)
        {
            (Sprite icon, Rect area) = _bonusOptions[i];

            ImageOps.BlitSprite(target, area.Position, i == _selectedBonusIndex ? _bonusPressedIcon : icon);
        }
    }

    private void DrawObtainedAwards(PixelBuffer target)
    {
        IReadOnlyList<CampaignAwardData> awards = ObtainedAwards;

        if (awards.Count == 0)
        {
            const string none = "None";
            int width = GlyphText.Measure(_font, none);

            GlyphText.DrawLine(
                _font,
                target,
                new Point(_top.X + 425 + (AwardTextWidth - width) / 2, _top.Y + AwardInitialOffsetY - _font.LineHeight / 2),
                none);

            return;
        }

        // 超过三条就改用小行距，最多列四条；第四条位置在「正好四条」时是真奖励，否则写 and more...
        int lineStep = awards.Count > 3 ? 16 : 22;
        int shown = Math.Min(awards.Count, 4);

        for (int i = 0; i < shown; ++i)
        {
            string text = i < 3
                ? awards[i].GetName()
                : (awards.Count == 4 ? awards[i].GetName() : "and more...");

            text = GlyphText.FitToOneRow(_font, text, AwardTextWidth);
            int width = GlyphText.Measure(_font, text);

            GlyphText.DrawLine(
                _font,
                target,
                new Point(_top.X + 425 + (AwardTextWidth - width) / 2, _top.Y + AwardInitialOffsetY + lineStep * i - _font.LineHeight / 2),
                text);
        }
    }
}
