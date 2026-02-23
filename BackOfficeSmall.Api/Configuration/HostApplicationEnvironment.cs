using BackOfficeSmall.Application.Abstractions;

namespace BackOfficeSmall.Api.Configuration;

public sealed class HostApplicationEnvironment : IApplicationEnvironment
{
    private readonly IWebHostEnvironment _webHostEnvironment;

    public HostApplicationEnvironment(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
    }

    public bool IsDevelopment => _webHostEnvironment.IsDevelopment();
}
