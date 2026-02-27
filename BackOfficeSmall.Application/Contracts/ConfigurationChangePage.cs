using SettingsRegister.Domain.Models.Configuration;

namespace SettingsRegister.Application.Contracts;

public sealed record ConfigurationChangePage(
    IReadOnlyList<ConfigurationChange> Items,
    string? NextCursor);

