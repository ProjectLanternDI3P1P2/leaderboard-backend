# syntax=docker/dockerfile:1.10

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.config ./
COPY Leaderboard.Presentation.slnx ./
COPY Leaderboard.Contracts/Leaderboard.Contracts.csproj Leaderboard.Contracts/
COPY Leaderboard.Domain/Leaderboard.Domain.csproj Leaderboard.Domain/
COPY Leaderboard.Application/Leaderboard.Application.csproj Leaderboard.Application/
COPY Leaderboard.Infrastructure/Leaderboard.Infrastructure.csproj Leaderboard.Infrastructure/
COPY Leaderboard.Presentation/Leaderboard.Presentation.csproj Leaderboard.Presentation/
COPY Leaderboard.Test/Leaderboard.Test.csproj Leaderboard.Test/

# Leaderboard always consumes the released contract from GitHub Packages.
RUN --mount=type=secret,id=github_packages_credentials,env=NuGetPackageSourceCredentials_github \
    dotnet restore Leaderboard.Presentation.slnx

COPY . .
RUN dotnet publish Leaderboard.Presentation/Leaderboard.Presentation.csproj \
    --configuration Release --output /app/publish --no-restore /p:UseAppHost=false

# --- Stage 2: Hardened Runtime ---
# Use a "chiseled" (distroless) image to drastically reduce the attack surface
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080 8081
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENTRYPOINT ["dotnet", "Leaderboard.Presentation.dll"]
