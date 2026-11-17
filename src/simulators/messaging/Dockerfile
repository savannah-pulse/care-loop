# syntax=docker/dockerfile:1
# Messaging simulator (WS8). SYNTHETIC DATA ONLY.
# Build context is the REPOSITORY ROOT (see docker-compose.yml), so every COPY path below is
# repo-root-relative:
#   docker build -f src/simulators/messaging/Dockerfile .

# ---- 1. Build the React ClientApp ------------------------------------------------------------
FROM node:22-alpine AS client
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /client
COPY src/simulators/messaging/MessagingSimulator/ClientApp/package.json \
     src/simulators/messaging/MessagingSimulator/ClientApp/yarn.lock \
     src/simulators/messaging/MessagingSimulator/ClientApp/.yarnrc.yml \
     ./
RUN corepack yarn install --immutable
COPY src/simulators/messaging/MessagingSimulator/ClientApp/ ./
RUN corepack yarn build

# ---- 2. Publish the .NET host ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
# Shared build rules (documentation enforcement, warnings as errors) and code style.
COPY Directory.Build.props .editorconfig global.json ./
COPY src/simulators/messaging/MessagingSimulator/MessagingSimulator.csproj \
     src/simulators/messaging/MessagingSimulator/
RUN dotnet restore src/simulators/messaging/MessagingSimulator/MessagingSimulator.csproj
COPY src/simulators/messaging/MessagingSimulator/ \
     src/simulators/messaging/MessagingSimulator/
RUN dotnet publish src/simulators/messaging/MessagingSimulator/MessagingSimulator.csproj \
      --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# ---- 3. Runtime ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl is used by the docker-compose health check (curl -fsS http://localhost:8080/health).
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
USER $APP_UID
ENTRYPOINT ["dotnet", "MessagingSimulator.dll"]
