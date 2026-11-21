# syntax=docker/dockerfile:1.7-labs
# (1.7-labs: "COPY --parents" icin; csproj'lari klasor yapisini koruyarak kopyalar)
#
# Booster.Auth içindeki tüm ASP.NET servisleri için ortak, parametrik Dockerfile.
# Build context: repo kökü (Booster.Auth/).
#
#   docker build -f docker/Dockerfile \
#     --build-arg PROJECT=Idps/Booster.Auth.Idp/Booster.Auth.Idp.csproj \
#     --build-arg APP_NAME=Booster.Auth.Idp \
#     -t <namespace>/booster-auth-idp:<tag> .
#
# Image'a hiçbir secret / ortam ayarı girmez: appsettings.Development.json, keys/,
# .env dosyaları .dockerignore ile dışarıda bırakılır. Tüm ayarlar çalışma anında
# ortam değişkenleri ve mount edilen secret dosyaları ile verilir.

ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------- build
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG PROJECT
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN test -n "${PROJECT}" || (echo "PROJECT build-arg zorunlu" >&2; exit 1)

# 1) Yalnizca proje dosyalari -> restore.
#    Bu katman yalnizca bir .csproj degisince yeniden olusur; kod degisikliklerinde
#    NuGet paketleri onbellekten gelir (CI'da ~15 sn restore + buyuk cache yuklemesi kazanilir).
#    Paketler katmanin ICINDE tutulur (cache mount degil): CI onbellegi yalnizca
#    katmanlari tasir, cache mount'lari tasimaz.
ENV NUGET_PACKAGES=/nuget/packages
COPY --parents **/*.csproj ./
RUN dotnet restore "${PROJECT}"

# 2) Kodun tamami -> publish (restore tekrarlanmaz).
COPY . .
RUN dotnet publish "${PROJECT}" \
      -c "${BUILD_CONFIGURATION}" \
      -o /app/publish \
      --no-restore \
      /p:UseAppHost=false \
      /p:DebugType=None \
      /p:GenerateDocumentationFile=false

# ---------------------------------------------------------------- runtime
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
ARG APP_NAME
ARG VERSION=dev
ARG REVISION=unknown

LABEL org.opencontainers.image.title="${APP_NAME}" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}" \
      org.opencontainers.image.vendor="Booster"

# BUILD_REVISION: image'in uretildigi commit; servislerin GET /version cevabinda "build".
ENV APP_DLL="${APP_NAME}.dll" \
    BUILD_REVISION="${REVISION}" \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_gcServer=0

WORKDIR /app

COPY --from=build /app/publish .
COPY docker/entrypoint.sh /usr/local/bin/docker-entrypoint.sh

# - Windows checkout'larında CRLF gelebilir; shebang bozulmasın.
# - Uygulama dosyaları root'a ait ve salt okunur kalır; yalnızca çalışma anında
#   yazılması gereken klasörler (DataProtection anahtarları, yüklenen logolar)
#   uygulama kullanıcısına verilir ve volume olarak bağlanabilir.
RUN test -n "${APP_NAME}" \
 && test -f "/app/${APP_NAME}.dll" \
 && sed -i 's/\r$//' /usr/local/bin/docker-entrypoint.sh \
 && chmod 0755 /usr/local/bin/docker-entrypoint.sh \
 && mkdir -p /home/app/.aspnet/DataProtection-Keys /app/wwwroot/client-logos \
 && chown -R "${APP_UID}:${APP_UID}" /home/app /app/wwwroot/client-logos

# aspnet image'ındaki root olmayan kullanıcı (UID 1654)
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
