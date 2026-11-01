using System.IO;
using System.Diagnostics;
using System.Management;
using System.Text.Json;

namespace VK03Panel.Core.Platform;

/// <summary>Status of the VK03 touch controller as Windows currently sees it.</summary>
public sealed record TouchDeviceStatus(
    string InstanceId,
    string Name,
    string Status,
    int ProblemCode,
    bool Started);

/// <summary>
/// Repairs the PUYA touch controller (374a:a401) when Windows failed to start it
/// (problem code 10). Requires an elevated process because PnP restart/removal does.
/// </summary>
public static class TouchDeviceRepair
{
    private const string VendorPattern = "VID_374A";

    public static TouchDeviceStatus? Query()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID, ConfigManagerErrorCode, Status FROM Win32_PnPEntity " +
                $"WHERE PNPDeviceID LIKE '%{VendorPattern}%'");
            using var results = searcher.Get();
            foreach (var item in results)
            {
                var instanceId = item["PNPDeviceID"] as string ?? string.Empty;
                if (instanceId.Length == 0)
                {
                    continue;
                }

                var status = new TouchDeviceStatus(
                    instanceId,
                    item["Name"] as string ?? string.Empty,
                    item["Status"] as string ?? string.Empty,
                    item["ConfigManagerErrorCode"] is null ? -1 : Convert.ToInt32(item["ConfigManagerErrorCode"]),
                    string.Equals(item["Status"] as string, "OK", StringComparison.OrdinalIgnoreCase));
                item.Dispose();
                return status;
            }
        }
        catch (Exception)
        {
            // WMI is best effort.
        }

        return null;
    }

    public static TouchRepairResult Repair(Action<string>? log = null)
    {
        var steps = new List<string>();
        var before = Query();
        steps.Add(before is null
            ? "未找到触屏设备（374a:a401）"
            : $"修复前：{before.Name} · {before.Status} · 问题代码 {before.ProblemCode}");
        log?.Invoke(steps[^1]);

        if (before is null)
        {
            return new TouchRepairResult(false, before, null, steps, "未找到触屏设备");
        }

        if (before.Started)
        {
            steps.Add("设备已启动，无需修复");
            return new TouchRepairResult(true, before, before, steps, null);
        }

        RunPnPUtil($"/restart-device \"{before.InstanceId}\"", steps, log);
        Thread.Sleep(2500);
        var after = Query();
        steps.Add(Describe("重启设备后", after));
        log?.Invoke(steps[^1]);

        if (after is { Started: true })
        {
            return new TouchRepairResult(true, before, after, steps, null);
        }

        RunPnPUtil($"/remove-device \"{before.InstanceId}\"", steps, log);
        Thread.Sleep(800);
        RunPnPUtil("/scan-devices", steps, log);
        Thread.Sleep(5000);
        after = Query();
        steps.Add(Describe("重新扫描后", after));
        log?.Invoke(steps[^1]);

        return new TouchRepairResult(after is { Started: true }, before, after, steps, after is { Started: true } ? null : "设备仍未启动");
    }

    private static string Describe(string prefix, TouchDeviceStatus? status) =>
        status is null
            ? $"{prefix}：设备未出现"
            : $"{prefix}：{status.Status} · 问题代码 {status.ProblemCode}";

    private static void RunPnPUtil(string arguments, List<string> steps, Action<string>? log)
    {
        try
        {
            var startInfo = new ProcessStartInfo("pnputil.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                steps.Add($"pnputil {arguments}：无法启动");
                return;
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            steps.Add($"pnputil {arguments}：退出码 {process.ExitCode} {output.Trim()}");
            log?.Invoke(steps[^1]);
        }
        catch (Exception error)
        {
            steps.Add($"pnputil {arguments}：{error.Message}");
            log?.Invoke(steps[^1]);
        }
    }

    public static string ResultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VK03Panel",
        "touch-repair.json");

    public static void WriteResult(TouchRepairResult result)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ResultPath, json);
        }
        catch (Exception)
        {
            // Diagnostics are best effort.
        }
    }

    public static TouchRepairResult? ReadResult()
    {
        try
        {
            if (!File.Exists(ResultPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<TouchRepairResult>(File.ReadAllText(ResultPath));
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public sealed record TouchRepairResult(
    bool Success,
    TouchDeviceStatus? Before,
    TouchDeviceStatus? After,
    IReadOnlyList<string> Steps,
    string? Error);
