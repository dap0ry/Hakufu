using System.Diagnostics;

// updater.exe
// Launched by Hakufu after it downloads and extracts an update ZIP to AppContext.BaseDirectory.
// Waits for the main process to exit, then restarts Hakufu.exe.
//
// Optional argument: PID of the running Hakufu process to wait for.
//   updater.exe 1234
// If no PID is supplied it falls back to a 2.5 s sleep.

var appDir  = AppContext.BaseDirectory;
var mainExe = Path.Combine(appDir, "Hakufu.exe");

if (args.Length > 0 && int.TryParse(args[0], out var pid))
{
    try
    {
        using var proc = Process.GetProcessById(pid);
        proc.WaitForExit(15_000);
    }
    catch { /* process already exited */ }
}
else
{
    Thread.Sleep(2500);
}

if (File.Exists(mainExe))
    Process.Start(new ProcessStartInfo(mainExe) { UseShellExecute = true });
