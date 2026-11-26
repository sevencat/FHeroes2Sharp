using FHeroes2.Game.Maps;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The strings the random map generator dialog draws, <c>_( "rmg|..." )</c> of
/// <c>src/fheroes2/dialog/dialog_random_map_generator.cpp</c>.
/// </summary>
/// <remarks>
/// 与 <see cref="ScenarioDialogText"/> 一样，UI 层不读翻译目录：调用方把 msgid 解析好再传进来。默认值是参考的那几句
/// 英文（去掉 <c>rmg|</c> 上下文前缀，好在没有目录时也读得懂），三组枚举名直接取
/// <see cref="RandomMapSizing"/> 的 msgid，所以「默认」与生成器自己写进地图描述里的那套名字永远一致。
/// </remarks>
public sealed record RandomMapGeneratorDialogText
{
    /// <summary>Title of the dialog, <c>_( "Random Map Generator" )</c>.</summary>
    public required string Title { get; init; }

    /// <summary>Label of the player count row, <c>_( "rmg|Player count:" )</c>.</summary>
    public required string PlayerCountLabel { get; init; }

    /// <summary>Label of the layout row, <c>_( "rmg|Map layout:" )</c>.</summary>
    public required string MapLayoutLabel { get; init; }

    /// <summary>Label of the water row, <c>_( "rmg|Water percentage:" )</c>.</summary>
    public required string WaterPercentageLabel { get; init; }

    /// <summary>Label of the monster strength row, <c>_( "rmg|Monster strength:" )</c>.</summary>
    public required string MonsterStrengthLabel { get; init; }

    /// <summary>Label of the resource density row, <c>_( "rmg|Resource availability:" )</c>.</summary>
    public required string ResourceAvailabilityLabel { get; init; }

    /// <summary>Label of the seed row, <c>_( "rmg|Map seed:" )</c>.</summary>
    public required string MapSeedLabel { get; init; }

    /// <summary>Names of the five layouts, indexed by <see cref="RandomMapLayout"/>.</summary>
    public required IReadOnlyList<string> LayoutNames { get; init; }

    /// <summary>Names of the four monster strengths, indexed by <see cref="RandomMapMonsterStrength"/>.</summary>
    public required IReadOnlyList<string> MonsterStrengthNames { get; init; }

    /// <summary>Names of the three resource densities, indexed by <see cref="RandomMapResourceDensity"/>.</summary>
    public required IReadOnlyList<string> ResourceDensityNames { get; init; }

    /// <summary>The untranslated strings of the reference implementation.</summary>
    public static RandomMapGeneratorDialogText Default { get; } = new()
    {
        Title = "Random Map Generator",
        PlayerCountLabel = "Player count:",
        MapLayoutLabel = "Map layout:",
        WaterPercentageLabel = "Water percentage:",
        MonsterStrengthLabel = "Monster strength:",
        ResourceAvailabilityLabel = "Resource availability:",
        MapSeedLabel = "Map seed:",
        LayoutNames =
        [
            RandomMapSizing.LayoutName(RandomMapLayout.Mirrored),
            RandomMapSizing.LayoutName(RandomMapLayout.Balanced),
            RandomMapSizing.LayoutName(RandomMapLayout.Islands),
            RandomMapSizing.LayoutName(RandomMapLayout.Pyramid),
            RandomMapSizing.LayoutName(RandomMapLayout.Quest),
        ],
        MonsterStrengthNames =
        [
            RandomMapSizing.MonsterStrengthName(RandomMapMonsterStrength.Weak),
            RandomMapSizing.MonsterStrengthName(RandomMapMonsterStrength.Normal),
            RandomMapSizing.MonsterStrengthName(RandomMapMonsterStrength.Strong),
            RandomMapSizing.MonsterStrengthName(RandomMapMonsterStrength.Deadly),
        ],
        ResourceDensityNames =
        [
            RandomMapSizing.ResourceDensityName(RandomMapResourceDensity.Scarce),
            RandomMapSizing.ResourceDensityName(RandomMapResourceDensity.Normal),
            RandomMapSizing.ResourceDensityName(RandomMapResourceDensity.Abundant),
        ],
    };

    /// <summary>Name of <paramref name="layout"/>; empty when the list is shorter than the enum.</summary>
    public string Layout(RandomMapLayout layout) => At(LayoutNames, (int)layout);

