# syntax=docker/dockerfile:1

ARG SDK_VERSION=10.0.103
ARG ASPNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${SDK_VERSION} AS restore
WORKDIR /src

COPY global.json MBank.slnx ./
COPY src/MBank.Api/MBank.Api.csproj src/MBank.Api/
COPY src/MBank.AppHost/MBank.AppHost.csproj src/MBank.AppHost/
COPY src/MBank.MigrationService/MBank.MigrationService.csproj src/MBank.MigrationService/
COPY tests/MBank.Tests/MBank.Tests.csproj tests/MBank.Tests/
RUN dotnet restore MBank.slnx

FROM restore AS build
COPY . .
RUN dotnet publish src/MBank.Api/MBank.Api.csproj \
    -c Release \
    --no-restore \
    --no-self-contained \
    -o /app/publish

FROM build AS migration-build
RUN dotnet publish src/MBank.MigrationService/MBank.MigrationService.csproj \
    -c Release \
    --no-restore \
    --no-self-contained \
    -o /app/migrate

FROM mcr.microsoft.com/dotnet/aspnet:${ASPNET_VERSION} AS api
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
COPY LICENSE .
USER $APP_UID
ENTRYPOINT ["dotnet", "MBank.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:${ASPNET_VERSION} AS migrate
WORKDIR /app
COPY --from=migration-build /app/migrate .
COPY LICENSE .
ENTRYPOINT ["dotnet", "MBank.MigrationService.dll"]
