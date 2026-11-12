FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY CareLoop.sln Directory.Build.props Directory.Packages.props global.json ./
COPY src/api/CareLoop.Domain/CareLoop.Domain.csproj src/api/CareLoop.Domain/
COPY src/api/CareLoop.Application/CareLoop.Application.csproj src/api/CareLoop.Application/
COPY src/api/CareLoop.Infrastructure/CareLoop.Infrastructure.csproj src/api/CareLoop.Infrastructure/
COPY src/api/CareLoop.Api/CareLoop.Api.csproj src/api/CareLoop.Api/
RUN dotnet restore src/api/CareLoop.Api/CareLoop.Api.csproj

COPY src/api/ src/api/
RUN dotnet publish src/api/CareLoop.Api/CareLoop.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CareLoop.Api.dll"]
