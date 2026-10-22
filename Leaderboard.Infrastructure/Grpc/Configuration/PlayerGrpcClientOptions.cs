namespace Leaderboard.Infrastructure.Grpc.Configuration;

public sealed class PlayerGrpcClientOptions
{
    public const string SectionName = "Grpc:Player";

    public string Address { get; init; } = "http://localhost:8081";
    public int TimeoutSeconds { get; init; } = 2;
}
