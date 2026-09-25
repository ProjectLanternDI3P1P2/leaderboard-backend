using Leaderboard.Application.Messaging;

namespace Leaderboard.Infrastructure.Messaging;

/// <summary>Local adapter used when RabbitMQ is intentionally disabled.</summary>
public sealed class NoOpMessagePublisher : IMessagePublisher
{
    public Task PublishAsync(MessageEnvelope message, CancellationToken cancellationToken) => Task.CompletedTask;
}
