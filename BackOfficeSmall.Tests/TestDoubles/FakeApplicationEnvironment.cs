using BackOfficeSmall.Application.Abstractions;

namespace BackOfficeSmall.Tests.TestDoubles;

internal sealed class FakeApplicationEnvironment : IApplicationEnvironment
{
    public FakeApplicationEnvironment(bool isDevelopment)
    {
        IsDevelopment = isDevelopment;
    }

    public bool IsDevelopment { get; }
}
