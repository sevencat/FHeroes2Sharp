using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Campaign;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// State of a scenario icon on the campaign track, <c>enum ScenarioIcon : uint32_t</c>
/// (<c>game_campaign.cpp:86-91</c>).
/// </summary>
/// <remarks>
/// 数值就是图标在 ICN 里的相对位置：每组关卡图标是「已通关、可进入、未开放」三张连着放的，所以
/// <c>iconStatusOffset + 状态</c> 就是要画的那一张。
/// </remarks>
public enum ScenarioIconStatus
{
    /// <summary>The scenario was already won, <c>SCENARIO_ICON_CLEARED</c>.</summary>
    Cleared = 0,

    /// <summary>The scenario can be entered, <c>SCENARIO_ICON_AVAILABLE</c>.</summary>
    Available = 1,

    /// <summary>The scenario is still locked, <c>SCENARIO_ICON_UNAVAILABLE</c>.</summary>
    Unavailable = 2,
}

/// <summary>
/// Which ICN holds the three status icons of a campaign and which frames they are,
/// <c>getScenarioIconId()</c> (<c>game_campaign.cpp:134-176</c>).
/// </summary>
/// <param name="IconsId">The ICN of the status icons, <c>iconsId</c>.</param>
/// <param name="StatusOffset">The frame of the "cleared" icon, <c>iconStatusOffset</c>.</param>
/// <param name="SelectedIndex">The frame drawn while the scenario is picked, <c>selectedIconIdx</c>.</param>
/// <remarks>
/// 罗兰与阿基巴德共用 <c>CAMPXTRG</c>/<c>CAMPXTRE</c> 的一套图标，只是「高亮」那张的编号互换（14 / 17）：
/// 同一个格子，红方与蓝方的高亮色不同。三个资料片战役共用 <c>X_CMPEXT</c>。
/// </remarks>
public readonly record struct ScenarioIconSet(IcnType IconsId, int StatusOffset, int SelectedIndex);

/// <summary>
/// One scenario on the campaign track: where its icon is, which state it shows, and the button when it can be
/// entered, <c>addScenarioButton()</c> (<c>game_campaign.cpp:184-251</c>).
/// </summary>
public sealed class CampaignScenarioIcon
{
    /// <summary>Which scenario the icon stands for.</summary>
    public required ScenarioInfoId Scenario { get; init; }

    /// <summary>Which of the three icons is drawn.</summary>
    public required ScenarioIconStatus Status { get; init; }

    /// <summary>The icon drawn right away, the released frame of the button when the scenario can be entered.</summary>
    public required Sprite Icon { get; init; }

    /// <summary>The button of an enterable scenario; null for the other two states.</summary>
    public ButtonSprite? Button { get; init; }

    /// <summary>Where the icon sits on the screen.</summary>
    public required Point Position { get; init; }

    /// <summary>The click area of the icon.</summary>
    public Rect Area => Button is not null ? Button.ReleasedArea : new Rect(Position, new Size(Icon.Width, Icon.Height));
}

/// <summary>
/// The pictures the campaign selection screen needs, the port's stand-in for the direct
/// <c>Assets::getImage()</c> calls of <c>game_campaign.cpp</c>.
/// </summary>
/// <remarks>
/// 这里不像 <see cref="ICampaignPopupArtwork"/> 那样一个用途一个方法：轨道与关卡图标是按数据表取的，
/// 一个战役就要二十来张图，逐个起名字只会把表抄第二遍。
/// </remarks>
public interface ICampaignScenarioArtwork
{
    /// <summary>One frame of an ICN, <c>Assets::getImage( icnId, frame )</c>.</summary>
    /// <param name="icnId">The ICN.</param>
    /// <param name="frame">The frame inside it.</param>
    /// <returns>The sprite.</returns>
    Sprite GetIcon(IcnType icnId, int frame);
}

/// <summary>
/// The map of the campaign with one icon per scenario, <c>DrawCampaignScenarioIcons()</c>
/// (<c>game_campaign.cpp:253-380</c>) and <c>getCampaignIconOffsets()</c> (<c>95-120</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这块是选关界面里唯一有真正分支的地方，所以单独成类：图标位置是每战役一张手调表（格坐标 × 37 / 42 像素），
/// 轨道底图按「叛变关是否已打通」「叛变关是不是下一关」换图，阿基巴德的叛变关还分上、下两条支线。
/// </para>
/// <para>
/// 参考把「画轨道」「画图标」「建按钮」混在同一个函数里，还直接往 <c>Display</c> 上画；端口拆成
/// <see cref="Icons"/>（数据，可测）+ <see cref="Draw"/>（轨道与静态图标）+ <see cref="DrawButtons"/>（可进入的关卡，
/// 带按下态），因为按钮的按下态由选关逻辑在两帧之间切换。
/// </para>
/// </remarks>
public sealed class CampaignScenarioTrack
{
    /// <summary>The horizontal distance between two grid columns, <c>deltaX</c>.</summary>
    public const int IconStepX = 37;

