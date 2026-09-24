namespace Leaderboard.Application.Messaging;

/// <summary>Seam used by application use cases to publish integration messages.</summary>
public interface IMessagePublisher
{
    Task PublishAsync(MessageEnvelope message, CancellationToken cancellationToken);
}
