using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Gui;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Battle;

/// <summary>What the player asked for in the battle result window, the reference's <c>btn</c>.</summary>
public enum BattleSummaryWindowAction
{
    /// <summary>Nothing yet, the window stays open.</summary>
    None = 0,

    /// <summary>The OK button or the close hotkey — the reference's <c>btn = 0</c>.</summary>
    Close = 1,

    /// <summary>The restart button — the reference's <c>btn = 1</c>.</summary>
    Restart = 2,
}

/// <summary>
/// The battle result window, <c>Battle::Arena::DialogBattleSummary()</c> (<c>battle_dialogs.cpp:484-783</c>).
/// </summary>
/// <remarks>
/// <para>
/// 内容与参考一一对应：一块 <c>WINLOSE</c> 动画框、标题、可选的投降赔款与战功、双方阵亡部队各一行、右下
/// （或正中）的 OK 与可选的重新开始，以及胜利方接收战利品的第二段界面（<c>WINLOSEB</c>）。
/// </para>
/// <para>
/// <b>与参考的差异</b>（都是本项目的分层约定，不改变玩家看到的流程）：
/// </para>
/// <list type="bullet">
/// <item>参考把两段界面写在一个模态循环里，端口拆成 <see cref="ProcessEvents"/> 与
/// <see cref="ProcessArtifactEvents"/> 两个入口，循环交给调用方（与 <see cref="BattleSurrenderWindow"/> 一致）。</item>
/// <item>参考在换神器时播音效（<c>AudioManager::PlaySound( M82::PickupArtifact )</c>）、在右键时弹神器说明，
/// 端口还没有音效层与说明弹窗，这两步留给接线的那一档；<see cref="CurrentArtifact"/> 已经暴露出来，调用方
/// 可以自己弹说明。</item>
/// <item>参考用 <c>ImageRestorer</c> 局部还原背景，端口每帧重画整块窗口，因此不需要还原器。</item>
/// </list>
/// </remarks>
public sealed class BattleSummaryWindow
{
    /// <summary>Width of the dialog's active area, <c>bsTextWidth + 32</c>.</summary>
    public const int Width = 335;

    /// <summary>Height of the dialog's active area.</summary>
    public const int Height = 424;

    /// <summary>Left edge of the border art inside <c>WINLOSE</c>/<c>WINLOSEB</c>, <c>animationBorderRoi.x</c> = 43.</summary>
    private const int BorderSourceX = 43;

    /// <summary>Top edge of the border art inside the two backgrounds, <c>animationBorderRoi.y</c> = 32.</summary>
    private const int BorderSourceY = 32;

    /// <summary>Width of the animation border, <c>animationBorderRoi.width</c> = 231.</summary>
    private const int BorderWidth = 231;

    /// <summary>Height of the animation border, <c>animationBorderRoi.height</c> = 133.</summary>
    private const int BorderHeight = 133;

    /// <summary>Horizontal offset of the animation inside the active area, <c>( roi.width - 231 ) / 2 + 4</c>.</summary>
    private const int AnimationOffsetX = ((Width - BorderWidth) / 2) + 4;

    /// <summary>Vertical offset of the animation, <c>roi.y + 21</c>.</summary>
    private const int AnimationOffsetY = 21;

    /// <summary>Horizontal inset of the summary block, <c>roi.x + 11</c>.</summary>
    private const int SummaryInset = 11;

    /// <summary>Vertical offset of the summary block, <c>bsTextYOffset</c> = 160.</summary>
    private const int SummaryOffsetY = 160;

    /// <summary>Inset of the victim line inside the summary block, <c>+ 13</c>.</summary>
    private const int CasualtiesInset = 13;

    /// <summary>Width taken away from the summary block for the victim line, <c>roi.width - 47</c>.</summary>
    private const int CasualtiesLoss = 47;

    /// <summary>Offset of the "Attacker" caption, <c>casualtiesOffsetY + 15</c>.</summary>
    private const int AttackerCaptionOffset = 15;

