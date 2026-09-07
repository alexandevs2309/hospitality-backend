# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Hospitality.sln", "Hospitality.sln"]
COPY ["src/Hospitality.API/Hospitality.API.csproj", "src/Hospitality.API/"]
COPY ["src/Hospitality.Application/Hospitality.Application.csproj", "src/Hospitality.Application/"]
COPY ["src/Hospitality.Domain/Hospitality.Domain.csproj", "src/Hospitality.Domain/"]
COPY ["src/Hospitality.Infrastructure/Hospitality.Infrastructure.csproj", "src/Hospitality.Infrastructure/"]
COPY ["tests/Hospitality.UnitTests/Hospitality.UnitTests.csproj", "tests/Hospitality.UnitTests/"]
COPY ["tests/Hospitality.IntegrationTests/Hospitality.IntegrationTests.csproj", "tests/Hospitality.IntegrationTests/"]

# Restore packages
RUN dotnet restore "Hospitality.sln"

# Copy all source code
COPY . .

# Build the solution
WORKDIR /src/src/Hospitality.API
RUN dotnet build "Hospitality.API.csproj" -c Release -o /app/build

# Publish the API
RUN dotnet publish "Hospitality.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install required runtime dependencies
RUN apt-get update && apt-get install -y curl

# Create directory for logs
RUN mkdir -p /app/logs

# Copy published app
COPY --from=build /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

# Set entrypoint
ENTRYPOINT ["dotnet", "Hospitality.API.dll"]