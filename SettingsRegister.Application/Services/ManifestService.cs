using SettingsRegister.Application.Abstractions;
using SettingsRegister.Application.Configuration;
using SettingsRegister.Application.Contracts;
using SettingsRegister.Application.Exceptions;
using SettingsRegister.Application.Mapping;
using SettingsRegister.Application.Observability;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Domain.Services;
using System.Diagnostics;

namespace SettingsRegister.Application.Services;

public sealed class ManifestService : IManifestService
{
    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;
    private readonly IOutboxDispatchService _notifierService;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;
    private readonly IServiceMetrics _serviceMetrics;
    private readonly TimeSpan _manifestImportLockTimeout;

    public ManifestService(
        IConfigurationWriteUnitOfWork configurationWriteUnitOfWork,
        IOutboxDispatchService notifierService,
        IDomainLock domainLock,
        ISystemClock clock,
        IServiceMetrics serviceMetrics,
        ApplicationSettings applicationSettings)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
        _notifierService = notifierService ?? throw new ArgumentNullException(nameof(notifierService));
        _domainLock = domainLock ?? throw new ArgumentNullException(nameof(domainLock));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _serviceMetrics = serviceMetrics ?? throw new ArgumentNullException(nameof(serviceMetrics));

        if (applicationSettings is null)
        {
            throw new ArgumentNullException(nameof(applicationSettings));
        }

        if (applicationSettings.ManifestImportLockTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationSettings),
                "Manifest import lock timeout seconds must be greater than zero.");
        }

        _manifestImportLockTimeout = TimeSpan.FromSeconds(applicationSettings.ManifestImportLockTimeoutSeconds);
    }

    public async Task<ManifestValueObject> ImportManifestAsync(ManifestImportRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using var activity = ApplicationActivitySource.Source.StartActivity("ManifestService.ImportManifest");
        activity?.SetTag("manifest.name", request.Name);

        request.Validate();
        _serviceMetrics.RecordManifestImportAttempt();

        // lock all manifest versions
        await using var lockHandle = await _domainLock.TryTakeLockAsync(request.Name, _manifestImportLockTimeout, cancellationToken);
        
        if (lockHandle is null)
        {
            _serviceMetrics.RecordManifestImportConflict();
            activity?.SetStatus(ActivityStatusCode.Error, "Manifest import lock acquisition failed.");
            throw new ConflictException($"Could not acquire manifest import lock for '{request.Name}'.");
        }

        var manifests = await _configurationWriteUnitOfWork.ManifestRepository.ListAsync(request.Name, cancellationToken);
        
        var latestVersion = manifests.OrderByDescending(manifest => manifest.Version).FirstOrDefault();

        var newVersion = latestVersion is null ? 1 : latestVersion.Version + 1;
        activity?.SetTag("manifest.version", newVersion);

        var manifest = request.ToDomainRoot(newVersion, _clock.UtcNow);
        manifest.Validate();

        var manifestImportChange = BuildManifestImportChange(manifest);
        var outboxMessage = MonitoringNotifierOutboxMessage.CreatePending(manifestImportChange, _clock.UtcNow);

        try
        {
            await _configurationWriteUnitOfWork.ManifestRepository.AddAsync(manifest, cancellationToken);
            await _configurationWriteUnitOfWork.ConfigurationChangeRepository.AddAsync(manifestImportChange, cancellationToken);
            await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.AddAsync(outboxMessage, cancellationToken);
            await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _serviceMetrics.RecordManifestImportConflict();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new ConflictException(ex.Message);
        }

        _serviceMetrics.RecordConfigurationChangeCreated(isCritical: true);
        _serviceMetrics.RecordOutboxMessageCreated(isCritical: true);
        _notifierService.NotifyChanges();
        activity?.SetTag("manifest.id", manifest.ManifestId.ToString());

        return ManifestValueObject.FromDomainRoot(manifest);
    }

    public async Task<ManifestValueObject> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        if (manifestId == Guid.Empty)
        {
            throw new ValidationException("ManifestId must be a non-empty GUID.");
        }

        using var activity = ApplicationActivitySource.Source.StartActivity("ManifestService.GetById");
        activity?.SetTag("manifest.id", manifestId.ToString());

        ManifestValueObject? manifest = await _configurationWriteUnitOfWork.ManifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Manifest not found.");
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    public async Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
    {
        using var activity = ApplicationActivitySource.Source.StartActivity("ManifestService.List");
        activity?.SetTag("manifest.name.filter", name);

        var manifests = await _configurationWriteUnitOfWork.ManifestRepository.ListAsync(name, cancellationToken);
        activity?.SetTag("manifest.count", manifests.Count);
        return manifests;
    }

    private ConfigurationChange BuildManifestImportChange(ManifestDomainRoot manifest)
    {
        string afterValue = $"{manifest.Name}:v{manifest.Version}";

        return new ConfigurationChange(
            Guid.NewGuid(),
            manifest.ManifestId,
            manifest.Name,
            0,
            ConfigurationOperation.Add,
            null,
            afterValue,
            manifest.CreatedBy,
            _clock.UtcNow,
            ConfigurationChangeEventType.ManifestImport);
    }
}

