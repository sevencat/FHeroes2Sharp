using FHeroes2.Assets.Maps;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Editor;

/// <summary>
/// Builds the two condition panel views out of <see cref="MapSpecsState"/>, the members the reference keeps on
/// <c>VictoryConditionUI</c> / <c>LossConditionUI</c> (<c>editor_map_specs_window.cpp:1021-1217</c>, <c>:1668-1743</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在开窗前把地图的城镇、英雄、神器和联盟一次算好存进两个条件对象；端口没有这两个对象，每次重画前用状态现拼。
/// 条件是状态的一部分，所以 <c>UpdateVictoryCondition()</c>／<c>UpdateLossCondition()</c> 保证目标城镇、英雄一定在
/// 各自的候选表里，这里只把 <c>Point</c> 换成绘制用的行数据。
/// </para>
/// <para>
/// 参考对「占领城镇」和「击败英雄」用 <c>assert( !_mapTownInfos.empty() )</c> 兜底；端口的空白地图没有城镇、也没有
/// 只归电脑的英雄，所以这种情况下视图的目标留 <see langword="null"/>，由 <see cref="CanDrawVictory"/> 让调用方跳过，
/// 而不是在绘制层抛断言。
/// </para>
/// </remarks>
public static class MapSpecsPanelViews
{
    /// <summary>Builds the view of the victory panel.</summary>
    /// <param name="state">The specifications being edited.</param>
    /// <returns>The condition, its flags and the row it has to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static MapSpecsVictoryPanelView CreateVictory(MapSpecsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        MapVictoryCondition condition = state.VictoryCondition;
        int mapWidth = state.Map.Info.Width;

