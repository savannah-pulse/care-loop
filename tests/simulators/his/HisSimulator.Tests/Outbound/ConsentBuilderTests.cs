using HisSimulator.Outbound;
using Hl7.Fhir.Model;

namespace HisSimulator.Tests.Outbound;

public sealed class ConsentBuilderTests
{
    [Fact] public void Grant_carries_contained_caregiver_and_report_correlation()
    {
        var request = SimulatorTestData.With(SimulatorTestData.Request(status: "active"), "caregiverContact", "SYN-CG-1");
        var consent = new ConsentBuilder().Build(SimulatorTestData.Case, request);
        var caregiver = Assert.Single(consent.Contained.OfType<RelatedPerson>());
        Assert.Equal(Consent.ConsentState.Active, consent.Status);
        Assert.Equal(ContactPoint.ContactPointSystem.Other, Assert.Single(caregiver.Telecom).System);
        Assert.Equal("#" + caregiver.Id, Assert.Single(consent.Provision!.Actor).Reference!.Reference);
        Assert.Contains(consent.Extension, e => e.Url == SimulatorTestData.Base + "/StructureDefinition/screening-result");
        SimulatorTestData.Tagged(consent);
    }
    [Fact] public void Withdrawal_needs_no_caregiver_contact()
    {
        var consent = new ConsentBuilder().Build(SimulatorTestData.Case, SimulatorTestData.Request(status: "inactive"));
        Assert.Empty(consent.Contained); Assert.Equal(Consent.ConsentProvisionType.Deny, consent.Provision!.Type);
    }
    [Fact] public void Grant_without_synthetic_contact_is_refused() =>
        Assert.Throws<ArgumentException>(() => new ConsentBuilder().Build(SimulatorTestData.Case, SimulatorTestData.Request(status: "active")));
}
