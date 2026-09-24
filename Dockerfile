FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Leaderboard.Presentation.slnx ./
COPY Leaderboard.Domain/Leaderboard.Domain.csproj Leaderboard.Domain/
COPY Leaderboard.Application/Leaderboard.Application.csproj Leaderboard.Application/
COPY Leaderboard.Infrastructure/Leaderboard.Infrastructure.csproj Leaderboard.Infrastructure/
COPY Leaderboard.Presentation/Leaderboard.Presentation.csproj Leaderboard.Presentation/
COPY Leaderboard.Test/Leaderboard.Test.csproj Leaderboard.Test/

RUN dotnet restore Leaderboard.Presentation.slnx

COPY . .
RUN dotnet publish Leaderboard.Presentation/Leaderboard.Presentation.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080 8081

COPY --from=build /app/publish .

# Unprivileged "app" user shipped by the aspnet image.
USER $APP_UID

ENTRYPOINT ["dotnet", "Leaderboard.Presentation.dll"]
