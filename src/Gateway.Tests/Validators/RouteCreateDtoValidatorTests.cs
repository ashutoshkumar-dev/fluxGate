using FluentAssertions;
using FluentValidation.TestHelper;
using Gateway.Core.DTOs;
using Gateway.Core.Validators;

namespace Gateway.Tests.Validators;

public class RouteCreateDtoValidatorTests
{
    private readonly RouteCreateDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidDto_PassesWithNoErrors()
    {
        var dto = new RouteCreateDto
        {
            Path = "/orders",
            Method = "GET",
            Destination = "http://order-service/api/orders",
            AuthRequired = true,
            Roles = ["admin"],
            IsActive = true
        };

        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyPath_FailsWithError()
    {
        var dto = new RouteCreateDto { Path = "", Method = "GET", Destination = "http://svc/test" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Path);
    }

    [Fact]
    public void Validate_PathWithoutLeadingSlash_FailsWithError()
    {
        var dto = new RouteCreateDto { Path = "orders", Method = "GET", Destination = "http://svc/test" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Path);
    }

    [Fact]
    public void Validate_EmptyMethod_FailsWithError()
    {
        var dto = new RouteCreateDto { Path = "/orders", Method = "", Destination = "http://svc/test" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Method);
    }

    [Fact]
    public void Validate_InvalidMethod_FailsWithError()
    {
        var dto = new RouteCreateDto { Path = "/orders", Method = "INVALID", Destination = "http://svc/test" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Method);
    }

    [Fact]
    public void Validate_EmptyDestination_FailsWithError()
    {
        var dto = new RouteCreateDto { Path = "/orders", Method = "GET", Destination = "" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Destination);
    }

    [Fact]
    public void Validate_InvalidDestinationUrl_FailsWithError()
    {
        var dto = new RouteCreateDto { Path = "/orders", Method = "GET", Destination = "not-a-url" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Destination);
    }

    [Fact]
    public void Validate_RateLimitWithZeroLimit_FailsWithError()
    {
        var dto = new RouteCreateDto
        {
            Path = "/orders",
            Method = "GET",
            Destination = "http://svc/test",
            RateLimit = new RateLimitDto { Limit = 0, WindowSeconds = 60 }
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.RateLimit!.Limit);
    }

    [Fact]
    public void Validate_WildcardMethod_Passes()
    {
        var dto = new RouteCreateDto { Path = "/orders", Method = "*", Destination = "http://svc/test" };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Method);
    }
}
