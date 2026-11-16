namespace FHeroes2.Assets.Audio;

/// <summary>
/// The lookup of the <see cref="M82Sound"/> ids, the <c>M82::GetString</c> of
/// <c>src/fheroes2/agg/m82.cpp:100</c>.
/// </summary>
/// <remarks>
/// 参考：<c>M82::GetString( int m82 )</c>——在范围内就取 <c>m82map[m82].string</c>，否则取
/// <c>m82map[UNKNOWN].string</c>（也就是 <c>"UNKNOWN"</c>）。名字表由 <c>tools/scripts/GenerateAggEnums.ps1</c>
/// 从 <c>m82map</c> 生成，逐条核对过顺序，所以这里用下标而不是查找。
/// 音效的另外两支不在这里：<c>FromSpell</c> 要把法术映射成音效，<c>getAdventureMapTileSound</c> 要读瓦片，
/// 两者都依赖游戏层的类型，分别在 <c>FHeroes2.Game.SpellSound</c> 与 <c>FHeroes2.Game.TileAmbientSound</c>。
/// </remarks>
public static class M82Catalog
{
    /// <summary>
    /// The archive entry name of a sound, the reference's <c>M82::GetString</c>.
    /// </summary>
    /// <param name="sound">The sound.</param>
    /// <returns>
    /// The file name, or <c>UNKNOWN</c> for <see cref="M82Sound.Unknown"/> and for any value outside the
    /// enumeration.
    /// </returns>
    /// <remarks>参考：<c>m82.cpp:100</c>、表在 <c>m82.cpp:36</c>。</remarks>
    public static string GetFileName(M82Sound sound) =>
        sound >= M82Sound.Aelmattk && sound < M82Sound.Unknown ? M82FileNames.ById[(int)sound] : M82FileNames.ById[(int)M82Sound.Unknown];

    /// <summary>Every sound the game can ask for, in table order.</summary>
    /// <remarks>参考没有这个过程；它是端口为了让测试遍历全表而加的。</remarks>
    public static IReadOnlyList<M82Sound> AllSounds { get; } =
        [.. Enumerable.Range(0, (int)M82Sound.Unknown).Select(value => (M82Sound)value)];
}
