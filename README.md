# Forza Horizon 5 → DualSense Adaptive Triggers

Real-time adaptive trigger and haptic feedback for a PS5 DualSense controller while playing Forza Horizon 5 on PC. Reads the game's UDP telemetry and translates driving data into trigger resistance and surface rumble — no mods, no overlays.

## Effects

| Situation | Effect |
|---|---|
| Accelerating | Right trigger resists proportionally to speed and throttle input |
| Turbo boost active | Right trigger adds extra resistance |
| Wheel spin / traction loss | Right trigger goes light |
| RPM limiter | Right trigger buzzes at 20–40 Hz |
| Braking (not pressed) | Left trigger has a resistance wall at 50% travel |
| Braking (pressed) | Left trigger resists proportionally to pedal pressure |
| Dirt / rumble strips | Right motor rumbles with surface texture |
| Water / puddles | Left motor pulses with puddle depth |
| Menus / paused | All effects off |

## Requirements

- Windows 10 or 11
- Forza Horizon 5 (PC — Steam or Microsoft Store)
- PS5 DualSense controller connected via **USB** (Bluetooth is detected but trigger effects are not supported over BT on Windows)

## Quick start (pre-built)

Download `ForzaAdaptiveTriggers.exe` from the [Releases](../../releases/latest) page — no installation or .NET required.

## Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) or newer.

```
dotnet run --project ForzaAdaptiveTriggers
```

To produce a standalone exe:

```
dotnet publish ForzaAdaptiveTriggers -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Setup

### Enable telemetry in Forza Horizon 5

1. Open **Settings → HUD and Gameplay**
2. Scroll to **Data Out** and set:
   - **Data Out** → `ON`
   - **Data Out IP Address** → `127.0.0.1`
   - **Data Out IP Port** → `5300`

### Run

1. Connect your DualSense via USB
2. Launch `ForzaAdaptiveTriggers.exe`
3. Launch (or switch to) FH5 — effects start as soon as you're on track

Press **Ctrl+C** or close the window to exit. Triggers are reset to no resistance on shutdown.

## Troubleshooting

**"DualSense not found"**
- The app retries on every telemetry packet — you can plug in the controller after it's already running.
- Only DualSense (PS5) is supported. DualShock 4 is not.
- Use USB. Bluetooth connections are detected but trigger effects are silently ignored by the Windows BT HID driver.

**No trigger effects in-game**
- Confirm IP `127.0.0.1` and port `5300`.
- Check that no firewall is blocking UDP 5300 on localhost.
- Effects only apply when `IsRaceOn = 1` — you must be in an active race or free roam session, not a menu or loading screen.

**Packet rate shows 0 Hz**
- FH5 only sends telemetry while the game is in the foreground and a session is active.

**Tuning**
- `Mapping/RightTriggerMapper.cs` — throttle resistance curve, slip threshold, boost bonus
- `Mapping/LeftTriggerMapper.cs` — brake resistance curve, idle wall start position
- `Mapping/HapticsMapper.cs` — surface rumble intensity
- `Controller/DualSenseManager.cs` → `TranslateRaw()` — raw trigger mode bytes and param arrays

## Project structure

```
ForzaAdaptiveTriggers/
├── ForzaAdaptiveTriggers.csproj
├── Program.cs                        — entry point, shutdown handlers
├── NativeMethods.cs                  — P/Invoke for HID writes (hid.dll, kernel32.dll)
├── Telemetry/
│   ├── FH5Packet.cs                  — 324-byte struct mapped to FH5 Car Dash offsets
│   └── UdpListener.cs                — IAsyncEnumerable<FH5Packet> on UDP :5300
├── Controller/
│   ├── TriggerCommand.cs             — immutable record describing one trigger effect
│   └── DualSenseManager.cs           — direct HID writes to DualSense; USB + BT report builder
├── Mapping/
│   ├── RightTriggerMapper.cs         — throttle telemetry → TriggerCommand
│   ├── LeftTriggerMapper.cs          — brake telemetry → TriggerCommand
│   └── HapticsMapper.cs              — surface data → (leftMotor, rightMotor)
└── App/
    └── TelemetryLoop.cs              — main await foreach loop
```

## How it works

FH5 broadcasts a 324-byte UDP datagram at ~60 Hz containing real-time telemetry (speed, RPM, pedal input, tyre slip, surface data). This app parses that packet and maps the values to DualSense output reports sent directly over HID using `WriteFile` (Windows interrupt pipe). No third-party controller wrappers are used — the USB report layout is implemented from the [DualSense HID spec](https://controllers.fandom.com/wiki/Sony_DualSense).

## Dependencies

- [HidSharp](https://www.nuget.org/packages/HidSharp) v2.1.0 — cross-platform HID device enumeration
