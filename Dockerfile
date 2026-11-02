# syntax=docker/dockerfile:1
# Imagem Linux da aplicação. Build em dois estágios; o runtime roda sem root e já traz o cliente do
# PostgreSQL 18 (pg_dump/pg_restore) usado pelo backup.

FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

# Restauração primeiro, só com os arquivos de projeto, para aproveitar o cache de camadas.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Ags.Finance.SharedKernel/Ags.Finance.SharedKernel.csproj src/Ags.Finance.SharedKernel/
COPY src/Ags.Finance.Web/Ags.Finance.Web.csproj src/Ags.Finance.Web/
COPY src/Modules/Ags.Finance.Modules.Accounts/Ags.Finance.Modules.Accounts.csproj src/Modules/Ags.Finance.Modules.Accounts/
COPY src/Modules/Ags.Finance.Modules.Identity/Ags.Finance.Modules.Identity.csproj src/Modules/Ags.Finance.Modules.Identity/
COPY src/Modules/Ags.Finance.Modules.Imports/Ags.Finance.Modules.Imports.csproj src/Modules/Ags.Finance.Modules.Imports/
COPY src/Modules/Ags.Finance.Modules.Invoices/Ags.Finance.Modules.Invoices.csproj src/Modules/Ags.Finance.Modules.Invoices/
COPY src/Modules/Ags.Finance.Modules.Reimbursements/Ags.Finance.Modules.Reimbursements.csproj src/Modules/Ags.Finance.Modules.Reimbursements/
COPY src/Modules/Ags.Finance.Modules.Reports/Ags.Finance.Modules.Reports.csproj src/Modules/Ags.Finance.Modules.Reports/
COPY src/Modules/Ags.Finance.Modules.Transactions/Ags.Finance.Modules.Transactions.csproj src/Modules/Ags.Finance.Modules.Transactions/
RUN dotnet restore src/Ags.Finance.Web/Ags.Finance.Web.csproj

COPY src/ src/
ARG VERSION=0.0.0-dev
RUN dotnet publish src/Ags.Finance.Web/Ags.Finance.Web.csproj \
        --configuration Release --no-restore --output /app \
        -p:Version=${VERSION} -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble AS runtime

# Cliente do PostgreSQL na mesma versão principal do servidor do Compose (18), para backup e restauração.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && install -d /usr/share/postgresql-common/pgdg \
    && curl -fsSL -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc https://www.postgresql.org/media/keys/ACCC4CF8.asc \
    && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt noble-pgdg main" \
        > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client-18 \
    && apt-get purge -y --auto-remove curl \
    && rm -rf /var/lib/apt/lists/* \
    && install -d -o app -g app -m 0750 /data /backups

ARG VERSION=0.0.0-dev
LABEL org.opencontainers.image.title="ags-controle-financeiro" \
      org.opencontainers.image.description="Controle financeiro pessoal (Blazor, .NET 10, PostgreSQL)" \
      org.opencontainers.image.source="https://github.com/adnerscarpelini/ags-controle-financeiro" \
      org.opencontainers.image.version="${VERSION}"

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_gcServer=0 \
    TZ=America/Sao_Paulo \
    DataDirectory=/data \
    Hosting__Port=5280 \
    Backup__Directory=/backups

WORKDIR /app
COPY --from=build --chown=app:app /app ./

USER app
EXPOSE 5280

# Só uma checagem de porta: tudo na aplicação exige login, então não há endpoint anônimo de saúde.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/5280"]

ENTRYPOINT ["dotnet", "Ags.Finance.Web.dll"]
