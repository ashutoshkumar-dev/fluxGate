using FluentValidation;
using FluentValidation.AspNetCore;
using Gateway.AuthService.Security;
using Gateway.AuthService.Services;
using Gateway.AuthService.Validators;
using Gateway.Core.Interfaces;
using Gateway.Infrastructure.Persistence;
using Gateway.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services));

    builder.Services.AddControllers();

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();

    builder.Services.AddDbContext<AuthDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // RSA key provider — singleton so the key is stable for the app lifetime
    builder.Services.AddSingleton<RsaKeyProvider>();
    builder.Services.AddSingleton<TokenService>();

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<ApiKeyService>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway.AuthService failed to start");
}
finally
{
    Log.CloseAndFlush();
}
