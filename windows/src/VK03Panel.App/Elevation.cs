using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace VK03Panel.App;

/// <summary>Detects the current token elevation and can relaunch the app with UAC.</summary>
public static class Elevation
{
    public static bool IsElevated { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Relaunches the executable through ShellExecute "runas". Returns false when UAC is cancelled.</summary>
    public static bool Relaunch(params string[] arguments)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = JoinArguments(arguments),
                WorkingDirectory = AppContext.BaseDirectory,
            };
            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string JoinArguments(IEnumerable<string> arguments)
    {
        var builder = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            if (argument.Contains(' '))
            {
                builder.Append('"').Append(argument).Append('"');
            }
            else
            {
                builder.Append(argument);
            }
        }

        return builder.ToString();
    }
}
