using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.Mapping;

/// <summary>
/// "True spring" right-trigger mapping, tuned for a buttery engage transition:
///  - wide hysteresis band (enter 60 / exit 10) so the critical engage region
///    never lets telemetry jitter reach the trigger
///  - heavy EMA (alpha 0.12) swallowing game-side throttle modulation
///  - force floor of 45 once engaged, so the transition starts with a
///    perceptible baseline instead of a weak, jitter-prone low zone
///  - coarse quantization (12) eliminating per-frame trim writes that made
///    the motor "chase" telemetry as a high-frequency buzz
/// </summary>
public sealed class RightTriggerMapper
{
    // Progressive spring curve (true-car feel):
    //   force = (Accel/255)^CurveExponent * 255
    // Exponent >1 = light at the start (fine control for launch),
    // progressive midrange, firmer at the end (protects full-throttle).
    // 1.0 = linear, 1.5 = gentle progressive, 2.0 = aggressive.
    private const float CurveExponent = 1.5f;

    // Wide hysteresis: engage at 60, release at 10.
    private const byte AccelOnThreshold = 60;
    private const byte AccelOffThreshold = 10;

    // Force floor once engaged — helps the transition feel solid.
    private const byte ForceFloor = 45;

    // Heavier EMA: alpha 0.12 at ~82 Hz settles in ~9 packets (~110 ms).
    // Filters out game-side throttle modulation (shift splices, traction
    // control, assists) that would otherwise reach the motor as pulses.
    private const float SmoothingAlpha = 0.12f;

    // Coarse force quantization — a ~5% step of full travel. Eliminates the
    // ±1..3-strength trim-per-frame writes that made the motor "chase" the
    // telemetry as a high-frequency buzz (the "gunfire" feel).
    private const int ForceQuantum = 12;

    private bool _engaged;
    private float _smoothForce;
    private bool _haveSmooth;

    public TriggerCommand Map(in FH5Packet p)
    {
        bool shouldRelease = _engaged
            ? p.Accel < AccelOffThreshold
            : p.Accel < AccelOnThreshold;

        if (shouldRelease)
        {
            _engaged = false;
            _haveSmooth = false;
            return TriggerCommand.Off;
        }

        _engaged = true;

        float t = Math.Clamp(p.Accel / 255f, 0f, 1f);
        float rawForce = Math.Clamp((float)Math.Pow(t, CurveExponent) * 255f, ForceFloor, 255f);

        // EMA smooth
        _smoothForce = _haveSmooth
            ? _smoothForce + SmoothingAlpha * (rawForce - _smoothForce)
            : rawForce;
        _haveSmooth = true;

        // Quantize to coarse steps — kills the per-frame trim writes.
        int quantized = (int)(Math.Round(_smoothForce / ForceQuantum) * ForceQuantum);

        return new TriggerCommand(TriggerMode.ContinuousResistance, (byte)Math.Clamp(quantized, 0, 255));
    }
}