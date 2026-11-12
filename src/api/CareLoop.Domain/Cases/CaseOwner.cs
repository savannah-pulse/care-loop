namespace CareLoop.Domain.Cases;

public enum OwnerKind
{
    Person,
    Queue,
}

/// <summary>An accountable person or named queue.</summary>
public sealed record CaseOwner(OwnerKind Kind, string Id);
