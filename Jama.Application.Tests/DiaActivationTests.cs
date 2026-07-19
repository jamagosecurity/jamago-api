using AutoMapper;
using Jama.Application.Common.Interfaces;
using Jama.Application.Dia;
using Jama.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jama.Application.Tests;

public sealed class DiaActivationTests
{
    [Fact]
    public async Task Activation_lifecycle_sets_date_once_and_rejects_repeated_active_request()
    {
        var entity = new DiaInspection
        {
            Id = Guid.NewGuid(),
            DiaNumber = "DIA-1",
            NormalizedDiaNumber = "DIA-1",
            ClientNumber = "C-1",
            ClientName = "Client",
            ClientLocation = "Doha",
            CreatedById = Guid.NewGuid(),
        };
        var repository = new FakeRepository(entity);
        var unitOfWork = new FakeUnitOfWork();
        var actor = new FakeCurrentUser();
        var time = new MutableTimeProvider(new(2024, 2, 29, 8, 0, 0, TimeSpan.Zero));
        var calculator = new DiaInspectionCalculator(time);
        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<DiaMappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var handler = new ChangeDiaInspectionStateHandler(
            repository, unitOfWork, actor, time, calculator, mapper);

        var first = await handler.Handle(
            new(entity.Id, DiaMutation.Activate), CancellationToken.None);
        Assert.True(first.Succeeded);
        Assert.True(entity.IsActive);
        Assert.Equal(time.GetUtcNow().UtcDateTime, entity.ActivatedDate);
        var originalActivation = entity.ActivatedDate;

        var repeated = await handler.Handle(
            new(entity.Id, DiaMutation.Activate), CancellationToken.None);
        Assert.False(repeated.Succeeded);
        Assert.Equal(1, unitOfWork.SaveCount);

        await handler.Handle(new(entity.Id, DiaMutation.Deactivate), CancellationToken.None);
        Assert.False(entity.IsActive);
        Assert.Equal(originalActivation, entity.ActivatedDate);

        time.UtcNow = time.UtcNow.AddYears(1);
        await handler.Handle(new(entity.Id, DiaMutation.Activate), CancellationToken.None);
        Assert.True(entity.IsActive);
        Assert.Equal(originalActivation, entity.ActivatedDate);
        Assert.Equal(
            [DiaInspectionAction.Activate, DiaInspectionAction.Deactivate, DiaInspectionAction.Activate],
            repository.Audits.Select(x => x.Action));
    }

    private sealed class FakeRepository(DiaInspection entity) : IDiaInspectionRepository
    {
        public List<DiaInspectionHistory> Audits { get; } = [];
        public IQueryable<DiaInspection> Inspections => new[] { entity }.AsQueryable();
        public IQueryable<DiaInspectionHistory> History => Audits.AsQueryable();
        public Task<DiaInspection?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == entity.Id ? entity : null);
        public Task<bool> DiaNumberExistsAsync(string normalizedDiaNumber, Guid? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public void Add(DiaInspection inspection) => throw new NotSupportedException();
        public void AddHistory(DiaInspectionHistory history) => Audits.Add(history);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string DisplayName => "Admin <admin@example.test>";
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
