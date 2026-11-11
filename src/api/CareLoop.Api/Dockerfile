FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY CareLoop.sln ./
COPY src/api/CareLoop.Api/CareLoop.Api.csproj src/api/CareLoop.Api/
RUN dotnet restore src/api/CareLoop.Api/CareLoop.Api.csproj

COPY src/api/CareLoop.Api/ src/api/CareLoop.Api/
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
