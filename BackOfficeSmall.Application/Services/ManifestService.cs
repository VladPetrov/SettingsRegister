using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Mapping;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Application.Services;

public sealed class ManifestService : IManifestService
{
    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;
    private readonly INotifierService _notifierService;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;
    private readonly TimeSpan _manifestImportLockTimeout;

    public ManifestService(
        IConfigurationWriteUnitOfWork configurationWriteUnitOfWork,
        INotifierService notifierService,
        IDomainLock domainLock,
        ISystemClock clock,
        ApplicationSettings applicationSettings)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
        _notifierService = notifierService ?? throw new ArgumentNullException(nameof(notifierService));
        _domainLock = domainLock ?? throw new ArgumentNullException(nameof(domainLock));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

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

        request.Validate();

        // lock all manifest versions
        await using var lockHandle = await _domainLock.TryTakeLockAsync(request.Name, _manifestImportLockTimeout, cancellationToken);
        
        if (lockHandle is null)
        {
            throw new ConflictException($"Could not acquire manifest import lock for '{request.Name}'.");
        }

        var manifests = await _configurationWriteUnitOfWork.ManifestRepository.ListAsync(request.Name, cancellationToken);
        
        var latestVersion = manifests.OrderByDescending(manifest => manifest.Version).FirstOrDefault();

        var newVersion = latestVersion is null ? 1 : latestVersion.Version + 1;

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
            throw new ConflictException(ex.Message);
        }

        _notifierService.NotifyChanges();

        return ManifestValueObject.FromDomainRoot(manifest);
    }

    public async Task<ManifestValueObject> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        if (manifestId == Guid.Empty)
        {
            throw new ValidationException("ManifestId must be a non-empty GUID.");
        }

        ManifestValueObject? manifest = await _configurationWriteUnitOfWork.ManifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    public async Task<IReadOnlyList<ManifestValueObject>> ListAsync(string? name, CancellationToken cancellationToken)
    {
        return await _configurationWriteUnitOfWork.ManifestRepository.ListAsync(name, cancellationToken);
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
