using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog.Context;

namespace Leaderboard.Presentation.Grpc.Interceptors;

/// <summary>Preserves a caller correlation ID and makes it available to structured gRPC logs.</summary>
public sealed class CorrelationIdInterceptor : Interceptor
{
    private const string CorrelationIdHeader = "x-correlation-id";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation
    )
    {
        string correlationId =
            GetCorrelationId(context.RequestHeaders) ?? Guid.NewGuid().ToString();
        context.ResponseTrailers.Add(CorrelationIdHeader, correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            return await continuation(request, context);
        }
    }

    private static string? GetCorrelationId(Metadata headers)
    {
        string? value = headers.GetValue(CorrelationIdHeader);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
