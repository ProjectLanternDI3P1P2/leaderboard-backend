using Leaderboard.Application.PipelineBehavior;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;

namespace Leaderboard.Test.PipelineBehavior;

public class ValidationBehaviorTests
{
    public sealed class FakeRequest : IRequest<string>
    {
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<FakeRequest, string>([]);
        var request = new FakeRequest();
        var nextCalled = false;

        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(request, next, TestContext.Current.CancellationToken);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_PassingValidators_CallsNext()
    {
        // Arrange
        var validatorMock = new Mock<IValidator<FakeRequest>>();
        validatorMock
            .Setup(validator => validator.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<FakeRequest, string>([validatorMock.Object]);
        var request = new FakeRequest();

        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        var result = await behavior.Handle(request, next, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_FailingValidators_ThrowsValidationException()
    {
        // Arrange
        var failures = new List<ValidationFailure> { new("Name", "Name is required.") };
        var validatorMock = new Mock<IValidator<FakeRequest>>();
        validatorMock
            .Setup(validator => validator.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var behavior = new ValidationBehavior<FakeRequest, string>([validatorMock.Object]);
        var request = new FakeRequest();

        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        Func<Task> act = async () => await behavior.Handle(request, next, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
