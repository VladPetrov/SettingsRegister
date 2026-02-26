namespace BackOfficeSmall.Api.Dtos.ConfigurationChanges;

public sealed record ConfigurationChangePageResponseDto(
    IReadOnlyList<ConfigurationChangeResponseDto> Items,
    string? NextCursor);
