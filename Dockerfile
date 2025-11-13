# Dockerfile for AttechServer Backend (.NET 9) - Memory Optimized for 3GB VPS
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 80

# Install ICU libraries for globalization support (fix culture errors)
RUN apk add --no-cache icu-libs

# Create non-root user for security
RUN addgroup -g 1001 -S appgroup && \
    adduser -S -D -H -u 1001 -h /app -s /sbin/nologin -G appgroup appuser

# Multi-stage build to minimize final image size
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy project file and restore dependencies (layer caching)
COPY AttechServer/*.csproj AttechServer/
RUN dotnet restore "AttechServer/AttechServer.csproj" \
    --runtime linux-musl-x64 \
    --verbosity minimal

# Copy all source code
COPY . .
WORKDIR "/src/AttechServer"

# Build in Release mode with optimizations for small memory footprint
RUN dotnet build "AttechServer.csproj" \
    -c Release \
    -o /app/build \
    --runtime linux-musl-x64 \
    --self-contained false \
    --verbosity minimal \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=false

FROM build AS publish
RUN dotnet publish "AttechServer.csproj" \
    -c Release \
    -o /app/publish \
    --runtime linux-musl-x64 \
    --self-contained false \
    --verbosity minimal \
    -p:UseAppHost=false \
    -p:PublishTrimmed=false

# Final stage - Minimal runtime for 3GB VPS
FROM base AS final
WORKDIR /app

# Install curl for health checks (minimal)
RUN apk add --no-cache curl

# Copy published app
COPY --from=publish /app/publish .

# Create necessary directories and set permissions
RUN mkdir -p /app/Uploads /app/Logs && \
    chown -R appuser:appgroup /app && \
    chmod -R 755 /app

# Switch to non-root user
USER appuser

# Environment variables optimized for low memory
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:80 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_GCHeapCount=2 \
    DOTNET_GCConserveMemory=5 \
    DOTNET_GCRetainVM=0 \
    COMPlus_EnableDiagnostics=0

# Lightweight health check
HEALTHCHECK --interval=60s --timeout=5s --start-period=30s --retries=2 \
  CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "AttechServer.dll"]