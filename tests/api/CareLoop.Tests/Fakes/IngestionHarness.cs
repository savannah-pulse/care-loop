using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Events.Handlers;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace CareLoop.Tests.Fakes;

/// <summary>
/// The intake engine wired for tests: the real ingestor and the eight real handlers over the
/// in-memory repository, the fake clock and the placeholder pathway. No FHIR, no database.
/// </summary>
public sealed class IngestionHarness
{
    /// <summary>Creates the harness with the clock at its fixed start.</summary>
    public IngestionHarness()
    {
        Pathways = DomainTestData.Provider();
        Handlers =
        [
            new ScreeningResultHandler(Pathways, Clock),
            new ClinicalReviewHandler(Pathways, Clock),
            new NotificationUpdateHandler(Pathways, Clock),
            new ConsultationUpdateHandler(Pathways, Clock),
            new ReferralUpdateHandler(Pathways, Clock),
            new AppointmentUpdateHandler(Pathways, Clock),
            new OutcomeHandler(Pathways, Clock),
            new ClosureHandler(Pathways, Clock),
        ];
        Ingestor = new EventIngestor(Repository, Handlers, Clock, NullLogger<EventIngestor>.Instance);
    }

    /// <summary>The controllable clock.</summary>
    public FakeClock Clock { get; } = new();

    /// <summary>The in-memory single write path.</summary>
    public InMemoryCaseRepository Repository { get; } = new();

    /// <summary>The pathway provider reading the placeholder definitions.</summary>
    public IPathwayProvider Pathways { get; }

    /// <summary>The eight real handlers.</summary>
    public IReadOnlyList<IEventHandler> Handlers { get; }

    /// <summary>The real ingestor.</summary>
    public EventIngestor Ingestor { get; }

    /// <summary>The placeholder breast-screening pathway.</summary>
    public PathwayDefinition Pathway => Pathways.Get("breast-screening");

    /// <summary>Ingests one envelope.</summary>
    public Task<IngestResult> IngestAsync(EventEnvelope envelope) => Ingestor.IngestAsync(envelope, CancellationToken.None);

    /// <summary>Ingests one envelope and asserts it was applied.</summary>
    public async Task<Case> ApplyAsync(EventEnvelope envelope)
    {
        var result = await IngestAsync(envelope);
        Assert.True(result.Outcome == IngestOutcome.Applied, $"{envelope.Type} was {result.Outcome}: {result.Reason}");
        return (await Repository.GetAsync(result.CaseId!.Value, CancellationToken.None))!;
    }

    /// <summary>The one case for the default result.</summary>
    public Case TheCase(string resultId = Envelopes.ResultId) =>
        Repository.Cases.Single(c => c.SourceResultId == resultId);

    /// <summary>Opens a case with a named clinician owner and moves the clock on an hour.</summary>
    public async Task<Case> OpenCaseAsync(bool namedOwner = true)
    {
        var @case = await ApplyAsync(Envelopes.ScreeningResult(Clock.UtcNow, namedOwner: namedOwner ? Envelopes.Clinician() : null));
        Clock.Advance(TimeSpan.FromHours(1));
        return @case;
    }

    /// <summary>Opens a case and applies the clinical review, leaving it in follow-up with a requested notification.</summary>
    public async Task<Case> ReviewedCaseAsync()
    {
        await OpenCaseAsync();
        var @case = await ApplyAsync(Envelopes.ClinicalReview(Clock.UtcNow));
        Clock.Advance(TimeSpan.FromHours(1));
        return @case;
    }

    /// <summary>Runs the happy path up to and including the recorded outcome, leaving the case in closure review.</summary>
    public async Task<Case> CaseInClosureReviewAsync()
    {
        await ReviewedCaseAsync();
        await ApplyAsync(Envelopes.Notification(Clock.UtcNow, NotificationUpdateKind.Delivered));
        await ApplyAsync(Envelopes.Notification(Clock.UtcNow, NotificationUpdateKind.Acknowledged));
        Clock.Advance(TimeSpan.FromDays(1));
        await ApplyAsync(Envelopes.Referral(Clock.UtcNow, ReferralStatus.Accepted));
        Clock.Advance(TimeSpan.FromDays(1));
        await ApplyAsync(Envelopes.Appointment(Clock.UtcNow, AppointmentStatus.Attended));
        Clock.Advance(TimeSpan.FromHours(1));
        var @case = await ApplyAsync(Envelopes.Outcome(Clock.UtcNow));
        Clock.Advance(TimeSpan.FromHours(1));
        return @case;
    }
}
