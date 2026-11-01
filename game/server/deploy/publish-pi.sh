#!/usr/bin/env bash
# Baut den Server für den Raspberry Pi (64-Bit) und kopiert ihn per scp.
# Aufruf (im Repo-Root): game/server/deploy/publish-pi.sh pi@raspberrypi.local
set -euo pipefail
target="${1:?Ziel angeben, z. B. pi@raspberrypi.local}"
root="$(cd "$(dirname "$0")/../../.." && pwd)"
out="$root/game/publish/pi"

dotnet publish "$root/game/server/Meridian.Server" -c Release -r linux-arm64 --self-contained -o "$out"
cp "$root/game/server/deploy/meridian-server.service" "$root/game/server/deploy/install-pi.sh" "$out/"
scp -r "$out" "$target:~/meridian-upload"
echo "Kopiert. Auf dem Pi ausführen: sudo ~/meridian-upload/install-pi.sh"
