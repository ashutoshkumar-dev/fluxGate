using FluentValidation;
using Gateway.Core.DTOs.Auth;

namespace Gateway.AuthService.Validators;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(100).WithMessage("Username cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9_\-]+$").WithMessage("Username may only contain letters, digits, _ and -.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class CreateApiKeyDtoValidator : AbstractValidator<CreateApiKeyDto>
{
    public CreateApiKeyDtoValidator()
    {
        RuleFor(x => x.OwnerService)
            .NotEmpty().WithMessage("OwnerService is required.")
            .MaximumLength(200);
    }
}
