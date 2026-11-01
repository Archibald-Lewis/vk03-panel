using VK03Panel.Core.Touch;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class TouchParserTests
{
    [Fact]
    public void TryParse_RealDevicePressAndRelease()
    {
        // Captured from the physical 374a:a401 controller after the idle-read fix.
        var press = Convert.FromHexString("0101002606740800000000000000000000000001037903D50200000000000000");
        var release = Convert.FromHexString("0100000000000000000000000000000000000001037903D50200000000000000");
        Assert.True(TouchParser.TryParse(TouchParser.Normalize(press), out var down));
        Assert.True(down.Touching);
        Assert.Equal(1574 / 4096.0, down.X);
        Assert.Equal(2164 / 4096.0, down.Y);
        Assert.True(TouchParser.TryParse(TouchParser.Normalize(release), out var up));
        Assert.False(up.Touching);
    }

    private static byte[] Report(bool touching, byte id, ushort x, ushort y)
    {
        var report = new byte[32];
        report[0] = TouchParser.ReportId;
        report[1] = touching ? (byte)0x01 : (byte)0x00;
        report[2] = id;
        report[3] = (byte)(x & 0xFF);
        report[4] = (byte)(x >> 8);
        report[5] = (byte)(y & 0xFF);
        report[6] = (byte)(y >> 8);
        return report;
    }

    [Fact]
    public void TryParse_ReadsPrimaryContact()
    {
        var report = Report(true, 2, 2048, 1024);
        Assert.True(TouchParser.TryParse(report, out var contact));
        Assert.True(contact.Touching);
        Assert.Equal(2, contact.Id);
        Assert.Equal(0.5, contact.X, 3);
        Assert.Equal(0.25, contact.Y, 3);
    }

    [Fact]
    public void TryParse_DetectsRelease()
    {
        Assert.True(TouchParser.TryParse(Report(false, 0, 0, 0), out var contact));
        Assert.False(contact.Touching);
    }

    [Fact]
    public void TryParse_RejectsUnknownReportId()
    {
        var report = Report(true, 0, 10, 10);
        report[0] = 0x07;
        Assert.False(TouchParser.TryParse(report, out _));
    }

    [Fact]
    public void Normalize_PrependsReportIdForVendorPayload()
    {
        var payload = new byte[TouchParser.VendorPayloadLength];
        payload[0] = 0x01;
        var normalized = TouchParser.Normalize(payload);
        Assert.Equal(payload.Length + 1, normalized.Length);
        Assert.Equal(TouchParser.ReportId, normalized[0]);
    }

    [Fact]
    public void Normalize_KeepsWindowsReport()
    {
        var report = Report(true, 1, 100, 200);
        var normalized = TouchParser.Normalize(report);
        Assert.Equal(report.Length, normalized.Length);
        Assert.Equal(report[0], normalized[0]);
    }
}
