#!/usr/bin/env bash
# Auf dem Pi ausführen (sudo). Installiert/aktualisiert den Server nach /opt/meridian
# und richtet den systemd-Dienst ein. Accounts und Spielstand (meridian.db) in /opt/meridian/data bleiben erhalten.
set -euo pipefail
src="$(cd "$(dirname "$0")" && pwd)"

id meridian >/dev/null 2>&1 || useradd --system --home /opt/meridian --shell /usr/sbin/nologin meridian
mkdir -p /opt/meridian/data
systemctl stop meridian-server 2>/dev/null || true
cp -r "$src"/. /opt/meridian/
chmod +x /opt/meridian/Meridian.Server
chown -R meridian:meridian /opt/meridian
cp /opt/meridian/meridian-server.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now meridian-server
systemctl --no-pager status meridian-server | head -5
echo "Account anlegen: sudo -u meridian /opt/meridian/Meridian.Server add-account <name> <passwort> --accounts /opt/meridian/data/accounts.json"
