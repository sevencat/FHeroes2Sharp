using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// Which music track belongs to a ground, a race, or a battle, the three queries of
/// <c>MUS</c> in <c>src/fheroes2/agg/mus.cpp:141-211</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把这三支放在 <c>agg</c> 目录里，但它们问的是「哪首曲子」而不是「哪个文件」，而地形与种族都是游戏层的
/// 类型，所以端口把它们放在这里：<see cref="MusicTrack"/> 与文件名的部分仍在
/// <c>FHeroes2.Assets.Audio.MusicTrackCatalog</c>。
/// </para>
/// <para>
/// 参考的 <c>default</c> 分支是 <c>assert( 0 )</c> 后返回 <c>UNKNOWN</c>：地形与种族在参考里是封闭枚举，
/// 走不到那里。端口的 <see cref="MapGround.Unknown"/> 是地图读坏时的结果，所以它是唯一会走到
/// <see cref="MusicTrack.Unknown"/> 的输入——这与参考的行为一致，只是原因不同。
/// </para>
/// </remarks>
public static class MusicThemes
{
    /// <summary>
    /// The theme of a ground, the reference's <c>MUS::FromGround</c>.
    /// </summary>
    /// <param name="ground">The ground under the hero.</param>
    /// <returns>The theme; water and beach share the ocean theme.</returns>
    /// <remarks>参考：<c>mus.cpp:141</c>。</remarks>
    public static MusicTrack FromGround(MapGround ground) => ground switch
    {
        MapGround.Desert => MusicTrack.Desert,
        MapGround.Snow => MusicTrack.Snow,
        MapGround.Swamp => MusicTrack.Swamp,
        MapGround.Wasteland => MusicTrack.Wasteland,
        // A beach is the shore of the ocean, and it sounds like it.
        MapGround.Beach => MusicTrack.Ocean,
        MapGround.Lava => MusicTrack.Lava,
        MapGround.Dirt => MusicTrack.Dirt,
        MapGround.Grass => MusicTrack.Grass,
        MapGround.Water => MusicTrack.Ocean,
        _ => MusicTrack.Unknown,
    };

    /// <summary>
    /// The theme of a castle, the reference's <c>MUS::FromRace</c>.
    /// </summary>
    /// <param name="race">The race of the castle.</param>
    /// <returns>The theme, or <see cref="MusicTrack.Unknown"/> for a race that has no castle theme.</returns>
    /// <remarks>参考：<c>mus.cpp:171</c>。</remarks>
    public static MusicTrack FromRace(MapRace race) => race switch
    {
        MapRace.Knight => MusicTrack.KnightCastle,
        MapRace.Barbarian => MusicTrack.BarbarianCastle,
        MapRace.Sorceress => MusicTrack.SorceressCastle,
        MapRace.Warlock => MusicTrack.WarlockCastle,
        MapRace.Wizard => MusicTrack.WizardCastle,
        MapRace.Necromancer => MusicTrack.NecromancerCastle,
        _ => MusicTrack.Unknown,
    };

    /// <summary>
    /// One of the three battle themes at random, the reference's <c>MUS::GetBattleRandom</c>.
    /// </summary>
    /// <returns>One of <see cref="MusicTrack.Battle1"/>, <see cref="MusicTrack.Battle2"/>,
    /// <see cref="MusicTrack.Battle3"/>.</returns>
    /// <remarks>参考：<c>mus.cpp:195</c>——<c>Rand::Get( 1, 3 )</c>。</remarks>
    public static MusicTrack GetBattleRandom() => Rand.Get(1, 3) switch
    {
        1 => MusicTrack.Battle1,
        2 => MusicTrack.Battle2,
        // Rand::Get(1, 3) is inclusive on both ends, so 3 is the only other answer.
        _ => MusicTrack.Battle3,
    };
}
