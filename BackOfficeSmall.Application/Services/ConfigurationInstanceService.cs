using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Domain.Models.Configuration;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Application.Services;

public sealed class ConfigurationInstanceService : IConfigurationService
{
    private static readonly TimeSpan InstanceLockTimeout = TimeSpan.FromSeconds(30);

    private readonly IManifestRepository _manifestRepository;
    private readonly IConfigurationInstanceRepository _configInstanceRepository;
    private readonly IConfigurationChangeRepository _configChangeRepository;
    private readonly IMonitoringNotifier _monitoringNotifier;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;

    public ConfigurationInstanceService(
        IManifestRepository manifestRepository,
        IConfigurationInstanceRepository configInstanceRepository,
        IConfigurationChangeRepository configChangeRepository,
        IMonitoringNotifier monitoringNotifier,
        IDomainLock domainLock,
        ISystemClock clock)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _configInstanceRepository = configInstanceRepository ?? throw new ArgumentNullException(nameof(configInstanceRepository));
        _configChangeRepository = configChangeRepository ?? throw new ArgumentNullException(nameof(configChangeRepository));
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
        
        return await GetInstanceOrThrowAsync(instanceId, cancellationToken);
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
       
        var instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);
        
        if (instance is null)
        {
            return;
        }

        IReadOnlyList<SettingCell> existingCells = instance.Cells.ToList();

        // TODO: this is wrong
        foreach (var cell in existingCells)
        {
            ConfigurationChange change = new(
                Guid.NewGuid(),
                instance.ConfigurationInstanceId,
                cell.SettingKey,
                cell.LayerIndex,
                ConfigurationOperation.Delete,
                cell.Value,
                null,
                request.DeletedBy,
                _clock.UtcNow);

            await _configChangeRepository.AddAsync(change, cancellationToken);

            if (instance.Manifest.RequiresCriticalNotification(cell.SettingKey))
            {
                await _monitoringNotifier.NotifyCriticalChangeAsync(change, cancellationToken);
            }
        }

        await _configInstanceRepository.DeleteAsync(instanceId, cancellationToken);
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

        var instance = await GetInstanceOrThrowAsync(instanceId, cancellationToken);
        var beforeValue = instance.GetValue(request.SettingKey, request.LayerIndex);
        var afterValue = NormalizeValue(request.Value);

        TrySetValue(instance, request.SettingKey, request.LayerIndex, afterValue);

        try
        {
            await _configInstanceRepository.UpdateAsync(instance, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        var operation = ResolveOperation(beforeValue, afterValue);
        
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

        await _configChangeRepository.AddAsync(change, cancellationToken);

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

    private async Task<ConfigurationInstance> GetInstanceOrThrowAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);

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
