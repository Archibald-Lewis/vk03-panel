using System.IO;

namespace VK03Panel.Core.Cli;

/// <summary>Command-line entry points shared by VK03Panel.exe and VK03Panel.Cli.exe.</summary>
public static class ConsoleCommands
{
    public static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0)
        {
            PrintUsage(output);
            return 1;
        }

        var command = args[0];
        switch (command.ToLowerInvariant())
        {
            case "--self-test":
                return SelfTestCommand.Run(args, output);
            case "--demo":
                return DemoCommand.Run(args, output);
            case "--render":
                return RenderCommand.Run(args, output);
            case "--diagnose":
                return DiagnoseCommand.Run(args, output);
            case "--amd-dump":
            case "--repair-touch":
            case "--touch-status":
                return ElevatedCommands.Run(args, output);
            case "--help":
            case "-h":
            case "/?":
                PrintUsage(output);
                return 0;
            default:
                output.WriteLine($"未知参数：{command}");
                PrintUsage(output);
                return 1;
        }
    }

    public static void PrintUsage(TextWriter output)
    {
        output.WriteLine("VK03 Panel — VK03 USB 副屏监控（Windows 11）");
        output.WriteLine();
        output.WriteLine("用法：");
        output.WriteLine("  VK03Panel.exe                     启动托盘应用与设置窗口");
        output.WriteLine("  VK03Panel.exe --background        启动后最小化到托盘");
        output.WriteLine("  VK03Panel.exe --self-test [--frame] [--hold 秒] [--rotation 0|90|180|270]");
        output.WriteLine("                                    设备/驱动/芯片/EDID 自检；--frame 实际点亮");
        output.WriteLine("  VK03Panel.exe --demo <png> [--theme mac|win11|nebula|immersive] [--dark|--light]");
        output.WriteLine("  VK03Panel.exe --render <png>     使用本机真实数据渲染预览图");
        output.WriteLine("  VK03Panel.exe --diagnose         输出数据源与设备诊断摘要");
        output.WriteLine("  VK03Panel.exe --amd-dump         读取 AMD Ryzen Master SDK 并导出传感器明细（需管理员）");
        output.WriteLine("  VK03Panel.exe --repair-touch     重启/重装触屏设备（需管理员）");
        output.WriteLine("  VK03Panel.exe --touch-status     查看触屏 PnP 状态");
    }
}
