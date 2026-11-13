using CareLoop.Domain.Cases;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Opening a case is implemented in the scaffold, so it is tested for real.
/// </summary>
public sealed class CaseConstructionTests
{
    [Fact]
    public void New_case_starts_in_received_with_its_owner()
    {
        var clock = new FakeClock();
        var owner = TestData.FacilityLeadQueue();

        var @case = TestData.NewCase(clock.UtcNow, owner: owner);

        Assert.Equal(CaseStage.Received, @case.Stage);
        Assert.Equal(owner, @case.Owner);
        Assert.False(@case.IsEscalated);
        Assert.Null(@case.Closure);
        Assert.Equal(clock.UtcNow, @case.CreatedAt);
    }

    [Fact]
    public void Case_cannot_be_opened_without_an_owner()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Case(Guid.NewGuid(), "SYN-HIS", "SYN-RESULT-1", "SYN-PT-1", "breast-screening", "SYN-FAC-A", null!, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Case_cannot_be_opened_without_a_source_result()
    {
        Assert.Throws<ArgumentException>(() =>
            new Case(Guid.NewGuid(), "SYN-HIS", " ", "SYN-PT-1", "breast-screening", "SYN-FAC-A", TestData.Clinician(), DateTimeOffset.UnixEpoch));
    }
}
