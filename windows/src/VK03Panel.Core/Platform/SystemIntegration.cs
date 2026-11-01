using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VK03Panel.Core.Platform;

/// <summary>Session lock through user32, the Windows equivalent of the macOS login helper.</summary>
public static class LockScreen
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool LockWorkStation();

    public static bool IsAvailable => Environment.UserInteractive && OperatingSystem.IsWindows();

    public static bool Lock()
    {
        try
        {
            return LockWorkStation();
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>Per-user login startup through the classic Run key.</summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VK03Panel";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static string? CurrentCommand
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static bool SetEnabled(bool enabled, string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return false;
                }

                key.SetValue(ValueName, $"\"{executablePath}\" --background");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>
/// Elevated login startup through Task Scheduler. A "highest available" task starts the app
/// without a UAC prompt, which is required for the AMD sensor driver. Creating or deleting the
/// task needs administrator rights.
/// </summary>
public static class ScheduledStartup
{
    public const string TaskName = "VK03Panel";

    public static bool Exists()
    {
        try
        {
            return RunSchtasks($"/Query /TN \"{TaskName}\"", out _) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool Create(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            var command = $"\\\"{executablePath}\\\" --background";
            var result = RunSchtasks(
                $"/Create /F /RL HIGHEST /SC ONLOGON /TN \"{TaskName}\" /TR \"{command}\"",
                out _);
            return result == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool Delete()
    {
        try
        {
            var result = RunSchtasks($"/Delete /F /TN \"{TaskName}\"", out _);
            return result == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static int RunSchtasks(string arguments, out string output)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process is null)
        {
            output = string.Empty;
            return -1;
        }

        output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(15000);
        return process.ExitCode;
    }
}
