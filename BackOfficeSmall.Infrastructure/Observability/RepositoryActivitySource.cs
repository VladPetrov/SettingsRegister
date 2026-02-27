using System.Diagnostics;

namespace SettingsRegister.Infrastructure.Observability;

public static class RepositoryActivitySource
{
    public const string SourceName = "SettingsRegister.Infrastructure.Repositories";

    public static readonly ActivitySource Source = new(SourceName);
}
