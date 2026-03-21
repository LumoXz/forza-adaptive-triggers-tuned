using ForzaAdaptiveTriggers.App;
using ForzaAdaptiveTriggers.Controller;
using ForzaAdaptiveTriggers.Telemetry;

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"\n[ForzaTriggers] Fatal error: {e.ExceptionObject}");
    Console.Error.WriteLine("Press any key to exit…");
    Console.ReadKey(intercept: true);
};

Console.Error.WriteLine("╔══════════════════════════════════════════════════╗");
Console.Error.WriteLine("║  Forza Horizon 5 → DualSense Adaptive Triggers  ║");
Console.Error.WriteLine("║  Press Ctrl+C to quit                            ║");
Console.Error.WriteLine("╚══════════════════════════════════════════════════╝");
Console.Error.WriteLine();

using var cts        = new CancellationTokenSource();
using var controller = new DualSenseManager();
using var udp        = new UdpListener();

// ── Shutdown handlers ─────────────────────────────────────────────────────
bool cleanedUp = false;
void Cleanup()
{
    if (cleanedUp) return;
    cleanedUp = true;
    controller.ResetAll();
    Console.Error.WriteLine("[ForzaTriggers] Exited cleanly.");
}

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Handles terminal X button, logoff, shutdown (events that bypass Ctrl+C)
// Store in a variable so GC doesn't collect the delegate before exit
NativeMethods.ConsoleCtrlHandler ctrlHandler = eventType =>
{
    if (eventType is 2 or 5 or 6) // CTRL_CLOSE, CTRL_LOGOFF, CTRL_SHUTDOWN
        Cleanup();
    return false;
};
NativeMethods.SetConsoleCtrlHandler(ctrlHandler, true);

AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();

// ── Main loop ─────────────────────────────────────────────────────────────
var loop = new TelemetryLoop(controller, udp);

try
{
    await loop.RunAsync(cts.Token);
}
catch (OperationCanceledException) { }
finally
{
    Cleanup();
}
