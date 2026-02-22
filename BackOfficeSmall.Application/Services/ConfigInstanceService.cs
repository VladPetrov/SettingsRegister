using BackOfficeSmall.Application.Abstractions;
using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Domain.Models;
using BackOfficeSmall.Domain.Repositories;
using BackOfficeSmall.Domain.Services;

namespace BackOfficeSmall.Application.Services;

public sealed class ConfigInstanceService : IConfigInstanceService
{
    private readonly IManifestRepository _manifestRepository;
    private readonly IConfigInstanceRepository _configInstanceRepository;
    private readonly IConfigChangeRepository _configChangeRepository;
    private readonly IMonitoringNotifier _monitoringNotifier;
    private readonly ISystemClock _clock;

    public ConfigInstanceService(
        IManifestRepository manifestRepository,
        IConfigInstanceRepository configInstanceRepository,
        IConfigChangeRepository configChangeRepository,
        IMonitoringNotifier monitoringNotifier,
        ISystemClock clock)
    {
        _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
        _configInstanceRepository = configInstanceRepository ?? throw new ArgumentNullException(nameof(configInstanceRepository));
        _configChangeRepository = configChangeRepository ?? throw new ArgumentNullException(nameof(configChangeRepository));
        _monitoringNotifier = monitoringNotifier ?? throw new ArgumentNullException(nameof(monitoringNotifier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ConfigInstance> CreateInstanceAsync(ConfigInstanceCreateRequest request, CancellationToken cancellationToken)
    {
        ValidateCreateRequest(request);

        Manifest manifest = await GetManifestOrThrowAsync(request.ManifestId, cancellationToken);
        ConfigInstance instance = new(
            Guid.NewGuid(),
            request.Name,
            request.ManifestId,
            _clock.UtcNow,
            request.CreatedBy);

        if (request.Cells is not null)
        {
            foreach (SettingCellInput cell in request.Cells)
            {
                ValidateMutationAgainstManifest(manifest, cell.SettingKey, cell.LayerIndex);
                instance.SetValue(cell.SettingKey, cell.LayerIndex, NormalizeValue(cell.Value));
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

    public async Task<ConfigInstance> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigInstanceId must be a non-empty GUID.");
        }

        ConfigInstance? instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            throw new EntityNotFoundException("ConfigInstance", instanceId.ToString());
        }

        return instance;
    }

    public Task<IReadOnlyList<ConfigInstance>> ListAsync(CancellationToken cancellationToken)
    {
        return _configInstanceRepository.ListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigInstanceId must be a non-empty GUID.");
        }

        ConfigInstance? instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            throw new EntityNotFoundException("ConfigInstance", instanceId.ToString());
        }

        await _configInstanceRepository.DeleteAsync(instanceId, cancellationToken);
    }

    public async Task<ConfigChange> SetCellValueAsync(
        Guid instanceId,
        SetCellValueRequest request,
        CancellationToken cancellationToken)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ValidationException("ConfigInstanceId must be a non-empty GUID.");
        }

        ValidateSetCellRequest(request);

        ConfigInstance instance = await GetInstanceOrThrowAsync(instanceId, cancellationToken);
        Manifest manifest = await GetManifestOrThrowAsync(instance.ManifestId, cancellationToken);

        ValidateMutationAgainstManifest(manifest, request.SettingKey, request.LayerIndex);

        string? beforeValue = instance.GetValue(request.SettingKey, request.LayerIndex);
        string? afterValue = NormalizeValue(request.Value);

        if (beforeValue is null && afterValue is null)
        {
            throw new ValidationException("Cannot delete a value that does not exist.");
        }

        instance.SetValue(request.SettingKey, request.LayerIndex, afterValue);

        try
        {
            await _configInstanceRepository.UpdateAsync(instance, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        ConfigOperation operation = ResolveOperation(beforeValue, afterValue);
        ConfigChange change = new(
            Guid.NewGuid(),
            instance.ConfigInstanceId,
            request.SettingKey,
            request.LayerIndex,
            operation,
            beforeValue,
            afterValue,
            request.ChangedBy,
            _clock.UtcNow);

        await _configChangeRepository.AddAsync(change, cancellationToken);

        if (manifest.RequiresCriticalNotification(request.SettingKey))
        {
            await _monitoringNotifier.NotifyCriticalChangeAsync(change, cancellationToken);
        }

        return change;
    }

    private static void ValidateCreateRequest(ConfigInstanceCreateRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Config instance name is required.");
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

    private void ValidateMutationAgainstManifest(Manifest manifest, string settingKey, int layerIndex)
    {
        if (!manifest.HasSetting(settingKey))
        {
            throw new ValidationException($"Setting key '{settingKey}' does not exist in manifest '{manifest.ManifestId}'.");
        }

        if (layerIndex < 0 || layerIndex >= manifest.LayerCount)
        {
            throw new ValidationException(
                $"LayerIndex '{layerIndex}' is outside allowed range 0..{manifest.LayerCount - 1}.");
        }

        if (!manifest.CanOverride(settingKey, layerIndex))
        {
            throw new ValidationException(
                $"Override is not allowed for setting '{settingKey}' at layer '{layerIndex}'.");
        }
    }

    private async Task<Manifest> GetManifestOrThrowAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        Manifest? manifest = await _manifestRepository.GetByIdAsync(manifestId, cancellationToken);
        if (manifest is null)
        {
            throw new EntityNotFoundException("Manifest", manifestId.ToString());
        }

        return manifest;
    }

    private async Task<ConfigInstance> GetInstanceOrThrowAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        ConfigInstance? instance = await _configInstanceRepository.GetByIdAsync(instanceId, cancellationToken);
        if (instance is null)
        {
            throw new EntityNotFoundException("ConfigInstance", instanceId.ToString());
        }

        return instance;
    }

    private static ConfigOperation ResolveOperation(string? beforeValue, string? afterValue)
    {
        if (beforeValue is null && afterValue is not null)
        {
            return ConfigOperation.Add;
        }

        if (beforeValue is not null && afterValue is null)
        {
            return ConfigOperation.Delete;
        }

        return ConfigOperation.Update;
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }
}
