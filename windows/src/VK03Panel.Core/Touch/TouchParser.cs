namespace VK03Panel.Core.Touch;

/// <summary>Primary contact of a PUYA touch report, normalised to 0..1.</summary>
public readonly record struct TouchContact(bool Touching, byte Id, double X, double Y);

/// <summary>
/// Parses the panel's touch controller reports. The protocol is report ID 1 followed by
/// up to five 6-byte contacts: tip flag, contact id, little-endian x and y (0..4096).
/// </summary>
public static class TouchParser
{
    public const int ReportId = 0x01;
    public const int ContactSize = 6;
    public const int ContactsPerReport = 5;
    public const int MinimumReportLength = 1 + ContactSize;
    public const double LogicalMaximum = 4096;

    /// <summary>Vendor reports on the wire are 31 bytes and omit the leading report ID.</summary>
    public const int VendorPayloadLength = 31;

    public static bool TryParse(ReadOnlySpan<byte> report, out TouchContact contact)
    {
        contact = default;
        if (report.Length < MinimumReportLength)
        {
            return false;
        }

        if (report[0] != ReportId)
        {
            return false;
        }

        var touching = (report[1] & 0x01) != 0;
        var id = report[2];
        var x = report[3] | (report[4] << 8);
        var y = report[5] | (report[6] << 8);
        if (x > 4096 || y > 4096)
        {
            return false;
        }

        contact = new TouchContact(touching, id, x / LogicalMaximum, y / LogicalMaximum);
        return true;
    }

    /// <summary>Normalises raw reports: the 31-byte vendor payload lacks the report-ID byte.</summary>
    public static byte[] Normalize(ReadOnlySpan<byte> report)
    {
        if (report.Length == VendorPayloadLength)
        {
            var copy = new byte[report.Length + 1];
            copy[0] = ReportId;
            report.CopyTo(copy.AsSpan(1));
            return copy;
        }

        return report.ToArray();
    }
}
