using MediatR;

namespace Leaderboard.Application.Features.PlayerUseCase.GetPlayerById;

public record GetPlayerByIdQuery(Guid PlayerId) : IRequest<GetPlayerByIdResult>;
