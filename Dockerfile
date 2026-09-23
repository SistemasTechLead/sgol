# syntax=docker/dockerfile:1.7
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS restore
WORKDIR /src
COPY .editorconfig Directory.Build.props Directory.Packages.props global.json NuGet.config ./
COPY src ./src
RUN dotnet restore src/Sgol.Worker/Sgol.Worker.csproj --locked-mode

FROM restore AS publish
RUN dotnet publish src/Sgol.Web/Sgol.Web.csproj --no-restore --configuration Release --output /out/web -p:UseAppHost=false \
    && dotnet publish src/Sgol.Worker/Sgol.Worker.csproj --no-restore --configuration Release --output /out/worker -p:UseAppHost=false \
    && dotnet publish src/Sgol.Operations/Sgol.Operations.csproj --no-restore --configuration Release --output /out/operations -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c AS runtime
ARG VCS_REF=unknown
ARG IMAGE_VERSION=0.0.0-local
ARG BUILD_CREATED=1970-01-01T00:00:00Z
ARG SOURCE_DIRTY=true
LABEL org.opencontainers.image.source="https://github.com/SistemasTechLead/SGOL" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.version="${IMAGE_VERSION}" \
      org.opencontainers.image.created="${BUILD_CREATED}" \
      org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:10.0-noble" \
      org.opencontainers.image.base.digest="sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c" \
      com.sgol.source.dirty="${SOURCE_DIRTY}"
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    TZ=Etc/UTC
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl gnupg \
    && curl --fail --location --silent --show-error https://www.postgresql.org/media/keys/ACCC4CF8.asc --output /tmp/postgresql.asc \
    && test "$(gpg --show-keys --with-colons /tmp/postgresql.asc | awk -F: '$1 == "fpr" { print $10; exit }')" = "B97B0AFCAA1A47F044F244A07FCC7D46ACCC4CF8" \
    && gpg --dearmor --output /usr/share/keyrings/postgresql.gpg /tmp/postgresql.asc \
    && echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] https://apt.postgresql.org/pub/repos/apt noble-pgdg main" > /etc/apt/sources.list.d/postgresql.list \
    && apt-get update \
    && apt-get install --yes --no-install-recommends \
        age=1.1.1-1ubuntu0.24.04.3 \
        postgresql-client-18=18.6-1.pgdg24.04+2 \
        tzdata \
    && apt-get purge --yes --auto-remove curl gnupg \
    && rm -f /tmp/postgresql.asc \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish --chown=0:0 /out/web/ ./
COPY --from=publish --chown=0:0 /out/worker/ ./
COPY --from=publish --chown=0:0 /out/operations/ ./
USER 1654:1654
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD ["dotnet", "Sgol.Operations.dll", "probe-http", "--url", "http://127.0.0.1:8080/health/live"]
CMD ["dotnet", "Sgol.Web.dll"]
