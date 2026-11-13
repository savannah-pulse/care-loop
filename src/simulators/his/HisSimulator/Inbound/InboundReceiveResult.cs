using HisSimulator.State;

namespace HisSimulator.Inbound;

/// <summary>
/// What the simulator did with one FHIR resource received from CareLoop.
/// </summary>
/// <param name="ResourceType">FHIR resource type that was received.</param>
/// <param name="ResourceId">Logical id of the stored resource (taken from the resource, or assigned when absent).</param>
/// <param name="LogEntryId">Identifier of the <see cref="FhirMessageLogEntry"/> recording the message.</param>
/// <param name="InboxItem">The staff inbox item created from the resource, or <see langword="null"/> if it produced none.</param>
/// <param name="ResponseBody">The FHIR JSON to echo back to CareLoop (the stored resource).</param>
public sealed record InboundReceiveResult(
    string ResourceType,
    string ResourceId,
    Guid LogEntryId,
    StaffInboxItem? InboxItem,
    string ResponseBody);
