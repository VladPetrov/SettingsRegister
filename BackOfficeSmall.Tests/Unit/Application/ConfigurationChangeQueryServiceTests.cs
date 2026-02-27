using SettingsRegister.Application.Configuration;
using SettingsRegister.Application.Exceptions;
using SettingsRegister.Application.Services;
using SettingsRegister.Domain.Models.Configuration;
using SettingsRegister.Domain.Repositories;
using SettingsRegister.Infrastructure.Repositories;
using SettingsRegister.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace SettingsRegister.Tests.Unit.Application;

public sealed class ConfigurationChangeQueryServiceTests
{
    [Fact]
    public async Task GetChangeByIdAsync_WhenIdIsEmpty_ThrowsValidationException()
    {
        using TestContext context = CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Service.GetChangeByIdAsync(Guid.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task GetChangeByIdAsync_WhenChangeDoesNotExist_ThrowsEntityNotFoundException()
    {
        using TestContext context = CreateContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => context.Service.GetChangeByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetChangeByIdAsync_WhenChangeExists_ReturnsExpectedChange()
    {
        using TestContext context = CreateContext();
        ConfigurationChange change = CreateAddChange(Guid.NewGuid(), DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 0, 0), DateTimeKind.Utc));
        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(change, CancellationToken.None);

        ConfigurationChange result = await context.Service.GetChangeByIdAsync(change.Id, CancellationToken.None);

        Assert.Equal(change.Id, result.Id);
        Assert.Equal(change.Name, result.Name);
    }

    [Fact]
    public async Task ListChangesAsync_WhenCalledWithDefaults_ReturnsExistingResults()
    {
        using TestContext context = CreateContext();
        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(
            CreateAddChange(Guid.NewGuid(), DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 0, 0), DateTimeKind.Utc)),
            CancellationToken.None);
        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(
            CreateDeleteChange(Guid.NewGuid(), DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 1, 0), DateTimeKind.Utc)),
            CancellationToken.None);

        var result = await context.Service.ListChangesAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ListChangesAsync_WhenCursorIsInvalid_ThrowsValidationException()
    {
        using TestContext context = CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Service.ListChangesAsync(
            cursor: "not-valid-base64",
            pageSize: 10,
            cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ListChangesAsync_WhenFromUtcIsGreaterThanToUtc_ThrowsValidationException()
    {
        using TestContext context = CreateContext();
        DateTime fromUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 15, 0, 0), DateTimeKind.Utc);
        DateTime toUtc = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 0, 0), DateTimeKind.Utc);

        await Assert.ThrowsAsync<ValidationException>(() => context.Service.ListChangesAsync(
            fromUtc: fromUtc,
            toUtc: toUtc,
            cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ListChangesAsync_WhenPageSizeIsNotPositive_ThrowsValidationException()
    {
        using TestContext context = CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Service.ListChangesAsync(
            pageSize: 0,
            cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ListChangesAsync_WhenPageSizeIsAboveMaximum_ThrowsValidationException()
    {
        using TestContext context = CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Service.ListChangesAsync(
            pageSize: 201,
            cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ListChangesAsync_WhenOperationFilterIsProvided_ReturnsOnlyMatchingItems()
    {
        using TestContext context = CreateContext();
        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(
            CreateAddChange(Guid.NewGuid(), DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 0, 0), DateTimeKind.Utc)),
            CancellationToken.None);
        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(
            CreateDeleteChange(Guid.NewGuid(), DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 1, 0), DateTimeKind.Utc)),
            CancellationToken.None);

        var result = await context.Service.ListChangesAsync(
            operation: ConfigurationOperation.Delete,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(ConfigurationOperation.Delete, result.Items[0].Operation);
    }

    [Fact]
    public async Task ListChangesAsync_WithCursorPagination_IsDeterministicAndHasNoDuplicates()
    {
        using TestContext context = CreateContext();
        DateTime firstTimestamp = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 0, 0), DateTimeKind.Utc);
        DateTime secondTimestamp = DateTime.SpecifyKind(new DateTime(2026, 2, 26, 14, 1, 0), DateTimeKind.Utc);
        Guid id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid id3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(CreateAddChange(id2, firstTimestamp), CancellationToken.None);
        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(CreateAddChange(id1, firstTimestamp), CancellationToken.None);
        await context.UnitOfWork.ConfigurationChangeRepository.AddAsync(CreateAddChange(id3, secondTimestamp), CancellationToken.None);

        var firstPage = await context.Service.ListChangesAsync(
            pageSize: 2,
            cancellationToken: CancellationToken.None);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);
        Assert.Equal(id1, firstPage.Items[0].Id);
        Assert.Equal(id2, firstPage.Items[1].Id);

        var secondPage = await context.Service.ListChangesAsync(
            cursor: firstPage.NextCursor,
            pageSize: 2,
            cancellationToken: CancellationToken.None);

        Assert.Single(secondPage.Items);
        Assert.Equal(id3, secondPage.Items[0].Id);
        Assert.Null(secondPage.NextCursor);
    }

    private static ConfigurationChange CreateAddChange(Guid id, DateTime changedAtUtc)
    {
        return new ConfigurationChange(
            id,
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigurationOperation.Add,
            null,
            "on",
            "tester",
            changedAtUtc);
    }

    private static ConfigurationChange CreateDeleteChange(Guid id, DateTime changedAtUtc)
    {
        return new ConfigurationChange(
            id,
            Guid.NewGuid(),
            "FeatureFlag",
            0,
            ConfigurationOperation.Delete,
            "on",
            null,
            "tester",
            changedAtUtc);
    }

    private static TestContext CreateContext()
    {
        ApplicationSettings settings = new();
        MemoryCache memoryCache = new(new MemoryCacheOptions());
        ICachedManifestRepository cachedManifestRepository = new CachedManifestRepository(
            new InMemoryManifestRepository(),
            memoryCache,
            settings,
            new FakeRepositoryCacheMetrics());
        ICacheConfigurationRepository cachedConfigurationRepository = new CachedConfigurationRepository(
            new InMemoryConfigurationInstanceRepository(),
            memoryCache,
            settings,
            new FakeRepositoryCacheMetrics());
        InMemoryConfigurationChangeRepository configurationChangeRepository = new();
        InMemoryMonitoringNotifierOutboxRepository outboxRepository = new();

        InMemoryConfigurationWriteUnitOfWork unitOfWork = new(
            cachedManifestRepository,
            cachedConfigurationRepository,
            configurationChangeRepository,
            outboxRepository);

        ConfigurationChangeQueryService service = new(unitOfWork);
        return new TestContext(service, unitOfWork, memoryCache);
    }

    private sealed record TestContext(
        ConfigurationChangeQueryService Service,
        InMemoryConfigurationWriteUnitOfWork UnitOfWork,
        MemoryCache MemoryCache) : IDisposable
    {
        public void Dispose()
        {
            UnitOfWork.Dispose();
            MemoryCache.Dispose();
        }
    }
}