    /// <summary>Offset of the attacker's victims, <c>casualtiesOffsetY + 36</c>.</summary>
    private const int AttackerLineOffset = 36;

    /// <summary>Offset of the "Defender" caption, <c>casualtiesOffsetY + 75</c>.</summary>
    private const int DefenderCaptionOffset = 75;

    /// <summary>Offset of the defender's victims, <c>casualtiesOffsetY + 96</c>.</summary>
    private const int DefenderLineOffset = 96;

    /// <summary>Vertical offset of the casualties block inside the summary block, <c>+ 96</c>.</summary>
    private const int CasualtiesOffset = 96;

    /// <summary>Offset of the artifact block below the casualties, <c>casualtiesOffsetY + 38</c>.</summary>
    private const int ArtifactsOffset = 38;

    /// <summary>Space between the artifact border and its name, <c>+ 7</c>.</summary>
    private const int ArtifactNameOffset = 7;

    /// <summary>Horizontal margin of the buttons when there are two of them, <c>{ 23, 5 }</c>.</summary>
    private static readonly Point ButtonMargin = new(23, 5);

    /// <summary>Margin of the OK button when it is alone in the centre, <c>{ 0, 5 }</c>.</summary>
    private static readonly Point CenterButtonMargin = new(0, 5);

    /// <summary>The column width of the summary texts, <c>bsTextWidth</c> = 303.</summary>
    private const int TextWidth = Width - (SummaryInset * 2);

    private readonly StandardWindow _window;
    private readonly IViewWorldSpriteSource _sprites;
    private readonly IMiniMonsterSprites _miniMonsters;
    private readonly BattleSummaryText _text;
    private readonly BattleDialogAnimationSequence _sequence = new();
    private readonly IReadOnlyList<Troop> _attackerKilled;
    private readonly IReadOnlyList<Troop> _defenderKilled;

    private readonly Sprite? _border;
    private readonly Sprite? _artifactBorder;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite? _restart;
    private readonly bool _highlightTitle;

    private IReadOnlyList<ArtifactType> _artifacts = Array.Empty<ArtifactType>();
    private int _artifactCursor;
    private bool _isShowingArtifacts;
    private bool _winnerIsHuman;
    private bool _headerDrawn;
    private ArtifactType? _previousArtifact;
    private string _artifactHeader = string.Empty;

    private BattleSummaryWindow(StandardWindow window, IViewWorldSpriteSource sprites, IMiniMonsterSprites miniMonsters,
        BattleSummaryText text, IReadOnlyList<Troop> attackerKilled, IReadOnlyList<Troop> defenderKilled,
        Rect animationArea, Rect summaryArea, bool highlightTitle, Sprite? border, Sprite? artifactBorder,
        ButtonSprite okay, ButtonSprite? restart)
    {
        _window = window;
        _sprites = sprites;
        _miniMonsters = miniMonsters;
        _text = text;
        _attackerKilled = attackerKilled;
        _defenderKilled = defenderKilled;
        AnimationArea = animationArea;
        SummaryArea = summaryArea;
        _highlightTitle = highlightTitle;
        _border = border;
        _artifactBorder = artifactBorder;
        _okay = okay;
        _restart = restart;
        AnimationFrame = 1;

        foreach (BattleSummaryAnimationStep step in text.Animation)
        {
            _sequence.Push(step.Icn, step.Loop);
        }

        // 参考在 GetSummaryParams 之后才压入循环的胜利动画：具体结局播完就一直放它。
        _sequence.Push(IcnType.Wincmbt, true);
    }

    /// <summary>The dialog's active area, <c>background.activeArea()</c>.</summary>
    public Rect Area => _window.ActiveArea;

    /// <summary>Where the <c>WINCMBT</c>/<c>WINLOSE</c> frames go, <c>animationRoi</c>.</summary>
    public Rect AnimationArea { get; }

