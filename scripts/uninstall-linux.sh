#!/usr/bin/env bash
# Remove o serviço AGS Finanças, a regra do ufw e os binários. A pasta de dados e o usuário de sistema são preservados.
#
# Uso: sudo ./uninstall-linux.sh
set -euo pipefail

SERVICE=ags-finance
INSTALL_DIR=/opt/ags-finance
DATA_DIR=/var/lib/ags-finance
UNIT_FILE=/etc/systemd/system/${SERVICE}.service

if [[ $EUID -ne 0 ]]; then
    echo "Execute com sudo." >&2
    exit 1
fi

# Lê a porta da unit antes de apagá-la, para remover a regra certa do ufw.
PORT=
if [[ -f $UNIT_FILE ]]; then
    PORT=$(sed -n 's/^Environment=Hosting__Port=\([0-9]\+\)$/\1/p' "$UNIT_FILE")
    echo "Removendo o serviço $SERVICE..."
    systemctl disable --now "$SERVICE" 2>/dev/null || true
    rm -f "$UNIT_FILE"
    systemctl daemon-reload
fi

if [[ -n $PORT ]] && command -v ufw >/dev/null 2>&1 && ufw status | grep -q '^Status: active'; then
    ufw delete allow "${PORT}/tcp" >/dev/null 2>&1 || true
fi

rm -rf "$INSTALL_DIR"

echo "Desinstalado. Os dados foram preservados em $DATA_DIR."
