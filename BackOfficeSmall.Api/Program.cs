using BackOfficeSmall.Api.Configuration;
using BackOfficeSmall.Api.ErrorHandling;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;
using BackOfficeSmall.Infrastructure.Locking;
using BackOfficeSmall.Infrastructure.Monitoring;
using BackOfficeSmall.Infrastructure.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ApplicationSettings appSettings = builder.Configuration
    .GetSection(ApplicationSettings.SectionName)
    .Get<ApplicationSettings>() ?? new ApplicationSettings();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(appSettings);

builder.Services.AddSingleton<IManifestRepository, InMemoryManifestRepository>();
builder.Services.AddSingleton<IConfigInstanceRepository, InMemoryConfigInstanceRepository>();
builder.Services.AddSingleton<IConfigChangeRepository, InMemoryConfigChangeRepository>();
builder.Services.AddSingleton<IMonitoringNotifier, SimulatedMonitoringNotifier>();
builder.Services.AddSingleton<IDomainLock>(
    appSettings.AppScaling ? new DistributedDomainLock() : new InProcessDomainLock());
builder.Services.AddSingleton<ISystemClock, SystemClock>();

builder.Services.AddScoped<IManifestService, ManifestService>();
builder.Services.AddScoped<IConfigInstanceService, ConfigInstanceService>();
builder.Services.AddScoped<IConfigChangeQueryService, ConfigChangeQueryService>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ProblemDetailsExceptionHandlingMiddleware>();

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
    scope.ServiceProvider.GetRequiredService<IDomainLock>();
    scope.ServiceProvider.GetRequiredService<ApplicationSettings>();
}

public partial class Program
{
}
