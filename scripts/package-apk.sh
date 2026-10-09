#!/usr/bin/env bash
# APK de Android de Release → publish/Hakufu-android.apk. Con HAKUFU_KEYSTORE (ruta) y
# HAKUFU_KEYSTORE_PASSWORD, firmado con la clave de Hakufu (las actualizaciones se instalan
# encima); sin ellas, con la clave temporal de depuración de .NET (no se instala encima del firmado).
# Necesita el workload "android", el SDK de Android y un JDK 17 (ANDROID_HOME / JAVA_HOME).
set -euo pipefail
cd "$(dirname "$0")/.."
SIGN=()
if [ -n "${HAKUFU_KEYSTORE:-}" ]; then
  SIGN=(-p:AndroidKeyStore=true -p:AndroidSigningKeyStore="${HAKUFU_KEYSTORE}" -p:AndroidSigningKeyAlias=hakufu
        -p:AndroidSigningStorePass=env:HAKUFU_KEYSTORE_PASSWORD -p:AndroidSigningKeyPass=env:HAKUFU_KEYSTORE_PASSWORD)
fi
SDK=()
[ -n "${ANDROID_HOME:-}" ] && SDK+=(-p:AndroidSdkDirectory="${ANDROID_HOME}")
[ -n "${JAVA_HOME:-}" ] && SDK+=(-p:JavaSdkDirectory="${JAVA_HOME}")
rm -rf publish/android
dotnet publish Hakufu.csproj -c Release -p:HakufuAndroid=true -p:AndroidPackageFormat=apk -o publish/android \
  "${SDK[@]}" "${SIGN[@]}"
mkdir -p publish
cp "$(ls publish/android/*-Signed.apk | head -1)" publish/Hakufu-android.apk
ls -la publish/Hakufu-android.apk
