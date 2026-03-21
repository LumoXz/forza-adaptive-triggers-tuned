using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.Mapping;

/// <summary>
/// Maps surface telemetry to rumble motor intensities.
///
///   rightMotor = Clamp(avgSurfaceRumble * 60, 0, 255)   — road texture feel
///   leftMotor  = Clamp(avgPuddleDepth  * 200, 0, 255)   — water spray burst
/// </summary>
public static class HapticsMapper
{
    public static (byte leftMotor, byte rightMotor) Map(in FH5Packet p)
    {
        float avgRumble = (p.SurfaceRumbleFL + p.SurfaceRumbleFR +
                           p.SurfaceRumbleRL + p.SurfaceRumbleRR) * 0.25f;

        float avgPuddle = (p.WheelInPuddleFL + p.WheelInPuddleFR +
                           p.WheelInPuddleRL + p.WheelInPuddleRR) * 0.25f;

        byte right = (byte)Math.Clamp(avgRumble * 60f,  0f, 255f);
        byte left  = (byte)Math.Clamp(avgPuddle * 200f, 0f, 255f);

        return (left, right);
    }
}
