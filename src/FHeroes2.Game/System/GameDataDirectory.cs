namespace FHeroes2.Game.System;

/// <summary>
/// The per-user writable directory of the game, <c>System::GetDataDirectory()</c> plus the
/// <c>System::GetHomeDirectory()</c> it falls back on (<c>system.cpp:310-395</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在 Linux 上走 <c>XDG_DATA_HOME</c>（缺省 <c>~/.local/share</c>）、在 macOS 上走
/// <c>~/Library/Application Support</c>、在 Windows 上走 <c>$HOME/.fheroes2</c> 再退到 <c>%APPDATA%\fheroes2</c>。
/// 端口按同一套顺序取，只是把每条分支都放在一个方法里，测试通过 <see cref="OverrideRoot"/> 换成临时目录。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：参考在没有 <c>$HOME</c> 时会调 <c>SDL_GetPrefPath</c>；端口没有 SDL，这一档用
/// <see cref="Environment.SpecialFolder.ApplicationData"/> 兜底。三个平台的「有没有这个变量」判断保持一致，
/// 所以真机上两者的结果相同，只有环境变量被清空时才会走到这里。
/// </para>
/// </remarks>
public static class GameDataDirectory
{
    /// <summary>
    /// When set, every call to <see cref="Get"/> returns it instead of asking the operating system.
    /// </summary>
    /// <remarks>
    /// 给测试与 <c>--data-dir</c> 之类的启动参数用。设成 <see langword="null"/> 回到系统默认。
    /// </remarks>
    public static string? OverrideRoot { get; set; }

    /// <summary>
    /// The directory the game may write to, <c>System::GetDataDirectory( appName )</c> (<c>system.cpp:376-395</c>).
    /// </summary>
    /// <param name="appName">The per-game subdirectory, <c>"fheroes2"</c> for the reference.</param>
    /// <returns>An absolute path; the directory may not exist yet.</returns>
    /// <exception cref="ArgumentException"><paramref name="appName"/> is empty or whitespace.</exception>
    public static string Get(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        if (!string.IsNullOrEmpty(OverrideRoot))
        {
            return OverrideRoot;
        }

        // system.cpp:349-374 的分支顺序：先看各平台自己的变量，再看 $HOME 下的点目录。
        if (OperatingSystem.IsLinux())
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

            if (!string.IsNullOrEmpty(xdg))
            {
                return Path.Combine(xdg, appName);
            }

            return Path.Combine(GetHomeLinux(), appName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(GetHomeMac(), appName);
        }

        return Path.Combine(GetHomeWindows(appName), appName);
    }

    /// <summary><c>~/.local/share</c>, the Linux fallback of <c>GetDataDirectory</c>.</summary>
    private static string GetHomeLinux() =>
        Path.Combine(GetHomeOrAppData(), ".local", "share");

    /// <summary><c>~/Library/Application Support</c>, the macOS branch of <c>GetDataDirectory</c>.</summary>
    private static string GetHomeMac() =>
        Path.Combine(GetHomeOrAppData(), "Library", "Application Support");

    /// <summary>
    /// <c>$HOME/.fheroes2</c> on Windows, <c>%APPDATA%</c> when the home variable is missing
    /// (<c>system.cpp:196-244</c>).
    /// </summary>
    private static string GetHomeWindows(string appName)
    {
        string? home = Environment.GetEnvironmentVariable("HOME");

        if (!string.IsNullOrEmpty(home))
        {
            return Path.Combine(home, "." + appName);
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return string.IsNullOrEmpty(appData) ? Path.Combine(AppContext.BaseDirectory, appName) : appData;
    }

    /// <summary>The user's home directory, or the best approximation the platform offers.</summary>
    private static string GetHomeOrAppData()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return string.IsNullOrEmpty(home) ? AppContext.BaseDirectory : home;
    }
}
