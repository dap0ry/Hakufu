#!/usr/bin/env bash
# Abre Hakufu en el simulador de iOS (iPhone y iPad) con una biblioteca de ejemplo y
# hace una captura de cada pantalla. Lo usa el CI para comprobar que la app arranca y
# pinta en iOS (no hay Mac en el equipo). Necesita macOS con Xcode y Pillow:
#
#   dotnet build Hakufu.csproj -p:HakufuIos=true -r iossimulator-arm64
#   bash scripts/ios-simulator-shots.sh bin/Debug/net10.0-ios/iossimulator-arm64/Hakufu.app publish/ios-shots
#
# Cada pantalla se abre con HAKUFU_START_SCREEN (ver Services/StartScreen.cs).
set -euo pipefail

APP="$1"
OUT="$2"
BUNDLE="com.dapory.hakufu"
SCREENS=(home library collection reader pdf settings profile)
mkdir -p "${OUT}"
cd "$(dirname "$0")/.."

# Biblioteca de ejemplo: los mangas inventados de la landing (.cbz) y un tomo en PDF
# para probar el PDF de CoreGraphics.
LIB="$(mktemp -d)/biblioteca"
python3 scripts/landing-demo-mangas.py "${LIB}"
python3 - "${LIB}" <<'PY'
import sys, zipfile, io, os
from PIL import Image
lib = sys.argv[1]
src = os.path.join(lib, sorted(os.listdir(lib))[0])
cbz = sorted(f for f in os.listdir(src) if f.endswith('.cbz'))[0]
with zipfile.ZipFile(os.path.join(src, cbz)) as z:
    pages = [Image.open(io.BytesIO(z.read(n))).convert('RGB') for n in sorted(z.namelist())]
os.makedirs(os.path.join(lib, 'PDF de prueba'), exist_ok=True)
pages[0].save(os.path.join(lib, 'PDF de prueba', 'Tomo en PDF.pdf'), save_all=True, append_images=pages[1:])
PY

# El iPhone y el iPad más nuevos que tenga este Xcode.
pick() {
  xcrun simctl list devices available -j | python3 -c '
import json, sys
kind = sys.argv[1]
devs = [d for r, ds in json.load(sys.stdin)["devices"].items() if "iOS" in r for d in ds if d["name"].startswith(kind)]
print(devs[-1]["udid"] if devs else "")' "$1"
}

shoot() {
  local device="$1" name="$2"
  [ -n "${device}" ] || { echo "No hay simulador de ${name}"; return 0; }
  echo "── ${name} (${device})"
  xcrun simctl boot "${device}" 2>/dev/null || true
  xcrun simctl bootstatus "${device}" -b
  xcrun simctl install "${device}" "${APP}"
  local docs
  docs="$(xcrun simctl get_app_container "${device}" "${BUNDLE}" data)/Documents"
  mkdir -p "${docs}"
  cp -R "${LIB}/." "${docs}/"

  for screen in "${SCREENS[@]}"; do
    xcrun simctl terminate "${device}" "${BUNDLE}" 2>/dev/null || true
    SIMCTL_CHILD_HAKUFU_START_SCREEN="${screen}" xcrun simctl launch "${device}" "${BUNDLE}"
    sleep 15   # arrancar, leer la carpeta, sacar portadas
    xcrun simctl io "${device}" screenshot "${OUT}/${name}-${screen}.png"
  done

  # El registro de la app, por si algo falla (excepciones de .NET incluidas).
  xcrun simctl spawn "${device}" log show --last 10m --style compact \
    --predicate 'process == "Hakufu"' > "${OUT}/${name}-log.txt" 2>&1 || true
  xcrun simctl shutdown "${device}" || true
}

shoot "$(pick iPhone)" iphone
shoot "$(pick iPad)" ipad
ls -la "${OUT}"
