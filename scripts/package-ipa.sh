#!/usr/bin/env bash
# Hakufu para iPhone/iPad SIN FIRMAR: publish/Hakufu-ios.ipa. Necesita macOS, Xcode y
# `dotnet workload install ios`. Se instala con un sideloader (Impactor, Sideloadly,
# AltStore…) y un Apple ID, que la firman al instalarla (ver README).
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build Hakufu.csproj -c Release -p:HakufuIos=true -r ios-arm64 -p:EnableCodeSigning=false

APP="$(find bin/Release/net10.0-ios/ios-arm64 -maxdepth 2 -type d -name 'Hakufu.app' | head -1)"
[ -n "${APP}" ] || { echo "No se encuentra Hakufu.app"; exit 1; }

STAGE="$(mktemp -d)"
mkdir -p "${STAGE}/Payload" publish
cp -R "${APP}" "${STAGE}/Payload/"
rm -f publish/Hakufu-ios.ipa
(cd "${STAGE}" && zip -qry - Payload) > publish/Hakufu-ios.ipa
echo "Listo: publish/Hakufu-ios.ipa ($(du -h publish/Hakufu-ios.ipa | cut -f1))"
