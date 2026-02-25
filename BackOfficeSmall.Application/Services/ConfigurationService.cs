using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Application.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private static readonly TimeSpan InstanceLockTimeout = TimeSpan.FromSeconds(30);

    private readonly IManifestRepository _manifestRepository;
    private readonly IConfigurationRepository _configInstanceRepository;
    private readonly IConfigurationWriteUnitOfWorkFactory _configurationWriteUnitOfWorkFactory;
    private readonly IMonitoringNotifier _monitoringNotifier;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;

    public ConfigurationService(
        IManifestRepository manifestRepository,
        IConfigurationRepository configInstanceRepository,
        IConfigurationWriteUnitOfWorkFactory configurationWriteUnitOfWorkFactory,
        IMonitoringNotifier monitoringNotifier,
        IDomainLock domainLock,
        ISystemClock clock)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _configInstanceRepository = configInstanceRepository ?? throw new ArgumentNullException(nameof(configInstanceRepository));
        _configurationWriteUnitOfWorkFactory = configurationWriteUnitOfWorkFactory ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWorkFactory));
        _monitoringNotifier = monitoringNotifier ?? throw new ArgumentNullException(nameof(monitoringNotifier));
        _domainLock = domainLock ?? throw new ArgumentNullException(nameof(domainLock));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ConfigurationInstance> CreateInstanceAsync(ConfigurationInstanceCreateRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Validate();

        var manifest = await GetManifestOrThrowAsync(request.ManifestId, cancellationToken);
        ConfigurationInstance instance = new(Guid.NewGuid(), request.Name, manifest, _clock.UtcNow, request.CreatedBy);

        if (request.Cells is not null)
        {
            foreach (SettingCellInput cell in request.Cells)
            {
                TrySetValue(instance, cell.SettingKey, cell.LayerIndex, NormalizeValue(cell.Value));
            }
        }

        try
        {
            await _configInstanceRepository.AddAsync(instance, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        return instance;
    }

    public async Task<ConfigurationInstance> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationInstanceId must be a non-empty GUID.");
        }

        // This guaranties consistent reads
        await using var instanceLock = await AcquireInstanceLockOrThrowAsync(instanceId, cancellationToken);
        
        return await GetInstanceOrThrowAsync(instanceId, _configInstanceRepository, cancellationToken);
    }

    public Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken)
    {
        return _configInstanceRepository.ListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid instanceId, DeleteConfigurationInstanceRequest request, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationInstanceId must be a non-empty GUID.");
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Validate();

        await using var instanceLock = await AcquireInstanceLockOrThrowAsync(instanceId, cancellationToken);
       
        await using IConfigurationWriteUnitOfWork unitOfWork = _configurationWriteUnitOfWorkFactory.Create();
        ConfigurationInstance? instance = await unitOfWork.ConfigurationRepository.GetByIdAsync(instanceId, cancellationToken);

        if (instance is null)
        {
            return;
        }

        IReadOnlyList<ConfigurationChange> changes = BuildDeleteChanges(instance, request.DeletedBy);

        try
        {
            foreach (ConfigurationChange change in changes)
            {
                await unitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
            }

            await unitOfWork.ConfigurationRepository.DeleteAsync(instanceId, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        foreach (ConfigurationChange change in changes)
        {
            if (instance.Manifest.RequiresCriticalNotification(change.SettingKey))
            {
                await _monitoringNotifier.NotifyCriticalChangeAsync(change, cancellationToken);
            }
        }
    }

    public async Task<ConfigurationChange> SetValueAsync(Guid instanceId, SetCellValueRequest request, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationInstanceId must be a non-empty GUID.");
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Validate();

        await using var instanceLock = await AcquireInstanceLockOrThrowAsync(instanceId, cancellationToken);

        await using IConfigurationWriteUnitOfWork unitOfWork = _configurationWriteUnitOfWorkFactory.Create();
        ConfigurationInstance instance = await GetInstanceOrThrowAsync(
            instanceId,
            unitOfWork.ConfigurationRepository,
            cancellationToken);
        string? beforeValue = instance.GetValue(request.SettingKey, request.LayerIndex);
        string? afterValue = NormalizeValue(request.Value);

        TrySetValue(instance, request.SettingKey, request.LayerIndex, afterValue);
        ConfigurationOperation operation = ResolveOperation(beforeValue, afterValue);

        ConfigurationChange change = new(
            Guid.NewGuid(),
            instance.ConfigurationInstanceId,
            request.SettingKey,
            request.LayerIndex,
            operation,
            beforeValue,
            afterValue,
            request.ChangedBy,
            _clock.UtcNow);

        try
        {
            await unitOfWork.ConfigurationRepository.UpdateAsync(instance, cancellationToken);
            await unitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        if (instance.Manifest.RequiresCriticalNotification(request.SettingKey))
        {
            await _monitoringNotifier.NotifyCriticalChangeAsync(change, cancellationToken);
        }

        return change;
    }

    private async Task<IDomainLockLease> AcquireInstanceLockOrThrowAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instanceLock = await _domainLock.TryTakeLockAsync(instanceId.ToString(), InstanceLockTimeout, cancellationToken);
        
        if (instanceLock is null)
        {
            throw new ConflictException($"Could not acquire configuration instance lock for '{instanceId}'.");
        }

        return instanceLock;
    }

    private async Task<ManifestValueObject> GetManifestOrThrowAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        var manifest = await _manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    private IReadOnlyList<ConfigurationChange> BuildDeleteChanges(ConfigurationInstance instance, string deletedBy)
    {
        List<ConfigurationChange> changes = new();
        IReadOnlyList<SettingCell> existingCells = instance.Cells.ToList();
        DateTime changedAtUtc = _clock.UtcNow;

        foreach (SettingCell cell in existingCells)
        {
            ConfigurationChange change = new(
                Guid.NewGuid(),
                instance.ConfigurationInstanceId,
                cell.SettingKey,
                cell.LayerIndex,
                ConfigurationOperation.Delete,
                cell.Value,
                null,
                deletedBy,
                changedAtUtc);

            changes.Add(change);
        }

        return changes;
    }

    private async Task<ConfigurationInstance> GetInstanceOrThrowAsync(
        Guid instanceId,
        IConfigurationRepository configurationRepository,
        CancellationToken cancellationToken)
    {
        var instance = await configurationRepository.GetByIdAsync(instanceId, cancellationToken);

        if (instance is null)
        {
            throw new EntityNotFoundException("ConfigurationInstance", instanceId.ToString());
        }

        return instance;
    }

    private static ConfigurationOperation ResolveOperation(string? beforeValue, string? afterValue)
    {
        if (beforeValue is null && afterValue is not null)
        {
            return ConfigurationOperation.Add;
        }

        if (beforeValue is not null && afterValue is null)
        {
            return ConfigurationOperation.Delete;
        }

        return ConfigurationOperation.Update;
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }

    private static void TrySetValue(ConfigurationInstance instance, string settingKey, int layerIndex, string? value)
    {
        try
        {
            instance.SetValue(settingKey, layerIndex, value);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ValidationException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationException(ex.Message);
        }
    }
}
