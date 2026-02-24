namespace BackOfficeSmall.Domain.Models.Configuration;

public sealed record ConfigurationSettingSummaryRow(
    int LayerIndex,
    IReadOnlyList<ConfigurationSettingSummaryCell> Cells);
