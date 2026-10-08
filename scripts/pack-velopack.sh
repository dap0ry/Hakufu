#!/usr/bin/env bash
# Empaqueta con Velopack lo que dejó scripts/publish.sh: instalador + archivos
# de actualización (los que lee UpdateService desde la release de GitHub).
#
#   ./scripts/pack-velopack.sh osx-arm64 | osx-x64 | linux-x64
#
# Requiere: dotnet tool install -g vpk --version 1.2.161 (misma versión que el NuGet Velopack)
# Resultado en publish/velopack/
set -euo pipefail

cd "$(dirname "$0")/.."

RID="${1:?Uso: $0 osx-arm64|osx-x64|linux-x64}"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Hakufu.csproj | head -1)"
OUT="publish/velopack"
mkdir -p "${OUT}"

case "${RID}" in
  osx-*)
    # --signAppIdentity "-": firma ad hoc DESPUÉS de que Velopack meta UpdateMac.
    # Sin ella el .app sale con la firma rota (sin cuenta de Apple Developer).
    vpk pack -u Hakufu -v "${VERSION}" -p publish/Hakufu.app -e Hakufu \
      --channel "${RID}" --packTitle Hakufu --packAuthors "Daniel Poza Rivera" \
      --signAppIdentity "-" -o "${OUT}"
    ;;
  linux-x64)
    vpk pack -u Hakufu -v "${VERSION}" -p "publish/Hakufu-${RID}" -e Hakufu \
      --channel linux --packTitle Hakufu --packAuthors "Daniel Poza Rivera" \
      -i HakufuLogo.png --categories Graphics -o "${OUT}"
    ;;
  *)
    echo "RID no soportado: ${RID} (en Windows usa scripts/pack-velopack.ps1)" >&2
    exit 1
    ;;
esac
echo "Listo: ${OUT}"