    /// <summary>Name of <paramref name="strength"/>; empty when the list is shorter than the enum.</summary>
    public string MonsterStrength(RandomMapMonsterStrength strength) => At(MonsterStrengthNames, (int)strength);

    /// <summary>Name of <paramref name="density"/>; empty when the list is shorter than the enum.</summary>
    public string ResourceDensity(RandomMapResourceDensity density) => At(ResourceDensityNames, (int)density);

    private static string At(IReadOnlyList<string> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : string.Empty;
}

/// <summary>
/// The state and the answer of the random map generator dialog, <c>fheroes2::randomMapGeneratorDialog()</c>
/// (<c>src/fheroes2/dialog/dialog_random_map_generator.cpp:72-251</c>).
/// </summary>
/// <remarks>
/// <para>
/// 六个旋钮：玩家数、布局、水量、怪物强度、资源密度、种子。<b>两个约束是参考写在前面的</b>——玩家数夹在 2…6 之间，
/// 水量不超过「这么多玩家 + 一个中立区」还放得下的上限（<see cref="MaximumWaterPercentage"/>）；玩家数一变，上限跟着
/// 变，水量再被夹一次。这两条都在 <see cref="SetPlayerCount"/> 里成对出现，界面不需要自己算。
/// </para>
/// <para>
/// <b>布局这一项参考是禁用的</b>：下拉框画出来但涂暗、按钮 <c>disable()</c>，因为生成器只会 <c>MIRRORED</c>
/// （<c>map_random_generator.cpp:658</c> 的 TODO）。端口照抄：<see cref="Layout"/> 可读可写，但界面不给入口。
/// </para>
/// <para>
/// <b>Contract only</b> 的那一半（ADR-163）：这是模型，布局与输入在 <see cref="RandomMapGeneratorView"/>。
/// </para>
/// </remarks>
public sealed class RandomMapGeneratorDialog
{
    /// <summary>Lowest player count the dialog offers, <c>std::max( 2, ... )</c> of <c>:81</c>.</summary>
    public const int MinimumPlayers = RandomMapConfig.MinimumPlayers;

    /// <summary>Highest player count the dialog offers, <c>std::min( 6, ... )</c> of <c>:82</c>.</summary>
    public const int MaximumPlayers = RandomMapConfig.MaximumPlayers;

    /// <summary>Smallest seed the field accepts, the <c>0</c> of the reference's <c>ValueSelectionDialogElement</c>.</summary>
    public const int MinimumSeed = 0;

    /// <summary>Largest seed the field accepts, the <c>999999</c> of the reference.</summary>
    public const int MaximumSeed = 999999;

    private RandomMapGeneratorDialog(RandomMapConfig config, int mapWidth)
    {
        MapWidth = mapWidth;
        PlayerCount = Math.Clamp(config.PlayerCount, MinimumPlayers, MaximumPlayers);
        Layout = config.Layout;
        WaterPercentage = Math.Clamp(config.WaterPercentage, 0, MaximumWaterPercentage);
        MonsterStrength = config.MonsterStrength;
        ResourceDensity = config.ResourceDensity;
        Seed = Math.Clamp(config.Seed, MinimumSeed, MaximumSeed);
    }

    /// <summary>
    /// Builds the dialog for a map of <paramref name="mapWidth"/> tiles, clamping the configuration the way the
    /// reference does before it draws anything (<c>:74-86</c>).
    /// </summary>
    /// <param name="config">The configuration to start from; it is copied, not modified.</param>
    /// <param name="mapWidth">Edge length of the map that will be generated, which limits the water percentage.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    public static RandomMapGeneratorDialog Create(RandomMapConfig config, int mapWidth)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);

        return new RandomMapGeneratorDialog(config, mapWidth);
    }

    /// <summary>Builds the dialog on the generator's default configuration.</summary>
    /// <param name="mapWidth">Edge length of the map that will be generated.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    public static RandomMapGeneratorDialog CreateDefault(int mapWidth) =>
        Create(new RandomMapConfig(), mapWidth);

    /// <summary>Edge length of the map the configuration is for, the <c>mapWidth</c> argument of the reference.</summary>
    public int MapWidth { get; }

    /// <summary>How many players share the map, clamped into <see cref="MinimumPlayers"/>..<see cref="MaximumPlayers"/>.</summary>
    public int PlayerCount { get; private set; }

    /// <summary>The shape of the map, <c>mapLayout</c>. The dialog shows it but offers no way to change it.</summary>
    public RandomMapLayout Layout { get; private set; }

