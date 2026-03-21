using System.Runtime.InteropServices;

namespace ForzaAdaptiveTriggers.Telemetry;

/// <summary>
/// Forza Horizon 5 "Car Dash" UDP telemetry packet, 324 bytes.
/// Field offsets are per the official FH5 Data Out schema.
/// The 12-byte Horizon placeholder at offset 232 is intentionally unmapped.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 324, Pack = 1)]
public struct FH5Packet
{
    // ── Header ──────────────────────────────────────────────────────────
    [FieldOffset(0)]  public int   IsRaceOn;           // 1 = in-race, 0 = menus

    // ── Engine ──────────────────────────────────────────────────────────
    [FieldOffset(16)] public float CurrentEngineRpm;
    [FieldOffset(8)]  public float EngineMaxRpm;        // redline

    // ── Drivetrain ──────────────────────────────────────────────────────
    [FieldOffset(224)] public int  DrivetrainType;      // 0=FWD, 1=RWD, 2=AWD

    // ── Speed / Pedals ──────────────────────────────────────────────────
    [FieldOffset(256)] public float Speed;              // m/s
    [FieldOffset(315)] public byte  Accel;              // 0–255
    [FieldOffset(316)] public byte  Brake;              // 0–255

    // ── Boost ───────────────────────────────────────────────────────────
    [FieldOffset(284)] public float Boost;

    // ── Tire Slip Ratio (signed, >0 = spinning) ─────────────────────────
    [FieldOffset(84)]  public float TireSlipRatioFL;
    [FieldOffset(88)]  public float TireSlipRatioFR;
    [FieldOffset(92)]  public float TireSlipRatioRL;
    [FieldOffset(96)]  public float TireSlipRatioRR;

    // ── Tire Combined Slip (magnitude, 0–∞) ────────────────────────────
    [FieldOffset(180)] public float TireCombinedSlipFL;
    [FieldOffset(184)] public float TireCombinedSlipFR;
    [FieldOffset(188)] public float TireCombinedSlipRL;
    [FieldOffset(192)] public float TireCombinedSlipRR;

    // ── Surface Rumble (0–1 range typical) ─────────────────────────────
    [FieldOffset(148)] public float SurfaceRumbleFL;
    [FieldOffset(152)] public float SurfaceRumbleFR;
    [FieldOffset(156)] public float SurfaceRumbleRL;
    [FieldOffset(160)] public float SurfaceRumbleRR;

    // ── Wheel in Puddle depth (0–1 range typical) ───────────────────────
    [FieldOffset(132)] public float WheelInPuddleFL;
    [FieldOffset(136)] public float WheelInPuddleFR;
    [FieldOffset(140)] public float WheelInPuddleRL;
    [FieldOffset(144)] public float WheelInPuddleRR;
}
