using FluentAssertions;
using Google.Protobuf;
using Leaderboard.Contracts.Events.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;
using Leaderboard.Infrastructure.Warehouse;

namespace Leaderboard.Test.Warehouse;

public class GameEventPlayerActionDecoderTests
{
    private readonly GameEventPlayerActionDecoder _decoder = new();

    [Fact]
    public void Decode_GameEvent_ReturnsThePlayerAction()
    {
        // Arrange
        RawEvent rawEvent = RawEventBuilder.Action("ari", "attack", RawEventBuilder.Origin, 12.5);

        // Act
        PlayerAction? action = _decoder.Decode(rawEvent);

        // Assert
        action.Should().NotBeNull();
        action!.MessageId.Should().Be(rawEvent.MessageId);
        action.PlayerId.Should().Be("ari");
        action.ActionType.Should().Be("attack");
        action.Value.Should().Be(12.5m);
        action.OccurredAt.Should().Be(RawEventBuilder.Origin.AddSeconds(-1));
    }

    [Fact]
    public void Decode_NoNumericValue_CountsZero()
    {
        // Act
        PlayerAction? action = _decoder.Decode(
            RawEventBuilder.Action("ari", "potion", RawEventBuilder.Origin)
        );

        // Assert
        action!.Value.Should().Be(0m);
    }

    [Fact]
    public void Decode_NoActionType_FallsBackToTheEnvelopeType()
    {
        // Arrange
        RawEvent rawEvent = RawEventBuilder.Action("ari", string.Empty, RawEventBuilder.Origin);

        // Act
        PlayerAction? action = _decoder.Decode(rawEvent);

        // Assert
        action!.ActionType.Should().Be(rawEvent.Type);
    }

    [Fact]
    public void Decode_UnreadablePayload_ReturnsNull()
    {
        // Act & Assert
        _decoder.Decode(RawEventBuilder.Unreadable(RawEventBuilder.Origin)).Should().BeNull();
    }

    [Fact]
    public void Decode_NoPlayer_ReturnsNull()
    {
        // Act & Assert
        _decoder.Decode(RawEventBuilder.WithoutPlayer(RawEventBuilder.Origin)).Should().BeNull();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e300)]
    public void Decode_ValueThatCannotBeSummedExactly_ReturnsNull(double value)
    {
        // Arrange
        RawEvent rawEvent = RawEventBuilder.Action("ari", "attack", RawEventBuilder.Origin) with
        {
            Payload = new GameEventMessage
            {
                UserId = "ari",
                ActionType = "attack",
                Data = new EventData { ValueNumeric = value },
            }.ToByteArray(),
        };

        // Act & Assert
        _decoder.Decode(rawEvent).Should().BeNull();
    }
}
