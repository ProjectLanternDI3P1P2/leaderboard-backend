using FluentValidation;
using Grpc.Core;
using Grpc.Core.Interceptors;
using ILogger = Serilog.ILogger;

namespace Leaderboard.Presentation.Grpc.Interceptors;

/// <summary>Maps application exceptions to standard gRPC statuses for every gRPC endpoint.</summary>
public sealed class GrpcExceptionInterceptor(ILogger logger, IHostEnvironment environment) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (KeyNotFoundException exception)
        {
            logger.Warning(exception, "gRPC resource not found.");
            throw new RpcException(new Status(StatusCode.NotFound, exception.Message));
        }
        catch (ValidationException exception)
        {
            logger.Warning(exception, "gRPC validation error.");
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Validation error."));
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Unhandled gRPC exception.");
            string detail = environment.IsDevelopment() ? exception.Message : "Internal server error.";
            throw new RpcException(new Status(StatusCode.Internal, detail));
        }
    }
}
