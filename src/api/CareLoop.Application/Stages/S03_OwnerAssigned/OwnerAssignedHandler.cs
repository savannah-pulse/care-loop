using System.Text.Json;
using CareLoop.Application.Intake;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Stages;

namespace CareLoop.Application.Stages.S03_OwnerAssigned;

/// <summary>
/// Stage 3 (manual path): a coordinator or facility lead names the accountable owner. Only valid while the
/// case awaits an owner; transfer after assignment is an offer and acceptance flow that is not built yet.
/// </summary>
public sealed class OwnerAssignedHandler : ICaseEventHandler
{
    public string EventType => CaseEventTypes.OwnerAssigned;

    public ActorPermission Permission { get; } = ActorPermission.Humans(ActorRoles.Coordinator, ActorRoles.FacilityLead);

    public Task<HandlerResult> HandleAsync(CaseEventContext context, CancellationToken cancellationToken)
    {
        var @case = context.Case;
        if (@case is null)
        {
            return Task.FromResult(HandlerResult.NotFound("case_not_found", "caseId is required and must reference an existing case."));
        }

        var owner = ParseOwner(context.Envelope.Payload, out var error);
        if (owner is null)
        {
            return Task.FromResult(HandlerResult.Invalid("payload", error!));
        }

        if (@case.Stage >= CaseStage.OwnerAssigned)
        {
            return Task.FromResult(HandlerResult.Rejected(
                "owner_transfer_not_supported",
                "The case already has an owner. Owner transfer (offer and acceptance) is not implemented yet."));
        }

        context.AppendPrimaryEvent(@case);
        var transition = context.Advance(@case, CaseStage.OwnerAssigned);
        if (!transition.IsSuccess)
        {
            return Task.FromResult(HandlerResult.FromTransitionError(transition.Error!));
        }

        @case.AssignOwner(owner, context.Now);
        @case.ResolveExceptions(ExceptionType.OwnerUnassigned, context.PrimaryEventId, context.Now);
        return Task.FromResult(HandlerResult.Accepted(@case));
    }

    private static CaseOwner? ParseOwner(JsonElement? payload, out string? error)
    {
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } p)
        {
            error = "payload must contain ownerKind ('person' or 'queue') and ownerId.";
            return null;
        }

        var kindText = p.TryGetProperty("ownerKind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
        var id = p.TryGetProperty("ownerId", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;

        if (!Enum.TryParse<OwnerKind>(kindText, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            error = "ownerKind must be 'person' or 'queue'.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(id) || id.Length > 200)
        {
            error = "ownerId is required (at most 200 characters).";
            return null;
        }

        return new CaseOwner(kind, id);
    }
}
