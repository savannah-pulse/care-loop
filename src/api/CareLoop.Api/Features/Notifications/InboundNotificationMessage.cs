namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// One message a notification provider delivered to CareLoop, exactly as it arrived.
/// </summary>
/// <remarks>
/// The content is provider-specific: an SMS gateway's delivery receipt, an email reply, a
/// simulator callback. CareLoop does not interpret it here; the provider's
/// <see cref="IInboundNotificationHandler"/> does.
/// </remarks>
public sealed record InboundNotificationMessage
{
    /// <summary>The media type the provider declared for <see cref="Body"/>.</summary>
    public required string ContentType { get; init; }

    /// <summary>The raw request body.</summary>
    public required string Body { get; init; }

    /// <summary>The request headers, which is where providers usually put a signature.</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
