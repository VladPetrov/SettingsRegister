using BackOfficeSmall.Domain.Models.Configuration;

namespace BackOfficeSmall.Application.Contracts;

public sealed record ConfigurationChangePage(
    IReadOnlyList<ConfigurationChange> Items,
    string? NextCursor);
