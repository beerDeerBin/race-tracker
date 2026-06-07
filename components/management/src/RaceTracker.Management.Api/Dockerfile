# Build context is the repo root (see components/management/docker-compose.yml) so the
# hierarchical root build files (global.json, Directory.*.props, nuget.config) and the
# shared building-blocks project are available to restore/build.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Root build files first (better layer caching on restore). .editorconfig is included
# so the container build enforces the same analyzer/style rules as a local build.
COPY global.json Directory.Build.props Directory.Packages.props nuget.config .editorconfig ./
COPY components/building-blocks/ components/building-blocks/
COPY components/management/ components/management/

RUN dotnet restore components/management/src/RaceTracker.Management.Api/RaceTracker.Management.Api.csproj
RUN dotnet publish components/management/src/RaceTracker.Management.Api/RaceTracker.Management.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RaceTracker.Management.Api.dll"]
