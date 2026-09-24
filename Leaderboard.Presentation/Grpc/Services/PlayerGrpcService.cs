using Leaderboard.Application.Features.PlayerUseCase.GetPlayerById;
using Combat.Contracts.V1;
using Grpc.Core;
using MediatR;
using ILogger = Serilog.ILogger;

namespace Leaderboard.Presentation.Grpc.Services;

/// <summary>Internal gRPC surface. REST remains the public client-facing API.</summary>
public sealed class PlayerGrpcService(IMediator mediator, ILogger logger)
    : CombatPlayerService.CombatPlayerServiceBase
{
    public override async Task<GetPlayerResponse> GetPlayer(GetPlayerRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.PlayerId, out Guid playerId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "player_id must be a valid UUID."));
        }

        GetPlayerByIdResult player = await mediator.Send(
            new GetPlayerByIdQuery(playerId),
            context.CancellationToken);

        logger.Information("gRPC player lookup succeeded for {PlayerId}.", playerId);

        return new GetPlayerResponse
        {
            Id = player.Id.ToString(),
            Name = player.Name,
            Health = player.Health,
            MaxHealth = player.MaxHealth,
            Attack = player.Attack
        };
    }
}
