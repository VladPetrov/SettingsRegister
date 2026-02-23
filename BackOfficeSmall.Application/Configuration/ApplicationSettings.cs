namespace BackOfficeSmall.Application.Configuration;

public sealed class ApplicationSettings
{
    public const string SectionName = "Application";

    public bool AppScaling { get; init; } = false;
    public int ManifestImportLockTimeoutSeconds { get; init; } = 30;
}
