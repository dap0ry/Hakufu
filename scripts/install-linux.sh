#!/usr/bin/env bash
# Instala Hakufu para el usuario actual en Linux (sin sudo) y lo añade al menú
# de aplicaciones. Publica primero si hace falta.
#
#   ./scripts/install-linux.sh
set -euo pipefail

cd "$(dirname "$0")/.."

case "$(uname -m)" in
  arm64|aarch64) RID=linux-arm64 ;;
  *)             RID=linux-x64 ;;
esac

if [ ! -x "publish/Hakufu-${RID}/Hakufu" ]; then
  ./scripts/publish.sh "${RID}"
fi

DEST="$HOME/.local/share/hakufu"
rm -rf "${DEST}"
mkdir -p "${DEST}" "$HOME/.local/share/applications" "$HOME/.local/bin"
cp -R "publish/Hakufu-${RID}/." "${DEST}/"
cp HakufuLogo.png "${DEST}/hakufu.png"
ln -sf "${DEST}/Hakufu" "$HOME/.local/bin/hakufu"

cat > "$HOME/.local/share/applications/hakufu.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Hakufu
Comment=Gestor y lector de manga
Exec=${DEST}/Hakufu
Icon=${DEST}/hakufu.png
Terminal=false
Categories=Graphics;Viewer;
StartupWMClass=Hakufu
DESKTOP

echo "Hakufu instalado en ${DEST}. Búscalo en el menú de aplicaciones o ejecuta: hakufu"
