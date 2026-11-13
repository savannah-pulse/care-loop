using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Records the patient's consent for a caregiver, or its withdrawal, on the case.
/// </summary>
/// <remarks>
/// Consent is coordination data, not pathway progress: it is accepted in any stage, changes no
/// stage, owner or deadline and raises no domain event. A grant needs the caregiver's contact point
/// and is refused while another consent is active; a withdrawal is accepted even on a closed case,
/// because a consent the patient has taken back must never look active. Every grant and withdrawal
/// is audited through the single write path like any other event.
/// </remarks>
public sealed class ConsentHandler : IEventHandler
{
    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.ConsentRecorded;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="ConsentPayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        var payload = HandlerSupport.Payload<ConsentPayload>(envelope);
        var @case = HandlerSupport.RequireCase(existing, EventType);

        switch (payload.Kind)
        {
            case ConsentUpdateKind.Granted:
                if (payload.CaregiverContactPoint is null)
                {
                    return HandlerSupport.Refused(@case, "A granted consent must say how the caregiver is reached.");
                }

                if (@case.IsClosed)
                {
                    return HandlerSupport.Refused(@case, "Consent cannot be recorded on a closed case; nothing more will be sent.");
                }

                if (@case.CaregiverConsent is { IsActive: true })
                {
                    return HandlerSupport.Refused(@case, "A caregiver consent is already active on this case; it must be withdrawn before another is recorded.");
                }

                @case.RecordConsent(payload.CaregiverContactPoint, payload.ConsentReference, envelope.OccurredAt);
                break;

            case ConsentUpdateKind.Withdrawn:
                if (@case.CaregiverConsent is not { IsActive: true })
                {
                    return HandlerSupport.Refused(@case, "No caregiver consent is active on this case, so there is nothing to withdraw.");
                }

                @case.WithdrawConsent(payload.ConsentReference, envelope.OccurredAt);
                break;

            default:
                return HandlerSupport.Refused(@case, $"Consent update kind {payload.Kind} is not understood.");
        }

        return HandlerSupport.Applied(@case, payload.ConsentReference);
    }
}
