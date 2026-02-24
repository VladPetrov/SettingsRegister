namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationInstanceListItemDto(
    Guid ConfigurationInstanceId,
    string Name,
    Guid ManifestId,
    DateTime CreatedAtUtc);
