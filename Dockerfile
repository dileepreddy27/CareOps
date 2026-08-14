# syntax=docker/dockerfile:1.7
FROM node:24-alpine AS web-build
WORKDIR /source/src/CareOps.Web
COPY src/CareOps.Web/package.json src/CareOps.Web/package-lock.json ./
RUN npm ci
COPY src/CareOps.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS api-build
WORKDIR /source
COPY CareOps.sln global.json Directory.Build.props ./
COPY src/CareOps.Domain/CareOps.Domain.csproj src/CareOps.Domain/
COPY src/CareOps.Application/CareOps.Application.csproj src/CareOps.Application/
COPY src/CareOps.Infrastructure/CareOps.Infrastructure.csproj src/CareOps.Infrastructure/
COPY src/CareOps.Api/CareOps.Api.csproj src/CareOps.Api/
RUN dotnet restore src/CareOps.Api/CareOps.Api.csproj
COPY src/ src/
COPY --from=web-build /source/src/CareOps.Api/wwwroot/ src/CareOps.Api/wwwroot/
RUN dotnet publish src/CareOps.Api/CareOps.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
RUN apk add --no-cache krb5-libs \
    && mkdir -p /var/lib/careops/keys \
    && chown -R $APP_UID:$APP_UID /var/lib/careops
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=api-build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CareOps.Api.dll"]
