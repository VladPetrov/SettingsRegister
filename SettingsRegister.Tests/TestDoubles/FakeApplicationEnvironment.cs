using SettingsRegister.Application.Abstractions;

namespace SettingsRegister.Tests.TestDoubles;

internal sealed class FakeApplicationEnvironment : IApplicationEnvironment
{
    public FakeApplicationEnvironment(bool isDevelopment)
    {
        IsDevelopment = isDevelopment;
    }

    public bool IsDevelopment { get; }
}

