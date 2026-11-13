namespace FHeroes2.Core.Diagnostics;

/// <summary>Severity of a <see cref="GameLog"/> message.</summary>
public enum GameLogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    None = 4,
}

/// <summary>
/// Tiny, dependency free logger. The reference implementation relies on <c>DEBUG_LOG</c>,
/// <c>ERROR_LOG</c> and friends; the C# port keeps the same idea but with an explicit level
/// so a release build can silence verbose output.
/// </summary>
public static class GameLog
{
    private static readonly object SyncRoot = new();

    private static GameLogLevel _minimumLevel = ReadLevelFromEnvironment();

    /// <summary>Minimum level that is written to the console.</summary>
    public static GameLogLevel MinimumLevel
    {
        get => _minimumLevel;
        set => _minimumLevel = value;
    }

    public static void Debug(string message) => Write(GameLogLevel.Debug, message);

    public static void Info(string message) => Write(GameLogLevel.Info, message);

    public static void Warn(string message) => Write(GameLogLevel.Warning, message);

    public static void Error(string message) => Write(GameLogLevel.Error, message);

    public static void Error(string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write(GameLogLevel.Error, $"{message} :: {exception.GetType().Name}: {exception.Message}");
    }

    public static bool IsEnabled(GameLogLevel level) => level >= _minimumLevel && level != GameLogLevel.None;

    private static void Write(GameLogLevel level, string message)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        lock (SyncRoot)
        {
            Console.WriteLine($"[{level.ToString().ToUpperInvariant(),-7}] {message}");
            if (level >= GameLogLevel.Warning)
            {
                Console.Out.Flush();
            }
        }
    }

    private static GameLogLevel ReadLevelFromEnvironment()
    {
        string? raw = Environment.GetEnvironmentVariable("FHEROES2_LOG_LEVEL");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return GameLogLevel.Info;
        }

        return Enum.TryParse(raw, ignoreCase: true, out GameLogLevel parsed) ? parsed : GameLogLevel.Info;
    }
}
