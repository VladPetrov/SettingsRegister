namespace SettingsRegister.Api.Dtos.Manifests;

public sealed record ManifestListItemDto(
    Guid ManifestId,
    string Name,
    int Version,
    DateTime CreatedAtUtc);


