using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace VK03Panel.App;

/// <summary>Named mutex plus pipe so a second launch brings the existing window forward.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\VK03Panel.SingleInstance";
    private const string PipeName = "VK03Panel.Activate";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>
    /// Creates the instance lock. <paramref name="wait"/> lets an elevated relaunch wait for the
    /// previous process to release the mutex instead of handing activation back to it.
    /// </summary>
    public SingleInstance(TimeSpan? wait = null)
    {
        var deadline = DateTime.UtcNow + (wait ?? TimeSpan.Zero);
        while (true)
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
            if (created)
            {
                _mutex = mutex;
                IsPrimary = true;
                break;
            }

            mutex.Dispose();
            if (DateTime.UtcNow >= deadline)
            {
                _mutex = new Mutex(initiallyOwned: false, MutexName);
                IsPrimary = false;
                break;
            }

            Thread.Sleep(250);
        }

        if (IsPrimary)
        {
            _ = ListenAsync();
        }
    }

    public bool IsPrimary { get; }

    public event Action? ActivationRequested;

    /// <summary>Signals the first instance to show its window.</summary>
    public static void SignalPrimary()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(800);
            client.WriteByte(1);
            client.Flush();
        }
        catch (Exception)
        {
            // The other instance may be shutting down; ignore.
        }
    }

    private async Task ListenAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cancellation.Token);
                _ = server.ReadByte();
                ActivationRequested?.Invoke();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                await Task.Delay(500);
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            if (IsPrimary)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch (Exception)
        {
            // Ignore mutex release failures.
        }

        _mutex.Dispose();
        _cancellation.Dispose();
    }
}
