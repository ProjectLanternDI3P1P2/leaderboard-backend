using MediatR;

namespace Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;

public sealed record GetLeagueTableQuery(int Page, int PageSize) : IRequest<GetLeagueTableResult>;
