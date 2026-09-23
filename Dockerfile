# ── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layer for caching
COPY ["PlcMonitor.csproj", "./"]
RUN dotnet restore "PlcMonitor.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "PlcMonitor.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install tzdata so timestamps display correctly
RUN apt-get update && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

# Copy published output
COPY --from=build /app/publish .

# SQLite database lives on a mounted volume
ENV Database__Path=/data/stoppages.db
VOLUME ["/data"]

# Kestrel listens on 5000; expose it
EXPOSE 5000

ENTRYPOINT ["dotnet", "PlcMonitor.dll"]