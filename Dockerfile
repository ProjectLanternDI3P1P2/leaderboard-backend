# syntax=docker/dockerfile:1.7

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Disable .NET telemetry during build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY Leaderboard.Presentation.slnx ./
COPY Leaderboard.Contracts/Leaderboard.Contracts.csproj Leaderboard.Contracts/
COPY Leaderboard.Domain/Leaderboard.Domain.csproj Leaderboard.Domain/
COPY Leaderboard.Application/Leaderboard.Application.csproj Leaderboard.Application/
COPY Leaderboard.Infrastructure/Leaderboard.Infrastructure.csproj Leaderboard.Infrastructure/
COPY Leaderboard.Presentation/Leaderboard.Presentation.csproj Leaderboard.Presentation/
COPY Leaderboard.Test/Leaderboard.Test.csproj Leaderboard.Test/

RUN --mount=type=secret,id=github_token,required=true \
    token="$(cat /run/secrets/github_token)" && \
    dotnet nuget add source \
      "https://nuget.pkg.github.com/ProjectLanternDI3P1P2/index.json" \
      --name github \
      --username github-actions \
      --password "$token" \
      --store-password-in-clear-text && \
    dotnet restore Leaderboard.Presentation.slnx && \
    dotnet nuget remove source github

COPY . .

# Hardened build without debug symbols
RUN dotnet publish Leaderboard.Presentation/Leaderboard.Presentation.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

# --- Stage 2: Hardened Runtime ---
# Use a "chiseled" (distroless) image to drastically reduce the attack surface
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app

# Harden .NET environment variables
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_URLS=http://+:8080

# Single unprivileged port; HTTPS will be handled by the Kubernetes Ingress
EXPOSE 8080

# Explicitly grant ownership to the non-root user during copy
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Execute as non-root user (built-in by default in chiseled images)
USER $APP_UID

ENTRYPOINT ["dotnet", "Leaderboard.Presentation.dll"]
