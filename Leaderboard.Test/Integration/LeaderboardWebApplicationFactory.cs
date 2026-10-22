using Leaderboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Leaderboard.Test.Integration;

public sealed class LeaderboardWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["RabbitMq:Enabled"] = "false",
                }
            )
        );
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LeaderboardDbContext>>();
            services.RemoveAll<LeaderboardDbContext>();
            services.RemoveAll<IOptions<DatabaseOptions>>();
            services.AddSingleton<IOptions<DatabaseOptions>>(
                Options.Create(new DatabaseOptions { DefaultConnection = connectionString })
            );
            services.AddDbContext<LeaderboardDbContext>(options =>
                options.UseNpgsql(connectionString)
            );
        });
    }
}
