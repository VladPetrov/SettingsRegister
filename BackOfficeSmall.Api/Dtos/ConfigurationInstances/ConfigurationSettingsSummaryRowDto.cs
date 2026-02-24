namespace BackOfficeSmall.Api.Dtos.ConfigurationInstances;

public sealed record ConfigurationSettingsSummaryRowDto(
    int LayerIndex,
    IReadOnlyList<ConfigurationSettingsSummaryCellDto> Cells);
