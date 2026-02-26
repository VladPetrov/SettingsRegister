namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationInstanceListItemDto(
    Guid ConfigurationId,
    string Name,
    Guid ManifestId,
    DateTime CreatedAtUtc);
