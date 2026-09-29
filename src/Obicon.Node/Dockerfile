# Obicon node image: console runner that connects to the server over WebSocket.
# Configure with environment variables, e.g.:
#   docker run -e Node__ServerUrl=ws://server:5000/ws/nodes -e Node__Token=<token> obicon/node
# To reach the node metrics endpoint from outside the container, also set
#   -e Node__MetricsHost=+ -e Node__MetricsPort=9464 -p 9464:9464
# Ping and traceroute tests need raw sockets: add --cap-add=NET_RAW.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Obicon.Shared/ Obicon.Shared/
COPY src/Obicon.Node/ Obicon.Node/
RUN dotnet publish Obicon.Node/Obicon.Node.csproj --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Obicon.Node.dll"]
