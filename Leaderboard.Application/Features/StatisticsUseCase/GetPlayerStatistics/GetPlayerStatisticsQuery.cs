using MediatR;

namespace Leaderboard.Application.Features.StatisticsUseCase.GetPlayerStatistics;

public sealed record GetPlayerStatisticsQuery(string PlayerId)
    : IRequest<GetPlayerStatisticsResult>;