        return new MapSpecsVictoryPanelView(
            condition,
            mapWidth,
            state.AllowNormalVictory,
            state.ComputerAlsoWins,
            condition == MapVictoryCondition.CaptureTown ? FindTown(state.Towns, state.VictoryTarget, mapWidth) : null,
            condition == MapVictoryCondition.KillHero ? FindHero(state.VictoryHeroes, state.VictoryTarget, mapWidth) : null,
            condition == MapVictoryCondition.ObtainArtifact ? ToArtifact(state.VictoryArtifact) : ArtifactType.Unknown,
            state.AvailablePlayers,
            condition == MapVictoryCondition.DefeatOtherSide ? [state.VictorySide1, state.VictorySide2] : null);
    }

    /// <summary>Builds the view of the loss panel.</summary>
    /// <param name="state">The specifications being edited.</param>
    /// <returns>The condition and the row it has to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static MapSpecsLossPanelView CreateLoss(MapSpecsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        MapLossCondition condition = state.LossCondition;
        int mapWidth = state.Map.Info.Width;

        return new MapSpecsLossPanelView(
            condition,
            mapWidth,
            condition == MapLossCondition.LoseTown ? FindTown(state.Towns, state.LossTarget, mapWidth) : null,
            condition == MapLossCondition.LoseHero ? FindHero(state.LossHeroes, state.LossTarget, mapWidth) : null);
    }

    /// <summary>
    /// Whether the victory panel can be drawn and hit-tested, i.e. the condition that needs a row has one.
    /// </summary>
    /// <param name="view">The view about to be drawn.</param>
    /// <returns><see langword="true"/> when the panel has everything it needs.</returns>
    public static bool CanDrawVictory(in MapSpecsVictoryPanelView view) => view.Condition switch
    {
        MapVictoryCondition.CaptureTown => view.Town is not null,
        MapVictoryCondition.KillHero => view.Hero is not null,
        _ => true,
    };

    /// <summary>
    /// Whether the loss panel can be drawn and hit-tested, i.e. the condition that needs a row has one.
    /// </summary>
    /// <param name="view">The view about to be drawn.</param>
    /// <returns><see langword="true"/> when the panel has everything it needs.</returns>
    public static bool CanDrawLoss(in MapSpecsLossPanelView view) => view.Condition switch
    {
        MapLossCondition.LoseTown => view.Town is not null,
        MapLossCondition.LoseHero => view.Hero is not null,
        _ => true,
    };

    /// <summary>
    /// Builds a row for every town of the map, the <c>_mapTownInfos</c> list the reference fills once and the
    /// town picker shows in full (<c>editor_map_specs_window.cpp:220-240</c>).
    /// </summary>
    /// <param name="state">The specifications being edited.</param>
    /// <returns>One row per town, in the map's own order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<MapSpecsConditionTownRowData> CreateTownRows(MapSpecsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int mapWidth = state.Map.Info.Width;
        MapSpecsConditionTownRowData[] rows = new MapSpecsConditionTownRowData[state.Towns.Count];

        for (int index = 0; index < rows.Length; ++index)
        {
            rows[index] = ToTownRow(state.Towns[index], mapWidth);
        }

        return rows;
    }

    /// <summary>
    /// Builds a row for every hero of one of the two candidate lists, the <c>_mapHeroInfos</c> list of the
    /// reference (<c>:241-262</c>).
    /// </summary>
    /// <param name="heroes">The heroes of the condition, <see cref="MapSpecsState.VictoryHeroes"/> or its loss twin.</param>
    /// <param name="mapWidth">Width of the map, which turns the coordinates into the row id.</param>
    /// <returns>One row per hero, in the order the state holds them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="heroes"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<MapSpecsConditionHeroRowData> CreateHeroRows(
        IReadOnlyList<Mp2MapHero> heroes, int mapWidth)
    {
        ArgumentNullException.ThrowIfNull(heroes);

        MapSpecsConditionHeroRowData[] rows = new MapSpecsConditionHeroRowData[heroes.Count];

        for (int index = 0; index < rows.Length; ++index)
        {
            rows[index] = ToHeroRow(heroes[index], mapWidth);
        }

        return rows;
    }

    /// <summary>The row data of the town standing on <paramref name="tile"/>, <c>_mapTownInfos</c> of the reference.</summary>
    /// <remarks>
    /// 占领城镇的第二个分量（归属颜色）在端口恒为 <see cref="PlayerColor.None"/>：MP2 的城堡表只有位置与种族，
    /// 归属要查地块上的物体，端口的状态层还没做这件事（见 <see cref="MapSpecsState"/> 类注释）。
    /// </remarks>
    private static MapSpecsConditionTownRowData? FindTown(
        IReadOnlyList<Mp2CastlePosition> towns, Core.Geometry.Point tile, int mapWidth)
    {
        foreach (Mp2CastlePosition town in towns)
        {
            if (town.X == tile.X && town.Y == tile.Y)
            {
                return ToTownRow(town, mapWidth);
            }
        }

        return null;
    }

    /// <summary>The row data of the hero standing on <paramref name="tile"/>, <c>_mapHeroInfos</c> of the reference.</summary>
    private static MapSpecsConditionHeroRowData? FindHero(
        IReadOnlyList<Mp2MapHero> heroes, Core.Geometry.Point tile, int mapWidth)
    {
        foreach (Mp2MapHero hero in heroes)
        {
            if (hero.X == tile.X && hero.Y == tile.Y)
            {
                return ToHeroRow(hero, mapWidth);
            }
        }

        return null;
    }

    /// <summary>One town of the castle table as a row of the condition panel, <c>_mapTownInfos</c>.</summary>
    private static MapSpecsConditionTownRowData ToTownRow(Mp2CastlePosition town, int mapWidth) =>
        new(PlayerColor.None, town.Race, string.Empty, town.HasFort, (town.Y * mapWidth) + town.X);

    /// <summary>One hero of the map as a row of the condition panel, <c>_mapHeroInfos</c>.</summary>
    private static MapSpecsConditionHeroRowData ToHeroRow(Mp2MapHero hero, int mapWidth) =>
        new(hero.Color, hero.Race, string.Empty, (hero.Y * mapWidth) + hero.X);

    /// <summary>Maps the stored artifact id onto the artifact table, unknown ids becoming <see cref="ArtifactType.Unknown"/>.</summary>
    private static ArtifactType ToArtifact(int artifactId) =>
        Enum.IsDefined((ArtifactType)artifactId) ? (ArtifactType)artifactId : ArtifactType.Unknown;
}
