# Forza Horizon 5 → DualSense Adaptive Triggers

> 中文文档见 [README.md](README.md)

Real-time adaptive trigger support for the PS5 DualSense controller while playing Forza Horizon 5 on PC. Reads the game's UDP telemetry and drives the trigger motors directly over HID — no mods, no overlays, no third-party controller wrappers.

**Feels like a real pedal:** trigger force tracks how hard you press (progressive spring curve), instead of wobbling with telemetry noise.

## Effects

| Situation | Effect |
|---|---|
| Throttle pressed | Right trigger resists proportionally to pedal depth |
| Throttle released | Right trigger free |
| Brake pressed | Left trigger resists proportionally to pedal depth |
| Brake released | Left trigger free |
| Menus / paused | All effects off |
| Vibration (any time) | Untouched — owned by Steam Input / the game |

**Vibration is not touched at all.** The app's HID reports leave the rumble fields unclaimed (the vibration bits in `valid_flag0` are clear), so Steam Input / the game own the motors — the in-game vibration toggle and intensity slider behave exactly as they would without this app.

## Why it feels steady (design notes)

The original project's mappers fed high-frequency telemetry (RPM, speed, wheel slip) straight into the trigger force. Result: the trigger wobbled in sync with the tach needle. This fork changes the philosophy:

- **Force = f(pedal input) only.** Linear mapping, like a real spring. No RPM / speed / slip / boost in the force path — nothing noisy can reach the trigger motor.
- **Wide hysteresis** on the on/off boundary (`enter 60 / exit 10`) so the engage transition can't flicker on telemetry jitter.
- **EMA low-pass** (`alpha 0.25`) on the force value — swallows packet-to-packet noise, feels like a damped pedal.
- **Force floor (45)** once engaged — the transition starts with a perceptible baseline instead of a weak, jitter-prone low zone.
- **Frozen-telemetry detection removed.** It was designed to detect the pause menu, but "30 identical frames of accel/brake/speed" is exactly what light-throttle cruising looks like, so it periodically released the triggers there. Menus are already handled by `IsRaceOn == 0`.

## Requirements

- Windows 10 or 11
- Forza Horizon 5 (PC — Steam or Microsoft Store)
- PS5 DualSense controller connected via **USB** (Bluetooth is detected but trigger effects are not supported over BT on Windows)

## Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) or newer.

```
dotnet run --project ForzaAdaptiveTriggers
```

Standalone exe (no runtime install needed):

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
2. Launch `ForzaAdaptiveTriggers.exe` (or `dotnet run --project ForzaAdaptiveTriggers`)
3. Launch (or switch to) FH5 — effects start as soon as you're on track

Press **Ctrl+C** or close the window to exit. Triggers are reset to no resistance on shutdown.

> **Steam Input:** vibration can stay enabled. This app never claims the rumble fields of the DualSense output report, so Steam's rumble translation and the trigger effects write disjoint parts of the report and cannot fight each other.

## Tuning

All feel parameters are constants in `Mapping/`:

| Parameter | Location | Default | Meaning |
|---|---|---|---|
| `CurveExponent` | RightTriggerMapper | 1.5 | progressive spring curve (1.0 = linear, 2.0 = aggressive) |
| `AccelOnThreshold` / `AccelOffThreshold` | RightTriggerMapper | 60 / 10 | engage / release hysteresis on pedal depth (0–255) |
| `ForceFloor` | both mappers | 45 | minimum force once engaged |
| `SmoothingAlpha` | Right / Left mapper | 0.12 / 0.25 | EMA low-pass (lower = smoother) |
| `ForceQuantum` | RightTriggerMapper | 12 | coarse force quantization — removes per-frame motor "buzz" |
| `BrakeOnThreshold` / `BrakeOffThreshold` | LeftTriggerMapper | 60 / 10 | brake side, linear curve |

## Troubleshooting

**"DualSense not found"** — use USB; the app retries on every telemetry packet, so you can plug in after launch. DualShock 4 is not supported.

**No trigger effects in-game** — confirm IP `127.0.0.1` / port `5300`, no firewall blocking UDP 5300, and that you're in an active session (`IsRaceOn = 1`), not a menu.

## Project structure

```
ForzaAdaptiveTriggers/
├── Program.cs                    — entry point, shutdown handlers
├── NativeMethods.cs              — P/Invoke for HID writes (hid.dll, kernel32.dll)
├── Telemetry/
│   ├── FH5Packet.cs              — 324-byte struct mapped to FH5 Car Dash offsets
│   └── UdpListener.cs            — IAsyncEnumerable<FH5Packet> on UDP :5300
├── Controller/
│   ├── TriggerCommand.cs         — immutable record describing one trigger effect
│   └── DualSenseManager.cs       — direct HID writes to DualSense; USB + BT report builder
├── Mapping/
│   ├── RightTriggerMapper.cs     — throttle telemetry → TriggerCommand (progressive spring)
│   └── LeftTriggerMapper.cs      — brake telemetry → TriggerCommand (true spring)
└── App/
    └── TelemetryLoop.cs          — main await-foreach loop, watchdog
```

## How it works

FH5 broadcasts a 324-byte UDP datagram at ~60-80 Hz with real-time telemetry (speed, RPM, pedal input, tyre slip, surface data). This app parses the packet, maps the pedal values to trigger forces, and writes DualSense output reports directly over HID using `WriteFile` (Windows interrupt pipe). The USB/BT report layout is implemented from the [DualSense HID spec](https://controllers.fandom.com/wiki/Sony_DualSense) — no third-party controller wrappers. Only the trigger fields are claimed in the output report; rumble is left to Steam Input / the game.

## License

MIT (see [LICENSE](LICENSE)). Fork of [Jason13201/forza-adaptive-triggers](https://github.com/Jason13201/forza-adaptive-triggers).