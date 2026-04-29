using FluentValidation;
using Gateway.Core.DTOs;

namespace Gateway.Core.Validators;

public class RouteUpdateDtoValidator : AbstractValidator<RouteUpdateDto>
{
    private static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "*"];

    public RouteUpdateDtoValidator()
    {
        RuleFor(x => x.Path)
            .NotEmpty().WithMessage("Path is required.")
            .Must(p => p.StartsWith("/")).WithMessage("Path must start with '/'.")
            .MaximumLength(500);

        RuleFor(x => x.Method)
            .NotEmpty().WithMessage("Method is required.")
            .Must(m => AllowedMethods.Contains(m.ToUpperInvariant()))
            .WithMessage($"Method must be one of: {string.Join(", ", AllowedMethods)}.");

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Destination is required.")
            .Must(d => Uri.TryCreate(d, UriKind.Absolute, out _))
            .WithMessage("Destination must be a valid absolute URL.");

        RuleFor(x => x.Roles)
            .NotNull();

        When(x => x.RateLimit is not null, () =>
        {
            RuleFor(x => x.RateLimit!.Limit)
                .GreaterThan(0).WithMessage("RateLimit.Limit must be greater than 0.");

            RuleFor(x => x.RateLimit!.WindowSeconds)
                .GreaterThan(0).WithMessage("RateLimit.WindowSeconds must be greater than 0.");
        });

        When(x => x.CacheTtlSeconds is not null, () =>
        {
            RuleFor(x => x.CacheTtlSeconds!.Value)
                .GreaterThan(0).WithMessage("CacheTtlSeconds must be greater than 0.");
        });
    }
}
