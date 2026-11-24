using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.System;

/// <summary>
/// The parser of the <c>key = value</c> text files the reference reads with <c>TinyConfig</c>
/// (<c>engine/tinyconfig.cpp</c>), <c>fheroes2.cfg</c> above all.
/// </summary>
/// <remarks>
/// <para>
/// 参考拿它读 <c>fheroes2.cfg</c> 与热键文件：<c>TinyConfig( '=', '#' )</c> 意味着「名字」和「值」用 <c>=</c> 隔开、
/// <c>#</c> 起头的是注释。键会被 <c>ModifyKey()</c> 规整（小写、去首尾空白、连续空白压一个），所以 <c>Sound Volume</c>
/// 与 <c>sound  volume</c> 是同一个键；重复的键保留第一次出现的值，与参考的 <c>emplace</c> 一致。
/// </para>
/// <para>
/// <b>读文件与解析分开</b>：<see cref="LoadFile"/> 读磁盘，<see cref="Parse"/> 只吃文本。端口的测试与热键文件都用得上
/// 后者，而且配置文件的位置是宿主的事（参考拼 <c>GetConfigDirectory()</c>，端口没有那个函数）。
/// </para>
/// <para>
/// <b>数值解析照参考的严格程度</b>：<c>IntParams()</c> 用 <c>std::from_chars</c>，整串必须是数字，否则给 0；
/// <see cref="PointParams"/> 只认 <c>[ x, y ]</c>，<see cref="ResolutionParams"/> 认新格式
/// <c>宽x高:宽x高</c> 与旧格式 <c>宽x高x倍数</c>，格式不对一律回退到调用方给的默认值。
/// </para>
/// </remarks>
public sealed class TinyConfig
{
    private static readonly Regex PointPattern =
        new(@"^\[ *(-?[0-9]+) *, *(-?[0-9]+) *\]$", RegexOptions.CultureInvariant);

    private static readonly Regex ResolutionPattern =
        new(@"^ *([0-9]+) *x *([0-9]+) *: *([0-9]+) *x *([0-9]+) *$", RegexOptions.CultureInvariant);

    private static readonly Regex LegacyResolutionPattern =
        new(@"^ *([0-9]+) *x *([0-9]+) *x *([0-9]+) *$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);

    private readonly char _separator;

    private readonly char _comment;

    /// <summary>
    /// Creates an empty parser, <c>TinyConfig( sep, com )</c>.
    /// </summary>
    /// <param name="separator">The character between a key and its value; <c>'='</c> in both files of the reference.</param>
    /// <param name="comment">The character that starts a comment line; <c>'#'</c> in both files of the reference.</param>
    public TinyConfig(char separator = '=', char comment = '#')
    {
        _separator = separator;
        _comment = comment;
    }

    /// <summary>
    /// Parses the text of a configuration file, the body of <c>TinyConfig::Load()</c> after the file is read.
    /// </summary>
    /// <remarks>
    /// 空行与 <c>#</c> 起头的行跳过；没有分隔符的行也跳过（参考如此，不报错）。值只去掉首尾空白，中间的空白原样留着，
    /// 因为 <c>lang</c> 之类的值可能就是带空格的句子。
    /// </remarks>
    /// <param name="content">The text of the file.</param>
    public void Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line[0] == _comment)
            {
                continue;
            }

            int position = line.IndexOf(_separator, StringComparison.Ordinal);

            if (position < 0)
            {
                continue;
            }

            string key = ModifyKey(line[..position]);
            string value = line[(position + 1)..].Trim();

