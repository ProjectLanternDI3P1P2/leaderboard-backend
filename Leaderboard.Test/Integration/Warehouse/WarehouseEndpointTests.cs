using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Leaderboard.Test.Integration.Warehouse;

public sealed class WarehouseEndpointTests : IAsyncLifetime
{
    private TestDatabase database = null!;
    private LeaderboardWebApplicationFactory factory = null!;

    public async ValueTask InitializeAsync()
    {
        database = await TestDatabase.CreateAsync($"leaderboard_test_endpoints_{Guid.NewGuid():N}");
        factory = new LeaderboardWebApplicationFactory(database.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    private HttpClient Operator()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Roles", "player, operator");
        return client;
    }

    [Fact]
    public async Task PostRebuild_Operator_ReturnsAcceptedWithTheReportLocation()
    {
        // Act
        HttpResponseMessage response = await Operator()
            .PostAsync("/api/v1/warehouse/rebuilds", null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        JsonElement report = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken
        );
        report.GetProperty("status").GetString().Should().Be("Pending");
        response
            .Headers.Location!.ToString()
            .Should()
            .EndWith(report.GetProperty("id").GetString());

        HttpResponseMessage reportResponse = await Operator()
            .GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostRebuild_WithoutOperatorRole_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Roles", "player");

        // Act
        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/warehouse/rebuilds",
            null,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostRebuild_RebuildAlreadyPending_ReturnsConflict()
    {
        // Arrange
        HttpClient client = Operator();
        await client.PostAsync(
            "/api/v1/warehouse/rebuilds",
            null,
            TestContext.Current.CancellationToken
        );

        // Act
        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/warehouse/rebuilds",
            null,
            TestContext.Current.CancellationToken
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetRebuild_UnknownId_ReturnsNotFound()
    {
        // Act
        HttpResponseMessage response = await Operator()
            .GetAsync(
                $"/api/v1/warehouse/rebuilds/{Guid.NewGuid()}",
                TestContext.Current.CancellationToken
            );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLeaderboard_NothingConsolidatedYet_ReturnsAnEmptyTable()
    {
        // Act
        HttpResponseMessage response = await factory
            .CreateClient()
            .GetAsync(
                "/api/v1/leaderboard?page=1&pageSize=10",
                TestContext.Current.CancellationToken
            );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement table = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken
        );
        table.GetProperty("totalPlayers").GetInt64().Should().Be(0);
        table.GetProperty("entries").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetLeaderboard_PageSizeTooLarge_ReturnsUnprocessableEntity()
    {
        // Act
        HttpResponseMessage response = await factory
            .CreateClient()
            .GetAsync("/api/v1/leaderboard?pageSize=1000", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
