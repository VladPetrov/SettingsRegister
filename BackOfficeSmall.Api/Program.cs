using System.Text;
using BackOfficeSmall.Api.Configuration;
using BackOfficeSmall.Api.ErrorHandling;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;
using BackOfficeSmall.Infrastructure.Locking;
using BackOfficeSmall.Infrastructure.Monitoring;
using BackOfficeSmall.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ApplicationSettings appSettings = builder.Configuration
    .GetSection(ApplicationSettings.SectionName)
    .Get<ApplicationSettings>() ?? new ApplicationSettings();
AuthSettings authSettings = builder.Configuration
    .GetSection(AuthSettings.SectionName)
    .Get<AuthSettings>() ?? new AuthSettings();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, null),
            new List<string>()
        }
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton(authSettings);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings.DevSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = authSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = authSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IManifestRepository, InMemoryManifestRepository>();
builder.Services.AddSingleton<IConfigInstanceRepository, InMemoryConfigInstanceRepository>();
builder.Services.AddSingleton<IConfigChangeRepository, InMemoryConfigChangeRepository>();
builder.Services.AddSingleton<IMonitoringNotifier, SimulatedMonitoringNotifier>();
builder.Services.AddSingleton<IDomainLock>(appSettings.AppScaling ? new DistributedDomainLock() : new InProcessDomainLock());
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<IApplicationEnvironment, HostApplicationEnvironment>();

builder.Services.AddScoped<IManifestService, ManifestService>();
builder.Services.AddScoped<IConfigInstanceService, ConfigInstanceService>();
builder.Services.AddScoped<IConfigChangeQueryService, ConfigChangeQueryService>();
builder.Services.AddScoped<IAuthExchangeService, AuthExchangeService>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ProblemDetailsExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//TODO: this does not look good
app.MapGet("/health", async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
{
    HealthReport report = await healthCheckService.CheckHealthAsync(cancellationToken);

    if (report.Status == HealthStatus.Unhealthy)
    {
        return Results.Text(report.Status.ToString(), statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Text(report.Status.ToString(), statusCode: StatusCodes.Status200OK);
})
.WithName("Health");

ValidateStartup(app.Services);

app.Run();

static void ValidateStartup(IServiceProvider services)
{
    using IServiceScope scope = services.CreateScope();

    scope.ServiceProvider.GetRequiredService<IManifestService>();
    scope.ServiceProvider.GetRequiredService<IConfigInstanceService>();
    scope.ServiceProvider.GetRequiredService<IConfigChangeQueryService>();
    scope.ServiceProvider.GetRequiredService<IAuthExchangeService>();
    scope.ServiceProvider.GetRequiredService<IDomainLock>();
    scope.ServiceProvider.GetRequiredService<ApplicationSettings>();
    scope.ServiceProvider.GetRequiredService<AuthSettings>();
}

public partial class Program
{
}