            _entries.TryAdd(key, value);
        }
    }

    /// <summary>Reads and parses a configuration file, <c>TinyConfig::Load()</c>.</summary>
    /// <param name="filePath">The file to read; the caller builds the path.</param>
    /// <returns><see langword="false"/> when the file cannot be read, like the reference.</returns>
    public bool LoadFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        string content;

        try
        {
            content = File.ReadAllText(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              ArgumentException or NotSupportedException)
        {
            return false;
        }

        Parse(content);

        return true;
    }

    /// <summary>Whether the file had a value for <paramref name="key"/>, <c>TinyConfig::Exists()</c>.</summary>
    /// <param name="key">The key, in any case and spacing.</param>
    /// <returns><see langword="true"/> when the key was present.</returns>
    public bool Exists(string key) => _entries.ContainsKey(ModifyKey(key));

    /// <summary>The value of <paramref name="key"/>, or an empty string, <c>TinyConfig::StrParams()</c>.</summary>
    /// <param name="key">The key, in any case and spacing.</param>
    /// <returns>The value as written, or an empty string.</returns>
    public string StrParams(string key) => _entries.TryGetValue(ModifyKey(key), out string? value) ? value : string.Empty;

    /// <summary>The value of <paramref name="key"/> as an integer, <c>TinyConfig::IntParams()</c>.</summary>
    /// <remarks>
    /// 少一个键给 0，值不是整串数字也给 0——参考的 <c>std::from_chars</c> 不接受尾随垃圾，端口照此。
    /// </remarks>
    /// <param name="key">The key, in any case and spacing.</param>
    /// <returns>The number, or 0 when the key is missing or not a number.</returns>
    public int IntParams(string key) =>
        _entries.TryGetValue(ModifyKey(key), out string? value) &&
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number)
            ? number
            : 0;

    /// <summary>The value of <paramref name="key"/> as a point, <c>TinyConfig::PointParams()</c>.</summary>
    /// <remarks>只认 <c>[ x, y ]</c>，方括号内的空格可有可无，坐标可带负号。</remarks>
    /// <param name="key">The key, in any case and spacing.</param>
    /// <param name="fallbackValue">The point returned when the key is missing or malformed.</param>
    /// <returns>The parsed point, or <paramref name="fallbackValue"/>.</returns>
    public Point PointParams(string key, Point fallbackValue)
    {
        if (!_entries.TryGetValue(ModifyKey(key), out string? value))
        {
            return fallbackValue;
        }

        Match match = PointPattern.Match(value);

        if (!match.Success)
        {
            return fallbackValue;
        }

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out int x) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out int y))
        {
            return fallbackValue;
        }

        return new Point(x, y);
    }

    /// <summary>The value of <paramref name="key"/> as a resolution, <c>TinyConfig::ResolutionParams()</c>.</summary>
    /// <remarks>
    /// <para>
    /// 先认新格式 <c>游戏宽 x 游戏高 : 窗口宽 x 窗口高</c>，再认旧格式 <c>游戏宽 x 游戏高 x 倍数</c>。旧格式的倍数
    /// 为 0 或乘出来会溢出 <see cref="int"/> 时回退——参考在这两处也回退，因为乘出来的窗口尺寸会变成负数或乱码。
    /// </para>
    /// </remarks>
    /// <param name="key">The key, in any case and spacing.</param>
    /// <param name="fallbackValue">The resolution returned when the key is missing or malformed.</param>
    /// <returns>The parsed resolution, or <paramref name="fallbackValue"/>.</returns>
    public ResolutionInfo ResolutionParams(string key, ResolutionInfo fallbackValue)
    {
        if (!_entries.TryGetValue(ModifyKey(key), out string? value))
        {
            return fallbackValue;
        }

        if (ResolutionPattern.Match(value) is { Success: true } match &&
            TryParseInt(match.Groups[1].Value, out int gameWidth) &&
            TryParseInt(match.Groups[2].Value, out int gameHeight) &&
            TryParseInt(match.Groups[3].Value, out int screenWidth) &&
            TryParseInt(match.Groups[4].Value, out int screenHeight))
        {
            return new ResolutionInfo(gameWidth, gameHeight, screenWidth, screenHeight);
        }

        if (LegacyResolutionPattern.Match(value) is { Success: true } legacy &&
            TryParseInt(legacy.Groups[1].Value, out int legacyWidth) &&
            TryParseInt(legacy.Groups[2].Value, out int legacyHeight) &&
            TryParseInt(legacy.Groups[3].Value, out int multiplier))
        {
            if (multiplier == 0 ||
                legacyWidth > int.MaxValue / multiplier ||
                legacyHeight > int.MaxValue / multiplier)
            {
                return fallbackValue;
            }

            return new ResolutionInfo(legacyWidth, legacyHeight, legacyWidth * multiplier,
                legacyHeight * multiplier);
        }

        return fallbackValue;
    }

    /// <summary>
    /// The key normalization of the reference's <c>ModifyKey()</c>: lower case, trimmed, runs of white space collapsed
    /// into one space.
    /// </summary>
    /// <param name="key">The key as written in the file or asked for by a caller.</param>
    /// <returns>The canonical form of the key.</returns>
    public static string ModifyKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        StringBuilder builder = new(key.Length);

        foreach (char character in key)
        {
            char normalized = char.IsWhiteSpace(character) ? ' ' : char.ToLowerInvariant(character);

            // 连续空白压成一个：参考先 unique() 再 replace_if()，效果相同。
            if (normalized == ' ' && builder.Length > 0 && builder[^1] == ' ')
            {
                continue;
            }

            builder.Append(normalized);
        }

        return builder.ToString().Trim();
    }

    private static bool TryParseInt(string value, out int number) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
}
