using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.Mapping;

/// <summary>
/// Maps throttle telemetry to a right-trigger TriggerCommand.
///
/// Priority:
///   1. Accel &lt; 20        → Off
///   2. RPM limiter       → Vibration 20–40 Hz
///   3. Wheel slip > 1.5  → Off  (traction loss — trigger goes light)
///   4. Normal driving    → ContinuousResistance, force scales with speed × Accel input
/// </summary>
public static class RightTriggerMapper
{
    private const float SlipThreshold   = 1.5f;
    private const float RpmLimiterRatio = 0.98f;

    public static TriggerCommand Map(in FH5Packet p)
    {
        if (p.Accel < 20)
            return TriggerCommand.Off;

        // ── 1. RPM Limiter ───────────────────────────────────────────────
        if (p.EngineMaxRpm > 0 && p.CurrentEngineRpm >= p.EngineMaxRpm * RpmLimiterRatio)
        {
            float ratio = 0f;
            float range = p.EngineMaxRpm * (1f - RpmLimiterRatio);
            if (range > 0)
                ratio = Math.Clamp((p.CurrentEngineRpm - p.EngineMaxRpm * RpmLimiterRatio) / range, 0f, 1f);

            // Quantize to nearest 5 Hz to reduce redundant HID writes
            byte freq = (byte)((int)Math.Round((20f + ratio * 20f) / 5f) * 5);
            return new TriggerCommand(TriggerMode.Vibration, freq);
        }

        // ── 2. Wheel Slip ────────────────────────────────────────────────
        float avgSlip = p.DrivetrainType == 0
            ? (p.TireCombinedSlipFL + p.TireCombinedSlipFR) * 0.5f   // FWD
            : (p.TireCombinedSlipRL + p.TireCombinedSlipRR) * 0.5f;  // RWD / AWD

        if (avgSlip > SlipThreshold)
            return TriggerCommand.Off;

        // ── 3. Normal Driving ─────────────────────────────────────────────
        // Force scales with throttle input and speed
        // At standstill full throttle ≈ 150; at 200 kph full throttle = 255
        float speedKph   = p.Speed * 3.6f;
        float speedScale = Math.Clamp(0.8f + speedKph / 300f, 0.8f, 1.0f);
        float rawForce   = p.Accel * speedScale;

        // Floor: always at least 120 so light-press is still perceptible
        rawForce = Math.Max(rawForce, 120f);

        if (p.Boost > 1.0f)
            rawForce = Math.Min(rawForce * 1.15f, 255f);

        // Quantize to nearest 5 to reduce redundant HID writes
        byte force = (byte)Math.Clamp((int)(Math.Round(rawForce / 5f) * 5f), 0, 255);

        return new TriggerCommand(TriggerMode.ContinuousResistance, force);
    }
}
