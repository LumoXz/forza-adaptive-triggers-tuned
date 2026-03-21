using HidSharp;
using Microsoft.Win32.SafeHandles;

namespace ForzaAdaptiveTriggers.Controller;

/// <summary>
/// Drives a DualSense controller directly over HID — no third-party wrapper.
///
/// Device discovery uses HidSharp. Writes use WriteFile (interrupt OUT pipe).
///
/// USB:  Report ID 0x02, 48 bytes.
/// BT:   Report ID 0x31, 78 bytes, CRC32 appended over [0xA2 + report[0..73]].
///       Detected by presence of Bluetooth UUID in the device path.
///
/// Output report layout (USB):
///   [0]      Report ID (0x02)
///   [1]      valid_flag0 — 0xFF enables all features
///   [2]      valid_flag1 — 0xF7
///   [3]      motor_right (weak rumble)
///   [4]      motor_left  (strong rumble)
///   [5..10]  audio + power-save (0)
///   [11]     right trigger effect mode
///   [12..21] right trigger params (10 bytes)
///   [22]     left  trigger effect mode
///   [23..32] left  trigger params (10 bytes)
///   [33..42] timestamps + misc (0)
///   [43]     led_brightness (0xFF = max)
///   [44]     player_leds bitmask
///   [45]     lightbar R  [46] G  [47] B
///
/// Trigger effect modes:
///   0x05  Off/reset       — all params zero
///   0x01  Rigid           — continuous resistance: start(0-255), end(0-255), force(0-255)
///   0x02  Pulse           — section resistance:    start(0-255), end(0-255), force(0-255)
///   0x06  Pulse_B         — vibration: pos, amplitude, freq, repeated x2
/// </summary>
public sealed class DualSenseManager : IDisposable
{
    private const int  VendorId  = 0x054C;
    private const int  ProductId = 0x0CE6;

    private const byte UsbReportId  = 0x02;
    private const int  UsbReportLen = 48;

    private const byte BtReportId  = 0x31;
    private const int  BtReportLen = 78;

    // BT UUID present in device path when connected over Bluetooth
    private const string BtUuid = "00001124-0000-1000-8000-00805f9b34fb";

    private SafeFileHandle? _handle;
    private bool            _isBluetooth;

    private TriggerCommand _lastLeft  = TriggerCommand.Off;
    private TriggerCommand _lastRight = TriggerCommand.Off;
    private byte _lastLeftMotor  = 255; // force send on first connect
    private byte _lastRightMotor = 255;
    private bool _disposed;

    public bool IsConnected => _handle is { IsInvalid: false, IsClosed: false };

    public bool TryConnect()
    {
        DisposeHandle();

        try
        {
            var allDevices = DeviceList.Local
                .GetHidDevices(vendorID: VendorId, productID: ProductId)
                .ToList();

            Console.Error.WriteLine($"[DualSense] Found {allDevices.Count} HID interface(s):");
            foreach (var d in allDevices)
                Console.Error.WriteLine($"  {d.DevicePath}  in={d.GetMaxInputReportLength()} out={d.GetMaxOutputReportLength()}");

            var device = allDevices
                .OrderByDescending(d => d.GetMaxOutputReportLength())
                .FirstOrDefault();

            if (device == null)
                return false;

            _isBluetooth = device.DevicePath.Contains(BtUuid, StringComparison.OrdinalIgnoreCase);

            Console.Error.WriteLine($"[DualSense] Selected: {device.DevicePath}");
            Console.Error.WriteLine($"[DualSense] Bluetooth: {_isBluetooth}");

            _handle = NativeMethods.CreateFile(
                device.DevicePath,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                nint.Zero,
                NativeMethods.OPEN_EXISTING,
                0,
                nint.Zero);

            if (_handle.IsInvalid)
            {
                Console.Error.WriteLine($"[DualSense] CreateFile failed: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                DisposeHandle();
                return false;
            }

            _lastLeft  = TriggerCommand.Off;
            _lastRight = TriggerCommand.Off;
            _lastLeftMotor  = 255;
            _lastRightMotor = 255;

            // Initial state: triggers off, blue lightbar, player-1 LED
            SendReport(0x05, new byte[10], 0x05, new byte[10], 0, 0, 0, 0, 128, 0x04, true);

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DualSense] TryConnect error: {ex.Message}");
            DisposeHandle();
            return false;
        }
    }

    public void SetRightTrigger(TriggerCommand cmd)
    {
        if (!IsConnected || cmd == _lastRight) return;
        _lastRight = cmd;
        Flush();
    }

    public void SetLeftTrigger(TriggerCommand cmd)
    {
        if (!IsConnected || cmd == _lastLeft) return;
        _lastLeft = cmd;
        Flush();
    }

    public void SetRumble(byte leftMotor, byte rightMotor)
    {
        if (!IsConnected) return;
        if (leftMotor == _lastLeftMotor && rightMotor == _lastRightMotor) return;
        _lastLeftMotor  = leftMotor;
        _lastRightMotor = rightMotor;
        Flush();
    }

