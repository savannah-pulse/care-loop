# syntax=docker/dockerfile:1
#
# HIS simulator image (WS7). SYNTHETIC DATA ONLY.
#
# Build context is the REPOSITORY ROOT (see docker-compose.yml), so every COPY path below is
# repository-root-relative:
#   docker build -f src/simulators/his/Dockerfile -t careloop-his-simulator .

# ---- Stage 1: build the React ClientApp --------------------------------------------------
FROM node:22-alpine AS client
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /client

# Install dependencies first so this layer is cached until the manifest or lockfile changes.
COPY src/simulators/his/HisSimulator/ClientApp/package.json \
     src/simulators/his/HisSimulator/ClientApp/yarn.lock \
     src/simulators/his/HisSimulator/ClientApp/.yarnrc.yml \
     ./
RUN corepack yarn install --immutable

COPY src/simulators/his/HisSimulator/ClientApp/ ./
RUN corepack yarn build

# ---- Stage 2: publish the ASP.NET Core app ------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Shared build rules (target framework, documentation enforcement) and code style live at the root.
COPY Directory.Build.props .editorconfig global.json ./

COPY src/simulators/his/HisSimulator/HisSimulator.csproj src/simulators/his/HisSimulator/
RUN dotnet restore src/simulators/his/HisSimulator/HisSimulator.csproj

COPY src/simulators/his/HisSimulator/ src/simulators/his/HisSimulator/
RUN dotnet publish src/simulators/his/HisSimulator/HisSimulator.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---- Stage 3: runtime ---------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl is used by the docker-compose healthcheck: curl -fsS http://localhost:8080/health
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=client /client/dist ./wwwroot
COPY src/simulators/scenarios ./scenarios

ENV ASPNETCORE_URLS=http://+:8080 \
    Scenarios__Directory=/app/scenarios
EXPOSE 8080

# Non-root user provided by the .NET runtime image.
USER $APP_UID

ENTRYPOINT ["dotnet", "HisSimulator.dll"]
