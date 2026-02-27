using System.Diagnostics;

namespace SettingsRegister.Application.Observability;

public static class ApplicationActivitySource
{
    public const string SourceName = "SettingsRegister.Application";

    public static readonly ActivitySource Source = new(SourceName);
}
