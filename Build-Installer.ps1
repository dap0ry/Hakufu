#Requires -Version 5.1
# ============================================================================
#  Build-Installer.ps1 — Genera el instalador .exe de Hakufu con Inno Setup
#
#  Prerequisito: Inno Setup 6  →  https://jrsoftware.org/isinfo.php
#
#  Uso:
#    .\Build-Installer.ps1                      # versión por defecto (0.1.0)
#    .\Build-Installer.ps1 -Version "0.2.0"     # versión específica
#
#  Salida:
#    output\Hakufu-<version>-Setup.exe
# ============================================================================

param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$root       = $PSScriptRoot
$publishDir = Join-Path $root "publish"
$outputDir  = Join-Path $root "output"

# ── Helpers ──────────────────────────────────────────────────────────────────

function Write-Step([int]$n, [int]$total, [string]$msg) {
    Write-Host ""
    Write-Host "[$n/$total] $msg" -ForegroundColor Cyan
}

function Find-InnoSetup {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
        "C:\Program Files\Inno Setup 5\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    # Intento via PATH
    $fromPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }
    return $null
}

# ── Verificar Inno Setup antes de hacer nada ─────────────────────────────────
$iscc = Find-InnoSetup
if (-not $iscc) {
    Write-Host ""
    Write-Host "ERROR: Inno Setup no encontrado." -ForegroundColor Red
    Write-Host ""
    Write-Host "Instálalo desde: https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    Write-Host "  · Descarga la versión estable de Inno Setup 6"
    Write-Host "  · Instala con las opciones por defecto"
    Write-Host "  · Vuelve a ejecutar este script"
    Write-Host ""
    exit 1
}
Write-Host "Inno Setup encontrado: $iscc" -ForegroundColor Green

# ── 1. Publicar la app principal ─────────────────────────────────────────────
Write-Step 1 4 "Publicando Hakufu (self-contained x64 Release)..."

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish "$root\Hakufu.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló (código $LASTEXITCODE)." }

# Limpiar artefactos que no deben ir en el instalador
Remove-Item (Join-Path $publishDir "AppxManifest.xml") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $publishDir "Images")           -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $publishDir "*.pdb")            -ErrorAction SilentlyContinue

Write-Host "   Publicación completada." -ForegroundColor Green

# ── 2. Compilar e incluir updater.exe ────────────────────────────────────────
Write-Step 2 4 "Compilando updater.exe..."

$updaterBuildDir = Join-Path $env:TEMP "HakufuUpdaterBuild_$(Get-Random)"

dotnet publish "$root\Updater\Updater.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -o $updaterBuildDir `
    --verbosity quiet

if ($LASTEXITCODE -ne 0) { throw "Compilación de updater.exe falló (código $LASTEXITCODE)." }

$updaterExe = Join-Path $updaterBuildDir "updater.exe"
if (-not (Test-Path $updaterExe)) { throw "updater.exe no se generó." }

Copy-Item $updaterExe $publishDir -Force
Remove-Item $updaterBuildDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "   updater.exe incluido en publish." -ForegroundColor Green

# ── 3. Verificar archivos clave ───────────────────────────────────────────────
Write-Step 3 4 "Verificando archivos..."

$required = @("Hakufu.exe", "updater.exe", "pdfium.dll")
foreach ($file in $required) {
    $path = Join-Path $publishDir $file
    if (Test-Path $path) {
        $size = [math]::Round((Get-Item $path).Length / 1MB, 1)
        Write-Host "   OK  $file  ($size MB)" -ForegroundColor Green
    } else {
        Write-Host "   ADVERTENCIA: $file no encontrado en publish\." -ForegroundColor Yellow
    }
}

# ── 4. Compilar el instalador con Inno Setup ─────────────────────────────────
Write-Step 4 4 "Compilando instalador con Inno Setup..."

New-Item $outputDir -ItemType Directory -Force | Out-Null

& $iscc `
    /DAppVersion="$Version" `
    "$root\Installer\Hakufu.iss"

if ($LASTEXITCODE -ne 0) { throw "ISCC.exe falló (código $LASTEXITCODE)." }

# ── Resumen ───────────────────────────────────────────────────────────────────
$installerPath = Join-Path $outputDir "Hakufu-$Version-Setup.exe"
$installerSize = if (Test-Path $installerPath) {
    "$([math]::Round((Get-Item $installerPath).Length / 1MB, 1)) MB"
} else { "?" }

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Instalador generado correctamente" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Archivo : output\Hakufu-$Version-Setup.exe  ($installerSize)"
Write-Host "  Instala en: C:\Program Files\Hakufu\"
Write-Host "  Datos usuario: %APPDATA%\Hakufu  (no se tocan)"
Write-Host ""
Write-Host "  Distribuir solo el archivo .exe — no requiere certificado."
Write-Host ""
