using System.Windows;
using VK03Panel.Core.Themes;

namespace VK03Panel.Core.Touch;

/// <summary>Callbacks the touch layer needs from the panel worker.</summary>
public interface ITouchSink
{
    void PerformAction(PanelAction action);

    void SetPressedAction(PanelAction? action);

    void SetVolumeFraction(double fraction);

    void SetLockProgress(double progress);

    void TouchStateChanged(string backend, bool connected, long reports, string error);
}

/// <summary>
/// Turns raw touch reports into the same gesture model as the macOS app: tap actions,
/// a volume scrub, a 44 px cancel threshold and hold-to-lock with progress.
/// </summary>
public sealed class TouchController : IDisposable
{
    private static readonly TimeSpan LockHoldDuration = TimeSpan.FromSeconds(1.2);
    private const double CancelDistance = 44;

    private readonly ITouchSource _hid = new HidTouchSource();
    private readonly ITouchSource _usb = new LibUsb0TouchSource();
    private readonly Func<int> _rotation;
    private readonly Func<IPanelTheme> _theme;
    private readonly ITouchSink _sink;
    private readonly object _sync = new();
    private readonly Timer _stateTimer;

    private ITouchSource? _activeSource;
    private TouchContact? _activeContact;
    private PanelAction? _pressedAction;
    private Point _downPoint;
    private Timer? _lockTimer;
    private DateTime _lockStarted;
    private bool _started;

    public TouchController(Func<int> rotation, Func<IPanelTheme> theme, ITouchSink sink)
    {
        _rotation = rotation;
        _theme = theme;
        _sink = sink;
        _stateTimer = new Timer(_ => PublishState(), null, 1000, 2000);
    }

    public string Backend => _hid.IsConnected ? _hid.Name : _usb.IsConnected ? _usb.Name : "等待触屏";

    public bool IsConnected => _hid.IsConnected || _usb.IsConnected;

    public long Reports => _hid.Reports + _usb.Reports;

    public string LastError => _hid.IsConnected ? _hid.LastError : _usb.LastError;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _hid.ReportReceived += OnReport;
        _usb.ReportReceived += OnReport;
        _hid.Start();
        _usb.Start();
    }

    private void OnReport(byte[] report)
    {
        try
        {
            var source = _hid.IsConnected ? _hid : _usb.IsConnected ? _usb : null;
            if (source is not null && _activeSource != source)
            {
                _activeSource = source;
                PublishState();
            }

            if (!TouchParser.TryParse(report, out var contact))
            {
                return;
            }

            lock (_sync)
            {
                Handle(contact);
            }
        }
        catch (Exception)
        {
            // A malformed report must never break the reader thread.
        }
    }

    private void Handle(TouchContact contact)
    {
        var point = ToLogicalPoint(contact);
        if (contact.Touching)
        {
            if (_activeContact is null)
            {
                _activeContact = contact;
                _pressedAction = _theme().HitTest(point);
                _downPoint = point;
                _sink.SetPressedAction(_pressedAction);

                if (_pressedAction == PanelAction.Volume)
                {
                    ScrubVolume(point);
                }
                else if (_pressedAction == PanelAction.Lock)
                {
                    StartLockHold();
                }
            }
            else
            {
                _activeContact = contact;
                if (_pressedAction == PanelAction.Volume)
                {
                    ScrubVolume(point);
                }
                else if (_pressedAction is not null)
                {
                    var moved = Math.Sqrt(
                        ((point.X - _downPoint.X) * (point.X - _downPoint.X)) +
                        ((point.Y - _downPoint.Y) * (point.Y - _downPoint.Y)));
                    if (moved > CancelDistance || _theme().HitTest(point) != _pressedAction)
                    {
                        CancelGesture();
                    }
                }
            }

            return;
        }

        var wasPressed = _pressedAction;
        var releasePoint = _activeContact is { } previous ? ToLogicalPoint(previous) : point;
        _activeContact = null;
        _pressedAction = null;
        StopLockHold();

        if (wasPressed is not null &&
            wasPressed is not (PanelAction.Volume or PanelAction.Lock) &&
            _theme().HitTest(releasePoint) == wasPressed)
        {
            _sink.PerformAction(wasPressed.Value);
        }

        _sink.SetPressedAction(null);
    }

    private void ScrubVolume(Point point)
    {
        var theme = _theme();
        var fraction = (point.X - theme.SliderLeft) / Math.Max(1, theme.SliderWidth);
        _sink.SetVolumeFraction(Math.Clamp(fraction, 0, 1));
    }

    private void StartLockHold()
    {
        _lockStarted = DateTime.UtcNow;
        _lockTimer?.Dispose();
        _lockTimer = new Timer(OnLockTick, null, 0, 80);
    }

    private void OnLockTick(object? state)
    {
        var progress = (DateTime.UtcNow - _lockStarted).TotalSeconds / LockHoldDuration.TotalSeconds;
        _sink.SetLockProgress(progress);
        if (progress < 1)
        {
            return;
        }

        StopLockHold();
        _pressedAction = null;
        _sink.SetPressedAction(null);
        _sink.PerformAction(PanelAction.Lock);
    }

    private void StopLockHold()
    {
        _lockTimer?.Dispose();
        _lockTimer = null;
        _sink.SetLockProgress(0);
    }

    private void CancelGesture()
    {
        _pressedAction = null;
        _sink.SetPressedAction(null);
        StopLockHold();
    }

    private Point ToLogicalPoint(TouchContact contact)
    {
        var rawX = Math.Clamp(contact.X, 0, 1);
        var rawY = Math.Clamp(contact.Y, 0, 1);
        return _rotation() is 180 or 270
            ? new Point((1 - rawY) * 959, rawX * 359)
            : new Point(rawY * 959, (1 - rawX) * 359);
    }

    private void PublishState()
    {
        try
        {
            _sink.TouchStateChanged(Backend, IsConnected, Reports, LastError);
        }
        catch (Exception)
        {
            // Status publishing is best effort.
        }
    }

    public void Dispose()
    {
        _stateTimer.Dispose();
        _lockTimer?.Dispose();
        _hid.Dispose();
        _usb.Dispose();
    }
}
