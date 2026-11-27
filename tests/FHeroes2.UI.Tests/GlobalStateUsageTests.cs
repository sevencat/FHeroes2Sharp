using System.Runtime.CompilerServices;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Guards the one invariant the whole assembly leans on: whoever swaps the installed archive runs inside
/// <see cref="GlobalArchiveCollection"/>.
/// </summary>
/// <remarks>
/// <para>
/// ``GameAssets.Current`` 是进程级的静态槽，一百多个用例类靠「装好自己的归档、用完拆掉」来画窗口。xUnit 默认让
/// 不同 collection 并行跑，而 <see cref="GlobalArchiveCollection"/> 的定义带了 ``DisableParallelization = true``，
/// 所以只要**改**这个槽的类都进这个 collection，就没人能在别人读素材的半途把它换走或置空。
/// </para>
/// <para>
/// 这条规则以前只靠自觉，``bp7`` 里那条「全量并行跑时偶发挂一条、没有错误信息」就是漏洞：
/// <c>RandomMapGeneratorDialogTests</c> 自己开归档、跑完还 <c>= null</c>，并行池里读素材的用例就会拿到空素材，
/// 于是断言像素的那几条挂了（像素断言不带异常信息，正好对上「无 Error Message」）。
/// </para>
/// <para>
/// 用例读的是源码本身（<see cref="CallerFilePathAttribute"/> 给出的目录），因为「有没有这个 attribute」是编译期的
/// 事实，反射看不出来。源码不在机器上时（只发了 bin）直接跳过。
/// </para>
/// </remarks>
public sealed class GlobalStateUsageTests
{
    [Fact]
    public void EveryTestThatSwapsTheInstalledArchiveIsSerialized()
    {
        // 拼出来，免得这个文件自己的字面量被当成犯规。
        string needle = "GameAssets" + ".Current =";
        string? directory = FindSourceDirectory();

        if (directory is null)
        {
            return;
        }

        string[] offenders = Directory
            .EnumerateFiles(directory, "*.cs")
            .Where(file =>
            {
                string text = File.ReadAllText(file);

                return text.Contains(needle, StringComparison.Ordinal) &&
                    !text.Contains("[Collection(", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>Locates the directory this file lives in, the one callers are scanned in.</summary>
    /// <returns>The directory, or <see langword="null"/> when the sources are not beside the tests.</returns>
    private static string? FindSourceDirectory([CallerFilePath] string path = "")
    {
        string? directory = Path.GetDirectoryName(path);

        return directory is not null && Directory.Exists(directory) ? directory : null;
    }
}
