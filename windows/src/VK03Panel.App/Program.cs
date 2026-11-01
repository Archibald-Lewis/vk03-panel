using System.IO;
using System.Runtime.InteropServices;
using VK03Panel.Core.Cli;

namespace VK03Panel.App;

internal static class Program
{
    private static readonly string[] ConsoleCommandNames =
    {
        "--self-test",
        "--demo",
        "--render",
        "--diagnose",
        "--amd-dump",
        "--repair-touch",
        "--touch-status",
        "--help",
        "-h",
        "/?",
    };

    [STAThread]
    private static int Main(string[] args)
    {
        var wantsConsole = args.Length > 0 &&
                           ConsoleCommandNames.Contains(args[0], StringComparer.OrdinalIgnoreCase);
        if (wantsConsole)
        {
            AttachConsole(-1);
            var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(output);
            return ConsoleCommands.Run(args, output);
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
        return 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);
}
