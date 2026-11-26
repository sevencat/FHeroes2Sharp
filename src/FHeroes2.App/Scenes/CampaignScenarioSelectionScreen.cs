using FHeroes2.App.Fonts;
using FHeroes2.App.Localization;
using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Campaign;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The campaign selection screen, <c>Game::SelectCampaignScenario()</c> (<c>game_campaign.cpp:1284-1710</c>),
/// as a window the host pumps until it closes.
/// </summary>
/// <remarks>
/// <para>
/// 参考的这个函数自己转事件循环，而且会顺带播片、弹帮助窗、开难度窗、最终把地图交给 <c>world.LoadMapMP2</c>。
/// 端口把「一屏」和「一屏之后干什么」分开：这一屏只负责画与读输入，玩家做了什么由宿主按
/// <see cref="Result"/> 继续——开难度窗、重开这一屏、播片、还是读地图进冒险地图。这样内层循环不必嵌套，
/// 与 <see cref="GameHost.RunModal"/> 只允许一层模态的约定一致。
/// </para>
/// <para>
/// 与参考的差别只有一处：右键帮助还没接。对话框已经把「点到哪一块」算好了
/// （<see cref="CampaignSelectionHelpRequest"/>），宿主补上弹窗即可，不影响任何一处玩法分支。
/// </para>
/// </remarks>
internal sealed class CampaignScenarioSelectionScreen : IModalScreen
{
    private static readonly Point OffScreen = new(-1, -1);

    private readonly CampaignScenarioSelectionDialog _dialog;
    private readonly LocalEvent _events = new();

    private Point? _cursor;
    private CampaignSelectionHotKeys _hotKeys = new();

    /// <summary>
    /// Builds the screen of the scenario the save currently sits on.
    /// </summary>
    /// <param name="assets">Where the pictures come from.</param>
    /// <param name="text">Translations of the labels of the generated button set.</param>
    /// <param name="fonts">The fonts of the screen; the normal white face is the one the reference uses.</param>
    /// <param name="saveData">The campaign save being played.</param>
    /// <param name="mapDirectories">Where the campaign maps may live, usually the data directory and its maps.</param>
    /// <param name="screenSize">The size of the screen the background is centred on.</param>
    /// <param name="allowToRestart">Whether the running scenario may be restarted, <c>allowToRestart</c>.</param>
    /// <param name="error">Why the screen cannot be built, for the caller's log.</param>
    /// <returns>The screen, or <see langword="null"/> when the save does not name a scenario of a known campaign.</returns>
    public static CampaignScenarioSelectionScreen? TryCreate(
        AssetBundle assets,
        GameText text,
        GameFonts fonts,
        CampaignSaveData saveData,
        IReadOnlyList<string> mapDirectories,
        Size screenSize,
        bool allowToRestart,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(mapDirectories);

        error = null;

        if (saveData.CurrentScenarioInfoId == ScenarioInfoId.NotStarted)
        {
            error = "The campaign save does not name a scenario yet.";

            return null;
        }

        CampaignData campaignData = CampaignData.GetCampaignData(saveData.CampaignId);

        if (campaignData.AllScenarios.Count == 0
            || saveData.CurrentScenarioId < 0
            || saveData.CurrentScenarioId >= campaignData.AllScenarios.Count)
        {
            error = $"The campaign {saveData.CampaignId} has no scenario {saveData.CurrentScenarioId}.";

            return null;
        }

        ScenarioData scenario = campaignData.AllScenarios[saveData.CurrentScenarioId];
        bool isMapFilePresent = scenario.FindMapFile(mapDirectories) is not null;

        // 按钮表不在游戏数据里，先按这套战役的按钮风格现做一份。
        IcnType buttonSet = CampaignScenarioSelectionDialog.GetButtonSet(saveData.CampaignId);
        Sprite[]? buttonFrames = CampaignButtonsArtwork.TryCreate(assets, text, buttonSet);
        var generated = new Dictionary<IcnType, Sprite[]>();

        if (buttonFrames is not null)
        {
            generated[buttonSet] = buttonFrames;
        }

        var dialog = new CampaignScenarioSelectionDialog(
            new CampaignScenarioArtwork(assets, generated),
            saveData,
            campaignData,
            scenario,
            fonts.Normal,
            screenSize,
            isMapFilePresent,
            allowToRestart);

        return new CampaignScenarioSelectionScreen(dialog);
    }

    /// <summary>
    /// Wraps a dialog the caller has already built.
    /// </summary>
    /// <param name="dialog">The dialog of this screen.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> is <see langword="null"/>.</exception>
    public CampaignScenarioSelectionScreen(CampaignScenarioSelectionDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        _dialog = dialog;
    }

    /// <summary>What the player asked for; <see cref="CampaignSelectionAction.None"/> until the screen closes.</summary>
    public CampaignSelectionAction Result { get; private set; }

    /// <summary>The dialog below, for tests and for the host's own drawing checks.</summary>
    public CampaignScenarioSelectionDialog Dialog => _dialog;

    /// <inheritdoc/>
    public bool IsFinished { get; private set; }

    /// <inheritdoc/>
    public void SetCursor(Point? position)
    {
        _cursor = position;

        if (position is { } point)
        {
            _events.OnMouseMotion(point);
        }
    }

    /// <inheritdoc/>
    public void SetLeftButton(bool down) =>
        _events.OnMouseButton(down, MouseButtonType.Left, _cursor ?? OffScreen);

    /// <inheritdoc/>
    public void SetRightButton(bool down) =>
        _events.OnMouseButton(down, MouseButtonType.Right, _cursor ?? OffScreen);

    /// <inheritdoc/>
    public void HotKey(ButtonHotKey hotKey)
    {
        switch (hotKey)
        {
            case ButtonHotKey.Cancel:
                _hotKeys = _hotKeys with { Cancel = true };
                break;

            case ButtonHotKey.Okay:
                _hotKeys = _hotKeys with { Okay = true };
                break;
        }
    }

    /// <inheritdoc/>
    public void PressKey(Key key, KeyModifier modifier)
    {
        switch (key)
        {
            case Key.Digit1:
                _hotKeys = _hotKeys with { Bonus = 0 };
                break;

            case Key.Digit2:
                _hotKeys = _hotKeys with { Bonus = 1 };
                break;

            case Key.Digit3:
                _hotKeys = _hotKeys with { Bonus = 2 };
                break;

            case Key.V:
                _hotKeys = _hotKeys with { ViewIntro = true };
                break;

            case Key.D:
                _hotKeys = _hotKeys with { Difficulty = true };
                break;

            case Key.R:
                _hotKeys = _hotKeys with { Restart = true };
                break;
        }
    }

    /// <inheritdoc/>
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        _dialog.PressButtons(_events);

        CampaignSelectionAction action = _dialog.ProcessLeftClick(_events, _hotKeys);

        // 必须用 new() 而不是 default：record struct 的 default 是「全零」，Bonus 会变成 0，
        // 也就是每一帧都假装第一项奖励被按下，玩家选的第二、三项会被立刻打回去。
        _hotKeys = new();
        _events.Reset();

        if (action == CampaignSelectionAction.None)
        {
            return;
        }

        Result = action;
        IsFinished = true;
    }

    /// <inheritdoc/>
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // 参考在开屏前把整屏刷黑，背景图只占 640x480 的中间那块，四周留黑边。
        frame.Pixels.Clear();
        _dialog.Draw(frame.Pixels);
        frame.InvalidateAll();
    }
}
