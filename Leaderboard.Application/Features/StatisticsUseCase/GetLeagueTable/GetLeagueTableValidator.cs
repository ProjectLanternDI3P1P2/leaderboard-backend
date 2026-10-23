using FluentValidation;

namespace Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;

public class GetLeagueTableValidator : AbstractValidator<GetLeagueTableQuery>
{
    public const int MaximumPageSize = 100;

    public GetLeagueTableValidator()
    {
        RuleFor(q => q.Page).GreaterThan(0).WithMessage("Page must be greater than 0.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, MaximumPageSize)
            .WithMessage($"PageSize must be between 1 and {MaximumPageSize}.");
    }
}
