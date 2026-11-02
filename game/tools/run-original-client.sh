#!/usr/bin/env bash
# Startet den Original-Client (Steam-Installation, unter Wine) gegen den eigenen Server.
# Aufruf: game/tools/run-original-client.sh NAME PASSWORT [HOST] [PORT]
# Der Client wird nach ~/.cache/meridian/m59 kopiert, damit die Steam-Installation unverändert bleibt.
# Server dazu z. B.: dotnet run --project game/server/Meridian.Server -- --port 5959 --client-dir ~/.cache/meridian/m59/resource
set -euo pipefail
user=${1:?Name fehlt}; pass=${2:?Passwort fehlt}; host=${3:-127.0.0.1}; port=${4:-5959}
src="$HOME/.steam/steam/steamapps/common/Meridian 59"
dst="$HOME/.cache/meridian/m59"
if [ ! -d "$dst" ]; then
    mkdir -p "$(dirname "$dst")"
    cp -r "$src" "$dst"
fi
cd "$dst"
export WINEPREFIX="$HOME/.cache/meridian/wineprefix" WINEDEBUG=-all
# Virtueller Wine-Desktop: ohne ihn bleiben unter Wayland/XWayland Textfenster, Werte und Inventar schwarz.
# /Q: Schnellstart ohne Startbildschirm (clientd3d/config.c ConfigOverride)
exec wine explorer "/desktop=Meridian,${M59_SIZE:-1600x900}" "$(winepath -w "$dst/Meridian.exe")" \
    /Q "/H:$host" "/P:$port" "/U:$user" "/W:$pass"
