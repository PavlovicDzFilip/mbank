# syntax=docker/dockerfile:1

ARG SDK_VERSION=10.0.201
ARG ASPNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${SDK_VERSION} AS restore
WORKDIR /src

COPY global.json MBank.slnx ./
COPY src/MBank.Api/MBank.Api.csproj src/MBank.Api/
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
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef --version 10.0.0
RUN dotnet ef migrations bundle \
    --configuration Release \
    --project src/MBank.Api/MBank.Api.csproj \
    --startup-project src/MBank.Api/MBank.Api.csproj \
    --force \
    --output /app/migrate/mbank-migrate

FROM mcr.microsoft.com/dotnet/aspnet:${ASPNET_VERSION} AS api
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "MBank.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:${ASPNET_VERSION} AS migrate
WORKDIR /app
COPY --from=migration-build /app/migrate/mbank-migrate ./mbank-migrate
RUN chmod +x ./mbank-migrate
ENTRYPOINT ["./mbank-migrate"]
