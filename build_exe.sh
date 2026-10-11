#!/usr/bin/env bash
# =====================================================================
# Build Modern Native Windows Executable (.exe) for DJI LUT Studio (WPF)
# Uses Microsoft C# Compiler (csc.exe) on Windows with WPF Framework
# =====================================================================
set -e

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
CSC="/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
WPF_DIR="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

if [ ! -f "$CSC" ]; then
    echo "Error: csc.exe compiler not found at $CSC"
    exit 1
fi

WIN_SRC=$(wslpath -w "$DIR/src/DJI_LUT_Studio.cs")
WIN_OUT=$(wslpath -w "$DIR/DJI_LUT_Studio.exe")

echo "Compiling modern WPF native Windows GUI executable..."
"$CSC" /target:winexe /optimize+ \
    /r:"$WPF_DIR\PresentationFramework.dll" \
    /r:"$WPF_DIR\PresentationCore.dll" \
    /r:"$WPF_DIR\WindowsBase.dll" \
    /r:System.dll \
    /r:System.Xaml.dll \
    /r:System.Windows.Forms.dll \
    "/out:$WIN_OUT" "$WIN_SRC" < /dev/null

echo "✔ Build complete: $DIR/DJI_LUT_Studio.exe"

# Copy to Windows Desktop if available
DESKTOP="/mnt/c/Users/gal75/Desktop"
if [ -d "$DESKTOP" ]; then
    cp -v "$DIR/DJI_LUT_Studio.exe" "$DESKTOP/DJI_LUT_Studio.exe"
    echo "✔ Copied to Windows Desktop: $DESKTOP/DJI_LUT_Studio.exe"
fi
