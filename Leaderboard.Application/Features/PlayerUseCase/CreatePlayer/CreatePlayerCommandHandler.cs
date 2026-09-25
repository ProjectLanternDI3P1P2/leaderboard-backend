using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Application.Messaging;
using MediatR;

namespace Leaderboard.Application.Features.PlayerUseCase.CreatePlayer;

public sealed class CreatePlayerCommandHandler(
    IPlayerRepository playerRepository,
    IMessagePublisher messagePublisher) : IRequestHandler<CreatePlayerCommand>
{
    public async Task Handle(CreatePlayerCommand request, CancellationToken cancellationToken)
    {
        Player player = new()
        {
            Id = request.Id,
            Name = request.Name,
            Attack = request.Attack,
            Health = request.Health,
            MaxHealth = request.MaxHealth
        };

        await playerRepository.AddPlayerAsync(player, cancellationToken);

        await messagePublisher.PublishAsync(
            PlayerCreatedMessageFactory.Create(player.Id, player.Name, player.Attack, player.Health, player.MaxHealth),
            cancellationToken);
    }
}
