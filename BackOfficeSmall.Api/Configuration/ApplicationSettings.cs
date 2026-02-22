namespace BackOfficeSmall.Api.Configuration;

public sealed class ApplicationSettings
{
    public const string SectionName = "Application";

    public bool AppScaling { get; init; } = false;
}
