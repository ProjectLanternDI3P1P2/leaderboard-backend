using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Leaderboard.Presentation.DTO;

namespace Leaderboard.Test.Integration.Players;

public sealed class PlayerControllerIntegrationTests(PlayerEndpointFixture fixture)
    : IClassFixture<PlayerEndpointFixture>
{
    [Fact]
    public async Task PostPlayer_ValidPayload_ReturnsCreated()
    {
        var player = new PlayerDto
        {
            Id = Guid.NewGuid(),
            Name = "Ari",
            Health = 100,
            MaxHealth = 100,
            Attack = 12,
        };

        HttpResponseMessage response = await fixture.HttpClient.PostAsJsonAsync(
            "/api/v1/players",
            player,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }
}
