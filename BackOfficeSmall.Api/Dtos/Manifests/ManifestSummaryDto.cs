namespace BackOfficeSmall.Api.Dtos.Manifests;

public sealed record ManifestSummaryDto(
    Guid ManifestId,
    string Name,
    int Version,
    DateTime CreatedAtUtc);
