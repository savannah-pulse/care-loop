using HisSimulator.Outbound;
using Hl7.Fhir.Model;

namespace HisSimulator.Tests.Outbound;

public sealed class AppointmentBuilderTests
{
    private readonly AppointmentBuilder _builder = new();
    [Theory] [InlineData("booked", Appointment.AppointmentStatus.Booked)] [InlineData("fulfilled", Appointment.AppointmentStatus.Fulfilled)]
    [InlineData("noshow", Appointment.AppointmentStatus.Noshow)]
    public void Build_maps_booked_fulfilled_and_noshow_statuses(string status, Appointment.AppointmentStatus expected) =>
        Assert.Equal(expected, _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request(status: status)).Status);
    [Fact] public void Build_adds_the_synthetic_patient_as_participant() =>
        Assert.Contains(_builder.Build(SimulatorTestData.Case, SimulatorTestData.Request(status: "booked")).Participant, p => p.Actor?.Reference == "Patient/SYN-PT-1");
    [Fact] public void Build_keeps_one_id_and_changes_version_per_state()
    {
        var one = _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request(status: "booked"));
        var two = _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request(key: "s2", status: "fulfilled"));
        Assert.Equal(one.Id, two.Id); Assert.NotEqual(one.Meta!.VersionId, two.Meta!.VersionId);
    }
    [Fact] public void Build_tags_the_appointment_as_simulated() => SimulatorTestData.Tagged(_builder.Build(SimulatorTestData.Case, SimulatorTestData.Request(status: "booked")));
    [Fact] public void Build_rejects_an_unsupported_status() =>
        Assert.Throws<ArgumentException>(() => _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request(status: "invented")));
    [Fact] public void Build_preserves_source_location_and_scheduled_time()
    {
        var request = SimulatorTestData.With(SimulatorTestData.Request(status: "booked"), "location", "SYN Breast Imaging Clinic");
        request = SimulatorTestData.With(request, "scheduledFor", "2026-01-02T12:00:00Z");
        var appointment = _builder.Build(SimulatorTestData.Case, request);
        Assert.Contains(appointment.Participant, p => p.Actor?.Display == "SYN Breast Imaging Clinic");
        Assert.Equal(SimulatorTestData.At.AddDays(1), appointment.Start);
    }
}
