# Changelog

Notable changes to this fork, written so the next agent (or human) picking up
this repo can understand what changed, why, and how to verify it.

## 2026-08-30 — Rumble fully handed over to Steam/game; haptics code removed

### Background

FH5 (Steam) has no native DualSense support, so the controller runs through
Steam Input (DualSense → emulated Xbox pad). Both Steam Input and this app
write output reports to the same HID device, and a DualSense output report is
a full-state structure: rumble motors, adaptive-trigger params and LEDs all
live in one report, and each writer asserts whichever fields it claims.

### What changed

1. **All rumble code removed** (it was already dead — `HapticsMapper.Enabled
   = false`):
   - Deleted `Mapping/HapticsMapper.cs` (telemetry → motor mapping).
   - `App/TelemetryLoop.cs`: removed the `SetRumble` calls and the
     `Speed > 1.0f` branch.
   - `Controller/DualSenseManager.cs`: removed `SetRumble()`, the
     `_lastLeftMotor` / `_lastRightMotor` caches, and the motor parameters
     from `SendReport` / `BuildUsb` / `BuildBt`.

2. **HID reports no longer claim the rumble fields** — the actual fix:
   - `valid_flag0` in the output report changed `0xFF` → `0xFC` (USB byte 1,
     BT byte 2). Per the Linux kernel `hid-playstation.c` definitions, bit 0
     (0x01) is "compatible vibration enable" and bit 1 (0x02) is "haptics
     select"; both are now left clear. Valid-flag semantics: only fields
     whose bit is set are applied — unset fields keep their previous value.
     The controller therefore ignores the motor bytes (still written as 0)
     in our reports.
   - Result: Steam Input's rumble translation (game vibration → DualSense
     motors) is never overwritten by us, and this app is the only writer of
     the trigger params. Previously every trigger write claimed the motors
     with value 0 and momentarily ducked the game's vibration.

3. **Runtime log line added**: on controller connect, the app now prints
   `Rumble left to Steam/game — adaptive triggers only` so the ownership
   model is visible at runtime.

4. **README updated**:
   - Vibration is documented as "not touched at all"; the old advice to
     disable Steam-side DualSense vibration is gone — it is no longer
     needed. Steam/game vibration and trigger effects can coexist by
     protocol, not by luck of write timing.
   - Tuning table synced with the actual right-trigger code (the "feel v6"
     tuning commit had left it behind): `CurveExponent 1.5`,
     `SmoothingAlpha 0.12`, `ForceQuantum 12`; `AccelToForce` no longer
     exists.

### Why

- The game + Steam already provide vibration with user-facing toggles and an
  intensity slider (FH5 in-game settings; Steam Input per-game settings).
  Telemetry-derived rumble is strictly worse and had been disabled since
  before this change.
- Clearing the vibration bits makes app and Steam Input write **disjoint
  fields** of the report — the fight is prevented by the HID protocol
  instead of relying on the app's sparse write cadence to avoid it.

### Verification

```
dotnet build ForzaAdaptiveTriggers -c Release
```

On-device checklist (USB DualSense, FH5 via Steam with Steam Input enabled):

1. Steam-side and in-game vibration ON, intensity as desired.
2. Throttle in race: right trigger resistance ramps with pedal depth while
   game vibration keeps working simultaneously (this coexistence is the
   point of the change).
3. Menus / loading: triggers free.
4. Quit the game while the app runs: watchdog releases triggers within ~2 s.
5. Exit the app (Ctrl+C): triggers reset cleanly.

### Known remaining HID claims (deliberate, unchanged)

- The app still writes the lightbar (blue) + player-1 LED on every report.
  If Steam ever fights over the LEDs, the same pattern applies: clear the
  lightbar bit (0x04) / player-LED bit (0x10) in `valid_flag1` and stop
  writing those bytes.
