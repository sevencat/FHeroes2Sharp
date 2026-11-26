using System.Text;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Puts a generated map where the game looks for maps: an <c>.mp2</c> file inside the <c>maps</c> folder, named after
/// the seed it was generated with.
/// </summary>
/// <remarks>
/// <para>
/// 参考的编辑器把新图存进 <c>maps</c> 目录，之后它就出现在选关列表里——参考的 <c>Maps::getValidMaps()</c> 每次都扫那个目录，
/// 端口的主菜单把列表缓存在内存里（<c>MainMenuScene</c> 的 <c>_maps</c>），所以存完还要让那一份失效。
/// </para>
/// <para>
/// <b>文件名带种子</b>：同一张图重新生成会得到同一个名字，于是不会越存越多；不同种子互不覆盖。名字里的种子也让
/// 「这张图是什么时候摇的」一眼看得出来，与地图头里的 <c>Random map &lt;seed&gt;</c> 对应。
/// </para>
/// <para>
/// <b>地图名会被截断，文件名不会</b>：MP2 的场景名字段只有 16 字节（见 ADR-283），两个不同的种子可能截出同一个名字；
/// 文件名用的是完整的种子，所以它们仍然是两个文件。
/// </para>
/// </remarks>
public static class RandomMapArchive
{
    /// <summary>Extension of an original map file, the first entry of <see cref="MapCatalog.Extensions"/>.</summary>
    public const string Extension = ".mp2";

    /// <summary>The sub folder of the data directory maps live in, the <c>maps</c> of the reference.</summary>
    public const string DirectoryName = "maps";

    /// <summary>What every generated map file is called, before the seed and the extension.</summary>
    public const string FileNamePrefix = "random_map_";

    /// <summary>The file name a map of <paramref name="seed"/> is stored under, e.g. <c>random_map_424242.mp2</c>.</summary>
    /// <param name="seed">The seed the map was generated with.</param>
    /// <returns>The bare file name.</returns>
    public static string FileNameFor(uint seed) => $"{FileNamePrefix}{seed}{Extension}";

    /// <summary>
    /// Picks the folder a generated map goes into: the existing <c>maps</c> folder next to the data directory, or a
    /// new one created there.
    /// </summary>
    /// <param name="dataDirectory">Directory that holds <c>HEROES2.AGG</c>.</param>
    /// <returns>The full path of the folder, or <see langword="null"/> when it cannot be created.</returns>
    /// <remarks>
    /// 参考的 <c>Settings::GetMapsDirectories()</c> 给出一串候选；端口只写一个——数据目录旁边那个 <c>maps</c>，也就是
    /// <see cref="MapCatalog.FindDirectories"/> 一定会扫到的那个。写不进去（只读安装）就返回 <see langword="null"/>，
    /// 由调用方记一条警告并放弃保存，而不是让生成本身失败。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="dataDirectory"/> is empty.</exception>
    public static string? ResolveDirectory(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        string candidate = Path.Combine(dataDirectory, DirectoryName);

        try
        {
            Directory.CreateDirectory(candidate);

            return candidate;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes <paramref name="map"/> into <paramref name="directory"/> under its seed's name,
    /// <c>World::saveMapMP2()</c>'s whole-file path of the editor.
    /// </summary>
    /// <param name="map">The generated map.</param>
    /// <param name="directory">Folder to write into, normally <see cref="ResolveDirectory"/>'s answer.</param>
    /// <param name="seed">The seed the map was generated with, which names the file.</param>
    /// <returns>The full path of the file that was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty.</exception>
    /// <exception cref="BinaryFormatException">The map cannot be stored, see <see cref="Mp2MapWriter.WriteAll"/>.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static string Save(Mp2Map map, string directory, uint seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        string filePath = Path.Combine(directory, FileNameFor(seed));

        // 从零生成的图没有源文件可以打补丁，只能整份写（Save 会去读源文件）。
        Mp2MapWriter.WriteAll(filePath, map);

        return Path.GetFullPath(filePath);
    }

    /// <summary>
    /// A description of what a generated map holds, the log line the editor and the host print after a save.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="filePath">Where it was written.</param>
    /// <returns>The text, e.g. <c>72x72, 3 castles, 812 add-ons</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public static string Describe(Mp2Map map, string filePath)
    {
        ArgumentNullException.ThrowIfNull(map);

        StringBuilder text = new();

        text.Append($"{map.Width}x{map.Height}, {map.Castles.Count} castle(s), {map.Addons.Count} add-on(s)");
        text.Append($"; written to '{filePath}'");

        return text.ToString();
    }
}
