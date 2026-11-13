namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// Sends on every enabled, reachable channel.
/// </summary>
public sealed class NotificationSenderTests
{
    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Sends_on_all_enabled_reachable_channels()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Skips_a_provider_that_cannot_reach_the_recipient()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Skips_a_provider_that_is_not_enabled()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Sms_gets_the_sms_safe_template()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Delivered_if_any_channel_delivers()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Failed_only_if_all_channels_fail()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Each_send_is_recorded_as_its_own_attempt()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Throwing_provider_is_recorded_as_failed_and_does_not_stop_the_others()
    {
    }

    // TODO(WS-4): implement this test.
    [Fact(Skip = "WS4: not implemented")]
    public void Per_event_override_replaces_the_enabled_providers()
    {
    }
}
