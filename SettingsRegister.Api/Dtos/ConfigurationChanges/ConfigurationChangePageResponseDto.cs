namespace SettingsRegister.Api.Dtos.ConfigurationChanges;

public sealed record ConfigurationChangePageResponseDto(
    IReadOnlyList<ConfigurationChangeResponseDto> Items,
    string? NextCursor);