    /// <summary>The vertical distance between two grid rows, <c>deltaY</c>.</summary>
    public const int IconStepY = 42;

    /// <summary>
    /// The index the betrayal scenario of Roland and Archibald is drawn at once it is beaten, <c>betrayalScenarioId</c>.
    /// </summary>
    /// <remarks>
    /// 叛变关是「跨战役」的关卡，通关后它就不该再占自己那个格子（那格子跑出轨道图外了），于是改画在第五关的位置上——
    /// 参考就是这么做的，注释里没写原因。
    /// </remarks>
    public const int BetrayalScenarioId = 4;

    private readonly Sprite _trackSprite;
    private readonly Point _trackPosition;
    private readonly List<CampaignScenarioIcon> _icons;

    private CampaignScenarioTrack(Sprite trackSprite, Point trackPosition, List<CampaignScenarioIcon> icons)
    {
        _trackSprite = trackSprite;
        _trackPosition = trackPosition;
        _icons = icons;
    }

    /// <summary>The icons, in drawing order.</summary>
    public IReadOnlyList<CampaignScenarioIcon> Icons => _icons;

    /// <summary>Where the top left corner of the track picture is drawn.</summary>
    public Point TrackPosition => _trackPosition;

    /// <summary>
    /// The grid positions of the scenario icons of a campaign, <c>getCampaignIconOffsets()</c>
    /// (<c>game_campaign.cpp:95-120</c>).
    /// </summary>
    /// <param name="campaignId">Which campaign.</param>
    /// <param name="archibaldLowerBetrayalBranch">
    /// True when Archibald's betrayal scenario was reached through the fourth scenario instead of the third,
    /// <c>archibaldLowerBetrayalBranch</c>.
    /// </param>
    /// <returns>The grid position of every scenario, in scenario order.</returns>
    public static IReadOnlyList<Point> GetIconOffsets(int campaignId, bool archibaldLowerBetrayalBranch = false) => campaignId switch
    {
        CampaignId.Roland =>
        [
            new Point(0, 1), new Point(2, 1), new Point(3, 0), new Point(4, 1), new Point(6, 1), new Point(8, 1),
            new Point(10, 2), new Point(10, 0), new Point(12, 1), new Point(14, 1), new Point(6, 2),
        ],
        CampaignId.Archibald when archibaldLowerBetrayalBranch =>
        [
            new Point(0, 1), new Point(2, 1), new Point(4, 0), new Point(4, 2), new Point(6, 1), new Point(8, 1),
            new Point(9, 0), new Point(10, 1), new Point(12, 0), new Point(12, 2), new Point(14, 1), new Point(6, 2),
        ],
        CampaignId.Archibald =>
        [
            new Point(0, 1), new Point(2, 1), new Point(4, 0), new Point(4, 2), new Point(6, 1), new Point(8, 1),
            new Point(9, 0), new Point(10, 1), new Point(12, 0), new Point(12, 2), new Point(14, 1), new Point(6, 0),
        ],
        CampaignId.PriceOfLoyalty =>
        [
            new Point(0, 0), new Point(2, 0), new Point(4, 1), new Point(4, 0), new Point(6, 1), new Point(7, 0),
            new Point(9, 1), new Point(10, 0),
        ],
        CampaignId.Descendants =>
        [
            new Point(0, 1), new Point(2, 1), new Point(4, 0), new Point(4, 2), new Point(6, 1), new Point(8, 2),
            new Point(8, 0), new Point(10, 1),
        ],
        CampaignId.WizardsIsle => [new Point(0, 0), new Point(2, 0), new Point(4, 1), new Point(6, 0)],
        CampaignId.VoyageHome => [new Point(0, 0), new Point(2, 0), new Point(4, 0), new Point(4, 1)],
        _ => [],
    };

