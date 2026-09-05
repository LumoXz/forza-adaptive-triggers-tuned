using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Mapping;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.App;

public sealed class TelemetryLoop
{
    private readonly DualSenseManager _controller;
    private readonly UdpListener      _udp;
    private readonly RightTriggerMapper _rightMapper = new();

    // Watchdog: reset if no packet arrives for 2 s (game closed / loading screen)
    private volatile bool _packetReceivedRecently;
    private const int WatchdogIntervalMs = 500;
    private const int IdleTimeoutMs      = 2000;

    public TelemetryLoop(DualSenseManager controller, UdpListener udp)
    {
        _controller = controller;
        _udp        = udp;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.Error.WriteLine("[ForzaTriggers] Listening for FH5 telemetry on UDP :5300 …");
        TryConnectController();

        _ = RunWatchdogAsync(ct);

        await foreach (var packet in _udp.ListenAsync(ct))
        {
            if (!_controller.IsConnected)
                TryConnectController();

            _packetReceivedRecently = true;

            // In menus / loading screens the game zeroes telemetry; release the triggers.
            // (Vibration is never touched — it belongs to Steam Input / the game.)
            if (packet.IsRaceOn == 0)
            {
                _controller.SetRightTrigger(TriggerCommand.Off);
                _controller.SetLeftTrigger(TriggerCommand.Off);
                continue;
            }

            _controller.SetRightTrigger(_rightMapper.Map(in packet));
            _controller.SetLeftTrigger(LeftTriggerMapper.Map(in packet));
        }
    }

    private async Task RunWatchdogAsync(CancellationToken ct)
    {
        int silentMs = 0;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(WatchdogIntervalMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            if (_packetReceivedRecently)
            {
                _packetReceivedRecently = false;
                silentMs = 0;
            }
            else
            {
                silentMs += WatchdogIntervalMs;
                if (silentMs >= IdleTimeoutMs)
                {
                    _controller.SetRightTrigger(TriggerCommand.Off);
                    _controller.SetLeftTrigger(TriggerCommand.Off);
                }
            }
        }
    }

    private void TryConnectController()
    {
        if (_controller.TryConnect())
            Console.Error.WriteLine("[ForzaTriggers] DualSense connected.");
        else
            Console.Error.WriteLine("[ForzaTriggers] DualSense not found — will retry on next packet.");
    }
}