    /// <summary>How much of the map is water, never above <see cref="MaximumWaterPercentage"/>.</summary>
    public int WaterPercentage { get; private set; }

    /// <summary>How hard the neutral stacks are.</summary>
    public RandomMapMonsterStrength MonsterStrength { get; private set; }

    /// <summary>How rich the regions are.</summary>
    public RandomMapResourceDensity ResourceDensity { get; private set; }

    /// <summary>The seed of the generator, <c>0</c> meaning «draw one when the map is built».</summary>
    public int Seed { get; private set; }

    /// <summary>
    /// The wettest this map can be with the current player count, <see cref="RandomMapSizing.MaximumWaterPercentage"/>
    /// (<c>:84</c>).
    /// </summary>
    /// <remarks>玩家数与地图尺寸一起决定它还放得下几个起点区域，剩下的才可以是水。</remarks>
    public int MaximumWaterPercentage => RandomMapSizing.MaximumWaterPercentage(PlayerCount, MapWidth);

    /// <summary>
    /// The configuration the dialog accepts, or <see langword="null"/> while it is still open or after a cancel.
    /// </summary>
    public RandomMapConfig? Result { get; private set; }

    /// <summary>Whether the player pressed OK, i.e. whether <see cref="Result"/> holds a configuration.</summary>
    public bool IsAccepted => Result is not null;

    /// <summary>
    /// Sets the player count and re-clamps the water percentage, the <c>playerCountSlider</c> branch of <c>:216-227</c>.
    /// </summary>
    /// <param name="value">The new count, clamped into the allowed range.</param>
    public void SetPlayerCount(int value)
    {
        PlayerCount = Math.Clamp(value, MinimumPlayers, MaximumPlayers);

        // 上限跟着玩家数走：参考同时把水量的滑块范围改小，再把当前值夹进去。
        WaterPercentage = Math.Min(WaterPercentage, MaximumWaterPercentage);
    }

    /// <summary>Sets the water percentage, clamped into <c>0</c>..<see cref="MaximumWaterPercentage"/>.</summary>
    /// <param name="value">The new percentage.</param>
    public void SetWaterPercentage(int value) =>
        WaterPercentage = Math.Clamp(value, 0, MaximumWaterPercentage);

    /// <summary>Sets the monster strength, clamped into the four levels.</summary>
    /// <param name="value">The new level.</param>
    public void SetMonsterStrength(RandomMapMonsterStrength value) =>
        MonsterStrength = value < RandomMapMonsterStrength.Weak ? RandomMapMonsterStrength.Weak
            : value > RandomMapMonsterStrength.Deadly ? RandomMapMonsterStrength.Deadly
            : value;

    /// <summary>Sets the resource density, clamped into the three levels.</summary>
    /// <param name="value">The new density.</param>
    public void SetResourceDensity(RandomMapResourceDensity value) =>
        ResourceDensity = value < RandomMapResourceDensity.Scarce ? RandomMapResourceDensity.Scarce
            : value > RandomMapResourceDensity.Abundant ? RandomMapResourceDensity.Abundant
            : value;

    /// <summary>Sets the layout. The dialog itself never calls this; a caller that enables the dropdown would.</summary>
    /// <param name="value">The new layout.</param>
    public void SetLayout(RandomMapLayout value) =>
        Layout = value < RandomMapLayout.Mirrored ? RandomMapLayout.Mirrored
            : value > RandomMapLayout.Quest ? RandomMapLayout.Quest
            : value;

    /// <summary>Sets the seed, clamped into <see cref="MinimumSeed"/>..<see cref="MaximumSeed"/>.</summary>
    /// <param name="value">The new seed.</param>
    public void SetSeed(int value) => Seed = Math.Clamp(value, MinimumSeed, MaximumSeed);

    /// <summary>Accepts the dialog: the current values become <see cref="Result"/>, the <c>return true</c> of <c>:251</c>.</summary>
    public void Accept() => Result = new RandomMapConfig
    {
        PlayerCount = PlayerCount,
        WaterPercentage = WaterPercentage,
        Seed = Seed,
        Layout = Layout,
        ResourceDensity = ResourceDensity,
        MonsterStrength = MonsterStrength,
    };

    /// <summary>Cancels the dialog: no configuration is produced, the <c>return false</c> of <c>:203</c>.</summary>
    public void Cancel() => Result = null;
}
