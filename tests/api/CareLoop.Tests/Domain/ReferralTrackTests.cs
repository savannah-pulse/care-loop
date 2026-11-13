namespace CareLoop.Tests.Domain;

/// <summary>
/// A sent or accepted referral is never completed care.
/// </summary>
public sealed class ReferralTrackTests
{
    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Accepted_referral_completes_the_track()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Sent_referral_does_not_complete_the_track()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Rejected_referral_records_the_reason()
    {
    }

    // TODO(WS-1): implement this test.
    [Fact(Skip = "WS1: not implemented")]
    public void Invalid_status_move_throws()
    {
    }
}
