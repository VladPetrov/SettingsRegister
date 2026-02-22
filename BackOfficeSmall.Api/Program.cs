using BackOfficeSmall.Api.ErrorHandling;
using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;
using BackOfficeSmall.Infrastructure.Monitoring;
using BackOfficeSmall.Infrastructure.Repositories;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton<IManifestRepository, InMemoryManifestRepository>();
builder.Services.AddSingleton<IConfigInstanceRepository, InMemoryConfigInstanceRepository>();
builder.Services.AddSingleton<IConfigChangeRepository, InMemoryConfigChangeRepository>();
builder.Services.AddSingleton<IMonitoringNotifier, SimulatedMonitoringNotifier>();
builder.Services.AddSingleton<ISystemClock, SystemClock>();

builder.Services.AddScoped<IManifestService, ManifestService>();
builder.Services.AddScoped<IConfigInstanceService, ConfigInstanceService>();
builder.Services.AddScoped<IConfigChangeQueryService, ConfigChangeQueryService>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ProblemDetailsExceptionHandlingMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

ValidateStartup(app.Services);

app.Run();

static void ValidateStartup(IServiceProvider services)
{
    using IServiceScope scope = services.CreateScope();

    scope.ServiceProvider.GetRequiredService<IManifestService>();
    scope.ServiceProvider.GetRequiredService<IConfigInstanceService>();
    scope.ServiceProvider.GetRequiredService<IConfigChangeQueryService>();
}

public partial class Program
{
}
