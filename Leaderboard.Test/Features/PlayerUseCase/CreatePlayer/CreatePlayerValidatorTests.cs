using Bogus;
using Leaderboard.Application.Features.PlayerUseCase.CreatePlayer;
using FluentValidation.TestHelper;

namespace Leaderboard.Test.Features.PlayerUseCase.CreatePlayer;

public class CreatePlayerValidatorTests
{
    private static readonly Faker Faker = new();
    private readonly CreatePlayerValidator _validator = new();

    private static CreatePlayerCommand ValidCommand() => new(
        Guid.NewGuid(),
        Faker.Name.FirstName(),
        Faker.Random.Int(1, 20),
        Faker.Random.Int(1, 100),
        Faker.Random.Int(100, 200));

    [Fact]
    public void Validate_ValidCommand_HasNoValidationErrors()
    {
        // Arrange
        var command = ValidCommand();

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_HasValidationErrorForId()
    {
        // Arrange
        var command = ValidCommand() with { Id = Guid.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(request => request.Id);
    }

    [Fact]
    public void Validate_EmptyName_HasValidationErrorForName()
    {
        // Arrange
        var command = ValidCommand() with { Name = string.Empty };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(request => request.Name);
    }

    [Fact]
    public void Validate_NameExceedsMaximumLength_HasValidationErrorForName()
    {
        // Arrange
        var command = ValidCommand() with { Name = new string('a', 51) };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(request => request.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidAttack_HasValidationErrorForAttack(int attack)
    {
        // Arrange
        var command = ValidCommand() with { Attack = attack };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(request => request.Attack);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidHealth_HasValidationErrorForHealth(int health)
    {
        // Arrange
        var command = ValidCommand() with { Health = health };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(request => request.Health);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxHealth_HasValidationErrorForMaxHealth(int maxHealth)
    {
        // Arrange
        var command = ValidCommand() with { MaxHealth = maxHealth };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(request => request.MaxHealth);
    }
}
