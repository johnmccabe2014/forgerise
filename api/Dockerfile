# syntax=docker/dockerfile:1.7
# ForgeRise API — .NET 9 multi-stage build, non-root, healthcheck-aware.
# Build context: api/

ARG DOTNET_SDK_TAG=9.0-alpine
ARG DOTNET_RUNTIME_TAG=9.0-alpine

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build
WORKDIR /src

# Restore as a discrete layer for cache.
COPY ForgeRise.sln ./
COPY ForgeRise.Api/ForgeRise.Api.csproj ForgeRise.Api/
COPY ForgeRise.Api.Tests/ForgeRise.Api.Tests.csproj ForgeRise.Api.Tests/
RUN dotnet restore ForgeRise.Api/ForgeRise.Api.csproj

# Build + publish.
COPY . .
RUN dotnet publish ForgeRise.Api/ForgeRise.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_TAG} AS runtime
WORKDIR /app

# Non-root user.
RUN addgroup -S -g 10001 forgerise \
 && adduser  -S -u 10001 -G forgerise forgerise

# wget for the healthcheck (alpine image is lean).
RUN apk add --no-cache wget=1.25.0-r0 || apk add --no-cache wget

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true \
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false

COPY --from=build --chown=forgerise:forgerise /app/publish ./

USER forgerise
EXPOSE 5080

HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD wget -q -O - http://127.0.0.1:5080/health || exit 1

ENTRYPOINT ["dotnet", "ForgeRise.Api.dll"]
