#!/usr/bin/env bash
# Ensaio de recuperação de desastre (#59/#31/#76), com dados fictícios e instalações descartáveis:
#   instalação A: cria usuário e dados -> backup
#   instalação B (vazia, senhas novas, sem o volume original): restaura -> confere login, registros, vínculos e totais.
#
# Roda em Linux ou dentro do WSL 2, com Docker Engine e Compose, a partir da raiz do repositório:
#   scripts/rehearse-recovery.sh
# Usa projetos Compose próprios (ags-rehearsal-<id>-a/b), portas altas e pastas temporárias; ao final apaga só o que criou.
set -euo pipefail

cd "$(dirname "$0")/.."
ROOT=$(pwd)
WORK=$(mktemp -d /var/tmp/ags-rehearsal.XXXXXX)
case "$(realpath "$WORK")" in /var/tmp/ags-rehearsal.*) ;; *) exit 1 ;; esac
RUN_ID=$(printf '%s' "${WORK##*.}" | tr '[:upper:]' '[:lower:]')
PORT_A=${REHEARSAL_PORT_A:-5391}
PORT_B=${REHEARSAL_PORT_B:-5392}
USER_NAME=ensaio
USER_PASSWORD=$(openssl rand -hex 12)
FAILED=1

log() { printf '\n==> %s\n' "$*"; }
fail() { printf 'FALHOU: %s\n' "$*" >&2; exit 1; }

compose() { # compose <instância> <argumentos...>
    local instance=$1; shift
    docker compose -p "ags-rehearsal-$RUN_ID-$instance" --env-file "$WORK/$instance.env" "$@"
}

write_env() { # write_env <instância> <porta>
    local instance=$1 port=$2
    install -d -m 0700 "$WORK/$instance-backups"
    install -d -m 0700 "$WORK/$instance-attachments"
    # O processo da imagem roda como o UID 1654 (usuário "app") e precisa ser dono da pasta de backup.
    docker run --rm -v "$WORK/$instance-backups:/d" -v "$WORK/$instance-attachments:/a" alpine:3 chown 1654:1654 /d /a
    cat > "$WORK/$instance.env" <<ENV
POSTGRES_PASSWORD=$(openssl rand -hex 24)
AGS_DB_PASSWORD=$(openssl rand -hex 24)
AGS_HOST_NAMES=localhost,127.0.0.1
AGS_PORT=$port
AGS_VERSION=0.0.0-rehearsal
AGS_BACKUP_TIME=00:00
AGS_BACKUP_DIR=$WORK/$instance-backups
AGS_ATTACHMENTS_DIR=$WORK/$instance-attachments
ENV
    chmod 600 "$WORK/$instance.env"
}

cleanup() {
    log "Limpando as instalações do ensaio"
    for instance in a b; do
        [ -f "$WORK/$instance.env" ] && compose "$instance" down -v --remove-orphans >/dev/null 2>&1 || true
    done
    # Os backups pertencem ao UID 1654 (usuário da imagem); apaga por um container para não depender de sudo.
    docker run --rm -v "$WORK:/work" alpine:3 sh -c 'rm -rf /work/* /work/.[!.]*' >/dev/null 2>&1 || true
    rmdir "$WORK" 2>/dev/null || true
    if [ "$FAILED" -eq 0 ]; then echo "ENSAIO CONCLUÍDO COM SUCESSO"; else echo "ENSAIO FALHOU"; fi
}
trap cleanup EXIT

psql_app() { # psql_app <instância> <sql>  -> saída sem formatação
    compose "$1" exec -T db sh -c 'PGPASSWORD="$AGS_DB_PASSWORD" psql -X -At -U "$AGS_DB_USER" -d "$AGS_DB_NAME" -c "$0"' "$2"
}

