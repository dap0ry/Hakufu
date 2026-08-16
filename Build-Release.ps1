#Requires -Version 5.1
# ============================================================================
#  Build-Release.ps1 -- Publica Hakufu y lo empaqueta con Velopack (vpk)
#
#  Uso:
#    .\Build-Release.ps1 -Version "0.9.0"
#
#  Salida (en Releases\):
#    Hakufu-win-Setup.exe          <- instalador de un solo exe
#    Hakufu-<version>-full.nupkg
#    Hakufu-win-Portable.zip       <- version portable, no hace falta instalar
#    RELEASES
#
#  Este script NO publica el release en GitHub -- eso es un paso manual
#  y deliberado (ver el bloque final que imprime el comando exacto).
# ============================================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root        = $PSScriptRoot
$publishDir  = Join-Path $root "publish"
$releasesDir = Join-Path $root "Releases"

function Write-Step([int]$n, [int]$total, [string]$msg) {
    Write-Host ""
    Write-Host "[$n/$total] $msg" -ForegroundColor Cyan
}

# -- 1. Publicar la app -----------------------------------------------------
Write-Step 1 3 "Publicando Hakufu (self-contained x64 Release)..."

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish "$root\Hakufu.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo (codigo $LASTEXITCODE)." }

Remove-Item (Join-Path $publishDir "*.pdb") -ErrorAction SilentlyContinue

Write-Host "   Publicacion completada." -ForegroundColor Green

# -- 2. Empaquetar con vpk ----------------------------------------------------
Write-Step 2 3 "Empaquetando con vpk..."

if (Test-Path $releasesDir) { Remove-Item $releasesDir -Recurse -Force }

dotnet tool run vpk pack `
    --packId "Hakufu" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "Hakufu.exe" `
    --icon "$root\HakufuLogo.ico" `
    --outputDir $releasesDir

if ($LASTEXITCODE -ne 0) { throw "vpk pack fallo (codigo $LASTEXITCODE)." }

Write-Host "   Paquete generado en Releases\." -ForegroundColor Green

# -- Resumen -----------------------------------------------------------------
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Paquete Velopack listo en Releases\ (instalador: Hakufu-win-Setup.exe)" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Para publicar el release en GitHub (paso manual):"
Write-Host ""
Write-Host "    gh release create v$Version (Get-ChildItem Releases -File).FullName --title v$Version --generate-notes" -ForegroundColor Yellow
Write-Host ""
