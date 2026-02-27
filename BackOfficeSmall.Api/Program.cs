using SettingsRegister.Api.Configuration;
using SettingsRegister.Api.ErrorHandling;
using SettingsRegister.Application.Abstractions;
using SettingsRegister.Application.Configuration;
using SettingsRegister.Application.Services;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Domain.Services;
using SettingsRegister.Infrastructure.Locking;
using SettingsRegister.Infrastructure.Monitoring;
using SettingsRegister.Infrastructure.Observability;
using SettingsRegister.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using System.Text;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ApplicationSettings appSettings = builder.Configuration
    .GetSection(ApplicationSettings.SectionName)
    .Get<ApplicationSettings>() ?? new ApplicationSettings();
AuthSettings authSettings = builder.Configuration
    .GetSection(AuthSettings.SectionName)
    .Get<AuthSettings>() ?? new AuthSettings();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddProcessInstrumentation();
        metrics.AddPrometheusExporter();
        metrics.AddMeter(ServiceMetrics.MeterName);
        metrics.AddMeter(RepositoryCacheMetrics.MeterName);
    });
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
builder.Services
    .AddHealthChecks()
    .AddCheck<ManifestRepositoryHealthCheck>("manifest-repository")
    .AddCheck<ConfigurationRepositoryHealthCheck>("configuration-repository")
    .AddCheck<ConfigurationChangeRepositoryHealthCheck>("configuration-change-repository")
    .AddCheck<MonitoringNotifierOutboxRepositoryHealthCheck>("monitoring-notifier-outbox-repository")
    .AddCheck<MonitoringNotifierHealthCheck>("monitoring-notifier");

builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton<ICachedManifestRepositorySettings>(appSettings);
builder.Services.AddSingleton<IConfigurationCachedSettings>(appSettings);
builder.Services.AddSingleton<IConfigurationChangeCachedSettings>(appSettings);
builder.Services.AddSingleton(authSettings);
builder.Services.AddSingleton<IRepositoryCacheMetrics, RepositoryCacheMetrics>();
builder.Services.AddSingleton<IServiceMetrics, ServiceMetrics>();
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

builder.Services.AddSingleton<ICachedManifestRepository>(serviceProvider =>
{
    // Had to resolve dependencies manually as repos are not registered in IoC.
    IMemoryCache memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
    ICachedManifestRepositorySettings settings = serviceProvider.GetRequiredService<ICachedManifestRepositorySettings>();
    IRepositoryCacheMetrics metrics = serviceProvider.GetRequiredService<IRepositoryCacheMetrics>();
    IManifestRepository innerRepository = new InMemoryManifestRepository();

    return new CachedManifestRepository(innerRepository, memoryCache, settings, metrics);
});
builder.Services.AddSingleton<ICacheConfigurationRepository>(serviceProvider =>
{
    // Had to resolve dependencies manually as repos are not registered in IoC.
    IMemoryCache memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
    IConfigurationCachedSettings settings = serviceProvider.GetRequiredService<IConfigurationCachedSettings>();
    IRepositoryCacheMetrics metrics = serviceProvider.GetRequiredService<IRepositoryCacheMetrics>();
    IConfigurationRepository innerRepository = new InMemoryConfigurationInstanceRepository();

    return new CachedConfigurationRepository(innerRepository, memoryCache, settings, metrics);
});
builder.Services.AddSingleton<IConfigurationChangeRepository>(serviceProvider =>
{
    IMemoryCache memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
    IConfigurationChangeCachedSettings settings = serviceProvider.GetRequiredService<IConfigurationChangeCachedSettings>();
    IRepositoryCacheMetrics metrics = serviceProvider.GetRequiredService<IRepositoryCacheMetrics>();
    IConfigurationChangeRepository innerRepository = new InMemoryConfigurationChangeRepository();

    return new CachedConfigurationChangeRepository(innerRepository, memoryCache, settings, metrics);
});
builder.Services.AddSingleton<InMemoryMonitoringNotifierOutboxRepository>();
builder.Services.AddScoped<IConfigurationWriteUnitOfWork, InMemoryConfigurationWriteUnitOfWork>();
builder.Services.AddSingleton<IMonitoringNotifier, SimulatedMonitoringNotifier>();
builder.Services.AddSingleton<IDomainLock>(appSettings.AppScaling ? new DistributedDomainLock() : new InProcessDomainLock());
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<IApplicationEnvironment, HostApplicationEnvironment>();
builder.Services.AddScoped<IManifestService, ManifestService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<IConfigurationChangeQueryService, ConfigurationChangeQueryService>();
builder.Services.AddScoped<IAuthExchangeService, AuthExchangeService>();
builder.Services.AddScoped<IOutboxDispatchService, OutboxDispatchService>();
builder.Services.AddScoped<DevelopmentSeedDataSeeder>();
builder.Services.AddHostedService<NotifierBackgroundWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();

    await SeedDevelopmentDataAsync(app.Services);
}

app.UseMiddleware<ProblemDetailsExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapPrometheusScrapingEndpoint("/metrics");

ValidateStartup(app.Services);

app.Run();

static void ValidateStartup(IServiceProvider services)
{
    using var scope = services.CreateScope();

    scope.ServiceProvider.GetRequiredService<IManifestService>();
    scope.ServiceProvider.GetRequiredService<IConfigurationService>();
    scope.ServiceProvider.GetRequiredService<IConfigurationChangeQueryService>();
    scope.ServiceProvider.GetRequiredService<IOutboxDispatchService>();
    scope.ServiceProvider.GetRequiredService<IConfigurationWriteUnitOfWork>();
    scope.ServiceProvider.GetRequiredService<ICachedManifestRepository>();
    scope.ServiceProvider.GetRequiredService<ICacheConfigurationRepository>();
    scope.ServiceProvider.GetRequiredService<IAuthExchangeService>();
    scope.ServiceProvider.GetRequiredService<IDomainLock>();
    scope.ServiceProvider.GetRequiredService<IRepositoryCacheMetrics>();
    scope.ServiceProvider.GetRequiredService<IServiceMetrics>();
    scope.ServiceProvider.GetRequiredService<ApplicationSettings>();
    scope.ServiceProvider.GetRequiredService<AuthSettings>();
}

static async Task SeedDevelopmentDataAsync(IServiceProvider services)
{
    using IServiceScope scope = services.CreateScope();
    DevelopmentSeedDataSeeder seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeedDataSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

public partial class Program
{
}
