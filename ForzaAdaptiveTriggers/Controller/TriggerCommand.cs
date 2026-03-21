namespace ForzaAdaptiveTriggers.Controller;

/// <summary>
/// Immutable descriptor for a single adaptive trigger effect.
/// </summary>
public enum TriggerMode
{
    NoResistance,
    ContinuousResistance,
    SlopeFeedback,
    Vibration,
}

public sealed record TriggerCommand(
    TriggerMode Mode,
    byte Param1 = 0,
    byte Param2 = 0,
    byte Param3 = 0,
    byte Param4 = 0)
{
    public static readonly TriggerCommand Off = new(TriggerMode.NoResistance);
}
