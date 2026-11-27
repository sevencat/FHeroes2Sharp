using FHeroes2.Assets.Maps;

namespace FHeroes2.UI.Editor;

/// <summary>
/// The hero a condition panel row stands for, the <c>HeroInfo</c> the reference keeps in the panel
/// (<c>editor_map_specs_window.cpp:91-101</c>) reduced to the fields the row draws.
/// </summary>
/// <param name="Color">Owner of the hero, which picks the colour of the two flags.</param>
/// <param name="Race">Race of the hero, which picks the portrait when there is no custom one.</param>
/// <param name="Name">Custom name of the hero, empty when the author did not type one.</param>
/// <param name="TileIndex">Tile the hero stands on, used by the title.</param>
/// <param name="CustomPortraitId">
/// Id of the custom portrait, <c>heroMetadata-&gt;customPortrait</c> of the reference; <c>0</c> means "no custom portrait,
/// draw the race portrait". The MP2 map model has no custom portraits, so editor callers always pass <c>0</c>.
/// </param>
/// <remarks>
/// 参考的 <c>HeroInfo</c> 还带 <c>language</c>（自定义名本身可能是别的语言的本地化串），端口没有本地化层，去掉。
/// </remarks>
public readonly record struct MapSpecsConditionHeroRowData(
    PlayerColor Color, MapRace Race, string Name, int TileIndex, int CustomPortraitId = 0);
