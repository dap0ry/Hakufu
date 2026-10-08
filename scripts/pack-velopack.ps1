# Empaqueta con Velopack lo que dejó scripts/publish.ps1: Setup.exe + archivos
# de actualización. Canal "win": el mismo que la 0.9.7, para que se actualice sola.
#
#   .\scripts\pack-velopack.ps1 [-Rid win-x64]
#
# Requiere: dotnet tool install -g vpk --version 1.2.161
# Resultado en publish\velopack\
param([string]$Rid = "win-x64")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$version = (Select-String -Path Hakufu.csproj -Pattern '<Version>(.*)</Version>').Matches[0].Groups[1].Value

vpk pack -u Hakufu -v $version -p "publish\Hakufu-$Rid" -e Hakufu.exe `
  --channel win --packTitle Hakufu --packAuthors "Daniel Poza Rivera" `
  -i HakufuLogo.ico -o publish\velopack
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Listo: publish\velopack"