    /// <summary>The block holding the texts, <c>summaryRoi</c>.</summary>
    public Rect SummaryArea { get; }

    /// <summary>The texts and the animations the window shows.</summary>
    public BattleSummaryText Text => _text;

    /// <summary>The OK button, exposed for hit tests and for tests.</summary>
    public ButtonSprite OkayButton => _okay;

    /// <summary>The restart button, only present when the caller allowed a restart.</summary>
    public ButtonSprite? RestartButton => _restart;

    /// <summary>The current animation frame; starts at 1, the frame <c>setupAnimation()</c> blits at once.</summary>
    public uint AnimationFrame { get; private set; }

    /// <summary>Whether the artifact hand-over section replaced the summary.</summary>
    public bool IsShowingArtifacts => _isShowingArtifacts;

    /// <summary>The artifact on screen during the hand-over section, <see langword="null"/> otherwise.</summary>
    public ArtifactType? CurrentArtifact { get; private set; }

    /// <summary>The yellow line above the artifact icon, empty outside the hand-over section.</summary>
    public string ArtifactHeader => _artifactHeader;

    /// <summary>
    /// Builds the window, or returns <see langword="null"/> when the border art is missing from the data.
    /// </summary>
    /// <param name="sprites">Where the backgrounds and the animation frames come from.</param>
    /// <param name="miniMonsters">Where the small portraits of the casualties come from.</param>
    /// <param name="text">Texts and animations from <see cref="BattleSummaryText.Create"/>.</param>
    /// <param name="attackerKilled">Stacks the attacker lost, <c>Force::GetKilledTroops()</c>.</param>
    /// <param name="defenderKilled">Stacks the defender lost.</param>
    /// <param name="highlightTitle">Whether the title is yellow (the human side won) rather than white.</param>
    /// <param name="allowToRestart">Whether the scenario allows restarting the battle.</param>
    /// <param name="okayReleased">Released OK artwork — <c>SYSTEM.ICN</c> frame 1 in this data set.</param>
    /// <param name="okayPressed">Pressed OK artwork.</param>
    /// <param name="restartReleased">Released restart artwork, required when <paramref name="allowToRestart"/>.</param>
    /// <param name="restartPressed">Pressed restart artwork, required when <paramref name="allowToRestart"/>.</param>
    /// <param name="screen">Screen the window is centered on.</param>
    /// <returns>The window, or <see langword="null"/> when the restart button has no artwork.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static BattleSummaryWindow? TryCreate(IViewWorldSpriteSource sprites, IMiniMonsterSprites miniMonsters,
        BattleSummaryText text, IReadOnlyList<Troop> attackerKilled, IReadOnlyList<Troop> defenderKilled,
        bool highlightTitle, bool allowToRestart, Sprite okayReleased, Sprite okayPressed,
        Sprite? restartReleased, Sprite? restartPressed, Size screen)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(miniMonsters);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(attackerKilled);
        ArgumentNullException.ThrowIfNull(defenderKilled);
        ArgumentNullException.ThrowIfNull(okayReleased);
        ArgumentNullException.ThrowIfNull(okayPressed);

        StandardWindow window = StandardWindow.CreateCentered(new Size(Width, Height), renderBackground: true, screen);
        Rect area = window.ActiveArea;

        Rect animationArea = new(area.X + AnimationOffsetX, area.Y + AnimationOffsetY, BorderWidth, BorderHeight);
        Rect summaryArea = new(area.X + SummaryInset, area.Y + SummaryOffsetY, TextWidth, Height - SummaryOffsetY);

        ButtonSprite okay = new(okayReleased, okayPressed);
        ButtonSprite? restart = null;

        if (allowToRestart)
        {
            if (restartReleased is null || restartPressed is null)
            {
                // 参考的按钮图永远在数据里；端口缺图就不给重开按钮，免得画出半个按钮。
                GameLog.Warn("The restart button has no artwork, the battle cannot be restarted from this window.");

                return null;
            }

            restart = new ButtonSprite(restartReleased, restartPressed);

            // 参考：两个按钮时分列左右下角。
            Point okayPosition = window.RenderPosition(ButtonMargin, new Size(okayReleased.Width, okayReleased.Height),
                WindowPadding.BottomLeft);
            okay.SetPosition(okayPosition.X, okayPosition.Y);

            Point restartPosition = window.RenderPosition(ButtonMargin,
                new Size(restartReleased.Width, restartReleased.Height), WindowPadding.BottomRight);
            restart.SetPosition(restartPosition.X, restartPosition.Y);
        }
        else
        {
            Point okayPosition = window.RenderPosition(CenterButtonMargin,
                new Size(okayReleased.Width, okayReleased.Height), WindowPadding.BottomCenter);
            okay.SetPosition(okayPosition.X, okayPosition.Y);
        }

        return new BattleSummaryWindow(window, sprites, miniMonsters, text, attackerKilled, defenderKilled,
            animationArea, summaryArea, highlightTitle, sprites.GetSprite(IcnType.Winlose, 0),
            sprites.GetSprite(IcnType.Winloseb, 0), okay, restart);
    }

    /// <summary>
    /// Advances the backdrop animation when the dialog delay has elapsed, <c>updateAnimation()</c>.
    /// </summary>
    /// <returns><see langword="true"/> when the animation advanced and the window should be repainted.</returns>
    public bool Update()
    {
        if (!GameDelays.ValidateAnimationDelay(DelayType.BattleDialogDelay) || _sequence.NextFrame())
        {
            return false;
        }

        AnimationFrame = _sequence.AdvanceFrame();

        return true;
    }

    /// <summary>
    /// Starts the artifact hand-over section, <c>for ( Artifact &amp; art : artifact )</c>'s prologue.
    /// </summary>
    /// <param name="artifacts">Artifacts of the defeated side, in bag order.</param>
    /// <param name="winnerIsHuman">
    /// Whether the winner is a human player; an AI winner only takes the ultimate artifacts, so the rest are skipped
    /// without being shown — the reference's <c>winner-&gt;isControlAI()</c> check.
    /// </param>
    /// <returns><see langword="true"/> when there is at least one artifact to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artifacts"/> is <see langword="null"/>.</exception>
    public bool BeginArtifacts(IReadOnlyList<ArtifactType> artifacts, bool winnerIsHuman)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        _artifacts = artifacts;
        _winnerIsHuman = winnerIsHuman;
        _artifactCursor = -1;
        _previousArtifact = null;
        _headerDrawn = false;
        _artifactHeader = string.Empty;
        CurrentArtifact = null;
        _isShowingArtifacts = true;

        if (AdvanceArtifact())
        {
            // 参考进入这一段时把 OK 挪到底部正中：神器块比阵亡列表矮，右下角会悬空。
            Point position = _window.RenderPosition(CenterButtonMargin,
                new Size(_okay.Area.Width, _okay.Area.Height), WindowPadding.BottomCenter);
            _okay.SetPosition(position.X, position.Y);

            return true;
        }

        _isShowingArtifacts = false;

        return false;
    }

    /// <summary>Paints the window: stone frame, backdrop, summary or artifact section, then the buttons.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);
        DrawAnimation(target);

        if (_isShowingArtifacts)
        {
            DrawArtifacts(target);
        }
        else
        {
            DrawSummary(target);
        }

        _okay.Draw(target);
        _restart?.Draw(target);
    }

    /// <summary>
    /// Consumes one frame of input of the summary section, the body of the reference's modal loop.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>What the player asked for.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public BattleSummaryWindowAction ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _okay.UpdatePressState(events);
        _restart?.UpdatePressState(events);

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultOkay)
            || GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel)
            || events.MouseClickLeft(_okay.Area))
        {
            return BattleSummaryWindowAction.Close;
        }

        if (_restart is { } restart && events.MouseClickLeft(restart.Area))
        {
            return BattleSummaryWindowAction.Restart;
        }

        return BattleSummaryWindowAction.None;
    }

    /// <summary>
    /// Consumes one frame of input of the artifact section, the body of the inner modal loop.
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns><see langword="true"/> when every artifact has been handed over and the window should close.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public bool ProcessArtifactEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _okay.UpdatePressState(events);

        if (!GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultOkay)
            && !GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel)
            && !events.MouseClickLeft(_okay.Area))
        {
            return false;
        }

        return !AdvanceArtifact();
    }

    /// <summary>Moves to the next artifact that has to be shown, <c>continue</c>ing the ones the winner skips.</summary>
    /// <returns><see langword="true"/> while an artifact is on screen.</returns>
    private bool AdvanceArtifact()
    {
        while (++_artifactCursor < _artifacts.Count)
        {
            ArtifactType artifact = _artifacts[_artifactCursor];

            if (!_winnerIsHuman && !ArtifactLevels.IsUltimate(artifact))
            {
                // AI 只认终极神器，其余连展示都跳过（参考也是直接 continue）。
                continue;
            }

            CurrentArtifact = artifact;

            if (_previousArtifact == artifact)
            {
                // 连续的同一种神器：参考只补一次音效，画面不动。
                continue;
            }

            _previousArtifact = artifact;

            if (ArtifactLevels.IsUltimate(artifact))
            {
                string name = ArtifactTable.Get(artifact).Name;

                _artifactHeader = _winnerIsHuman
                    ? $"As you reach for the {name}, it mysteriously disappears."
                    : $"As your enemy reaches for the {name}, it mysteriously disappears.";
            }
            else if (!_headerDrawn)
            {
                // 普通神器的标题只画一次，后面的神器沿用同一句。
                _artifactHeader = "You have captured an enemy artifact!";
                _headerDrawn = true;
            }

            return true;
        }

        CurrentArtifact = null;
        _isShowingArtifacts = false;

        return false;
    }

    /// <summary>Copies the border art and blits the current animation frame over it.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawAnimation(PixelBuffer target)
    {
        if (_border is { } border)
        {
            ImageOps.Blit(target, new Point(AnimationArea.X - 4, AnimationArea.Y - 4), border.Image,
                new Rect(BorderSourceX, BorderSourceY, BorderWidth, BorderHeight));
        }

        if (_sprites.GetSprite(_sequence.Icn, (int)AnimationFrame) is { } frame)
        {
            ImageOps.BlitSprite(target, new Point(AnimationArea.X, AnimationArea.Y), frame);
        }
    }

    /// <summary>Draws the title, the optional lines and the casualties, <c>fheroes2::Text</c> body of the dialog.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawSummary(PixelBuffer target)
    {
        Rect summary = SummaryArea;
        int offsetY = summary.Y;

        Text title = new(_text.Title, _highlightTitle ? FontType.NormalYellow() : FontType.NormalWhite());

        // 参考的 setUniformVerticalAlignment( false )：标题按贪心换行，不做行均衡。
        title.SetUniformVerticalAlignment(false);
        title.Draw(summary.X, offsetY, summary.Width, target);
        offsetY += title.Height(summary.Width);

        bool hasSurrender = !string.IsNullOrEmpty(_text.SurrenderText);
        bool hasOutcome = !string.IsNullOrEmpty(_text.OutcomeText);

        if (hasSurrender && hasOutcome)
        {
            // 下半段分三份：上面投降赔款，下面战功。
            int remaining = summary.Y + summary.Height - offsetY;
            int third = remaining / 3;

            Text surrender = new(_text.SurrenderText, FontType.NormalWhite());
            surrender.Draw(summary.X, offsetY, summary.Width, target);
            offsetY += third;

            Text outcome = new(_text.OutcomeText, FontType.NormalWhite());
            outcome.Draw(summary.X, offsetY, summary.Width, target);
        }
        else if (hasOutcome || hasSurrender)
        {
            // 只有一段时纵向居中；参考这里用的是默认的行均衡。
            string line = hasOutcome ? _text.OutcomeText : _text.SurrenderText;
            Text text = new(line, FontType.NormalWhite());
            int height = text.Height(summary.Width);

            offsetY += (summary.Height - (offsetY - summary.Y)) / 2 - (height / 2);

            text.Draw(summary.X, offsetY, summary.Width, target);
        }

        int casualtiesOffsetY = summary.Y + CasualtiesOffset;

        DrawCaption("Attacker", summary, casualtiesOffsetY + AttackerCaptionOffset, target);
        DrawCasualties(_attackerKilled, summary, casualtiesOffsetY + AttackerLineOffset, target);

        DrawCaption("Defender", summary, casualtiesOffsetY + DefenderCaptionOffset, target);
        DrawCasualties(_defenderKilled, summary, casualtiesOffsetY + DefenderLineOffset, target);
    }

    /// <summary>Draws one small white caption centred in the summary block, <c>text.draw( x + ..., y )</c>.</summary>
    /// <param name="caption">The text.</param>
    /// <param name="summary">The summary block.</param>
    /// <param name="y">Top edge of the caption.</param>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawCaption(string caption, Rect summary, int y, PixelBuffer target)
    {
        Text text = new(caption, FontType.SmallWhite());
        text.Draw(summary.X + ((summary.Width - text.Width()) / 2), y, target);
    }

    /// <summary>Draws one side's casualties as a full detail line, or "None" centred when nothing died.</summary>
    /// <param name="killed">The stacks that died.</param>
    /// <param name="summary">The summary block.</param>
    /// <param name="y">Top edge of the line.</param>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawCasualties(IReadOnlyList<Troop> killed, Rect summary, int y, PixelBuffer target)
    {
        if (killed.Count == 0)
        {
            DrawCaption("None", summary, y, target);

            return;
        }

        Army.DrawSingleDetailedMonsterLine(killed, summary.X + CasualtiesInset, y,
            summary.Width - CasualtiesLoss, target, _miniMonsters);
    }

    /// <summary>Draws the artifact border, its header and the current artifact icon with its name.</summary>
    /// <param name="target">Frame buffer of the battle screen.</param>
    private void DrawArtifacts(PixelBuffer target)
    {
        Rect summary = SummaryArea;
        Rect artifactArea = new(
            summary.X + ((summary.Width - BorderWidth) / 2),
            summary.Y + CasualtiesOffset + ArtifactsOffset,
            BorderWidth,
            BorderHeight);

        if (_artifactBorder is { } border)
        {
            // 参考的 Copy( 0, 0, width, height )：整张 WINLOSEB 盖住原摘要区。
            ImageOps.Blit(target, new Point(artifactArea.X, artifactArea.Y), border.Image, border.Image.Bounds);
        }

        if (!string.IsNullOrEmpty(_artifactHeader))
        {
            // 参考：从摘要区左上角开始，按摘要区宽度换行。
            new Text(_artifactHeader, FontType.NormalYellow()).Draw(summary.X, summary.Y, summary.Width, target);
        }

        if (CurrentArtifact is not { } artifact)
        {
            return;
        }

        if (_sprites.GetSprite(IcnType.Artifact, (int)ArtifactTypes.FullSpriteIndex(artifact)) is not { } icon)
        {
            return;
        }

        // 参考的 Copy：图标贴在边框内侧 ( 8, 8 )，只拷图像。
        ImageOps.Blit(target, new Point(artifactArea.X + 8, artifactArea.Y + 8), icon.Image, icon.Image.Bounds);

        new Text(ArtifactTable.Get(artifact).Name, FontType.SmallWhite()).Draw(summary.X,
            artifactArea.Y + BorderHeight + ArtifactNameOffset, summary.Width, target);
    }
}
