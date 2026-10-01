FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY dcArca.Core/dcArca.Core.csproj dcArca.Core/
COPY dcArca.McpServer/dcArca.McpServer.csproj dcArca.McpServer/
RUN dotnet restore dcArca.McpServer/dcArca.McpServer.csproj
COPY dcArca.Core/ dcArca.Core/
COPY dcArca.McpServer/ dcArca.McpServer/
RUN dotnet publish dcArca.McpServer/dcArca.McpServer.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "dcArca.McpServer.dll"]
