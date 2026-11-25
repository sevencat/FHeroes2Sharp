using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The pictures the scenario info screen is made of: the metallic box the map name sits in, the row of difficulty
/// icons with the <c>NGEXTRA</c> cursor over it, and the three buttons, <c>Game::SelectScenario()</c>
/// (<c>game_scenarioinfo.cpp:104-190</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在 <c>Game::SelectScenario()</c> 里按 <c>isEvilInterface</c> 挑好、从 <c>generateDefaultImages()</c> 造出来的
/// 图里裁（难度图标与金属文本框见 <see cref="ScenarioInfoIcons"/>），这里把「挑哪一帧」收拢成一个可注入的对象，
/// 让对话框本体不必知道 <c>ICN::</c> 与界面风格。
/// </para>
/// <para>
/// 懒加载与 <see cref="ScenarioDialogArtwork"/> 一致：第一次用到才取，取不到就抛，因为这一屏缺任何一张图都没法看。
/// </para>
/// </remarks>
public sealed class ScenarioInfoArtwork : IPlayersInfoArtwork
{
    /// <summary>Frame of <c>NGEXTRA</c> that marks the hovered difficulty, <c>ICN::NGEXTRA, 62</c>.</summary>
    public const uint DifficultyCursorFrame = 62;

    private readonly bool isEvilInterface;

    private readonly Sprite?[] difficultyIcons = new Sprite?[ScenarioInfoIcons.DifficultyFileNames.Length];

    private Sprite? scenarioBox;
    private Sprite? difficultyCursor;
    private Sprite? mapSelectReleased;
    private Sprite? mapSelectPressed;
    private Sprite? okayIcon;
    private Sprite? cancelIcon;

    /// <summary>
    /// Creates the artwork of one interface style.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil (Necromancer) interface is in use.</param>
    public ScenarioInfoArtwork(bool isEvilInterface) => this.isEvilInterface = isEvilInterface;

    /// <summary>Whether the evil interface is in use.</summary>
    public bool IsEvilInterface => isEvilInterface;

    /// <summary>The metallic box of the map name, <c>METALLIC_BORDERED_TEXTBOX_GOOD</c> / <c>_EVIL</c>.</summary>
    public Sprite ScenarioBox => scenarioBox ??= LoadScenarioBox();

    /// <summary>The cursor that marks the difficulty under the mouse, <see cref="DifficultyCursorFrame"/> of <c>NGEXTRA</c>.</summary>
    public Sprite DifficultyCursor => difficultyCursor ??= LoadDifficultyCursor();

    /// <summary>
    /// The icon of one difficulty: <c>DIFFICULTY_ICON_*</c>, the good state for both interfaces because the row keeps its
    /// colours (only the box and the buttons follow the interface).
    /// </summary>
    /// <param name="difficulty">The difficulty to draw.</param>
    public Sprite GetDifficultyIcon(GameDifficulty difficulty)
    {
        int index = (int)difficulty;

        if ((uint)index >= difficultyIcons.Length)
        {
            index = 0;
        }

        return difficultyIcons[index] ??= LoadDifficultyIcon(index);
    }

    /// <summary>The "Select Maps" button of the map name box, <c>BUTTON_MAP_SELECT_GOOD</c> / <c>_EVIL</c>.</summary>
    public Sprite MapSelectReleased => mapSelectReleased ??= LoadButton(IcnType.ButtonMapSelectGood, IcnType.ButtonMapSelectEvil);

    /// <summary>The pressed look of the "Select Maps" button.</summary>
    public Sprite MapSelectPressed => mapSelectPressed ??= LoadButtonPressed(IcnType.ButtonMapSelectGood, IcnType.ButtonMapSelectEvil);

    /// <summary>The okay button, <c>BUTTON_SMALL_OKAY_GOOD</c> / <c>_EVIL</c>.</summary>
    public Sprite OkayIcon => okayIcon ??= LoadButton(IcnType.ButtonSmallOkayGood, IcnType.ButtonSmallOkayEvil);

    /// <summary>The cancel button, <c>BUTTON_SMALL_CANCEL_GOOD</c> / <c>_EVIL</c>.</summary>
    public Sprite CancelIcon => cancelIcon ??= LoadButton(IcnType.ButtonSmallCancelGood, IcnType.ButtonSmallCancelEvil);

    /// <inheritdoc/>
    public Sprite GetIcon(uint frame) => GetFrame(IcnType.Ngextra, frame);

    private Sprite LoadScenarioBox()
    {
        string fileName = isEvilInterface
            ? ScenarioInfoIcons.MetallicTextboxEvilFileName
            : ScenarioInfoIcons.MetallicTextboxGoodFileName;

        Sprite? box = GameAssets.GetImage(fileName, 0) ?? GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Nghsbkg), 0);

        return box ?? throw new InvalidOperationException($"The scenario name box ({fileName}) is missing from the game data.");
    }

    private Sprite LoadDifficultyCursor()
    {
        Sprite? cursor = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Ngextra), (int)DifficultyCursorFrame);

        return cursor ?? throw new InvalidOperationException("NGEXTRA frame 62, the scenario info difficulty cursor, is missing from the game data.");
    }

    private Sprite LoadDifficultyIcon(int difficulty)
    {
        // The icon keeps the good-interface colours on the evil interface too: the reference blits frame 0 of the
        // DIFFICULTY_ICON_* table and only recolours it in getDifficultyIcon() of the info dialog, not here.
        Sprite? icon = GameAssets.GetImage(ScenarioInfoIcons.DifficultyFileNames[difficulty], 0);

        return icon ?? throw new InvalidOperationException(
            $"The difficulty icon {ScenarioInfoIcons.DifficultyFileNames[difficulty]} cannot be cut out of NGHSBKG.");
    }

    private Sprite LoadButton(IcnType good, IcnType evil) =>
        GetFrame(isEvilInterface ? evil : good, 0);

    /// <summary>
    /// The second frame of a small button is the pressed look in this game's archives, but the map select button has
    /// only one frame in some releases, so a missing second frame falls back to the released one.
    /// </summary>
    private Sprite LoadButtonPressed(IcnType good, IcnType evil)
    {
        IcnType type = isEvilInterface ? evil : good;
        string fileName = FileNameOf(type);

        return GameAssets.GetImage(fileName, 1) ?? GetFrame(type, 0);
    }

    private static Sprite GetFrame(IcnType type, uint frame)
    {
        string fileName = FileNameOf(type);

        return GameAssets.GetImage(fileName, (int)frame)
               ?? throw new InvalidOperationException($"{fileName} has no frame {frame}.");
    }

    /// <summary>
    /// The asset name of an ICN id, including the four tables the engine draws at run time.
    /// </summary>
    /// <param name="type">The ICN.</param>
    /// <remarks>
    /// <see cref="IcnCatalog.GetFileName"/> 对 <see cref="IcnType.LastValidFileIcn"/> 之后的 id 一律回 <c>CUSTOM</c>
    /// （参考的 <c>icn.cpp:224</c> 也这样，但它的 <c>icnFileNames</c> 表里这些 id 是有名字的，<c>loadICN</c> 才排得到
    /// <c>generateDefaultImages()</c>）。本项目把名字留在各自的生成器里，这里负责把 id 接过去。
    /// </remarks>
    private static string FileNameOf(IcnType type) => GameAssets.GetFileName(type);
}
