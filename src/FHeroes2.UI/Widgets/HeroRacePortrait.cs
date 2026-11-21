using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The race portrait of a hero, a port of <c>renderHeroRacePortrait()</c>
/// (<c>src/fheroes2/gui/ui_tool.cpp:841-888</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把 <c>ICN::STRIP</c> 的一张背景（索引 4..10，每个种族一张）用 <see cref="ImageOps.SubpixelResize"/>
/// 缩放到目标尺寸，再把 <c>CMBTHROx</c> 的第 1 张（各族的英雄立绘）按水平居中对齐贴上去；
/// <c>Race::RAND</c> 用的是 <c>CMBTHROW</c> 加紫色调色板，也就是「随机」那个问号人像。
/// </para>
/// <para>
/// 偏差：参考的 <c>Assets::getImage()</c> 是带缓存的全局取图接口，端口没有对应物，
/// <see cref="TryRender"/> 每次调用都读一遍两张 ICN，需要按帧重画的调用点应自己缓存结果。
/// </para>
/// </remarks>
public static class HeroRacePortrait
{
    /// <summary>
    /// Builds the portrait of <paramref name="race"/> at the requested size, reading the two ICN files
    /// it needs from <paramref name="assets"/>.
    /// </summary>
    /// <param name="assets">Game resources the ICN files are read from.</param>
    /// <param name="race">Race whose portrait is wanted.</param>
    /// <param name="size">Size of the portrait, <c>portPos</c> of the reference.</param>
    /// <param name="gamePalette">
    /// The game palette in video values, <c>getRGBGamePalette()</c> (<see cref="Palette.FromGamePalette"/>),
    /// used by the resampler to look the interpolated colours up.
    /// </param>
    /// <returns>
    /// The portrait, or <see langword="null"/> when the race has no portrait or an ICN file is missing.
    /// </returns>
    /// <remarks>
    /// 参考在 <c>default</c> 分支里 <c>assert( 0 )</c>（「加了新种族？把上面的逻辑补齐」），端口返回
    /// <see langword="null"/>，让调用点跳过这块绘制而不是崩掉。
    /// </remarks>
    public static Sprite? TryRender(AssetBundle assets, MapRace race, Size size, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (!TryGetArtwork(race, out HeroRaceArtwork artwork))
        {
            return null;
        }

        IcnSpriteFile? strip = assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Strip));
        IcnSpriteFile? heroes = assets.TryReadIcn(IcnCatalog.GetFileName(artwork.Heroes));

        if (strip is null || heroes is null || strip.Count <= artwork.Background || heroes.Count <= 1)
        {
            return null;
        }

        return Compose(strip.Sprites[artwork.Background], heroes.Sprites[1], artwork.ApplyRandomPalette, size, gamePalette);
    }

    /// <summary>
    /// Composes an already loaded background and hero sprite into a portrait, the <c>preparePortrait</c>
    /// lambda of <c>ui_tool.cpp:845-858</c> plus the final copy at <c>:887</c>.
    /// </summary>
    /// <param name="background">The <c>ICN::STRIP</c> entry of the race, scaled to <paramref name="size"/>.</param>
    /// <param name="hero">The <c>CMBTHROx</c> entry with the hero figure, drawn over the background.</param>
    /// <param name="applyRandomPalette">Whether <paramref name="hero"/> is tinted with the purple palette.</param>
    /// <param name="size">Size of the portrait, <c>portPos</c> of the reference.</param>
    /// <param name="gamePalette">The game palette in video values.</param>
    /// <returns>The portrait, ready to be blitted at the caller's <c>portPos</c>.</returns>
    /// <remarks>
    /// <para>
    /// 立绘按「底边对齐、水平居中」摆放：源区域从 <c>max( 0, sprite.height - portPos.height )</c> 行开始，
    /// 只取 <c>portPos.height</c> 行，目标 Y 为 <c>max( 0, portPos.height - sprite.height )</c>，
    /// 于是比肖像高的立绘截掉顶部、比肖像矮的立绘贴在底边。
    /// 参考随后 <c>Copy</c> 进调用方的 <paramref name="size"/> 区域，端口直接把这块图返回给调用点。
    /// </para>
    /// <para>
    /// 立绘带着变换层，因此用 <see cref="ImageOps.BlitSprite(PixelBuffer, Point, Sprite, in Rect)"/>：
    /// 变换值 1 的像素保持底色（透明），其余按变换表作用在底色上，和参考的
    /// <c>fheroes2::Blit( Sprite, ..., Image &amp; )</c> 一致。
    /// </para>
    /// </remarks>
    public static Sprite Compose(Sprite background, Sprite hero, bool applyRandomPalette, Size size, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(gamePalette);

        PixelBuffer portrait = new(size.Width, size.Height);

        // The background keeps its transform layer: a transparent STRIP pixel must stay transparent
        // instead of being averaged in as a colour.
        ImageOps.SubpixelResize(background, portrait, gamePalette);

        Sprite figure = hero;
        if (applyRandomPalette)
        {
            figure = hero.Clone();
            ImageOps.ApplyPalette(figure.Image, PaletteTable.Get(PaletteId.Purple));
        }

        int sourceY = Math.Max(0, figure.Height - size.Height);
        int destinationX = (size.Width - figure.Width) / 2;
        int destinationY = Math.Max(0, size.Height - figure.Height);

        ImageOps.BlitSprite(
            portrait,
            new Point(destinationX, destinationY - sourceY),
            figure,
            new Rect(0, sourceY, figure.Width, size.Height));

        return new Sprite(portrait);
    }

    /// <summary>
    /// Returns which resources a race portrait is made of, the <c>switch</c> at <c>ui_tool.cpp:860-886</c>.
    /// </summary>
    /// <param name="race">Race to look up.</param>
    /// <param name="artwork">Hero ICN file, background index and whether the purple palette is applied.</param>
    /// <returns><see langword="false"/> for <see cref="MapRace.None"/> and <see cref="MapRace.Multiple"/>.</returns>
    public static bool TryGetArtwork(MapRace race, out HeroRaceArtwork artwork)
    {
        switch (race)
        {
            case MapRace.Knight:
                artwork = new HeroRaceArtwork(IcnType.Cmbthrok, 4, false);
                return true;
            case MapRace.Barbarian:
                artwork = new HeroRaceArtwork(IcnType.Cmbthrob, 5, false);
                return true;
            case MapRace.Sorceress:
                artwork = new HeroRaceArtwork(IcnType.Cmbthros, 6, false);
                return true;
            case MapRace.Warlock:
                artwork = new HeroRaceArtwork(IcnType.Cmbthrow, 7, false);
                return true;
            case MapRace.Wizard:
                artwork = new HeroRaceArtwork(IcnType.Cmbthroz, 8, false);
                return true;
            case MapRace.Necromancer:
                artwork = new HeroRaceArtwork(IcnType.Cmbthron, 9, false);
                return true;
            case MapRace.Random:
                artwork = new HeroRaceArtwork(IcnType.Cmbthrow, 10, true);
                return true;
            default:
                artwork = default;
                return false;
        }
    }
}

/// <summary>
/// The three pieces of a race portrait: the hero ICN file, the index of its <c>STRIP</c> background and
/// whether the figure is tinted with the purple palette.
/// </summary>
/// <param name="Heroes">Hero figure ICN, one of the <c>CMBTHROx</c> files.</param>
/// <param name="Background">Index of the background in <c>STRIP.ICN</c>.</param>
/// <param name="ApplyRandomPalette">Whether the figure is recoloured purple, which marks the random race.</param>
public readonly record struct HeroRaceArtwork(IcnType Heroes, int Background, bool ApplyRandomPalette);
