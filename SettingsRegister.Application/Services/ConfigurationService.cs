using SettingsRegister.Application.Abstractions;
using SettingsRegister.Application.Contracts;
using SettingsRegister.Application.Exceptions;
using SettingsRegister.Application.Observability;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Models.Manifest;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Domain.Services;
using System.Diagnostics;

namespace SettingsRegister.Application.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private static readonly TimeSpan InstanceLockTimeout = TimeSpan.FromSeconds(30);

    private readonly IConfigurationWriteUnitOfWork _configurationWriteUnitOfWork;
    private readonly IOutboxDispatchService _notifierService;
    private readonly IDomainLock _domainLock;
    private readonly ISystemClock _clock;
    private readonly IServiceMetrics _serviceMetrics;

    public ConfigurationService(
        IConfigurationWriteUnitOfWork configurationWriteUnitOfWork,
        IOutboxDispatchService notifierService,
        IDomainLock domainLock,
        ISystemClock clock,
        IServiceMetrics serviceMetrics)
    {
        _configurationWriteUnitOfWork = configurationWriteUnitOfWork ?? throw new ArgumentNullException(nameof(configurationWriteUnitOfWork));
        _notifierService = notifierService ?? throw new ArgumentNullException(nameof(notifierService));
        _domainLock = domainLock ?? throw new ArgumentNullException(nameof(domainLock));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _serviceMetrics = serviceMetrics ?? throw new ArgumentNullException(nameof(serviceMetrics));
    }

    public async Task<ConfigurationInstance> CreateInstanceAsync(ConfigurationInstanceCreateRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using var activity = ApplicationActivitySource.Source.StartActivity("ConfigurationService.CreateInstance");
        activity?.SetTag("configuration.name", request.Name);
        activity?.SetTag("manifest.id", request.ManifestId.ToString());

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
        int criticalChangeCount = 0;

        try
        {
            await _configurationWriteUnitOfWork.ConfigurationRepository.AddAsync(instance, cancellationToken);

            foreach (ConfigurationChange change in createChanges)
            {
                await _configurationWriteUnitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
                bool isCritical = await AddOutboxMessageWhenCriticalAsync(instance.Manifest, change, cancellationToken);
                if (isCritical)
                {
                    criticalChangeCount++;
                }
            }

            await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        RecordCommittedChangeMetrics(createChanges.Count, criticalChangeCount);
        _notifierService.NotifyChanges();
        activity?.SetTag("configuration.id", instance.ConfigurationId.ToString());
        activity?.SetTag("change.total", createChanges.Count);
        activity?.SetTag("change.critical", criticalChangeCount);

        return instance;
    }

    public async Task<ConfigurationInstance> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationId must be a non-empty GUID.");
        }

        using var activity = ApplicationActivitySource.Source.StartActivity("ConfigurationService.GetById");
        activity?.SetTag("configuration.id", instanceId.ToString());

        // This guaranties consistent reads
        await using var instanceLock = await AcquireInstanceLockOrThrowAsync(instanceId, cancellationToken);

        return await GetInstanceOrThrowAsync(instanceId, _configurationWriteUnitOfWork.ConfigurationRepository, cancellationToken);
    }

    public async Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken)
    {
        using var activity = ApplicationActivitySource.Source.StartActivity("ConfigurationService.List");

        var instances = await _configurationWriteUnitOfWork.ConfigurationRepository.ListAsync(cancellationToken);
        activity?.SetTag("configuration.count", instances.Count);
        return instances;
    }

    public async Task DeleteAsync(Guid instanceId, DeleteConfigurationInstanceRequest request, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationId must be a non-empty GUID.");
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using var activity = ApplicationActivitySource.Source.StartActivity("ConfigurationService.Delete");
        activity?.SetTag("configuration.id", instanceId.ToString());

        request.Validate();

        await using var instanceLock = await AcquireInstanceLockOrThrowAsync(instanceId, cancellationToken);
       
        var instance = await _configurationWriteUnitOfWork.ConfigurationRepository.GetByIdAsync(instanceId, cancellationToken);

        if (instance is null)
        {
            activity?.SetTag("configuration.deleted", false);
            return;
        }

        var changes = BuildCellChanges(instance, request.DeletedBy, ConfigurationOperation.Delete);
        int criticalChangeCount = 0;

        try
        {
            foreach (ConfigurationChange change in changes)
            {
                await _configurationWriteUnitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
                bool isCritical = await AddOutboxMessageWhenCriticalAsync(instance.Manifest, change, cancellationToken);
                if (isCritical)
                {
                    criticalChangeCount++;
                }
            }

            await _configurationWriteUnitOfWork.ConfigurationRepository.DeleteAsync(instanceId, cancellationToken);
            await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        RecordCommittedChangeMetrics(changes.Count, criticalChangeCount);
        _notifierService.NotifyChanges();
        activity?.SetTag("configuration.deleted", true);
        activity?.SetTag("change.total", changes.Count);
        activity?.SetTag("change.critical", criticalChangeCount);
    }

    public async Task<ConfigurationChange> SetValueAsync(Guid instanceId, SetCellValueRequest request, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationId must be a non-empty GUID.");
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using var activity = ApplicationActivitySource.Source.StartActivity("ConfigurationService.SetValue");
        activity?.SetTag("configuration.id", instanceId.ToString());
        activity?.SetTag("setting.key", request.SettingKey);
        activity?.SetTag("setting.layer", request.LayerIndex);

        request.Validate();

        await using var instanceLock = await AcquireInstanceLockOrThrowAsync(instanceId, cancellationToken);

        var instance = await GetInstanceOrThrowAsync(instanceId, _configurationWriteUnitOfWork.ConfigurationRepository, cancellationToken);
        string? beforeValue = instance.GetValue(request.SettingKey, request.LayerIndex);
        string? afterValue = NormalizeValue(request.Value);

        TrySetValue(instance, request.SettingKey, request.LayerIndex, afterValue);
        var operation = ResolveOperation(beforeValue, afterValue);

        ConfigurationChange change = new(
            Guid.NewGuid(),
            instance.ConfigurationId,
            request.SettingKey,
            request.LayerIndex,
            operation,
            beforeValue,
            afterValue,
            request.ChangedBy,
            _clock.UtcNow);
        bool isCriticalChange;

        try
        {
            await _configurationWriteUnitOfWork.ConfigurationRepository.UpdateAsync(instance, cancellationToken);
            await _configurationWriteUnitOfWork.ConfigurationChangeRepository.AddAsync(change, cancellationToken);
            isCriticalChange = await AddOutboxMessageWhenCriticalAsync(instance.Manifest, change, cancellationToken);
            await _configurationWriteUnitOfWork.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        RecordCommittedChangeMetrics(totalChangeCount: 1, criticalChangeCount: isCriticalChange ? 1 : 0);
        _notifierService.NotifyChanges();
        activity?.SetTag("change.id", change.Id.ToString());
        activity?.SetTag("change.operation", change.Operation.ToString());
        activity?.SetTag("change.critical", isCriticalChange);

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
                instance.ConfigurationId,
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

    private async Task<bool> AddOutboxMessageWhenCriticalAsync(ManifestValueObject manifest, ConfigurationChange change, CancellationToken cancellationToken)
    {
        if (!manifest.RequiresCriticalNotification(change.Name))
        {
            return false;
        }

        var outboxMessage = MonitoringNotifierOutboxMessage.CreatePending(change, _clock.UtcNow);
        await _configurationWriteUnitOfWork.MonitoringNotifierOutboxRepository.AddAsync(outboxMessage, cancellationToken);
        return true;
    }

    private void RecordCommittedChangeMetrics(int totalChangeCount, int criticalChangeCount)
    {
        if (totalChangeCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalChangeCount), "Total change count must be greater than or equal to zero.");
        }

        if (criticalChangeCount < 0 || criticalChangeCount > totalChangeCount)
        {
            throw new ArgumentOutOfRangeException(nameof(criticalChangeCount), "Critical change count must be in range 0..total change count.");
        }

        for (int i = 0; i < totalChangeCount; i++)
        {
            bool isCritical = i < criticalChangeCount;
            _serviceMetrics.RecordConfigurationChangeCreated(isCritical);
        }

        for (int i = 0; i < criticalChangeCount; i++)
        {
            _serviceMetrics.RecordOutboxMessageCreated(isCritical: true);
        }
    }
}

