namespace FHeroes2.Core.Diagnostics;

/// <summary>
/// The text support mode channel: <c>Logging::setTextSupportMode()</c> / <c>isTextSupportModeEnabled()</c>
/// (<c>logging.cpp:148-155</c>) plus the <c>START_TEXT_SUPPORT_MODE</c> block of <c>logging.h:97-201</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考用一串平台分支的 <c>COUT</c> 宏把每一行写到 stderr 或日志文件，并在每块的进出各写一行十个短横线
/// （<c>Logging::TextSupportLogger</c>，<c>logging.h:179-201</c>），块内函数只在模式打开时才输出。
/// </para>
/// <para>
/// 端口只有 <see cref="GameLog"/>，而它给每行加 <c>[INFO]</c> 之类的前缀，外部工具解析起来不方便，所以这里
/// 自己写裸行。模式位仍然由 <c>Settings</c> 持有（<c>setTextSupportMode()</c>，<c>settings.cpp:824-836</c>），
/// 写入时同步到这里，与参考的 <c>Logging::textSupportMode</c> 一致。
/// </para>
/// </remarks>
public static class TextSupport
{
    /// <summary>The line <c>Logging::TextSupportLogger</c> writes at both ends of a block.</summary>
    public const string Separator = "----------";

    /// <summary>Mirror of <c>Logging::textSupportMode</c>, set by <c>Settings.SetTextSupportMode()</c>.</summary>
    public static bool IsEnabled { get; set; }

    /// <summary>
    /// Where the blocks go. <see langword="null"/> means the console, which is what the default branch of the
    /// reference's <c>COUT</c> writes to (<c>logging.h:146-152</c>); the tests point it somewhere they can read.
    /// </summary>
    public static TextWriter? Output { get; set; }

    /// <summary>
    /// Writes one block the way <c>START_TEXT_SUPPORT_MODE</c> does: nothing when the mode is off, otherwise the
    /// separator, the lines and the separator again.
    /// </summary>
    /// <param name="lines">The lines of the block, already formatted.</param>
    public static void WriteBlock(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (!IsEnabled)
        {
            return;
        }

        TextWriter output = Output ?? Console.Out;

        output.WriteLine(Separator);
        foreach (string line in lines)
        {
            output.WriteLine(line);
        }

        output.WriteLine(Separator);
        output.Flush();
    }
}
