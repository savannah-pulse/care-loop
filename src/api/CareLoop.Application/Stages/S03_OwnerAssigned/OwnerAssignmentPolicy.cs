using CareLoop.Domain.Cases;
using Microsoft.Extensions.Options;

namespace CareLoop.Application.Stages.S03_OwnerAssigned;

/// <summary>Placeholder routing configuration. No queue is invented: leave it unset until one is approved.</summary>
public sealed class OwnerAssignmentOptions
{
    public const string SectionName = "CareLoop:OwnerAssignment";

    /// <summary>Accountable facility lead queue used when a case is opened without a named owner.</summary>
    public string? DefaultQueueId { get; set; }
}

/// <summary>Decides the initial accountable owner. Replace with pathway/facility rules once approved.</summary>
public interface IOwnerAssignmentPolicy
{
    CaseOwner? ResolveInitialOwner(CoordinationCase @case);
}

public sealed class ConfiguredQueueOwnerAssignmentPolicy(IOptionsSnapshot<OwnerAssignmentOptions> options)
    : IOwnerAssignmentPolicy
{
    public CaseOwner? ResolveInitialOwner(CoordinationCase @case) =>
        string.IsNullOrWhiteSpace(options.Value.DefaultQueueId)
            ? null
            : new CaseOwner(OwnerKind.Queue, options.Value.DefaultQueueId);
}