sign_in() { # sign_in <porta> <jar>  -> 0 se o login funcionou
    local port=$1 jar=$2 page token status
    page=$(curl -ksS -c "$jar" "https://localhost:$port/login")
    token=$(printf '%s' "$page" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//; s/"$//')
    [ -n "$token" ] || fail "token antifalsificação não encontrado na página de login"
    status=$(curl -ksS -b "$jar" -c "$jar" -o /dev/null -w '%{http_code}' \
        --data-urlencode "UserName=$USER_NAME" --data-urlencode "Password=$USER_PASSWORD" \
        --data-urlencode "__RequestVerificationToken=$token" "https://localhost:$port/account/sign-in")
    [ "$status" = "302" ] || [ "$status" = "303" ]
}

authenticated() { # authenticated <jar> <porta> -> 0 se a sessão do jar é aceita (uma página protegida responde 200)
    local code
    code=$(curl -ksS -b "$1" -o /dev/null -w '%{http_code}' "https://localhost:$2/configuracoes")
    [ "$code" = "200" ]
}

log "Instalação A: sobe aplicação e banco novos"
write_env a "$PORT_A"
compose a up -d --build --wait --wait-timeout 300

log "Instalação A: cria o usuário pelo comando (senha por stdin, sem eco)"
printf '%s\n%s\n%s\n' "$USER_NAME" "$USER_PASSWORD" "$USER_PASSWORD" | compose a run --rm -T app user create

log "Instalação A: dados fictícios em vários registros e vínculos"
psql_app a "
INSERT INTO accounts.banks (\"Name\", \"ExternalId\", \"CreatedAt\", \"UpdatedAt\")
  VALUES ('Banco Ensaio', '019e0000-0000-7000-8000-000000000001', now(), now());
INSERT INTO accounts.accounts (\"BankExternalId\", \"Name\", \"NormalizedName\", \"Kind\", \"Color\", \"IsActive\", \"ExternalId\", \"CreatedAt\", \"UpdatedAt\")
  VALUES ('019e0000-0000-7000-8000-000000000001', 'Conta Ensaio', 'CONTA ENSAIO', 0, 'green', true, '019e0000-0000-7000-8000-000000000002', now(), now());
INSERT INTO accounts.credit_cards (\"AccountExternalId\", \"Name\", \"Brand\", \"CreditLimit\", \"ClosingDay\", \"DueDay\", \"IncludesClosingDay\", \"Color\", \"IsActive\", \"ExternalId\", \"CreatedAt\", \"UpdatedAt\")
  VALUES ('019e0000-0000-7000-8000-000000000002', 'Cartão Ensaio', 0, 1234567, 3, 10, false, 'green', true, '019e0000-0000-7000-8000-000000000003', now(), now()),
         ('019e0000-0000-7000-8000-000000000002', 'Cartão Dois', 1, 765433, 5, 12, true, 'green', true, '019e0000-0000-7000-8000-000000000004', now(), now());
INSERT INTO accounts.card_last_digits (\"CardExternalId\", \"Digits\", \"Usage\", \"Label\", \"IsActive\", \"ExternalId\", \"CreatedAt\", \"UpdatedAt\")
  VALUES ('019e0000-0000-7000-8000-000000000003', '1234', 0, 'Titular', true, '019e0000-0000-7000-8000-000000000005', now(), now());" >/dev/null

# Logo fictício gerado para o ensaio: PNG RGBA 256 × 256, círculo verde, sem marca real.
LOGO_PNG=iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAYAAABccqhmAAAEXUlEQVR42u3dQQHCMBAFUSRgeWWhDkzQJs28wzOQ7p9rX+/PvDjO9yLe9jAewbhFQgAwdlEQAIxdFAQAgxcEAcDoxUAAMHoxEACMXgwEAKMXAwHA8IVAAIweMRAAw0cIBMDwEQIBMHyEQAAMHyEQAMNHCATA8BECATB8hEAAjB8READDRwgEwPgRAQEwfIRAAIwfERAAw0cIBMD4EQEBMHyEQACMHxEQAONHBATAASIE3QA4PEQgGgAHhwhEA+DQEIFoABwYIhANgMNCBKIBcFCIQDQADgkRCAbA8SAE0QA4GEQgGgCHgggIAAhALQAOBBGIBsBhIALRADgIRCAaAIeACEQD4AAgGgAfHhZHwPghHAHjh3AEBAAEwPihGAHjh3AEjB/CERAAEADjh2IEjB/CERAAEADjh2IEjB/CERAAEADjh2IEjB/CERAAEADjh2IEjB/CERAAEADjh2IEBAAEwPihGAEBAAEwfihGQABAAIwfihEQABAA44diBAQABMD4oRgBAQABMH4oRkAAQAAEAATA+CEVAQEAATB+KEZAAEAABAAEwPghFQEBAAEQABAA44dUBAQABEAAQACMH1IREAAQAAEAATB+SEVAAEAABAAEQAAgHQAPBKEICAAIgACAAHgYEACgFwAPA7EICAAIgACAAAACALQC4EEgGAEBAAHwGCAAgAAAAgAIACAAgAAAZwXAQ0CUAIAAAAIACAAgAIAAAAIACAAgAIAAAAIACAAgAIAAAAIACAAgAIAAAAIACAAgAMCCAIgANPkvAAiAhwABAAQAEABAAAABAAQAODAAIgCx8QsACIAAgAB4EBAAoBkAEYDQ+AUABEAAQAAEAAQA6AZABCAyfgEAARAAEAABAAEQAWiMXwBAAAQABEAEIDV+AQABEAAQABGA1PgFAARAAEAARABS4xcAEAABAAEQAUiNXwBAAEYEIDh+AQABEAAQABGA1PgFAARgRACC4xcAEIARAQiOXwBAAEYEIDh+AQABGBGA4PgFAARgRACC4xcAEIARAQiOXwBAAEYEIDj+fwdABOBB4xcAEIARAQiO/6oAiAA8YPwCAAIwIgDB8V8dABGAjccvACAAIwIQHP9dARAB2HD8AgACMCIAwfHfHQARgI3GvyIAIgCbjF8AQABGBCA4/pUBEAFYPP7VARABWDj+HQIgAhh/PAAigPHHAyACGH88ACKA8ccDIAIYvwCAAFQDIAIYfzwAIoDxxwMgBBi+AIgAxl8PgAhg/PEAiADGHw+ACGD88QCIAMYfD4AIYPzxAIgAxh8PgBBg+AIgAhh/PQAigPHHAyAEGL4AiADGXw+AEGD4AiACGH89AEKA4QuACGD89QAIAfnhC4AIEB+/AAiB4ccZvxAYvgAgBIYvAAiB4QsAQmD4AoAQGL4AIASGLwCIgdELAEJg+AKAGBi9ACAGRi8AiIHRCwBiYPQCgCAYvAAgCsYuANSi4BsJAIdGwtse5gcPBmIDbpK37QAAAABJRU5ErkJggg==
psql_app a "UPDATE accounts.banks SET \"LogoData\" = decode('$LOGO_PNG', 'base64'), \"LogoVersion\" = '019e0000-0000-7000-8000-000000000010' WHERE \"ExternalId\" = '019e0000-0000-7000-8000-000000000001'" >/dev/null
LOGO_SQL="SELECT md5(\"LogoData\") || '|' || \"LogoVersion\" FROM accounts.banks WHERE \"ExternalId\" = '019e0000-0000-7000-8000-000000000001'"
LOGO_BEFORE=$(psql_app a "$LOGO_SQL")

SUMMARY_SQL="SELECT (SELECT count(*) FROM identity.users) || '|' || (SELECT count(*) FROM accounts.banks) || '|' || (SELECT count(*) FROM accounts.accounts) || '|' || (SELECT count(*) FROM accounts.credit_cards) || '|' || (SELECT sum(\"CreditLimit\") FROM accounts.credit_cards) || '|' || (SELECT count(*) FROM accounts.card_last_digits) || '|' || (SELECT string_agg(c.\"Name\", ',' ORDER BY c.\"Name\") FROM accounts.credit_cards c JOIN accounts.accounts a ON a.\"ExternalId\" = c.\"AccountExternalId\")"
BEFORE=$(psql_app a "$SUMMARY_SQL")
FOREIGN_KEYS_SQL="SELECT coalesce(string_agg(format('%I.%I:%I:%s', n.nspname, t.relname, c.conname, pg_get_constraintdef(c.oid)), ';' ORDER BY n.nspname, t.relname, c.conname), '') FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace WHERE c.contype = 'f' AND n.nspname <> 'pg_catalog'"
FOREIGN_KEYS_BEFORE=$(psql_app a "$FOREIGN_KEYS_SQL")
echo "Resumo antes: $BEFORE"
[ "$BEFORE" = "1|1|1|2|2000000|1|Cartão Dois,Cartão Ensaio" ] || fail "dados fictícios inesperados: $BEFORE"

log "Instalação A: login e sessão"
JAR_A="$WORK/jar-a"
sign_in "$PORT_A" "$JAR_A" || fail "login na instalação A"
authenticated "$JAR_A" "$PORT_A" || fail "sessão da instalação A não foi aceita"
CERT_A=$(compose a exec -T app sh -c 'sha256sum /data/certs/ags-finance.pfx' | cut -d' ' -f1)

log "Instalação A: documento PDF inteiramente fictício e metadados da fatura"
DOCUMENT_ID=019e0000-0000-7000-8000-000000000021
DOCUMENT_KEY=invoices/2026/11/03/20261103T120000Z_invoice_${DOCUMENT_ID}.pdf
compose a exec -T app sh -c 'mkdir -p /attachments/invoices/2026/11/03; printf "%s\n" "%PDF-1.7" "1 0 obj<</Type /Catalog>>endobj" "trailer<</Root 1 0 R>>" "%%EOF" > "$1"' sh "/attachments/$DOCUMENT_KEY"
DOCUMENT_BYTES=$(compose a exec -T app stat -c %s "/attachments/$DOCUMENT_KEY")
DOCUMENT_SHA=$(compose a exec -T app sha256sum "/attachments/$DOCUMENT_KEY" | cut -d' ' -f1)
psql_app a "INSERT INTO invoices.invoices (\"CardExternalId\", \"Month\", \"Status\", \"AutoCloseSuppressed\", \"HasConfirmedPayment\", \"ExternalId\", \"CreatedAt\", \"UpdatedAt\") VALUES ('019e0000-0000-7000-8000-000000000003', 202701, 0, false, false, '019e0000-0000-7000-8000-000000000020', now(), now());
INSERT INTO attachments.documents (\"OwnerType\", \"OwnerExternalId\", \"Kind\", \"OriginalFileName\", \"ContentType\", \"SizeBytes\", \"Sha256\", \"StorageKey\", \"ExternalId\", \"CreatedAt\", \"UpdatedAt\") VALUES ('Invoice', '019e0000-0000-7000-8000-000000000020', 'Invoice', 'fatura-ficticia.pdf', 'application/pdf', $DOCUMENT_BYTES, '$DOCUMENT_SHA', '$DOCUMENT_KEY', '$DOCUMENT_ID', now(), now())" >/dev/null
curl -ksS -b "$JAR_A" "https://localhost:$PORT_A/attachments/$DOCUMENT_ID" -o "$WORK/document-a.pdf"
[ "$(sha256sum "$WORK/document-a.pdf" | cut -d' ' -f1)" = "$DOCUMENT_SHA" ] || fail "download do documento fictício"

log "Instalação A: o backup agendado (horário já passado hoje) é feito sozinho pela aplicação"
for _ in $(seq 1 24); do
    if compose a run --rm -T app backup list 2>/dev/null | grep -q ' scheduled '; then break; fi
    sleep 5
done
compose a run --rm -T app backup list 2>/dev/null | grep -q ' scheduled ' || fail "o backup agendado não apareceu"

log "Instalação A: recriar só os containers mantém os backups (a pasta é persistente)"
compose a up -d --force-recreate --wait --wait-timeout 300 app
compose a run --rm -T app backup list 2>/dev/null | grep -q ' scheduled ' || fail "o backup agendado sumiu após recriar o container"

[ "$(psql_app a "$LOGO_SQL")" = "$LOGO_BEFORE" ] || fail "logo mudou após recriar containers"

log "Instalação A: backup sob demanda (dump custom + arquivos da aplicação) e conferência"
compose a run --rm -T app backup create
NAME=$(compose a run --rm -T app backup list 2>/dev/null | grep ' manual ' | head -1 | awk '{print $1}')
[ -n "$NAME" ] || fail "nenhum backup manual listado"
compose a run --rm -T app backup verify "$NAME"
echo "Backup: $NAME"

log "Instalação A: ZIP em fluxo e conferência independente de SHA256SUMS"
compose a run --rm -T app attachments export --from 2027-01-01 --to 2027-01-31 --output /backups/documentos.zip
docker run --rm -v "$WORK/a-backups:/d:ro" alpine:3 sh -c 'unzip -q /d/documentos.zip -d /tmp/export && cd /tmp/export && sha256sum -c SHA256SUMS'
log "Instalação A: ZIP autenticado por HTTP, com I/O assíncrono do servidor"
curl -fksS -b "$JAR_A" "https://localhost:$PORT_A/attachments/export?from=2027-01-01&to=2027-01-31" -o "$WORK/http-export.zip"
docker run --rm -v "$WORK:/w:ro" alpine:3 sh -c 'unzip -q /w/http-export.zip -d /tmp/export && cd /tmp/export && sha256sum -c SHA256SUMS'

log "Cópia do backup para fora das instalações (como seria para outro disco/máquina)"
docker run --rm -v "$WORK/a-backups:/from:ro" -v "$WORK/b-backups:/to" alpine:3 sh -c 'cp -a /from/. /to/ && chown -R 1654:1654 /to'

log "Instalação A destruída com todos os volumes"
compose a down -v --remove-orphans

log "Instalação B: máquina nova, banco vazio e senhas novas"
write_env b "$PORT_B"
compose b up -d --wait --wait-timeout 300 db
[ "$(psql_app b "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'identity'")" = "0" ] || fail "instalação B deveria começar vazia"

log "Instalação B: restauração"
compose b run --rm -T app backup restore "$NAME" --yes
compose b up -d --wait --wait-timeout 300 app

log "Instalação B: confere registros, vínculos e totais"
AFTER=$(psql_app b "$SUMMARY_SQL")
echo "Resumo depois: $AFTER"
[ "$(psql_app b "$LOGO_SQL")" = "$LOGO_BEFORE" ] || fail "logo ou versão divergente após restaurar"
[ "$AFTER" = "$BEFORE" ] || fail "dados divergentes: antes=$BEFORE depois=$AFTER"
[ "$(psql_app b "$FOREIGN_KEYS_SQL")" = "$FOREIGN_KEYS_BEFORE" ] || fail "chaves estrangeiras divergentes após a restauração"
# FKs dentro do próprio módulo são permitidas; apenas vínculos entre schemas violam a fronteira.
[ "$(psql_app b "SELECT count(*) FROM pg_constraint c JOIN pg_class source ON source.oid = c.conrelid JOIN pg_class target ON target.oid = c.confrelid WHERE c.contype = 'f' AND source.relnamespace <> target.relnamespace")" = "0" ] || fail "FK entre módulos inesperada"
[ "$(psql_app b "SELECT last_value > 0 FROM pg_sequences WHERE schemaname = 'accounts' AND sequencename = 'credit_cards_Id_seq'")" = "t" ] || fail "sequência do banco não restaurada"

log "Instalação B: login com a mesma senha e continuidade da sessão (chaves de Data Protection restauradas)"
sign_in "$PORT_B" "$WORK/jar-b" || fail "login na instalação B"
authenticated "$JAR_A" "$PORT_B" || fail "a sessão da instalação A deveria continuar válida com as chaves restauradas"

log "Instalação B: certificado restaurado"
CERT_B=$(compose b exec -T app sh -c 'sha256sum /data/certs/ags-finance.pfx' | cut -d' ' -f1)
[ "$CERT_A" = "$CERT_B" ] || fail "o certificado restaurado difere do original"

log "Instalação B: anexos restaurados a partir da cópia compartilhada do backup"
compose b run --rm -T app attachments verify
DOCUMENT_AFTER=$(compose b exec -T app sha256sum "/attachments/$DOCUMENT_KEY" | cut -d' ' -f1)
[ "$DOCUMENT_AFTER" = "$DOCUMENT_SHA" ] || fail "SHA-256 do anexo restaurado"
curl -ksS -b "$WORK/jar-b" "https://localhost:$PORT_B/attachments/$DOCUMENT_ID" -o "$WORK/document-b.pdf"
[ "$(sha256sum "$WORK/document-b.pdf" | cut -d' ' -f1)" = "$DOCUMENT_SHA" ] || fail "abertura autenticada do anexo restaurado"

log "Instalação B: novas gravações funcionam (sequências continuam sem colidir)"
psql_app b "INSERT INTO accounts.banks (\"Name\", \"ExternalId\", \"CreatedAt\", \"UpdatedAt\") VALUES ('Banco Novo', '019e0000-0000-7000-8000-000000000009', now(), now())" >/dev/null

log "Compatibilidade v1: dump sem schema de anexos e manifesto legado sem attachments.json"
compose b stop app
psql_app b 'DROP SCHEMA attachments CASCADE' >/dev/null
compose b run --rm -T app backup create
LEGACY=$(compose b run --rm -T app backup list 2>/dev/null | grep ' manual ' | head -1 | awk '{print $1}')
LEGACY_JSON=$(compose b run --rm -T --entrypoint cat app "/backups/$LEGACY/manifest.json" | python3 -c 'import json,sys; m=json.loads(sys.stdin.buffer.read().decode("utf-8-sig")); m["formatVersion"]=1; m["files"]=[f for f in m["files"] if f["name"]!="attachments.json"]; json.dump(m,sys.stdout,indent=2)')
printf '%s\n' "$LEGACY_JSON" | compose b run --rm -T --entrypoint sh app -c 'cat > "$1"' sh "/backups/$LEGACY/manifest.json"
compose b run --rm -T --entrypoint rm app "/backups/$LEGACY/attachments.json"
compose b run --rm -T app backup verify "$LEGACY"
compose b run --rm -T app backup restore "$LEGACY" --yes
[ "$(psql_app b 'SELECT count(*) FROM attachments.documents')" = "0" ] || fail "migrations após restaurar v1"
compose b up -d --wait --wait-timeout 300 app
sign_in "$PORT_B" "$WORK/jar-legacy" || fail "login após restaurar v1"
[ "$(compose b exec -T app sha256sum "/attachments/$DOCUMENT_KEY" | cut -d' ' -f1)" = "$DOCUMENT_SHA" ] || fail "arquivo sem referência foi apagado no restore v1"

FAILED=0
