#!/usr/bin/env bash
# Publica Hakufu autocontenido (no hace falta tener .NET instalado para abrirlo).
#
#   ./scripts/publish.sh              → para este equipo (detecta SO y arquitectura)
#   ./scripts/publish.sh osx-arm64    → Mac con chip Apple (M1/M2/M3/M4)
#   ./scripts/publish.sh osx-x64      → Mac Intel
#   ./scripts/publish.sh linux-x64    → Linux
#   ./scripts/publish.sh linux-arm64  → Linux ARM
#
# Resultado en publish/:
#   macOS  → Hakufu.app (y Hakufu-<rid>.zip). Hay que generarlo EN un Mac.
#   Linux  → carpeta Hakufu-<rid>/ y Hakufu-<rid>.tar.gz
set -euo pipefail

cd "$(dirname "$0")/.."

detect_rid() {
  local os arch
  case "$(uname -s)" in
    Darwin) os=osx ;;
    Linux)  os=linux ;;
    *) echo "SO no soportado por este script; en Windows usa scripts/publish.ps1" >&2; exit 1 ;;
  esac
  case "$(uname -m)" in
    arm64|aarch64) arch=arm64 ;;
    *)             arch=x64 ;;
  esac
  echo "$os-$arch"
}

RID="${1:-$(detect_rid)}"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Hakufu.csproj | head -1)"
OUT="publish/Hakufu-${RID}"

echo "Publicando Hakufu ${VERSION} para $RID…"
rm -rf "${OUT}"
dotnet publish Hakufu.csproj -c Release -r "${RID}" --self-contained true \
  -p:DebugType=none -o "${OUT}"

case "${RID}" in
  osx-*)
    APP="publish/Hakufu.app"
    rm -rf "${APP}"
    mkdir -p "${APP}/Contents/MacOS" "${APP}/Contents/Resources"
    cp -R "${OUT}/." "${APP}/Contents/MacOS/"

    # Icono .icns a partir del PNG (herramientas que trae macOS).
    ICON_KEY=""
    if command -v iconutil >/dev/null && command -v sips >/dev/null; then
      ICONSET="$(mktemp -d)/Hakufu.iconset"
      mkdir -p "${ICONSET}"
      for size in 16 32 128 256 512; do
        sips -z $size $size HakufuLogo.png --out "${ICONSET}/icon_${size}x${size}.png" >/dev/null
        sips -z $((size*2)) $((size*2)) HakufuLogo.png --out "${ICONSET}/icon_${size}x${size}@2x.png" >/dev/null
      done
      iconutil -c icns "${ICONSET}" -o "${APP}/Contents/Resources/Hakufu.icns"
      ICON_KEY="<key>CFBundleIconFile</key><string>Hakufu</string>"
    fi

    cat > "${APP}/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Hakufu</string>
  <key>CFBundleDisplayName</key><string>Hakufu</string>
  <key>CFBundleIdentifier</key><string>com.dapory.hakufu</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleExecutable</key><string>Hakufu</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  ${ICON_KEY}
</dict>
</plist>
PLIST

    # Sin firma de Apple (cuenta de 99 €/año) basta una firma "ad hoc" para
    # que los Mac con chip Apple lo ejecuten. Si se descarga (no hecho en este
    # Mac): xattr -dr com.apple.quarantine Hakufu.app
    if command -v codesign >/dev/null; then
      codesign --force --deep --sign - "${APP}"
    fi

    rm -f "publish/Hakufu-${RID}.zip"
    if command -v ditto >/dev/null; then
      ditto -c -k --keepParent "${APP}" "publish/Hakufu-${RID}.zip"
    fi
    echo "Listo: ${APP}"
    ;;
  linux-*)
    tar -czf "publish/Hakufu-${RID}.tar.gz" -C publish "Hakufu-${RID}"
    echo "Listo: ${OUT} (y publish/Hakufu-${RID}.tar.gz). Para instalarlo: ./scripts/install-linux.sh"
    ;;
esac