    /// <summary>
    /// Which ICN and frames the status icons of a scenario come from, <c>getScenarioIconId()</c>
    /// (<c>game_campaign.cpp:134-176</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario.</param>
    /// <returns>The icon set of its campaign.</returns>
    public static ScenarioIconSet GetIconSet(ScenarioInfoId scenarioInfoId) => scenarioInfoId.CampaignId switch
    {
        CampaignId.Roland => new ScenarioIconSet(IcnType.Campxtrg, 10, CampaignData.IsBetrayalScenario(scenarioInfoId) ? 17 : 14),
        CampaignId.Archibald => new ScenarioIconSet(IcnType.Campxtre, 10, CampaignData.IsBetrayalScenario(scenarioInfoId) ? 14 : 17),
        CampaignId.PriceOfLoyalty => new ScenarioIconSet(IcnType.XCmpext, 0, 4),
        CampaignId.Descendants => new ScenarioIconSet(IcnType.XCmpext, 0, 7),
        CampaignId.WizardsIsle => new ScenarioIconSet(IcnType.XCmpext, 0, 10),
        CampaignId.VoyageHome => new ScenarioIconSet(IcnType.XCmpext, 0, 13),
        _ => new ScenarioIconSet(IcnType.Unknown, 0, 0),
    };

    /// <summary>
    /// Builds the track of a campaign, <c>DrawCampaignScenarioIcons()</c> (<c>game_campaign.cpp:253-380</c>).
    /// </summary>
    /// <param name="artwork">Where the pictures come from.</param>
    /// <param name="saveData">The campaign save, for the cleared scenarios and the current one.</param>
    /// <param name="campaignData">The campaign being shown.</param>
    /// <param name="top">The top left corner of the selection screen, <c>top</c>.</param>
    /// <param name="chosenScenarioId">
    /// A scenario forced to be the only enterable one, <c>chosenScenarioId</c>; -1 to use the campaign state.
    /// </param>
    /// <returns>The track, already positioned, ready to be drawn.</returns>
    public static CampaignScenarioTrack Create(
        ICampaignScenarioArtwork artwork,
        CampaignSaveData saveData,
        CampaignData campaignData,
        Point top,
        int chosenScenarioId = -1)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(campaignData);

        List<ScenarioInfoId> clearedMaps = [.. saveData.FinishedMaps];

        List<ScenarioInfoId> availableMaps = chosenScenarioId >= 0
            ? [new ScenarioInfoId(saveData.CampaignId, chosenScenarioId)]
            : saveData.IsStarting
                ? campaignData.GetStartingScenarios()
                : [.. CampaignData.GetScenariosAfter(saveData.GetLastCompletedScenarioInfoId())];

        bool isBetrayalScenarioNext = availableMaps.Exists(CampaignData.IsBetrayalScenario);

        // 还有叛变关可打时，「打过叛变关」这件事不重要：轨道还停在叛变之前的样子。
        bool isBetrayalScenarioCompleted = !isBetrayalScenarioNext && clearedMaps.Exists(CampaignData.IsBetrayalScenario);

        bool archibaldLowerBetrayalBranch = false;
        IcnType trackIcn = GetTrackIcn(campaignData.Id, isBetrayalScenarioNext, isBetrayalScenarioCompleted, clearedMaps, out archibaldLowerBetrayalBranch);

        Sprite trackSprite = artwork.GetIcon(trackIcn, 0);

        // 轨道图带 anchor：参考的 trackOffset 是 top 加上 sprite 自己那点偏移。
        var trackPosition = new Point(top.X + trackSprite.OffsetX, top.Y + trackSprite.OffsetY);
        var track = new CampaignScenarioTrack(trackSprite, trackPosition, []);

        if (isBetrayalScenarioCompleted)
        {
            // 叛变关打通后，轨道图左半边是「来路」那个战役，右半边是现在这个战役；两边的图标风格不一样。
            bool isRolandCurrentCampaign = campaignData.Id == CampaignId.Roland;
            CampaignData beforeCampaignData = CampaignData.GetCampaignData(isRolandCurrentCampaign ? CampaignId.Archibald : CampaignId.Roland);
            CampaignData currentCampaignData = CampaignData.GetCampaignData(isRolandCurrentCampaign ? CampaignId.Roland : CampaignId.Archibald);

            foreach (ScenarioData scenario in beforeCampaignData.AllScenarios)
            {
                if (scenario.ScenarioId <= BetrayalScenarioId || CampaignData.IsBetrayalScenario(scenario.ScenarioInfo))
                {
                    track.AddScenarioIcon(artwork, scenario.ScenarioInfo, availableMaps, clearedMaps, archibaldLowerBetrayalBranch);
                }
            }

            foreach (ScenarioData scenario in currentCampaignData.AllScenarios)
            {
                if (scenario.ScenarioId > BetrayalScenarioId)
                {
                    track.AddScenarioIcon(artwork, scenario.ScenarioInfo, availableMaps, clearedMaps, archibaldLowerBetrayalBranch);
                }
            }
        }
        else
        {
            foreach (ScenarioData scenario in campaignData.AllScenarios)
            {
                track.AddScenarioIcon(artwork, scenario.ScenarioInfo, availableMaps, clearedMaps, archibaldLowerBetrayalBranch);
            }
        }

