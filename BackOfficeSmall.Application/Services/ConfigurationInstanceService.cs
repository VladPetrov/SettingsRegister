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
    private readonly IManifestRepository _manifestRepository;
    private readonly IConfigurationInstanceRepository _configInstanceRepository;
    private readonly IConfigurationChangeRepository _configChangeRepository;
    private readonly IMonitoringNotifier _monitoringNotifier;
    private readonly ISystemClock _clock;

    public ConfigurationInstanceService(
        IManifestRepository manifestRepository,
        IConfigurationInstanceRepository configInstanceRepository,
        IConfigurationChangeRepository configChangeRepository,
        IMonitoringNotifier monitoringNotifier,
        ISystemClock clock)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _configInstanceRepository = configInstanceRepository ?? throw new ArgumentNullException(nameof(configInstanceRepository));
        _configChangeRepository = configChangeRepository ?? throw new ArgumentNullException(nameof(configChangeRepository));
        _monitoringNotifier = monitoringNotifier ?? throw new ArgumentNullException(nameof(monitoringNotifier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ConfigurationInstance> CreateInstanceAsync(ConfigurationInstanceCreateRequest request, CancellationToken cancellationToken)
    {
        ValidateCreateRequest(request);

        ManifestValueObject manifest = await GetManifestOrThrowAsync(request.ManifestId, cancellationToken);
        ConfigurationInstance instance = new(
            Guid.NewGuid(),
            request.Name,
            manifest,
            _clock.UtcNow,
            request.CreatedBy);

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

        ConfigurationInstance? instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            throw new EntityNotFoundException("ConfigurationInstance", instanceId.ToString());
        }

        return instance;
    }

    public Task<IReadOnlyList<ConfigurationInstance>> ListAsync(CancellationToken cancellationToken)
    {
        return _configInstanceRepository.ListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationInstanceId must be a non-empty GUID.");
        }

        ConfigurationInstance? instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            throw new EntityNotFoundException("ConfigurationInstance", instanceId.ToString());
        }

        await _configInstanceRepository.DeleteAsync(instanceId, cancellationToken);
    }

    public async Task<ConfigurationChange> SetCellValueAsync(
        Guid instanceId,
        SetCellValueRequest request,
        CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigurationInstanceId must be a non-empty GUID.");
        }

        ValidateSetCellRequest(request);

        ConfigurationInstance instance = await GetInstanceOrThrowAsync(instanceId, cancellationToken);

        string? beforeValue = instance.GetValue(request.SettingKey, request.LayerIndex);
        string? afterValue = NormalizeValue(request.Value);

        if (beforeValue is null && afterValue is null)
        {
            throw new ValidationException("Cannot delete a value that does not exist.");
        }

        TrySetValue(instance, request.SettingKey, request.LayerIndex, afterValue);

        try
        {
            await _configInstanceRepository.UpdateAsync(instance, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

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

        await _configChangeRepository.AddAsync(change, cancellationToken);

        if (instance.Manifest.RequiresCriticalNotification(request.SettingKey))
        {
            await _monitoringNotifier.NotifyCriticalChangeAsync(change, cancellationToken);
        }

        return change;
    }

    private static void ValidateCreateRequest(ConfigurationInstanceCreateRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Configuration instance name is required.");
        }

        if (request.ManifestId == Guid.Empty)
        {
            throw new ValidationException("ManifestId must be a non-empty GUID.");
        }

        if (string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            throw new ValidationException("CreatedBy is required.");
        }
    }

    private static void ValidateSetCellRequest(SetCellValueRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SettingKey))
        {
            throw new ValidationException("SettingKey is required.");
        }

        if (request.LayerIndex < 0)
        {
            throw new ValidationException("LayerIndex must be greater than or equal to zero.");
        }

        if (string.IsNullOrWhiteSpace(request.ChangedBy))
        {
            throw new ValidationException("ChangedBy is required.");
        }
    }

    private async Task<ManifestValueObject> GetManifestOrThrowAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        ManifestValueObject? manifest = await _manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    private async Task<ConfigurationInstance> GetInstanceOrThrowAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        ConfigurationInstance? instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);
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
