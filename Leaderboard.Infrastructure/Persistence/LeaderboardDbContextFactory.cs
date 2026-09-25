using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Leaderboard.Infrastructure.Persistence;

/// <summary>Creates the context for EF Core commands without starting the HTTP application.</summary>
public sealed class LeaderboardDbContextFactory : IDesignTimeDbContextFactory<LeaderboardDbContext>
{
    public LeaderboardDbContext CreateDbContext(string[] args)
    {
        string configurationDirectory = FindPresentationConfigurationDirectory();
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(configurationDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required to create EF Core migrations.");

        DbContextOptions<LeaderboardDbContext> options = new DbContextOptionsBuilder<LeaderboardDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new LeaderboardDbContext(options);
    }

    private static string FindPresentationConfigurationDirectory()
    {
        for (DirectoryInfo? directory = new(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            string presentationDirectory = Path.Combine(directory.FullName, "Leaderboard.Presentation");
            if (File.Exists(Path.Combine(presentationDirectory, "appsettings.json")))
            {
                return presentationDirectory;
            }
        }

        throw new InvalidOperationException("Could not find Leaderboard.Presentation/appsettings.json from the current directory.");
    }
}
