using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The two tables <c>generateDefaultImages</c> cuts out of the scenario backgrounds for the scenario info screen:
/// the five difficulty icons (<c>game_assets.cpp:3993-4042</c>) and the metallic text box of the difficulty and map
/// name (<c>game_assets.cpp:4043-4093</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/game/game_assets.cpp:3993</c>（<c>DIFFICULTY_ICON_*</c>）与 <c>4043</c>
/// （<c>METALLIC_BORDERED_TEXTBOX_GOOD</c> / <c>_EVIL</c>）。和 <see cref="EmptyButtons"/> 一样，这些「文件」
/// 没有归档条目，<see cref="Assets.GetImage"/> 查归档之前先问 <see cref="Build"/>。
/// </para>
/// <para>
/// 难度图标是 <c>NGHSBKG</c> 第一帧里那排 65×65 小图，恶势力版本由 <c>PAL::GOOD_TO_EVIL_INTERFACE</c>
/// 换色得到；金属边框文本框好/恶两种界面各一张，恶势力那张还要把 <c>CAMPBKGE</c> 里被拉高的边框裁掉 14 行。
/// </para>
/// </remarks>
public static class ScenarioInfoIcons
{
    /// <summary>The name <c>ICN::DIFFICULTY_ICON_EASY</c> is cached under.</summary>
    public const string DifficultyEasyFileName = "DIFFICULTY_ICON_EASY.ICN";

    /// <summary>The name <c>ICN::DIFFICULTY_ICON_NORMAL</c> is cached under.</summary>
    public const string DifficultyNormalFileName = "DIFFICULTY_ICON_NORMAL.ICN";

    /// <summary>The name <c>ICN::DIFFICULTY_ICON_HARD</c> is cached under.</summary>
    public const string DifficultyHardFileName = "DIFFICULTY_ICON_HARD.ICN";

    /// <summary>The name <c>ICN::DIFFICULTY_ICON_EXPERT</c> is cached under.</summary>
    public const string DifficultyExpertFileName = "DIFFICULTY_ICON_EXPERT.ICN";

    /// <summary>The name <c>ICN::DIFFICULTY_ICON_IMPOSSIBLE</c> is cached under.</summary>
    public const string DifficultyImpossibleFileName = "DIFFICULTY_ICON_IMPOSSIBLE.ICN";

    /// <summary>The name <c>ICN::METALLIC_BORDERED_TEXTBOX_GOOD</c> is cached under.</summary>
    public const string MetallicTextboxGoodFileName = "METALLIC_BORDERED_TEXTBOX_GOOD.ICN";

    /// <summary>The name <c>ICN::METALLIC_BORDERED_TEXTBOX_EVIL</c> is cached under.</summary>
    public const string MetallicTextboxEvilFileName = "METALLIC_BORDERED_TEXTBOX_EVIL.ICN";

    /// <summary>The name <c>ICN::BUTTON_MAP_SELECT_GOOD</c> is cached under.</summary>
    public const string MapSelectGoodFileName = "BUTTON_MAP_SELECT_GOOD.ICN";

    /// <summary>The name <c>ICN::BUTTON_MAP_SELECT_EVIL</c> is cached under.</summary>
    public const string MapSelectEvilFileName = "BUTTON_MAP_SELECT_EVIL.ICN";

    /// <summary>Side length of one difficulty icon inside <c>NGHSBKG</c>.</summary>
    private const int DifficultyIconSide = 65;

    /// <summary>Row of the difficulty icons inside <c>NGHSBKG</c>.</summary>
    private const int DifficultyIconTop = 94;

    /// <summary>Released frame of the "Select Maps" button inside <c>NGEXTRA</c>.</summary>
    private const int MapSelectReleasedFrame = 64;

    /// <summary>Pressed frame of the "Select Maps" button inside <c>NGEXTRA</c>.</summary>
    private const int MapSelectPressedFrame = 65;

    /// <summary>Left column of each difficulty icon inside <c>NGHSBKG</c>, indexed by difficulty.</summary>
    private static readonly int[] DifficultyIconLefts = { 24, 101, 177, 254, 331 };

