namespace VK03Panel.Core.Transport;

/// <summary>A transport failure with a message intended for the status bar / diagnostics.</summary>
public sealed class PanelTransportException : Exception
{
    public PanelTransportException(string message)
        : base(message)
    {
    }

    public PanelTransportException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
