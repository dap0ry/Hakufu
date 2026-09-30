# Publica Hakufu para Windows, autocontenido (no hace falta tener .NET instalado).
#
#   .\scripts\publish.ps1                 # win-x64
#   .\scripts\publish.ps1 -Rid win-arm64
#
# Resultado: publish\Hakufu-<rid>\ y publish\Hakufu-<rid>.zip
param([string]$Rid = "win-x64")

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$out = "publish\Hakufu-$Rid"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }

Write-Host "Publicando Hakufu para $Rid..."
dotnet publish Hakufu.csproj -c Release -r $Rid --self-contained true -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$zip = "publish\Hakufu-$Rid.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path "$out\*" -DestinationPath $zip
Write-Host "Listo: $out (y $zip). Se abre con Hakufu.exe."
