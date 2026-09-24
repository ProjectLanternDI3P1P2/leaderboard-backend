using Leaderboard.Application.Abstractions;
using Leaderboard.Infrastructure.Persistence;
using MediatR;

namespace Leaderboard.Infrastructure.PipelineBehavior;

public sealed class CommandTransactionBehavior<TRequest, TResponse>(LeaderboardDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        TResponse response = await next(cancellationToken);

        if (request is ICommand)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return response;
    }
}
