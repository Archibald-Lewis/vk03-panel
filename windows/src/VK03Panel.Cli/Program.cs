using System.Text;
using VK03Panel.Core.Cli;

namespace VK03Panel.Cli;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            return ConsoleCommands.Run(args, Console.Out);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"未处理的异常：{error}");
            return 3;
        }
    }
}
