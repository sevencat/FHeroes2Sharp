using FHeroes2.App.Fonts;
using FHeroes2.App.Localization;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The campaign difficulty window of <c>Game::setCampaignDifficulty()</c> (<c>game_campaign.cpp:900-1130</c>)
/// as a window the host pumps until it closes.
/// </summary>
/// <remarks>
/// <para>
/// 窗口本体在 UI 层（<see cref="CampaignDifficultyView"/>），这里只做三件事：按界面风格挑素材、把输入转给窗口、
/// 把「确定」这一下变成 <see cref="CurrentDifficulty"/> 的最终值。
/// </para>
/// <para>
/// 素材有一半是 fheroes2 自己造的：<c>DIFFICULTY_ICON_*</c> 从 <c>NGHSBKG</c> 第一帧裁出，
/// <c>BUTTON_SMALL_OKAY_GOOD/EVIL</c> 是现做的按钮表。前者由 <see cref="ScenarioInfoIcons"/> 补齐，缺了就画空白；
/// 后者退回原版带字的 <c>SPANBTN</c>/<c>SPANBTNE</c>，再没有才现场拼一对。只有选择框（<c>NGEXTRA</c> 62 帧）
/// 缺了才返回 <see langword="null"/>，因为那时玩家看不出选中了哪一档。
/// </para>
/// </remarks>
internal sealed class CampaignDifficultyScreen : IModalScreen
{
    private static readonly Point OffScreen = new(-1, -1);

    private readonly CampaignDifficultyView _view;

    private Point? _cursor;

    private CampaignDifficultyScreen(CampaignDifficultyView view) => _view = view;

    /// <summary>The difficulty as last shown by the window, one of <see cref="CampaignDifficulty"/>.</summary>
    public int CurrentDifficulty => _view.CurrentDifficulty;

    /// <summary>The window below, for the host's own drawing checks.</summary>
    public CampaignDifficultyView View => _view;

    /// <inheritdoc/>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// Builds the window with the pictures of the installed game data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 三张难度图标 <c>DIFFICULTY_ICON_EASY/NORMAL/HARD</c> 是 fheroes2 自带的资源，原版 AGG 里没有；缺的时候
    /// 参考会画一张空图（<c>Assets::getImage</c> 对缺失资源返回空 sprite），这里同样画一张同尺寸的透明图，
    /// 窗口照开、选择框照点，只是图标区是空的。
    /// </para>
    /// <para>
    /// <c>BUTTON_SMALL_OKAY_GOOD/EVIL</c> 也不在 AGG 里，与战役选关屏的按钮一样现做
    /// （<c>createCampaignButtonSet()</c> 的路子，见 <see cref="CampaignButtonsArtwork"/>）。
    /// </para>
    /// <para>
    /// 只有 <c>NGEXTRA</c> 第 62 帧的选择框缺了就返回 <see langword="null"/>：没有它玩家看不出选中了哪一档。
    /// </para>
    /// </remarks>
    /// <param name="fonts">The fonts of the window.</param>
    /// <param name="screenSize">The size of the screen the window is centred on.</param>
    /// <param name="currentDifficulty">The difficulty to preselect, one of <see cref="CampaignDifficulty"/>.</param>
    /// <param name="text">Translations of the labels, needed for the button that is made here.</param>
    /// <returns>The window, or <see langword="null"/> when the game data lacks the selection box.</returns>
    public static CampaignDifficultyScreen? TryCreate(
        GameFonts fonts, Size screenSize, int currentDifficulty, GameText text)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(text);

        bool isEvilInterface = Settings.Get().IsEvilInterfaceEnabled;
        int frame = isEvilInterface ? 1 : 0;

        Sprite[] icons =
        [
            LoadIcon(difficulty: 0, frame),
            LoadIcon(difficulty: 1, frame),
            LoadIcon(difficulty: 2, frame),
        ];

