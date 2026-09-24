using MediatR;
using System.Diagnostics;
using ILogger = Serilog.ILogger;

namespace Leaderboard.Application.PipelineBehavior;

public class LoggingBehavior<TRequest, TResponse>(ILogger logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        sw.Stop();

        var requestType = typeof(TRequest).Name;
        var responseType = typeof(TResponse).Name;

        logger.Information(
            "[HandlerMetrics] {RequestType} => {ResponseType} | DurationMs={DurationMs}",
            requestType,
            responseType,
            sw.Elapsed.TotalMilliseconds);

        return response;
    }
}
