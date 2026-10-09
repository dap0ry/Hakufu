#!/usr/bin/env bash
# Abre Hakufu en un emulador de Android ya arrancado, con una biblioteca de ejemplo, y hace
# una captura de cada pantalla. Lo usa el CI para comprobar que la app arranca y pinta en
# Android (nadie del equipo tiene Android). Necesita adb y Pillow:
#
#   bash scripts/android-emulator-shots.sh publish/Hakufu-android.apk publish/android-shots
#
# Cada pantalla se abre con el extra start_screen (ver MainActivity y Services/StartScreen.cs).
set -euo pipefail

APK="$1"
OUT="$2"
PKG="com.dapory.hakufu"
ACTIVITY="${PKG}/${PKG}.MainActivity"
SCREENS=(home library collection reader pdf settings profile)
mkdir -p "${OUT}"
cd "$(dirname "$0")/.."

# Biblioteca de ejemplo: los mangas de la landing (.cbz) y un tomo en PDF (PdfRenderer).
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

adb wait-for-device
until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = "1" ]; do sleep 2; done
adb install -r "${APK}"
# Acceso a todos los archivos, como si se diera en Ajustes.
adb shell appops set --uid "${PKG}" MANAGE_EXTERNAL_STORAGE allow
adb shell rm -rf /sdcard/Hakufu
adb push "${LIB}/." /sdcard/Hakufu/

for screen in "${SCREENS[@]}"; do
  adb shell am force-stop "${PKG}"
  adb shell am start -n "${ACTIVITY}" --es start_screen "${screen}"
  sleep 15   # arrancar, leer la carpeta, sacar portadas
  adb exec-out screencap -p > "${OUT}/phone-${screen}.png"
done

# En horizontal (tablet/móvil girado): Inicio y lector.
adb shell settings put system accelerometer_rotation 0
adb shell settings put system user_rotation 1
for screen in home reader; do
  adb shell am force-stop "${PKG}"
  adb shell am start -n "${ACTIVITY}" --es start_screen "${screen}"
  sleep 15
  adb exec-out screencap -p > "${OUT}/landscape-${screen}.png"
done
adb shell settings put system user_rotation 0

# El registro de la app, por si algo falla (excepciones de .NET incluidas).
adb logcat -d > "${OUT}/logcat.txt" || true
ls -la "${OUT}"
