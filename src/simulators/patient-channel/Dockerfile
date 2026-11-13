# syntax=docker/dockerfile:1
# Patient-channel simulator (WS8). SYNTHETIC DATA ONLY.
# Build context is the REPOSITORY ROOT (see docker-compose.yml), so every COPY path below is
# repo-root-relative:
#   docker build -f src/simulators/patient-channel/Dockerfile .

# ---- 1. Build the React ClientApp ------------------------------------------------------------
FROM node:22-alpine AS client
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /client
COPY src/simulators/patient-channel/PatientChannelSimulator/ClientApp/package.json \
     src/simulators/patient-channel/PatientChannelSimulator/ClientApp/yarn.lock \
     src/simulators/patient-channel/PatientChannelSimulator/ClientApp/.yarnrc.yml \
     ./
RUN corepack yarn install --immutable
COPY src/simulators/patient-channel/PatientChannelSimulator/ClientApp/ ./
RUN corepack yarn build

# ---- 2. Publish the .NET host ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
# Shared build rules (documentation enforcement, warnings as errors) and code style.
COPY Directory.Build.props .editorconfig global.json ./
COPY src/simulators/patient-channel/PatientChannelSimulator/PatientChannelSimulator.csproj \
     src/simulators/patient-channel/PatientChannelSimulator/
RUN dotnet restore src/simulators/patient-channel/PatientChannelSimulator/PatientChannelSimulator.csproj
COPY src/simulators/patient-channel/PatientChannelSimulator/ \
     src/simulators/patient-channel/PatientChannelSimulator/
RUN dotnet publish src/simulators/patient-channel/PatientChannelSimulator/PatientChannelSimulator.csproj \
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
ENTRYPOINT ["dotnet", "PatientChannelSimulator.dll"]
