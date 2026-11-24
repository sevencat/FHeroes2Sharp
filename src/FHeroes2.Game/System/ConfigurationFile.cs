using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.System;

/// <summary>
/// Where the client keeps <c>fheroes2.cfg</c>, the file <see cref="Settings.Read(string)"/> and
/// <see cref="Settings.Save(string, ResolutionInfo?, Point?)"/> work on.
/// </summary>
/// <remarks>
/// <para>
/// 参考把这件事拆成三段：<c>System::GetConfigDirectory( "fheroes2" )</c> 给目录（Windows 是
/// <c>%APPDATA%\fheroes2</c>，见 <c>system.cpp:105-118</c>；<c>APPDATA</c> 缺失时退回 <c>SDL_GetPrefPath</c>），
/// <c>Settings::configFileName</c> 给文件名（<c>"fheroes2.cfg"</c>），<c>Settings::GetLastFile()</c> 在若干个候选
/// 目录里挑已经存在的那一份。
/// </para>
/// <para>
/// 端口只有一个候选目录，前两段够用；「取 %APPDATA%」留在宿主（<c>Environment.GetFolderPath</c>），这里收已经
/// 解析好的目录，是纯函数，能直接测。显式路径来自 <c>--config</c>——参考没有这个开关，端口加它是因为基线截图与
/// 冒烟不能被开发机上已有的配置文件影响（<c>--no-config</c> 则完全不碰文件）。
/// </para>
/// </remarks>
public static class ConfigurationFile
{
    /// <summary>The name of the file, <c>Settings::configFileName</c>.</summary>
    public const string FileName = "fheroes2.cfg";

    /// <summary>
    /// The folder below the user's application data, the argument of <c>System::GetConfigDirectory()</c>.
    /// </summary>
    public const string DirectoryName = "fheroes2";

    /// <summary>
    /// The path of the file inside a user's application data folder, <c>System::GetConfigDirectory()</c>
    /// joined with <c>Settings::configFileName</c>.
    /// </summary>
    /// <param name="appDataDirectory">
    /// The user's application data folder, i.e. <c>%APPDATA%</c>; <see langword="null"/> or blank when the
    /// platform has none.
    /// </param>
    /// <returns><c>&lt;appData&gt;\fheroes2\fheroes2.cfg</c>, or <see langword="null"/> without a folder.</returns>
    public static string? DefaultPath(string? appDataDirectory) =>
        string.IsNullOrWhiteSpace(appDataDirectory)
            ? null
            : Path.Combine(appDataDirectory, DirectoryName, FileName);

    /// <summary>
    /// The file the client should use: <paramref name="explicitFile"/> when given, the default otherwise,
    /// <c>Settings::GetLastFile()</c>.
    /// </summary>
    /// <param name="appDataDirectory">The user's application data folder, as for <see cref="DefaultPath"/>.</param>
    /// <param name="explicitFile">The file passed with <c>--config</c>, or <see langword="null"/>.</param>
    /// <returns>The file to read and write, or <see langword="null"/> when neither is available.</returns>
    public static string? Resolve(string? appDataDirectory, string? explicitFile) =>
        string.IsNullOrWhiteSpace(explicitFile) ? DefaultPath(appDataDirectory) : explicitFile;

    /// <summary>
    /// Creates the folder of <paramref name="filePath"/> when it does not exist yet, the
    /// <c>System::MakeDirectory( configDir )</c> of <c>game_init.cpp:221</c>.
    /// </summary>
    /// <param name="filePath">The file that is going to be written.</param>
    /// <returns><see langword="true"/> when the folder is there afterwards.</returns>
    public static bool EnsureDirectory(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));

        if (string.IsNullOrEmpty(directory))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(directory);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
