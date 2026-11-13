namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// The provider callback endpoint hands translated events to the intake pipeline.
/// </summary>
public sealed class ReceiveInboundNotificationTests
{
    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Message_for_an_enabled_provider_is_translated_and_ingested()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Redelivered_message_is_deduplicated()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Unknown_provider_returns_404()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Provider_that_is_not_enabled_returns_404()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Message_that_cannot_be_verified_returns_401()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Message_in_the_wrong_format_returns_400()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Event_refused_by_a_rule_returns_422()
    {
    }
}
