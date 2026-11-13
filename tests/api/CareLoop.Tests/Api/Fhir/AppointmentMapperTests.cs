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

    [Fact]
    public void A_booking_happened_when_it_was_made_not_at_the_appointment_time()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<Appointment>("Appointment")));

        Assert.Equal(new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), envelope.OccurredAt);
    }

    [Theory]
    [InlineData(FhirAppointmentStatus.Fulfilled)]
    [InlineData(FhirAppointmentStatus.Noshow)]
    public void Attendance_and_no_show_happened_at_the_appointment_not_at_the_booking(FhirAppointmentStatus status)
    {
        var appointment = Sample<Appointment>("Appointment");
        appointment.Status = status;

        var envelope = Assert.Single(Map(Mapper, appointment));

        Assert.Equal(new DateTimeOffset(2026, 1, 3, 8, 30, 0, TimeSpan.Zero), envelope.OccurredAt);
    }

    [Theory]
    [InlineData(FhirAppointmentStatus.Booked)]
    [InlineData(FhirAppointmentStatus.Fulfilled)]
    [InlineData(FhirAppointmentStatus.Noshow)]
    public void The_source_update_time_comes_first_so_a_rebooking_is_not_dated_at_the_first_booking(FhirAppointmentStatus status)
    {
        var appointment = Sample<Appointment>("Appointment");
        appointment.Status = status;
        appointment.Meta!.LastUpdated = new DateTimeOffset(2026, 1, 5, 14, 0, 0, TimeSpan.Zero);

        var envelope = Assert.Single(Map(Mapper, appointment));

        Assert.Equal(new DateTimeOffset(2026, 1, 5, 14, 0, 0, TimeSpan.Zero), envelope.OccurredAt);
    }

    [Fact]
    public void Attendance_without_an_end_happened_at_the_start()
    {
        var appointment = Sample<Appointment>("Appointment");
        appointment.Status = FhirAppointmentStatus.Fulfilled;
        appointment.End = null;

        var envelope = Assert.Single(Map(Mapper, appointment));

        Assert.Equal(new DateTimeOffset(2026, 1, 3, 8, 0, 0, TimeSpan.Zero), envelope.OccurredAt);
    }

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
