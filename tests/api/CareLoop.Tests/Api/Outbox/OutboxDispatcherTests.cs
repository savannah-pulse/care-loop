namespace CareLoop.Tests.Api.Outbox;

/// <summary>
/// Each subscriber is delivered to and retried independently.
/// </summary>
public sealed class OutboxDispatcherTests
{
    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Event_is_delivered_to_every_subscriber()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Failing_subscriber_does_not_block_the_others()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Failing_subscriber_is_retried()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Delivered_subscriber_is_not_called_again_on_retry()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Delivery_is_parked_after_the_attempt_limit()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Next_attempt_time_comes_from_the_clock()
    {
    }
}
