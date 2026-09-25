using Leaderboard.Application.Models;
using Leaderboard.Application.Ports;
using Combat.Contracts.V1;
using Leaderboard.Infrastructure.Grpc.Configuration;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Leaderboard.Infrastructure.Grpc.Clients;

/// <summary>gRPC implementation of the application port for the Player service.</summary>
public sealed class PlayerGrpcClient(
    CombatPlayerService.CombatPlayerServiceClient client,
    IOptions<PlayerGrpcClientOptions> options) : IPlayerClient
{
    public async Task<PlayerSummary> GetPlayerAsync(Guid playerId, CancellationToken cancellationToken)
    {
        try
        {
            GetPlayerResponse player = await client.GetPlayerAsync(
                new GetPlayerRequest { PlayerId = playerId.ToString() },
                new CallOptions(
                    deadline: DateTime.UtcNow.AddSeconds(options.Value.TimeoutSeconds),
                    cancellationToken: cancellationToken));

            return new PlayerSummary
            {
                Id = Guid.Parse(player.Id),
                Name = player.Name,
                Health = player.Health,
                MaxHealth = player.MaxHealth,
                Attack = player.Attack
            };
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Player '{playerId}' was not found.", exception);
        }
    }
}
