# ── Build Stage ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first (layer caching)
COPY ["MyMealPlanner.Web/MyMealPlanner.Web.csproj",           "MyMealPlanner.Web/"]
COPY ["MyMealPlanner.Core/MyMealPlanner.Core.csproj",         "MyMealPlanner.Core/"]
COPY ["MyMealPlanner.Infrastructure/MyMealPlanner.Infrastructure.csproj", "MyMealPlanner.Infrastructure/"]
COPY ["MyMealPlanner.Services/MyMealPlanner.Services.csproj", "MyMealPlanner.Services/"]

RUN dotnet restore "MyMealPlanner.Web/MyMealPlanner.Web.csproj"

# Copy everything else
COPY . .

# Build & publish
WORKDIR "/src/MyMealPlanner.Web"
RUN dotnet publish "MyMealPlanner.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime Stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Expose port
EXPOSE 8080
ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MyMealPlanner.Web.dll"]
