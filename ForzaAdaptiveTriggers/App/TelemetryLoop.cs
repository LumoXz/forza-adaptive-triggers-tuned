using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Mapping;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.App;

public sealed class TelemetryLoop
{
    private readonly DualSenseManager _controller;
    private readonly UdpListener      _udp;

    // Packet-rate reporting (stderr every 5 s)
    private int      _packetCount;
    private DateTime _rateWindow = DateTime.UtcNow;

    // Watchdog: reset if no packet arrives for 2 s (game closed / loading screen)
    private volatile bool _packetReceivedRecently;
    private const int WatchdogIntervalMs = 500;
    private const int IdleTimeoutMs      = 2000;

    // Frozen-telemetry detection: FH5 keeps sending the same values when paused
    // If Accel, Brake, and Speed are identical for 30+ consecutive frames (~0.5 s at 60 Hz),
    // treat the session as paused and release triggers
    private byte  _frozenAccel;
    private byte  _frozenBrake;
    private float _frozenSpeed;
    private int   _frozenFrames;
    private const int FrozenThreshold = 30;

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
            ReportPacketRate();

            // Release triggers when game is in a menu or telemetry is frozen (paused)
            bool frozen = IsFrozen(in packet);
            if (packet.IsRaceOn == 0 || frozen)
            {
                _controller.SetRightTrigger(TriggerCommand.Off);
                _controller.SetLeftTrigger(TriggerCommand.Off);
                _controller.SetRumble(0, 0);
                continue;
            }

            _controller.SetRightTrigger(RightTriggerMapper.Map(in packet));
            _controller.SetLeftTrigger(LeftTriggerMapper.Map(in packet));

            // Haptics only while actually moving — surface values are noisy at standstill
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

    /// <summary>
    /// Returns true if Accel, Brake, and Speed have been identical for FrozenThreshold
    /// consecutive frames — the signature of FH5's pause menu.
    /// </summary>
    private bool IsFrozen(in FH5Packet p)
    {
        if (p.Accel == _frozenAccel && p.Brake == _frozenBrake &&
            Math.Abs(p.Speed - _frozenSpeed) < 0.001f)
        {
            _frozenFrames++;
        }
        else
        {
            _frozenFrames = 0;
            _frozenAccel  = p.Accel;
            _frozenBrake  = p.Brake;
            _frozenSpeed  = p.Speed;
        }

        return _frozenFrames >= FrozenThreshold;
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

    private void ReportPacketRate()
    {
        _packetCount++;
        var elapsed = (DateTime.UtcNow - _rateWindow).TotalSeconds;
        if (elapsed < 5.0) return;

        Console.Error.WriteLine($"[ForzaTriggers] Packet rate: {_packetCount / elapsed:F1} Hz");
        _packetCount = 0;
        _rateWindow  = DateTime.UtcNow;
    }
}
