namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Thrown when no valid draft could be produced.
/// </summary>
/// <remarks>
/// Fail closed: the case screen shows "draft unavailable, human review needed" and the reason is
/// audited. A success-shaped fallback would be indistinguishable from a real draft to the person
/// about to approve it, which is the one outcome this feature must never produce.
/// </remarks>
public sealed class AiDraftUnavailableException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="reason">Why no draft is available, in words safe to show a reviewer and to store in the audit record.</param>
    public AiDraftUnavailableException(string reason)
        : base(reason)
    {
    }

    /// <summary>
    /// Creates the exception from an underlying failure.
    /// </summary>
    /// <param name="reason">Why no draft is available.</param>
    /// <param name="innerException">The transport or validation failure behind it.</param>
    public AiDraftUnavailableException(string reason, Exception innerException)
        : base(reason, innerException)
    {
    }
}
