using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;
using FhirAppointmentStatus = Hl7.Fhir.Model.Appointment.AppointmentStatus;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Maps Samples/Appointment.json.
/// </summary>
public sealed class AppointmentMapperTests
{
    private static readonly AppointmentMapper Mapper = new();

    [Fact]
    public void Booked_maps_to_scheduled()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<Appointment>("Appointment")));

        Assert.Equal(CaseEventType.AppointmentUpdated, envelope.Type);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        var payload = Assert.IsType<AppointmentUpdatePayload>(envelope.Payload);
        Assert.Equal(AppointmentStatus.Scheduled, payload.Status);
        Assert.Equal("Appointment/SYN-APPT-0001", payload.AppointmentReference);
        Assert.Equal(new DateTimeOffset(2026, 1, 3, 8, 0, 0, TimeSpan.Zero), payload.ScheduledFor);
    }

    [Fact]
    public void Fulfilled_maps_to_attended() => Assert.Equal(AppointmentStatus.Attended, StatusAfter(FhirAppointmentStatus.Fulfilled));

    [Fact]
    public void Noshow_maps_to_missed() => Assert.Equal(AppointmentStatus.Missed, StatusAfter(FhirAppointmentStatus.Noshow));

    [Theory]
    [InlineData(FhirAppointmentStatus.Proposed)]
    [InlineData(FhirAppointmentStatus.Pending)]
    [InlineData(FhirAppointmentStatus.Cancelled)]
    [InlineData(FhirAppointmentStatus.Arrived)]
    public void Other_statuses_map_to_no_event(FhirAppointmentStatus status)
    {
        var appointment = Sample<Appointment>("Appointment");
        appointment.Status = status;

        Assert.Empty(Map(Mapper, appointment));
    }

    private static AppointmentStatus StatusAfter(FhirAppointmentStatus status)
    {
        var appointment = Sample<Appointment>("Appointment");
        appointment.Status = status;
        appointment.Meta!.VersionId = "2";

        var envelope = Assert.Single(Map(Mapper, appointment));
        Assert.Equal("Appointment/SYN-APPT-0001/_history/2", envelope.SourceEventId);
        return ((AppointmentUpdatePayload)envelope.Payload).Status;
    }
}
