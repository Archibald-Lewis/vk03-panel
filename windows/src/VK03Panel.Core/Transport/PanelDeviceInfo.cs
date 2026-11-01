namespace VK03Panel.Core.Transport;

/// <summary>Everything read from the panel during handshake, for settings and diagnostics.</summary>
public sealed class PanelDeviceInfo
{
    public string UsbPath { get; init; } = string.Empty;

    public string HidPath { get; init; } = string.Empty;

    public string HidProductName { get; init; } = string.Empty;

    public int HidReportLength { get; init; }

    public ushort VendorId { get; init; }

    public ushort ProductId { get; init; }

    public string ChipSignature { get; init; } = string.Empty;

    public string ChipFamily { get; init; } = string.Empty;

    public int VideoPort { get; init; }

    public string PanelModel { get; init; } = string.Empty;

    public int PanelWidth { get; init; }

    public int PanelHeight { get; init; }

    public bool EdidValid { get; init; }

    public static readonly PanelDeviceInfo Empty = new();

    public string Summary =>
        $"{PanelModel} · {PanelWidth}×{PanelHeight} · 芯片 {ChipFamily} · 端口 {VideoPort} · HID {HidReportLength}B";
}
