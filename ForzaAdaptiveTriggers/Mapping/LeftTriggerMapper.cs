using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Telemetry;

namespace ForzaAdaptiveTriggers.Mapping;

/// <summary>
/// "True spring" left-trigger mapping, same transition tuning as the right:
///  - wide hysteresis (enter 60 / exit 10)
///  - longer EMA (alpha 0.25)
///  - force floor of 45 once engaged
/// </summary>
public static class LeftTriggerMapper
{
    private const float BrakeToForce = 1.0f;
    private const float BrakeOffset  = 0f;

    private const byte BrakeOnThreshold  = 60;
    private const byte BrakeOffThreshold = 10;

    private const byte ForceFloor = 45;

    private const float SmoothingAlpha = 0.25f;

    private static bool  _engaged;
    private static float _smoothForce;
    private static bool  _haveSmooth;

    public static TriggerCommand Map(in FH5Packet p)
    {
        bool shouldRelease = _engaged
            ? p.Brake < BrakeOffThreshold
            : p.Brake < BrakeOnThreshold;

        if (shouldRelease)
        {
            _engaged    = false;
            _haveSmooth = false;
            return TriggerCommand.Off;
        }

        _engaged = true;

        float rawForce = Math.Clamp(p.Brake * BrakeToForce + BrakeOffset, ForceFloor, 255f);

        _smoothForce = _haveSmooth
            ? _smoothForce + SmoothingAlpha * (rawForce - _smoothForce)
            : rawForce;
        _haveSmooth = true;

        return new TriggerCommand(TriggerMode.ContinuousResistance, (byte)Math.Clamp((int)_smoothForce, 0, 255));
    }
}