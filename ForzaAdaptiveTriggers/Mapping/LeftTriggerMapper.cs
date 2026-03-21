using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.Mapping;

/// <summary>
/// Maps brake telemetry to a left-trigger TriggerCommand.
///
///   Brake &lt; 20   → hard wall at 50% travel (first half free, then resistance)
///   Brake ≥ 20   → ContinuousResistance from the start, force scales 120–255
/// </summary>
public static class LeftTriggerMapper
{
    // Idle: resistance wall starts at ~50% trigger travel so quick taps still feel it.
    // TriggerMode.SlopeFeedback maps to mode 0x01 with Param1=startPos, Param2=force.
    private static readonly TriggerCommand IdleWall =
        new TriggerCommand(TriggerMode.SlopeFeedback, Param1: 128, Param2: 255);

    public static TriggerCommand Map(in FH5Packet p)
    {
        if (p.Brake < 20)
            return IdleWall;

        // Scale 120 (light press) → 255 (full press)
        float rawForce = 120f + (p.Brake - 20f) * (135f / 235f);
        byte force = (byte)Math.Clamp((int)(Math.Round(rawForce / 10f) * 10f), 0, 255);
        return new TriggerCommand(TriggerMode.ContinuousResistance, force);
    }
}
