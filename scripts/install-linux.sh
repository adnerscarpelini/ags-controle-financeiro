#!/usr/bin/env bash
# Instala o AGS Finanças como serviço systemd.
#
# Uso: sudo ./install-linux.sh [--source <pasta>] [--port <porta>]
#
#   --source  pasta da publicação self-contained (dotnet publish -r linux-x64).
#             Padrão: artifacts/linux-x64 na raiz do repositório.
#   --port    porta HTTPS da aplicação. Padrão: 5280.
#
# Pode ser executado de novo para atualizar os binários ou trocar a porta.
set -euo pipefail

SERVICE=ags-finance
INSTALL_DIR=/opt/ags-finance
DATA_DIR=/var/lib/ags-finance
UNIT_FILE=/etc/systemd/system/${SERVICE}.service
EXE=Ags.Finance.Web

SOURCE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/artifacts/linux-x64"
PORT=5280

while [[ $# -gt 0 ]]; do
    case "$1" in
        --source) SOURCE="${2:?--source exige uma pasta}"; shift 2 ;;
        --port) PORT="${2:?--port exige um número}"; shift 2 ;;
        *) echo "Argumento desconhecido: $1" >&2; exit 64 ;;
    esac
done

if [[ $EUID -ne 0 ]]; then
    echo "Execute com sudo." >&2
    exit 1
fi

if ! [[ $PORT =~ ^[0-9]+$ ]] || (( PORT < 1 || PORT > 65535 )); then
    echo "Porta inválida: $PORT" >&2
    exit 64
fi

if [[ ! -f "$SOURCE/$EXE" ]]; then
    echo "Não encontrei $EXE em '$SOURCE'. Publique antes: dotnet publish src/Ags.Finance.Web -c Release -r linux-x64 --self-contained -o artifacts/linux-x64" >&2
    exit 1
fi

if systemctl is-active --quiet "$SERVICE"; then
    echo "Parando o serviço $SERVICE..."
    systemctl stop "$SERVICE"
fi

if ! id -u "$SERVICE" >/dev/null 2>&1; then
    echo "Criando o usuário de sistema $SERVICE..."
    useradd --system --user-group --no-create-home --home-dir "$DATA_DIR" --shell /usr/sbin/nologin "$SERVICE"
fi

echo "Copiando os binários para $INSTALL_DIR..."
install -d -m 755 "$INSTALL_DIR"
cp -a "$SOURCE"/. "$INSTALL_DIR"/
chown -R root:root "$INSTALL_DIR"
chmod 755 "$INSTALL_DIR/$EXE"

# Pasta de dados fora de qualquer pasta sincronizada, legível só pelo usuário do serviço.
install -d -m 750 -o "$SERVICE" -g "$SERVICE" "$DATA_DIR"

# Restart=on-failure com limite de 5 tentativas em 5 minutos: porta em uso registra o erro no journal e,
# esgotadas as tentativas, o serviço fica parado em vez de reiniciar para sempre.
cat > "$UNIT_FILE" <<UNIT
[Unit]
Description=AGS Finanças
After=network-online.target
Wants=network-online.target
StartLimitIntervalSec=300
StartLimitBurst=5

[Service]
Type=notify
ExecStart=$INSTALL_DIR/$EXE
WorkingDirectory=$INSTALL_DIR
User=$SERVICE
Group=$SERVICE
Restart=on-failure
RestartSec=5
SyslogIdentifier=$SERVICE
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DataDirectory=$DATA_DIR
Environment=Hosting__Port=$PORT
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
UNIT

# Cria o usuário único. O comando roda como o usuário do serviço, com a mesma pasta de dados, para que o banco
# fique com o dono certo. Ele recusa (código 3) quando o usuário já existe, o que acontece ao atualizar uma
# instalação; a senha se troca depois com "user reset-password".
echo "Criando o usuário da aplicação..."
user_create_status=0
runuser -u "$SERVICE" -- env DataDirectory="$DATA_DIR" "$INSTALL_DIR/$EXE" user create || user_create_status=$?
if [[ $user_create_status -ne 0 && $user_create_status -ne 3 ]]; then
    echo "Aviso: o comando 'user create' não foi concluído (código $user_create_status). Rode-o depois: sudo runuser -u $SERVICE -- env DataDirectory=$DATA_DIR $INSTALL_DIR/$EXE user create" >&2
fi

if command -v ufw >/dev/null 2>&1 && ufw status | grep -q '^Status: active'; then
    echo "Liberando a porta $PORT/tcp no ufw..."
    ufw allow "${PORT}/tcp" comment 'AGS Finance' >/dev/null
fi

systemctl daemon-reload
systemctl enable --now "$SERVICE"

echo
echo "AGS Finanças instalado. Acesse por um destes endereços:"
for address in $(hostname -I 2>/dev/null); do
    [[ $address == *:* ]] && continue
    echo "  https://${address}:${PORT}"
done
echo "  https://$(hostname):${PORT}"
echo "Na primeira visita o navegador avisa sobre o certificado; veja Configurações para confiar nele."
