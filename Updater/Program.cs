using System.Diagnostics;
using System.IO.Compression;

// updater.exe — lanzado por Hakufu cuando hay una actualización lista.
//
// Modo nuevo (desde v0.3): recibe 4 argumentos
//   args[0] = ruta del zip descargado (en %TEMP%\HakufuUpdate\update.zip)
//   args[1] = directorio de instalación de la app (appDir)
//   args[2] = ruta del ejecutable principal (Hakufu.exe)
//   args[3] = PID del proceso de Hakufu a esperar
//
// Modo legacy (sin argumentos): espera 2.5 s y relanza Hakufu.exe

if (args.Length < 3)
{
    // Compatibilidad con versiones anteriores
    Thread.Sleep(2500);
    var legacyExe = Path.Combine(AppContext.BaseDirectory, "Hakufu.exe");
    if (File.Exists(legacyExe))
        Process.Start(new ProcessStartInfo(legacyExe) { UseShellExecute = true });
    return;
}

var zipPath   = args[0];
var targetDir = args[1];
var appExe    = args[2];

// Esperar a que el proceso principal termine
if (args.Length > 3 && int.TryParse(args[3], out var pid))
{
    try
    {
        using var proc = Process.GetProcessById(pid);
        proc.WaitForExit(15_000);
    }
    catch { /* ya terminó */ }
}
else
{
    Thread.Sleep(2500);
}

// Extraer el zip encima de la carpeta de la app
// (Hakufu.exe ya no está en ejecución, se puede sobreescribir)
if (File.Exists(zipPath))
{
    try
    {
        ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);
    }
    catch { /* si falla, lanzamos igual la app con lo que haya */ }

    try { File.Delete(zipPath); } catch { }
}

// Limpiar la carpeta temporal de HakufuUpdate
try
{
    var tempUpdateDir = Path.GetDirectoryName(zipPath);
    if (!string.IsNullOrEmpty(tempUpdateDir) &&
        tempUpdateDir.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
    {
        Directory.Delete(tempUpdateDir, recursive: true);
    }
}
catch { }

// Relanzar la app actualizada
if (File.Exists(appExe))
    Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true });
