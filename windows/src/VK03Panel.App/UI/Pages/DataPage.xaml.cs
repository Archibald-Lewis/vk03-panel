using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VK03Panel.App;
using VK03Panel.Core.Config;
using VK03Panel.Core.Platform;
using VK03Panel.Core.Runtime;

namespace VK03Panel.App.UI.Pages;

public partial class DataPage : UserControl, ISettingsPage
{
    private readonly PanelWorker _worker;
    private DispatcherTimer? _repairPoll;
    private DateTime _repairStarted;

    public DataPage(PanelWorker worker)
    {
        _worker = worker;
        InitializeComponent();

        ElevationText.Text = Elevation.IsElevated
            ? "管理员（可读取 AMD 传感器）"
            : "标准权限（AMD 驱动不可访问）";
        ElevateButton.Visibility = Elevation.IsElevated ? Visibility.Collapsed : Visibility.Visible;

        UpdateStatus(worker.Status);
        ShowExistingRepairResult();
    }

    public void UpdateStatus(PanelWorkerStatus status)
    {
        var hardware = _worker.Metrics.Hardware;
        CpuText.Text = string.IsNullOrEmpty(hardware.CpuModel)
            ? "—"
            : $"{hardware.CpuModel} · {hardware.PhysicalCores} 核 / {hardware.LogicalCores} 线程";

        MemoryText.Text = hardware.MemoryTotalGb.HasValue
            ? $"{hardware.MemoryTotalGb:0.#} GB · {hardware.MemoryModules} · {hardware.MemorySpeed}"
            : "—";

        GpuText.Text = _worker.Metrics.GpuName + (_worker.Metrics.HasGpuTelemetry ? " · NVML" : string.Empty);
        GpuTelemetryText.Text = status.Metrics.GpuLoad.HasValue
            ? $"{(status.Metrics.GpuLoad.Value * 100).ToString("0", CultureInfo.InvariantCulture)}%" +
              (status.Metrics.GpuTemperatureCelsius.HasValue
                  ? $" · {status.Metrics.GpuTemperatureCelsius.Value:0} °C"
                  : string.Empty)
            : "—";

        TemperatureText.Text = status.Metrics.CpuTemperatureCelsius.HasValue
            ? $"{status.Metrics.CpuTemperatureCelsius.Value:0.0} °C"
            : "—";
        BridgeText.Text = status.TemperatureSource;
        AmdText.Text = _worker.Metrics.AmdSdkStatus;

        MediaText.Text = status.MediaTitle + (status.MediaPlaying ? "（播放中）" : string.Empty);
        VolumeText.Text = status.Volume.HasValue
            ? $"{(status.Volume.Value * 100).ToString("0", CultureInfo.InvariantCulture)}%{(status.Muted ? " · 已静音" : string.Empty)}"
            : "—";
        TouchText.Text = status.TouchConnected
            ? $"{status.TouchBackend} · {status.TouchReports} 个报告"
            : "等待触屏";
    }

    private void OnElevateClicked(object sender, RoutedEventArgs e)
    {
        if (Elevation.IsElevated)
        {
            ElevationText.Text = "已是管理员权限";
            return;
        }

        ElevationText.Text = "正在请求管理员权限…";
        if (Elevation.Relaunch("--elevate-wait"))
        {
            ((App)Application.Current).ExitApplication();
        }
        else
        {
            ElevationText.Text = "已取消授权；CPU 温度暂时只能来自 HWiNFO / LibreHardwareMonitor。";
        }
    }

    private void OnRepairTouchClicked(object sender, RoutedEventArgs e)
    {
        var resultPath = TouchDeviceRepair.ResultPath;
        try
        {
            if (File.Exists(resultPath))
            {
                File.Delete(resultPath);
            }
        }
        catch (Exception)
        {
            // A stale result is harmless; the poll checks the timestamp.
        }

        _repairStarted = DateTime.UtcNow;
        RepairText.Text = "已请求管理员权限；在上方的 UAC 提示中允许后会自动修复触屏…";

        if (!Elevation.Relaunch("--repair-touch"))
        {
            RepairText.Text = "未能启动管理员修复进程（UAC 可能被取消）。";
            return;
        }

        StartRepairPolling();
    }

    private void StartRepairPolling()
    {
        _repairPoll?.Stop();
        _repairPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _repairPoll.Tick += (_, _) =>
        {
            try
            {
                if (!File.Exists(TouchDeviceRepair.ResultPath))
                {
                    return;
                }

                if (File.GetLastWriteTimeUtc(TouchDeviceRepair.ResultPath) < _repairStarted.AddSeconds(-2))
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            _repairPoll?.Stop();
            var result = TouchDeviceRepair.ReadResult();
            if (result is null)
            {
                RepairText.Text = "修复进程已结束，但结果文件无法读取。";
                return;
            }

            RepairText.Text = result.Success
                ? $"修复成功：{result.After?.Name} 已启动（{result.After?.Status}）。"
                : $"修复未成功：{result.Error}\n{string.Join("\n", result.Steps)}";

            if (result.Success)
            {
                _worker.Reconnect();
                _worker.RefreshSensors();
            }

            UpdateStatus(_worker.Status);
        };
        _repairPoll.Start();
    }

    private void OnTouchStatusClicked(object sender, RoutedEventArgs e)
    {
        var status = TouchDeviceRepair.Query();
        RepairText.Text = status is null
            ? "未找到触屏设备（374a:a401）。请检查内部 USB 线是否连接。"
            : $"触屏：{status.Name} · {status.Status} · 问题代码 {status.ProblemCode}\n{status.InstanceId}";
        UpdateStatus(_worker.Status);
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        _worker.RefreshSensors();
        UpdateStatus(_worker.Status);
    }

    private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.DataDirectory) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening Explorer is best effort.
        }
    }

    private void ShowExistingRepairResult()
    {
        var result = TouchDeviceRepair.ReadResult();
        if (result is null)
        {
            return;
        }

        RepairText.Text = result.Success
            ? $"上次修复成功：{result.After?.Name} 已启动。"
            : $"上次修复未成功：{result.Error}";
    }
}
