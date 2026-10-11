#!/usr/bin/env bash
# DJI LUT Studio - Universal CLI Runner for Linux and WSL
set -e

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
export PYTHONPATH="$DIR:$PYTHONPATH"
python3 -m dji_lut_studio.cli "$@"
