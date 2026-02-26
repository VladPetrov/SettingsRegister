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

    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;
    private readonly IOutboxDispatchService _notifierService;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;

    public ConfigurationService(
        IConfigurationWriteUnitOfWork configurationWriteUnitOfWork,
        IOutboxDispatchService notifierService,
        IDomainLock domainLock,
        ISystemClock clock)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
        _notifierService = notifierService ?? throw new ArgumentNullException(nameof(notifierService));
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

        IReadOnlyList<ConfigurationChange> createChanges = BuildCellChanges(instance, request.CreatedBy, ConfigurationOperation.Add);

        try
        {
            await _configurationWriteUnitOfWork.ConfigurationRepository.AddAsync(instance, cancellationToken);

            foreach (ConfigurationChange change in createChanges)
            {
                await _configurationWriteUnitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
                await AddOutboxMessageWhenCriticalAsync(instance.Manifest, change, cancellationToken);
            }

            await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        _notifierService.NotifyChanges();

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

        return await GetInstanceOrThrowAsync(instanceId, _configurationWriteUnitOfWork.ConfigurationRepository, cancellationToken);
    }

    public async Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken)
    {
        return await _configurationWriteUnitOfWork.ConfigurationRepository.ListAsync(cancellationToken);
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
       
        var instance = await _configurationWriteUnitOfWork.ConfigurationRepository.GetByIdAsync(instanceId, cancellationToken);

        if (instance is null)
        {
            return;
        }

        var changes = BuildCellChanges(instance, request.DeletedBy, ConfigurationOperation.Delete);

        try
        {
            foreach (ConfigurationChange change in changes)
            {
                await _configurationWriteUnitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
                await AddOutboxMessageWhenCriticalAsync(instance.Manifest, change, cancellationToken);
            }

            await _configurationWriteUnitOfWork.ConfigurationRepository.DeleteAsync(instanceId, cancellationToken);
            await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        _notifierService.NotifyChanges();
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

        var instance = await GetInstanceOrThrowAsync(instanceId, _configurationWriteUnitOfWork.ConfigurationRepository, cancellationToken);
        string? beforeValue = instance.GetValue(request.SettingKey, request.LayerIndex);
        string? afterValue = NormalizeValue(request.Value);

        TrySetValue(instance, request.SettingKey, request.LayerIndex, afterValue);
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

        try
        {
            await _configurationWriteUnitOfWork.ConfigurationRepository.UpdateAsync(instance, cancellationToken);
            await _configurationWriteUnitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
            await AddOutboxMessageWhenCriticalAsync(instance.Manifest, change, cancellationToken);
            await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        _notifierService.NotifyChanges();

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
        var manifest = await _configurationWriteUnitOfWork.ManifestRepository.GetByIdAsync(manifestId, cancellationToken);
        
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    private IReadOnlyList<ConfigurationChange> BuildCellChanges(ConfigurationInstance instance, string changedBy, ConfigurationOperation operation)
    {
        if (operation != ConfigurationOperation.Add && operation != ConfigurationOperation.Delete)
        {
            throw new ArgumentOutOfRangeException(nameof(operation), "Only Add or Delete operations are supported.");
        }

        List<ConfigurationChange> changes = new();
        DateTime changedAtUtc = _clock.UtcNow;

        foreach (SettingCell cell in instance.Cells)
        {
            string? beforeValue;
            string? afterValue;

            if (operation == ConfigurationOperation.Add)
            {
                beforeValue = null;
                afterValue = cell.Value;
            }
            else
            {
                beforeValue = cell.Value;
                afterValue = null;
            }

            ConfigurationChange change = new(
                Guid.NewGuid(),
                instance.ConfigurationInstanceId,
                cell.SettingKey,
                cell.LayerIndex,
                operation,
                beforeValue,
                afterValue,
                changedBy,
                changedAtUtc);

            changes.Add(change);
        }

        return changes;
    }

    private async Task<ConfigurationInstance> GetInstanceOrThrowAsync(Guid instanceId, IConfigurationRepository configurationRepository, CancellationToken cancellationToken)
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

    private async Task AddOutboxMessageWhenCriticalAsync(ManifestValueObject manifest, ConfigurationChange change, CancellationToken cancellationToken)
    {
        if (!manifest.RequiresCriticalNotification(change.Name))
        {
            return;
        }

        var outboxMessage = MonitoringNotifierOutboxMessage.CreatePending(change, _clock.UtcNow);
        await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.AddAsync(outboxMessage, cancellationToken);
    }
}
