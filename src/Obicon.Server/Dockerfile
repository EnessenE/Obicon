# Obicon server image: API, WebSocket hub, and observability egress.
# The database is PostgreSQL: point ConnectionStrings__Default at a reachable
# instance (the repo root's docker-compose.yml runs one next to this image).
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
