namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Base type of the eight event payloads carried by an <see cref="EventEnvelope"/>.
/// </summary>
/// <remarks>
/// Each derived record corresponds to exactly one <see cref="CareLoop.Domain.Events.CaseEventType"/>
/// source event and one event handler.
/// </remarks>
public abstract record EventPayload;
