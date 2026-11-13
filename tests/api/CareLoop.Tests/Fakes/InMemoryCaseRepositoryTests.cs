using CareLoop.Domain.Events.DomainEvents;

namespace CareLoop.Tests.Fakes;

/// <summary>
/// The in-memory repository is working infrastructure, so it is tested for real.
/// </summary>
public sealed class InMemoryCaseRepositoryTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemoryCaseRepository _repository = new();

    [Fact]
    public async Task Saved_case_can_be_loaded_by_id_and_by_source()
    {
        var @case = TestData.NewCase(_clock.UtcNow);
        await _repository.SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);

        Assert.Same(@case, await _repository.GetAsync(@case.Id, CancellationToken.None));
        Assert.Same(@case, await _repository.FindBySourceAsync(TestData.SourceSystem, "SYN-RESULT-0001", CancellationToken.None));
        Assert.Null(await _repository.FindBySourceAsync(TestData.SourceSystem, "SYN-RESULT-UNKNOWN", CancellationToken.None));
    }

    [Fact]
    public async Task Stored_event_is_reported_as_existing()
    {
        var @case = TestData.NewCase(_clock.UtcNow);
        await _repository.SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);

        Assert.True(await _repository.EventExistsAsync(TestData.SourceSystem, "SYN-EVT-1", CancellationToken.None));
        Assert.False(await _repository.EventExistsAsync(TestData.SourceSystem, "SYN-EVT-2", CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_source_event_is_refused()
    {
        var @case = TestData.NewCase(_clock.UtcNow);
        await _repository.SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None));
        Assert.Single(_repository.Events);
    }

    [Fact]
    public async Task Domain_events_are_captured_and_cleared_on_save()
    {
        var @case = TestData.NewCase(_clock.UtcNow);
        @case.Raise(new DeadlineApproaching
        {
            EventId = Guid.NewGuid(),
            CaseId = @case.Id,
            OccurredAt = _clock.UtcNow,
            CurrentOwner = @case.Owner,
            NextAction = "placeholder-action",
            DueAt = _clock.UtcNow.AddHours(1),
        });

        await _repository.SaveAsync(@case, TestData.AppliedEvent(@case, "SYN-EVT-1", _clock.UtcNow), CancellationToken.None);

        Assert.Single(_repository.PublishedDomainEvents);
        Assert.Empty(@case.DomainEvents);
    }
}
