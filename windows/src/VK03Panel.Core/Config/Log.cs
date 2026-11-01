using System.IO;
using System.Text;

namespace VK03Panel.Core.Config;

/// <summary>Small rolling file logger under %LOCALAPPDATA%\VK03Panel\logs.</summary>
public static class Log
{
    private static readonly object Sync = new();
    private static readonly string LogFile = Path.Combine(AppPaths.LogDirectory, "panel.log");
    private const long MaxBytes = 1_000_000;
    private const int MaxFiles = 5;

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? error = null) => Write("ERROR", message, error);

    private static void Write(string level, string message, Exception? error)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                RotateIfNeeded();
                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.Append(" [").Append(level).Append("] ").Append(message);
                if (error is not null)
                {
                    builder.Append(" | ").Append(error.GetType().Name).Append(": ").Append(error.Message);
                }

                File.AppendAllText(LogFile, builder.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Logging is best effort.
        }
    }

    private static void RotateIfNeeded()
    {
        var info = new FileInfo(LogFile);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }

        for (var index = MaxFiles - 1; index >= 1; index--)
        {
            var source = $"{LogFile}.{index}";
            var destination = $"{LogFile}.{index + 1}";
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }

        File.Move(LogFile, LogFile + ".1", overwrite: true);
    }
}
