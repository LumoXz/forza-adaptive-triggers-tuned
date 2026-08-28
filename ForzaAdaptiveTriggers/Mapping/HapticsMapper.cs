using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.Mapping;

/// <summary>
/// Maps surface telemetry to rumble motor intensities.
///
/// DISABLED by default: the game's own vibration (Steam/game-side) already
/// provides haptic feedback on this setup. Keeping this off also removes the
/// motor writes that were contending with trigger writes in shared HID
/// reports. Flip Enabled to true to re-enable surface rumble mapped from
/// telemetry (road texture → right motor, puddle depth → left motor).
/// </summary>
public static class HapticsMapper
{
    /// <summary>Diagnostic toggle: disables motor rumble while keeping trigger effects.</summary>
    public const bool Enabled = false;

    public static (byte leftMotor, byte rightMotor) Map(in FH5Packet p)
    {
        if (!Enabled)
            return (0, 0);

        float avgRumble = (p.SurfaceRumbleFL + p.SurfaceRumbleFR +
                           p.SurfaceRumbleRL + p.SurfaceRumbleRR) * 0.25f;

        float avgPuddle = (p.WheelInPuddleFL + p.WheelInPuddleFR +
                           p.WheelInPuddleRL + p.WheelInPuddleRR) * 0.25f;

        byte right = (byte)Math.Clamp(avgRumble * 60f,  0f, 255f);
        byte left  = (byte)Math.Clamp(avgPuddle * 200f, 0f, 255f);

        return (left, right);
    }
}