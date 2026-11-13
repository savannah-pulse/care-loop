# CareLoop API image. Build context is the REPOSITORY ROOT (see docker-compose.yml):
#   docker build -f src/api/CareLoop.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Shared build rules (XML docs enforced, warnings as errors) and the SDK pin live at the repository root.
COPY Directory.Build.props .editorconfig global.json ./
COPY src/api/CareLoop.Domain/CareLoop.Domain.csproj src/api/CareLoop.Domain/
COPY src/api/CareLoop.Api/CareLoop.Api.csproj src/api/CareLoop.Api/
RUN dotnet restore src/api/CareLoop.Api/CareLoop.Api.csproj

COPY src/api/ src/api/
# The OpenAPI document is generated on developer and CI builds, not inside the image.
RUN dotnet publish src/api/CareLoop.Api/CareLoop.Api.csproj -c Release -o /app/publish --no-restore \
    -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the Docker Compose health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "CareLoop.Api.dll"]
