using Leaderboard.Presentation.Extensions;
using Leaderboard.Application;
using Leaderboard.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureApi();

builder.Services
    .AddInfrastructureServices(builder.Configuration)
    .AddApplicationServices();

var app = builder.Build();

app.ConfigureStart();

await app.RunAsync();
