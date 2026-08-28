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

            // In menus / loading screens the game zeroes telemetry; release everything.
            if (packet.IsRaceOn == 0)
            {
                _controller.SetRightTrigger(TriggerCommand.Off);
                _controller.SetLeftTrigger(TriggerCommand.Off);
                _controller.SetRumble(0, 0);
                continue;
            }

            _controller.SetRightTrigger(_rightMapper.Map(in packet));
            _controller.SetLeftTrigger(LeftTriggerMapper.Map(in packet));

            // Surface rumble only while moving — values are noisy at standstill.
            // NOTE: HapticsMapper is currently disabled (see its header) so this
            // just keeps the motors zero.
            if (packet.Speed > 1.0f)
            {
                var (leftMotor, rightMotor) = HapticsMapper.Map(in packet);
                _controller.SetRumble(leftMotor, rightMotor);
            }
            else
            {
                _controller.SetRumble(0, 0);
            }
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
                    _controller.SetRumble(0, 0);
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