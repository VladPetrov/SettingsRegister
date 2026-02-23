using BackOfficeSmall.Application.Contracts;
using BackOfficeSmall.Application.Configuration;
using BackOfficeSmall.Application.Exceptions;
using BackOfficeSmall.Application.Services;
using BackOfficeSmall.Domain.Models.Manifest;
using BackOfficeSmall.Infrastructure.Repositories;
using BackOfficeSmall.Tests.TestDoubles;

namespace BackOfficeSmall.Tests.Unit.Application;

public sealed class ManifestServiceTests
{
    private static readonly int ImportLockTimeoutSeconds = 15;

    [Fact]
    public async Task ImportManifestAsync_IncrementsVersion_AndKeepsOlderVersionImmutable()
    {
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 0, 0), DateTimeKind.Utc));
        FakeDomainLock domainLock = new();
        InMemoryManifestRepository manifestRepository = new();
        ApplicationSettings applicationSettings = new()
        {
            ManifestImportLockTimeoutSeconds = ImportLockTimeoutSeconds
        };
        ManifestService service = new(manifestRepository, domainLock, clock, applicationSettings);

        ManifestImportRequest request = CreateManifestRequest("Main", "tester");

        ManifestValueObject first = await service.ImportManifestAsync(request, CancellationToken.None);
        ManifestValueObject firstSnapshot = await service.GetByIdAsync(first.ManifestId, CancellationToken.None);

        clock.Set(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 5, 0), DateTimeKind.Utc));
        ManifestValueObject second = await service.ImportManifestAsync(request, CancellationToken.None);

        Assert.Equal(1, first.Version);
        Assert.Equal(1, firstSnapshot.Version);
        Assert.Equal(2, second.Version);
        Assert.NotEqual(first.ManifestId, second.ManifestId);
        Assert.Equal("Main", domainLock.LastKey);
        Assert.Equal(TimeSpan.FromSeconds(ImportLockTimeoutSeconds), domainLock.LastTimeout);
        Assert.Equal(2, domainLock.DisposeCalls);
    }

    [Fact]
    public async Task ImportManifestAsync_WhenDomainLockIsNotAcquired_ThrowsConflictException()
    {
        FakeSystemClock clock = new(DateTime.SpecifyKind(new DateTime(2026, 2, 22, 10, 0, 0), DateTimeKind.Utc));
        FakeDomainLock domainLock = new(false);
        InMemoryManifestRepository manifestRepository = new();
        ApplicationSettings applicationSettings = new()
        {
            ManifestImportLockTimeoutSeconds = ImportLockTimeoutSeconds
        };
        ManifestService service = new(manifestRepository, domainLock, clock, applicationSettings);

        ManifestImportRequest request = CreateManifestRequest("Main", "tester");

        await Assert.ThrowsAsync<ConflictException>(() => service.ImportManifestAsync(request, CancellationToken.None));
        Assert.Equal("Main", domainLock.LastKey);
        Assert.Equal(0, domainLock.DisposeCalls);
    }

    private static ManifestImportRequest CreateManifestRequest(string name, string createdBy)
    {
        return new ManifestImportRequest(
            name,
            2,
            createdBy,
            new[]
            {
                new ManifestSettingDefinitionInput("FeatureFlag", RequiresCriticalNotification: true)
            },
            new[]
            {
                new ManifestOverridePermissionInput("FeatureFlag", 0, CanOverride: true),
                new ManifestOverridePermissionInput("FeatureFlag", 1, CanOverride: true)
            });
    }
}
