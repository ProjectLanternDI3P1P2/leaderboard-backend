using FluentValidation;

namespace Leaderboard.Application.Features.PlayerUseCase.CreatePlayer;

public class CreatePlayerValidator : AbstractValidator<CreatePlayerCommand>
{
    public CreatePlayerValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty().WithMessage("Id is required.")
            .Must(id => Guid.TryParse(id.ToString(), out _)).WithMessage("Id must be a valid GUID.");

        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(50).WithMessage("Name cannot exceed 50 characters.");

        RuleFor(p => p.Attack)
            .GreaterThan(0).WithMessage("Attack must be greater than 0.");

        RuleFor(p => p.Health)
            .GreaterThan(0).WithMessage("Health must be greater than 0.");

        RuleFor(p => p.MaxHealth)
            .GreaterThan(0).WithMessage("MaxHealth must be greater than 0.");
    }
}