    /// <summary>The file names of the difficulty icons, indexed by <c>GameDifficulty</c>.</summary>
    public static readonly string[] DifficultyFileNames =
    {
        DifficultyEasyFileName,
        DifficultyNormalFileName,
        DifficultyHardFileName,
        DifficultyExpertFileName,
        DifficultyImpossibleFileName,
    };

    /// <summary>Width of the metallic text box, the only frame the reference generates.</summary>
    private const int TextboxWidth = 371;

    /// <summary>Height of the metallic text box.</summary>
    private const int TextboxHeight = 30;

    /// <summary>
    /// Builds the frames of a synthetic table of this class.
    /// </summary>
    /// <param name="fileName">Asset name, one of the constants of this class.</param>
    /// <returns>
    /// The frames (good state first, evil state second for the difficulty icons), or <see langword="null"/> when
    /// <paramref name="fileName"/> is not a table of this class or the archive is too short for it.
    /// </returns>
    /// <remarks>
    /// 参考在资材缺失时让 <c>_icnVsSprite[id]</c> 留空，之后 <c>getImage</c> 返回 <c>errorImage</c>；这里返回
    /// <see langword="null"/>，与 <see cref="EmptyButtons.Build"/> 一致。
    /// </remarks>
    public static Sprite?[]? Build(string fileName)
    {
        for (int difficulty = 0; difficulty < DifficultyFileNames.Length; ++difficulty)
        {
            if (string.Equals(fileName, DifficultyFileNames[difficulty], StringComparison.OrdinalIgnoreCase))
            {
                return BuildDifficultyIcon(difficulty);
            }
        }

        if (string.Equals(fileName, MetallicTextboxGoodFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildMetallicTextbox(isGoodInterface: true);
        }

        if (string.Equals(fileName, MetallicTextboxEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildMetallicTextbox(isGoodInterface: false);
        }

        if (string.Equals(fileName, MapSelectGoodFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildMapSelectButton(isEvilInterface: false);
        }

        if (string.Equals(fileName, MapSelectEvilFileName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildMapSelectButton(isEvilInterface: true);
        }

        return null;
    }

    /// <summary>
    /// <c>ICN::BUTTON_MAP_SELECT_GOOD</c> / <c>_EVIL</c> (<c>game_assets.cpp:752-765</c> 与 <c>1488-1502</c>).
    /// </summary>
    /// <remarks>
    /// 原资材时按钮本身就是 <c>NGEXTRA</c> 的 64、65 两帧（文字烤在图里）；恶势力那张不另取图，参考对整个好界面
    /// 版本做 <c>PAL::GOOD_TO_EVIL_INTERFACE</c> 换色。
    /// </remarks>
    /// <param name="isEvilInterface">Whether the evil interface frames are built.</param>
    private static Sprite?[]? BuildMapSelectButton(bool isEvilInterface)
    {
        string originalName = IcnCatalog.GetFileName(IcnType.Ngextra);

        if (Assets.GetImageCount(originalName) < MapSelectPressedFrame + 1)
        {
            return null;
        }

        if (Assets.GetImage(originalName, MapSelectReleasedFrame) is not { } released ||
            Assets.GetImage(originalName, MapSelectPressedFrame) is not { } pressed)
        {
            return null;
        }

        Sprite releasedFrame = new(released.Image.Clone());
        Sprite pressedFrame = new(pressed.Image.Clone());

        if (isEvilInterface)
        {
            ImageOps.ApplyPalette(releasedFrame.Image, PaletteId.GoodToEvilInterface);
            ImageOps.ApplyPalette(pressedFrame.Image, PaletteId.GoodToEvilInterface);
        }

        return new Sprite?[] { releasedFrame, pressedFrame };
    }

    /// <summary>
    /// <c>ICN::DIFFICULTY_ICON_*</c>: one 65×65 crop of the <c>NGHSBKG</c> background, plus its evil recolour
    /// (<c>game_assets.cpp:3993-4042</c>).
    /// </summary>
    private static Sprite?[]? BuildDifficultyIcon(int difficulty)
    {
        string backgroundName = IcnCatalog.GetFileName(IcnType.Nghsbkg);

        if (Assets.GetImage(backgroundName, 0) is not { } background)
        {
            return null;
        }

        Sprite good = ImageOps.Crop(background, DifficultyIconLefts[difficulty], DifficultyIconTop, DifficultyIconSide, DifficultyIconSide);

        if (good.Image.IsEmpty)
        {
            return null;
        }

        // The crop keeps the (x, y) it was cut at; the reference calls setPosition( 0, 0 ) because the icons are
        // blitted by their absolute position.
        good = new Sprite(good.Image);

        Sprite evil = new(good.Image.Clone());
        ImageOps.ApplyPalette(evil.Image, PaletteId.GoodToEvilInterface);

        return new Sprite?[] { good, evil };
    }

    /// <summary>
    /// <c>ICN::METALLIC_BORDERED_TEXTBOX_GOOD</c> / <c>_EVIL</c> (<c>game_assets.cpp:4043-4093</c>).
    /// </summary>
    /// <remarks>
    /// 好界面直接裁 <c>NGHSBKG</c> 的一块，再把右边盖住内嵌按钮的红纹补上（左右翻转的一小条）；
    /// 恶界面的边框在 <c>CAMPBKGE</c> 里比好界面高 14 行，参考把下半截上移，再用好界面的红纹盖住中间。
    /// </remarks>
    private static Sprite?[]? BuildMetallicTextbox(bool isGoodInterface)
    {
        if (isGoodInterface)
        {
            string backgroundName = IcnCatalog.GetFileName(IcnType.Nghsbkg);

            if (Assets.GetImage(backgroundName, 0) is not { } background)
            {
                return null;
            }

            Sprite box = ImageOps.Crop(background, 24, 40, TextboxWidth, TextboxHeight);

            if (box.Image.IsEmpty)
            {
                return null;
            }

            box = new Sprite(box.Image);

            // Copy red pattern and cover up embedded button.
            Sprite redPart = ImageOps.Crop(background, 80, 45, 81, 19);

            if (redPart.Image.IsEmpty)
            {
                return null;
            }

            ImageOps.Copy(new Sprite(ImageOps.Flip(redPart.Image, true, false)), new Rect(0, 0, 81, 19), box, new Point(284, 5));

            return new Sprite?[] { box };
        }

        string evilBackgroundName = IcnCatalog.GetFileName(IcnType.Campbkge);

        if (Assets.GetImage(evilBackgroundName, 0) is not { } evilBackground)
        {
            return null;
        }

        const int evilOffsetX = 26;
        const int evilOffsetY = 27;
        const int upperPartHeight = 20;

        Sprite evilBox = new(TextboxWidth, TextboxHeight);

        Sprite upperPart = ImageOps.Crop(evilBackground, evilOffsetX, evilOffsetY, TextboxWidth, upperPartHeight);
        if (upperPart.Image.IsEmpty)
        {
            return null;
        }

        ImageOps.Copy(upperPart, new Rect(0, 0, TextboxWidth, upperPartHeight), evilBox, Point.Zero);

        // The box in CAMPBKGE is taller than the one in NGHSBKG, so the lower part is pulled up by 14 rows.
        Sprite lowerPart = ImageOps.Crop(
            evilBackground,
            evilOffsetX,
            evilOffsetY + upperPartHeight + 14,
            TextboxWidth,
            10);
        if (lowerPart.Image.IsEmpty)
        {
            return null;
        }

        ImageOps.Copy(lowerPart, new Rect(0, 0, TextboxWidth, 10), evilBox, new Point(0, upperPartHeight));

        if (Assets.GetImage(MetallicTextboxGoodFileName, 0) is not { } goodBox)
        {
            return null;
        }

        ImageOps.Copy(goodBox, new Rect(6, 5, 359, 19), evilBox, new Point(6, 5));

        return new Sprite?[] { evilBox };
    }
}
