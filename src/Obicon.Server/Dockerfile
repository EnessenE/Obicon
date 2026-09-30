# Obicon server image: API, WebSocket hub, and SQLite storage.
# The database is a file in the content root (obicon.db); mount a volume
# on /app/data and set ConnectionStrings__Default to keep it, or mount
# /app/obicon.db directly.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Obicon.Shared/ Obicon.Shared/
COPY src/Obicon.Server/ Obicon.Server/
RUN dotnet publish Obicon.Server/Obicon.Server.csproj --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Obicon.Server.dll"]
