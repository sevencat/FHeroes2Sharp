using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The <c>BUTTON_EXIT_PUZZLE_DIM_DOOR_*</c> and <c>BUTTON_VIEWWORLD_EXIT_*</c> tables of
/// <c>generateDefaultImages</c> (<c>src/fheroes2/game/game_assets.cpp:946-989</c>), the original-resources branch.
/// </summary>
/// <remarks>
/// <para>
/// 这两对按钮在原资材分支里只是别处的两帧：Dimension Door 选择方框的 EXIT 取 <c>LGNDXTRA</c>（邪派 <c>LGNDXTRE</c>）
/// 的第 4、5 帧，View World 的 EXIT 取同一图集的第 2、3 帧。非原资材分支是 70x25 现画的按钮，本项目一律走原资材
/// 分支（与 <see cref="SmallOkayCancelButtons"/> 同一做法）。
/// </para>
/// <para>
/// 参考的 <c>icnFileNames</c> 表里这些 id 是有名字的（<c>icn.cpp:252-256</c>），名字后面才轮到
/// <c>generateDefaultImages()</c>；本项目的 <see cref="Assets.GetFileName"/> 负责把 id 接到这里的文件名常量上。
/// </para>
/// </remarks>
public static class ExitButtons
{
    /// <summary>The name <c>BUTTON_EXIT_PUZZLE_DIM_DOOR_GOOD</c> is cached under.</summary>
    public const string PuzzleDimDoorGoodFileName = "BUTTON_EXIT_PUZZLE_DIM_DOOR_GOOD.ICN";

    /// <summary>The name <c>BUTTON_EXIT_PUZZLE_DIM_DOOR_EVIL</c> is cached under.</summary>
    public const string PuzzleDimDoorEvilFileName = "BUTTON_EXIT_PUZZLE_DIM_DOOR_EVIL.ICN";

    /// <summary>The name <c>BUTTON_VIEWWORLD_EXIT_GOOD</c> is cached under.</summary>
    public const string ViewWorldGoodFileName = "BUTTON_VIEWWORLD_EXIT_GOOD.ICN";

    /// <summary>The name <c>BUTTON_VIEWWORLD_EXIT_EVIL</c> is cached under.</summary>
    public const string ViewWorldEvilFileName = "BUTTON_VIEWWORLD_EXIT_EVIL.ICN";

    /// <summary>Released frame of the Dimension Door button in <c>LGNDXTRA</c> / <c>LGNDXTRE</c>.</summary>
    private const int PuzzleDimDoorReleasedFrame = 4;

    /// <summary>Pressed frame of the Dimension Door button.</summary>
    private const int PuzzleDimDoorPressedFrame = 5;

    /// <summary>Released frame of the View World button.</summary>
    private const int ViewWorldReleasedFrame = 2;

    /// <summary>Pressed frame of the View World button.</summary>
    private const int ViewWorldPressedFrame = 3;

    /// <summary>
    /// Builds the two frames of one of the four tables.
    /// </summary>
    /// <param name="fileName">Asset name, one of the constants of this class.</param>
    /// <returns>
    /// The released frame followed by the pressed one, or <see langword="null"/> when
    /// <paramref name="fileName"/> is not one of the four tables or the archive is too short for it.
    /// </returns>
    public static Sprite?[]? Build(string fileName)
    {
        if (string.Equals(fileName, PuzzleDimDoorGoodFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Take(IcnType.Lgndxtra, PuzzleDimDoorReleasedFrame, PuzzleDimDoorPressedFrame);
        }

        if (string.Equals(fileName, PuzzleDimDoorEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Take(IcnType.Lgndxtre, PuzzleDimDoorReleasedFrame, PuzzleDimDoorPressedFrame);
        }

        if (string.Equals(fileName, ViewWorldGoodFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Take(IcnType.Lgndxtra, ViewWorldReleasedFrame, ViewWorldPressedFrame);
        }

        if (string.Equals(fileName, ViewWorldEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Take(IcnType.Lgndxtre, ViewWorldReleasedFrame, ViewWorldPressedFrame);
        }

        return null;
    }

    /// <summary>Takes two frames of <paramref name="type"/>, released first.</summary>
    /// <param name="type">The archive entry the frames come from.</param>
    /// <param name="releasedFrame">Frame of the released state.</param>
    /// <param name="pressedFrame">Frame of the pressed state.</param>
    private static Sprite?[]? Take(IcnType type, int releasedFrame, int pressedFrame)
    {
        string originalName = IcnCatalog.GetFileName(type);

        if (Assets.GetImageCount(originalName) < pressedFrame + 1)
        {
            return null;
        }

        if (Assets.GetImage(originalName, releasedFrame) is not { } released ||
            Assets.GetImage(originalName, pressedFrame) is not { } pressed)
        {
            return null;
        }

        return new Sprite?[] { released, pressed };
    }
}
