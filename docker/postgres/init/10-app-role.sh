#!/bin/sh
# Executado pelo PostgreSQL somente na primeira inicialização de um volume vazio.
# Cria o papel da aplicação, sem privilégios de superusuário, dono do banco da aplicação. O superusuário do
# cluster (postgres) fica para manutenção. Os papéis não fazem parte do pg_dump de um banco: em uma instalação
# nova, este script os recria antes da restauração.
set -eu

: "${AGS_DB_NAME:?AGS_DB_NAME não definido}"
: "${AGS_DB_USER:?AGS_DB_USER não definido}"
: "${AGS_DB_PASSWORD:?AGS_DB_PASSWORD não definido}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
    -v app_user="$AGS_DB_USER" -v app_password="$AGS_DB_PASSWORD" -v app_db="$AGS_DB_NAME" <<'SQL'
CREATE ROLE :"app_user" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD :'app_password';
CREATE DATABASE :"app_db" OWNER :"app_user";
REVOKE ALL ON DATABASE :"app_db" FROM PUBLIC;
SQL
