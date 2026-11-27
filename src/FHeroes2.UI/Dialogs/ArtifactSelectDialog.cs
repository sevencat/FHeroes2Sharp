using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Assets.Maps;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Builds the list of artifacts a selection window shows and the window itself, <c>Dialog::selectArtifact()</c>
/// (<c>dialog_selectitems.cpp:1162-1194</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference is one blocking function: it fills a vector (the Magic Book or the ultimate prize first, then every
/// valid artifact), bails out with a message when nothing is left, opens <c>SelectEnumArtifact</c>, moves the selection
/// to the id the caller passed and returns what the loop ended on. The port splits that into the pieces a scene can
/// drive: <see cref="BuildArtifactList"/> fills the vector, <see cref="DialogSize"/> gives the reference's
/// <c>{ 370, display.height - 150 }</c>, <see cref="TryCreate"/> opens the window and returns <see langword="null"/>
/// instead of showing the message, and <see cref="ArtifactSelectionWindow.SelectedArtifact"/> is the return value once
/// the window says <see cref="Widgets.DialogButton.Ok"/>.
/// </para>
/// <para>
/// 参考按当前地图版本过滤资料片神器（<c>isPriceOfLoyaltyArtifact</c>），所以版本是参数而不是去读全局设置。
/// 编辑器自己选神器时不限等级，<see cref="BuildArtifactList"/> 因此多一个「只留某个等级」的可选参数，给
/// 随机神器与终极神器那两条路用。
/// </para>
/// </remarks>
public static class ArtifactSelectDialog
{
    /// <summary>Height taken off the screen height, <c>dialogHeightDeduction</c> in the reference.</summary>
    public const int DialogHeightDeduction = 150;

    /// <summary>Width of the window, 370 pixels in the reference.</summary>
    public const int DialogWidth = 370;

    /// <summary>
    /// The message the reference shows when the list turns out empty,
    /// <c>_( "There are no artifacts to select from." )</c>.
    /// </summary>
    public const string EmptyListMessage = "There are no artifacts to select from.";

    /// <summary>
    /// The size of the window, <c>{ 370, display.height - dialogHeightDeduction }</c>
    /// (<c>dialog_selectitems.cpp:1177</c>).
    /// </summary>
    /// <param name="screen">Screen size, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The size of the active area the reference asks for.</returns>
    public static Size DialogSize(Size? screen = null)
    {
        Size display = screen ?? ScreenBuffer.DefaultSize;

        return new Size(DialogWidth, Math.Max(0, display.Height - DialogHeightDeduction));
    }

    /// <summary>
    /// Fills the id vector, the head of the reference's <c>selectArtifact()</c>
    /// (<c>dialog_selectitems.cpp:1164-1175</c>).
    /// </summary>
    /// <remarks>
    /// 第一项是「魔法书」（胜利条件那条路换成「任意终极神器」），然后是 <see cref="ArtifactType.Unknown"/> 之后的每一件：
    /// 跳过魔法书自己、跳过法术卷轴（只在胜利条件那条路上跳）、跳过地图版本不允许的资料片神器、跳过无效的编号。
    /// </remarks>
    /// <param name="version">Version of the map, which decides whether the expansion artifacts are offered.</param>
    /// <param name="isForVictoryConditions">
    /// Whether the list is for a victory condition: the ultimate prize replaces the magic book and the spell scroll is
    /// left out.
    /// </param>
    /// <param name="level">
    /// When set, only the artifacts whose tier matches this mask are offered — the random artifact and the ultimate
    /// prize of the editor (<c>editor_interface.cpp:2385-2427</c>).
    /// </param>
    /// <returns>The ids to show, the reference's <c>std::vector&lt;int32_t&gt; artifacts</c>.</returns>
    public static List<int> BuildArtifactList(MapVersion version = MapVersion.PriceOfLoyalty,
        bool isForVictoryConditions = false, ArtifactLevel? level = null)
    {
        List<int> artifacts = [];

        if (level is null)
        {
            artifacts.Add((int)(isForVictoryConditions
                ? ArtifactType.EditorAnyUltimateArtifact
                : ArtifactType.MagicBook));
        }

        for (int id = (int)ArtifactType.Unknown + 1; id < (int)ArtifactType.ArtifactCount; ++id)
        {
            var artifact = (ArtifactType)id;

            if (artifact == ArtifactType.MagicBook)
            {
                continue;
            }

            if (isForVictoryConditions && artifact == ArtifactType.SpellScroll)
            {
                continue;
            }

            if (ArtifactLevels.IsPriceOfLoyalty(artifact) && version == MapVersion.SuccessionWars)
            {
                continue;
            }

            if (!ArtifactTypes.IsValid(artifact))
            {
                continue;
            }

            if (level is { } mask && (ArtifactLevels.Level(artifact, version) & mask) == 0)
            {
                continue;
            }

            artifacts.Add(id);
        }

        return artifacts;
    }

    /// <summary>
    /// Opens the window of the reference's <c>selectArtifact()</c> and moves the selection to the requested artifact
    /// (<c>dialog_selectitems.cpp:1176-1192</c>).
    /// </summary>
    /// <param name="artifact">The artifact the list starts on, or <see cref="ArtifactType.Unknown"/> for the first row.</param>
    /// <param name="icons">The <c>ARTFX</c> mini icons.</param>
    /// <param name="artwork">Frames of the scroll bar and the two buttons.</param>
    /// <param name="titleFont">Yellow font, <c>FontType::normalYellow()</c>.</param>
    /// <param name="font">White font, <c>FontType::normalWhite()</c>.</param>
    /// <param name="version">Version of the map, which decides whether the expansion artifacts are offered.</param>
    /// <param name="isForVictoryConditions">Whether the ultimate prize replaces the magic book.</param>
    /// <param name="level">When set, only the artifacts of that tier are offered.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>
    /// The window, or <see langword="null"/> when there is nothing to pick — the caller shows
    /// <see cref="EmptyListMessage"/> with the heading "Warning" exactly like the reference does.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="icons"/>, <paramref name="artwork"/>, <paramref name="titleFont"/> or
    /// <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public static ArtifactSelectionWindow? TryCreate(ArtifactType artifact, IArtifactIcons icons,
        ItemSelectionWindowArtwork artwork, IGlyphSource titleFont, IGlyphSource font,
        MapVersion version = MapVersion.PriceOfLoyalty, bool isForVictoryConditions = false,
        ArtifactLevel? level = null, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(font);

        List<int> artifacts = BuildArtifactList(version, isForVictoryConditions, level);

        if (artifacts.Count == 0)
        {
            return null;
        }

        ArtifactSelectionWindow window = new(artifacts, icons, DialogSize(screen), artwork, titleFont, font, screen);

        if (artifact != ArtifactType.Unknown)
        {
            window.SelectArtifact(artifact);
        }

        return window;
    }
}