    public void ResetAll()
    {
        if (!IsConnected) return;
        try { SendReport(0x05, new byte[10], 0x05, new byte[10], 0, 0, 0, 0, 128, 0x04, true); }
        catch { }
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private void Flush()
    {
        (byte rm, byte[] rp) = TranslateRaw(_lastRight);
        (byte lm, byte[] lp) = TranslateRaw(_lastLeft);
        try
        {
            SendReport(rm, rp, lm, lp, _lastRightMotor, _lastLeftMotor,
                       0, 0, 128, 0x04, true);
        }
        catch { DisposeHandle(); }
    }

    private void SendReport(
        byte rightMode, byte[] rightParams,
        byte leftMode,  byte[] leftParams,
        byte motorRight, byte motorLeft,
        byte r, byte g, byte b, byte playerLed,
        bool includeUi)
    {
        if (_handle is null || _handle.IsInvalid) return;

        var report = _isBluetooth ? BuildBt(rightMode, rightParams, leftMode, leftParams,
                                            motorRight, motorLeft, r, g, b, playerLed, includeUi)
                                  : BuildUsb(rightMode, rightParams, leftMode, leftParams,
                                             motorRight, motorLeft, r, g, b, playerLed, includeUi);

        bool ok = NativeMethods.WriteFile(_handle, report, report.Length, out _, nint.Zero);
        if (!ok)
        {
            int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"[DualSense] WriteFile failed: err={err}");
        }
    }

    private static byte[] BuildUsb(
        byte rightMode, byte[] rightParams,
        byte leftMode,  byte[] leftParams,
        byte motorRight, byte motorLeft,
        byte r, byte g, byte b, byte playerLed, bool includeUi)
    {
        var report = new byte[UsbReportLen];
        report[0]  = UsbReportId;
        report[1]  = 0xFF;  // valid_flag0
        report[2]  = 0xF7;  // valid_flag1
        report[3]  = motorRight;
        report[4]  = motorLeft;
        report[11] = rightMode;
        Array.Copy(rightParams, 0, report, 12, Math.Min(10, rightParams.Length));
        report[22] = leftMode;
        Array.Copy(leftParams,  0, report, 23, Math.Min(10, leftParams.Length));
        if (includeUi)
        {
            report[43] = 0xFF;      // led_brightness max
            report[44] = playerLed;
            report[45] = r;
            report[46] = g;
            report[47] = b;
        }
        return report;
    }

    // BT output report (78 bytes, Report ID 0x31) — same payload shifted by +1
    // due to the extra 0x10 flag byte at [1]. CRC32 appended at [74..77].
    private static byte[] BuildBt(
        byte rightMode, byte[] rightParams,
        byte leftMode,  byte[] leftParams,
        byte motorRight, byte motorLeft,
        byte r, byte g, byte b, byte playerLed, bool includeUi)
    {
        var report = new byte[BtReportLen];
        report[0]  = BtReportId;
        report[1]  = 0x10;   // BT HID output-enable flag
        report[2]  = 0xFF;   // valid_flag0
        report[3]  = 0xF7;   // valid_flag1
        report[4]  = motorRight;
        report[5]  = motorLeft;
        report[12] = rightMode;
        Array.Copy(rightParams, 0, report, 13, Math.Min(10, rightParams.Length));
        report[23] = leftMode;
        Array.Copy(leftParams,  0, report, 24, Math.Min(10, leftParams.Length));
        if (includeUi)
        {
            report[44] = 0xFF;      // led_brightness max
            report[45] = playerLed;
            report[46] = r;
            report[47] = g;
            report[48] = b;
        }
        uint crc = ComputeBtCrc(report);
        report[74] = (byte)(crc        & 0xFF);
        report[75] = (byte)((crc >>  8) & 0xFF);
        report[76] = (byte)((crc >> 16) & 0xFF);
        report[77] = (byte)((crc >> 24) & 0xFF);
        return report;
    }

    /// <summary>
    /// Maps TriggerCommand to raw hardware effect mode + 10-byte params.
    ///
    /// 0x05  Off/reset      — params all zero
    /// 0x01  Rigid          — continuous resistance: start(0), end(255), force
    /// 0x02  Pulse          — section resistance:    start(0), end(250), force
    /// 0x06  Pulse_B        — vibration: pos, amplitude, freq, repeated x2
    /// </summary>
    private static (byte mode, byte[] @params) TranslateRaw(TriggerCommand cmd)
    {
        switch (cmd.Mode)
        {
            case TriggerMode.NoResistance:
                return (0x05, new byte[10]);

            case TriggerMode.ContinuousResistance:
            {
                byte force = (byte)Math.Clamp((int)cmd.Param1, 0, 255);
                return (0x01, new byte[] { 0, 255, force, 0, 0, 0, 0, 0, 0, 0 });
            }

            case TriggerMode.SlopeFeedback:
            {
                byte start = cmd.Param1;
                byte force = (byte)Math.Clamp((int)cmd.Param2, 0, 255);
                return (0x01, new byte[] { start, 255, force, 0, 0, 0, 0, 0, 0, 0 });
            }

            case TriggerMode.Vibration:
            {
                byte freq = cmd.Param1;
                return (0x06, new byte[] { 0, 255, freq, 0, 255, freq, 0, 0, 0, 0 });
            }

            default:
                return (0x05, new byte[10]);
        }
    }

    // CRC32 over [0xA2, report[0..73]] — standard poly 0xEDB88320
    private static uint ComputeBtCrc(byte[] report)
    {
        uint crc = 0xFFFF_FFFF;
        crc = StepCrc(crc, 0xA2);
        for (int i = 0; i < 74; i++) crc = StepCrc(crc, report[i]);
        return ~crc;
    }

    private static uint StepCrc(uint crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB8_8320u : crc >> 1;
        return crc;
    }

    private void DisposeHandle()
    {
        try { _handle?.Dispose(); } catch { }
        _handle = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ResetAll();
        DisposeHandle();
    }
}
