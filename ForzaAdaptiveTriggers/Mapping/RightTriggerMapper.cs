using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.Mapping;

/// <summary>
/// "True spring" right-trigger mapping, tuned for a buttery engage transition:
///  - wide hysteresis band (enter 60 / exit 10) so the critical engage region
///    never lets telemetry jitter reach the trigger
///  - longer EMA window (alpha 0.25) swallowing quick Accel flips
///  - force floor of 45 once engaged, so the transition starts with a
///    perceptible baseline instead of a weak, jitter-prone low zone
/// </summary>
public sealed class RightTriggerMapper
{
    // Linear spring: force = Accel * 1.0 + 0 — full pedal = full force.
    private const float AccelToForce = 1.0f;
    private const float AccelOffset  = 0f;

    // Wide hysteresis: engage at 60, release at 10.
    private const byte AccelOnThreshold  = 60;
    private const byte AccelOffThreshold = 10;

    // Force floor once engaged — helps the transition feel solid.
    private const byte ForceFloor = 45;

    // Longer EMA: alpha 0.25 at ~82 Hz settles in ~4 packets (~50 ms).
    private const float SmoothingAlpha = 0.25f;

    private bool  _engaged;
    private float _smoothForce;
    private bool  _haveSmooth;

    public TriggerCommand Map(in FH5Packet p)
    {
        bool shouldRelease = _engaged
            ? p.Accel < AccelOffThreshold
            : p.Accel < AccelOnThreshold;

        if (shouldRelease)
        {
            _engaged   = false;
            _haveSmooth = false;
            return TriggerCommand.Off;
        }

        _engaged = true;

        float rawForce = Math.Clamp(p.Accel * AccelToForce + AccelOffset, ForceFloor, 255f);

        // EMA smooth
        _smoothForce = _haveSmooth
            ? _smoothForce + SmoothingAlpha * (rawForce - _smoothForce)
            : rawForce;
        _haveSmooth = true;

        return new TriggerCommand(TriggerMode.ContinuousResistance, (byte)Math.Clamp((int)_smoothForce, 0, 255));
    }
}