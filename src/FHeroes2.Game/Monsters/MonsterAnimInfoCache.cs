using FHeroes2.Assets;
using FHeroes2.Core.Diagnostics;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// Reads the animation record of a creature from the archives and keeps it for the rest of the session,
/// the reference's <c>Bin_Info::GetMonsterInfo( monsterID )</c> (<c>src/fheroes2/agg/bin_info.cpp:51</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>bin_info.cpp:51-73</c>——用 <c>binFileName</c> 表里的名字取 AGG 条目，解析成
/// <see cref="MonsterAnimInfo"/>，通过 <see cref="MonsterAnimInfo.IsValid"/> 才进缓存；
/// 失败就报一条警告并返回空记录（不缓存，下次还会再试）。
/// </para>
/// <para>
/// 差异：参考用的是全局缓存与全局 AGG；端口把 <see cref="AssetBundle"/> 交给调用方注入，
/// 这也让测试可以喂一份自造的归档。
/// </para>
/// </remarks>
public sealed class MonsterAnimInfoCache
{
    private readonly AssetBundle _assets;
    private readonly Dictionary<MonsterType, MonsterAnimInfo> _cache = [];

    /// <summary>Creates a cache that reads from <paramref name="assets"/>.</summary>
    public MonsterAnimInfoCache(AssetBundle assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
    }

    /// <summary>Number of creatures whose record has been read successfully so far.</summary>
    public int Count => _cache.Count;

    /// <summary>
    /// The animation record of <paramref name="monsterId"/>, or an empty record when the archives do not
    /// carry a usable one.
    /// </summary>
    /// <remarks>参考：<c>bin_info.cpp:51</c>。</remarks>
    public MonsterAnimInfo Get(MonsterType monsterId)
    {
        if (_cache.TryGetValue(monsterId, out MonsterAnimInfo? cached))
        {
            return cached;
        }

        // 参考：bin_info.cpp:53 直接下标取 binFileName；端口多一道范围检查，越界不越狱。
        int id = (int)monsterId;
        if (id < 0 || id >= MonsterBinFileNames.ById.Length)
        {
            GameLog.Warn($"{monsterId} has no animation data in the archive table.");
            return new MonsterAnimInfo();
        }

        string fileName = MonsterBinFileNames.ById[id];
        byte[]? bytes = _assets.TryRead(fileName);
        if (bytes is null)
        {
            GameLog.Warn($"The animation data '{fileName}' of {monsterId} is missing; the creature will not animate.");
            return new MonsterAnimInfo();
        }

        MonsterAnimInfo info = new(monsterId, bytes);
        if (!info.IsValid())
        {
            GameLog.Warn($"The animation data '{fileName}' of {monsterId} is incomplete; the creature will not animate.");
            return new MonsterAnimInfo();
        }

        _cache[monsterId] = info;
        return info;
    }
}