        return track;
    }

    /// <summary>Draws the track picture and the icons that are not buttons.</summary>
    /// <param name="target">The screen or window to draw on.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, _trackPosition, _trackSprite);

        foreach (CampaignScenarioIcon icon in _icons)
        {
            if (icon.Button is null)
            {
                ImageOps.BlitSprite(target, icon.Position, icon.Icon);
            }
        }
    }

    /// <summary>Draws the buttons of the enterable scenarios, honouring their pressed state.</summary>
    /// <param name="target">The screen or window to draw on.</param>
    public void DrawButtons(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (CampaignScenarioIcon icon in _icons)
        {
            icon.Button?.Draw(target);
        }
    }

    /// <summary>
    /// The scenario whose button covers a point, for a click.
    /// </summary>
    /// <param name="point">Where the mouse is.</param>
    /// <returns>The scenario, or null when no button is there.</returns>
    public ScenarioInfoId? HitTest(Point point)
    {
        foreach (CampaignScenarioIcon icon in _icons)
        {
            if (icon.Button is not null && icon.Button.ReleasedArea.Contains(point))
            {
                return icon.Scenario;
            }
        }

        return null;
    }

    /// <summary>
    /// Puts the button of a scenario into its pressed state and releases every other one, the
    /// <c>buttonGroup[i].press()</c> loop of <c>SelectCampaignScenario()</c>.
    /// </summary>
    /// <param name="scenarioInfoId">The scenario to press; <see cref="ScenarioInfoId.NotStarted"/> releases all.</param>
    public void Press(ScenarioInfoId scenarioInfoId)
    {
        foreach (CampaignScenarioIcon icon in _icons)
        {
            if (icon.Button is null)
            {
                continue;
            }

            if (icon.Scenario == scenarioInfoId)
            {
                icon.Button.Press();
            }
            else
            {
                icon.Button.Release();
            }
        }
    }

    /// <summary>
    /// Picks the track picture, the <c>switch</c> of <c>DrawCampaignScenarioIcons()</c> (<c>game_campaign.cpp:291-339</c>).
    /// </summary>
    /// <param name="campaignId">Which campaign.</param>
    /// <param name="isBetrayalScenarioNext">True when the betrayal scenario is one of the enterable ones.</param>
    /// <param name="isBetrayalScenarioCompleted">True when the betrayal scenario was already won.</param>
    /// <param name="clearedMaps">The cleared scenarios, in order.</param>
    /// <param name="archibaldLowerBetrayalBranch">Tells the caller whether Archibald's other branch is shown.</param>
    /// <returns>The ICN of the track.</returns>
    private static IcnType GetTrackIcn(
        int campaignId,
        bool isBetrayalScenarioNext,
        bool isBetrayalScenarioCompleted,
        List<ScenarioInfoId> clearedMaps,
        out bool archibaldLowerBetrayalBranch)
    {
        archibaldLowerBetrayalBranch = false;

        switch (campaignId)
        {
        case CampaignId.Roland:
            if (isBetrayalScenarioNext)
            {
                return IcnType.Ctrack04;
            }

            return isBetrayalScenarioCompleted ? IcnType.Ctrack02 : IcnType.Ctrack00;

        case CampaignId.Archibald:
            if (isBetrayalScenarioNext)
            {
                // 阿基巴德是唯一有分支的战役：叛变关从第三关（上路）或第四关（下路）都能出去，靠最后一个已通关卡区分。
                // 参考在这里 assert 之后直接 clearedMaps.back()，空表就是越界；端口退回主线轨道。
                if (clearedMaps.Count == 0)
                {
                    return IcnType.Ctrack03;
                }

                if (clearedMaps[^1].ScenarioId == 2)
                {
                    return IcnType.Ctrack05;
                }

                archibaldLowerBetrayalBranch = true;
                return IcnType.Ctrack06;
            }

            return isBetrayalScenarioCompleted ? IcnType.Ctrack01 : IcnType.Ctrack03;

        case CampaignId.PriceOfLoyalty:
            return IcnType.XTrack1;

        case CampaignId.Descendants:
            return IcnType.XTrack2;

        case CampaignId.WizardsIsle:
            return IcnType.XTrack3;

        case CampaignId.VoyageHome:
            return IcnType.XTrack4;

        default:
            return IcnType.Unknown;
        }
    }

    /// <summary>
    /// Adds the icon of one scenario, <c>addScenarioButton()</c> (<c>game_campaign.cpp:184-251</c>).
    /// </summary>
    /// <param name="artwork">Where the pictures come from.</param>
    /// <param name="scenarioInfo">The scenario.</param>
    /// <param name="availableMaps">The scenarios that can be entered.</param>
    /// <param name="clearedMaps">The scenarios that were won.</param>
    /// <param name="archibaldLowerBetrayalBranch">Which of Archibald's two branches is shown.</param>
    private void AddScenarioIcon(
        ICampaignScenarioArtwork artwork,
        ScenarioInfoId scenarioInfo,
        List<ScenarioInfoId> availableMaps,
        List<ScenarioInfoId> clearedMaps,
        bool archibaldLowerBetrayalBranch)
    {
        IReadOnlyList<Point> iconOffsets = GetIconOffsets(scenarioInfo.CampaignId, archibaldLowerBetrayalBranch);

        // 参考在这里 assert：战役表里的关卡数必须与位置表对得上。端口照旧跳过，不让一张表出错拖垮整个界面。
        if (scenarioInfo.ScenarioId < 0 || scenarioInfo.ScenarioId >= iconOffsets.Count)
        {
            return;
        }

        Point offset = GetPixelOffset(scenarioInfo, iconOffsets[scenarioInfo.ScenarioId]);
        ScenarioIconSet iconSet = GetIconSet(scenarioInfo);

        if (availableMaps.Contains(scenarioInfo))
        {
            Sprite availableIcon = artwork.GetIcon(iconSet.IconsId, iconSet.StatusOffset + (int)ScenarioIconStatus.Available);
            Sprite selectedIcon = artwork.GetIcon(iconSet.IconsId, iconSet.SelectedIndex);
            var button = new ButtonSprite(availableIcon, selectedIcon, null, _trackPosition.X + offset.X, _trackPosition.Y + offset.Y);

            _icons.Add(new CampaignScenarioIcon
            {
                Scenario = scenarioInfo,
                Status = ScenarioIconStatus.Available,
                Icon = availableIcon,
                Button = button,
                Position = new Point(_trackPosition.X + offset.X, _trackPosition.Y + offset.Y),
            });

            return;
        }

        if (clearedMaps.Contains(scenarioInfo))
        {
            if (CampaignData.IsBetrayalScenario(scenarioInfo))
            {
                // 叛变关打完后改画在第五关的格子上，见 BetrayalScenarioId。
                offset = GetPixelOffset(scenarioInfo, iconOffsets[BetrayalScenarioId]);
            }

            _icons.Add(new CampaignScenarioIcon
            {
                Scenario = scenarioInfo,
                Status = ScenarioIconStatus.Cleared,
                Icon = artwork.GetIcon(iconSet.IconsId, iconSet.StatusOffset + (int)ScenarioIconStatus.Cleared),
                Position = new Point(_trackPosition.X + offset.X, _trackPosition.Y + offset.Y),
            });

            return;
        }

        if (!CampaignData.IsBetrayalScenario(scenarioInfo))
        {
            _icons.Add(new CampaignScenarioIcon
            {
                Scenario = scenarioInfo,
                Status = ScenarioIconStatus.Unavailable,
                Icon = artwork.GetIcon(iconSet.IconsId, iconSet.StatusOffset + (int)ScenarioIconStatus.Unavailable),
                Position = new Point(_trackPosition.X + offset.X, _trackPosition.Y + offset.Y),
            });
        }
    }

    /// <summary>
    /// Turns a grid position of the campaign table into a pixel offset inside the track,
    /// the packed arithmetic of <c>addScenarioButton()</c> (<c>game_campaign.cpp:198-220</c>).
    /// </summary>
    /// <param name="scenarioInfo">The scenario the icon belongs to.</param>
    /// <param name="grid">Its grid position.</param>
    /// <returns>The offset from the top left corner of the track.</returns>
    private static Point GetPixelOffset(ScenarioInfoId scenarioInfo, Point grid)
    {
        int x = grid.X * IconStepX;
        int y = grid.Y * IconStepY;

        // Price of Loyalty 的轨道图不是等距画的，后三关的格子要往左挪一点才对齐。
        if (scenarioInfo.CampaignId == CampaignId.PriceOfLoyalty && scenarioInfo.ScenarioId >= 5)
        {
            x += scenarioInfo.ScenarioId switch
            {
                5 => -2,
                6 => -1,
                7 => -4,
                _ => 0,
            };
        }

        // 图标自己左上角还留了 2 像素的空边，位置表从这块空边开始量。
        return new Point(x - 2, y - 2);
    }
}
