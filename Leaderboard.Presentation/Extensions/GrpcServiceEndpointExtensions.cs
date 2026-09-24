using GrpcEndpointExtensions = Microsoft.AspNetCore.Builder.GrpcEndpointRouteBuilderExtensions;
using System.Reflection;

namespace Leaderboard.Presentation.Extensions;

public static class GrpcServiceEndpointExtensions
{
    private static readonly MethodInfo MapGrpcServiceMethod = typeof(GrpcEndpointExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
            method.Name == nameof(GrpcEndpointExtensions.MapGrpcService) &&
            method.IsGenericMethodDefinition &&
            method.GetGenericArguments().Length == 1 &&
            method.GetParameters().Length == 1);

    /// <summary>Maps concrete gRPC services following the Presentation.Grpc.Services naming convention.</summary>
    public static IEndpointRouteBuilder MapGrpcServices(this IEndpointRouteBuilder endpoints)
    {
        IEnumerable<Type> serviceTypes = typeof(GrpcServiceEndpointExtensions).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace?.StartsWith("Leaderboard.Presentation.Grpc.Services", StringComparison.Ordinal) == true &&
                type.Name.EndsWith("GrpcService", StringComparison.Ordinal));

        foreach (Type serviceType in serviceTypes)
        {
            MapGrpcServiceMethod.MakeGenericMethod(serviceType).Invoke(null, [endpoints]);
        }

        return endpoints;
    }
}