        Sprite? selection = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Ngextra), 62);

        if (selection is null)
        {
            GameLog.Warn("The game data lacks frame 62 of NGEXTRA, the selection box of the campaign difficulty window.");

            return null;
        }

        (Sprite okayReleased, Sprite okayPressed) = LoadOkayButton(isEvilInterface, text);

        CampaignDifficultyView? view = CampaignDifficultyView.TryCreate(
            screenSize, fonts.Normal, fonts.Yellow, icons, selection, okayReleased, okayPressed, currentDifficulty);

        return view is null ? null : new CampaignDifficultyScreen(view);
    }

    /// <summary>A difficulty icon, or a transparent one of the same size when the data does not have it.</summary>
    /// <param name="difficulty">The index of the icon in <see cref="ScenarioInfoIcons.DifficultyFileNames"/>.</param>
    /// <param name="frame">The frame: 0 for the good interface, 1 for the evil one.</param>
    /// <returns>The icon, never <see langword="null"/>.</returns>
    /// <remarks>
    /// <c>DIFFICULTY_ICON_*</c> 不是档案条目，是 <see cref="ScenarioInfoIcons"/> 从 <c>NGHSBKG</c> 第一帧裁出来的
    /// 65×65 小块（<c>game_assets.cpp:3993-4042</c>），所以按它给的缓存名取图，不能拿 <see cref="IcnType"/>
    /// 去查（那会落到 <c>CUSTOM</c>）。
    /// </remarks>
    private static Sprite LoadIcon(int difficulty, int frame)
    {
        string name = ScenarioInfoIcons.DifficultyFileNames[difficulty];

        if (GameAssets.GetImage(name, frame) is { } sprite)
        {
            return sprite;
        }

        GameLog.Warn(
            $"The game data lacks frame {frame} of {name}, the campaign difficulty icons cut out of NGHSBKG; the window draws a blank icon there.");

        Sprite blank = new(CampaignDifficultyView.IconSize, CampaignDifficultyView.IconSize);
        blank.Reset();

        return blank;
    }

    /// <summary>
    /// The two states of the okay button, taken from the archive or made here when it is not installed.
    /// </summary>
    /// <remarks>
    /// <c>BUTTON_SMALL_OKAY_GOOD/EVIL</c> 是 fheroes2 生成的按钮表，原版 AGG 里没有。端口既有的做法是取原版
    /// 那张带「OKAY」字的 <c>SPANBTN.ICN</c> / <c>SPANBTNE.ICN</c>（<see cref="VirtualKeyboardArtwork"/> 与
    /// 战斗设置窗都这么干），再没有才照 <c>createCampaignButtonSet()</c> 的路子用 <c>EMPTY_*_BUTTON</c> 底板
    /// 加 <c>STONEBAK*</c> 底纹现做一对。
    /// </remarks>
    /// <param name="isEvilInterface">Whether the gray plates and the evil stone texture are used.</param>
    /// <param name="text">Translations of the labels.</param>
    /// <returns>The released and the pressed sprite.</returns>
    private static (Sprite Released, Sprite Pressed) LoadOkayButton(bool isEvilInterface, GameText text)
    {
        string name = GameAssets.GetFileName(isEvilInterface ? IcnType.Spanbtne : IcnType.Spanbtn);

        Sprite? released = GameAssets.GetImage(name, 0);
        Sprite? pressed = GameAssets.GetImage(name, 1);

        if (released is not null)
        {
            return (released, pressed ?? released);
        }

        DialogButtonTemplate template = isEvilInterface ? DialogButtonTemplate.Evil : DialogButtonTemplate.Good;
        IcnType stoneType = isEvilInterface ? IcnType.StonebakEvil : IcnType.Stonebak;
        Sprite? stone = GameAssets.GetImage(GameAssets.GetFileName(stoneType), 0);

        if (stone is null)
        {
            GameLog.Warn(
                $"The game data lacks {IcnCatalog.GetFileName(stoneType)}, the texture of the campaign difficulty okay button; it is drawn without one.");
        }

        return ButtonFactory.GetTextAdaptedSprite(text.Get("OKAY"), template, stone);
    }

    /// <inheritdoc/>
    public void SetCursor(Point? position)
    {
        _cursor = position;
        _view.SetCursor(position);
    }

    /// <inheritdoc/>
    public void SetLeftButton(bool down)
    {
        CampaignDifficultyChoice choice = _view.SetLeftButton(down, _cursor ?? OffScreen);

        if (choice.Action == CampaignDifficultyAction.Okay)
        {
            IsFinished = true;
        }
    }

    /// <inheritdoc/>
    public void SetRightButton(bool down)
    {
        // 参考的难度窗没有右键帮助，右键什么也不做。
    }

    /// <inheritdoc/>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (hotKey is ButtonHotKey.Okay or ButtonHotKey.Cancel)
        {
            // 参考的循环里 Okay 与 Cancel 都只是关窗，难度取窗口当前值。
            IsFinished = true;
        }
    }

    /// <inheritdoc/>
    public void PressKey(Key key, KeyModifier modifier)
    {
    }

    /// <inheritdoc/>
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
    }

    /// <inheritdoc/>
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _view.Draw(frame.Pixels);
        frame.InvalidateAll();
    }
}
