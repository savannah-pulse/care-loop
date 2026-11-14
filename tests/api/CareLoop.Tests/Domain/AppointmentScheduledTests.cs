using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Booking, rebooking or moving the appointment raises <see cref="AppointmentScheduled"/> (C13), once per new time.
/// </summary>
public sealed class AppointmentScheduledTests
{
    private const string Clinic = "SYN Breast Imaging Clinic, Building B";

    private readonly FakeClock _clock = new();

    [Fact]
    public void First_booking_raises_the_time_and_place()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ClearDomainEvents();
        var scheduledFor = _clock.UtcNow.AddDays(3);

        var track = @case.ScheduleAppointment("Appointment/SYN-APPT-0001", scheduledFor, _clock.UtcNow, Clinic);

        Assert.Equal(AppointmentStatus.Scheduled, track.Status);
        var raised = Assert.Single(@case.DomainEvents.OfType<AppointmentScheduled>());
        Assert.Equal(@case.Id, raised.CaseId);
        Assert.Equal(scheduledFor, raised.ScheduledFor);
        Assert.Equal(Clinic, raised.Location);
        Assert.Equal(_clock.UtcNow, raised.OccurredAt);
        Assert.NotEqual(Guid.Empty, raised.EventId);
    }

    [Fact]
    public void A_booking_without_a_place_carries_no_place()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ClearDomainEvents();

        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", _clock.UtcNow.AddDays(3), _clock.UtcNow, "  ");

        Assert.Null(Assert.Single(@case.DomainEvents.OfType<AppointmentScheduled>()).Location);
    }

    [Fact]
    public void Rebooking_after_a_missed_appointment_raises_the_new_time()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", _clock.UtcNow.AddDays(3), _clock.UtcNow, Clinic);
        _clock.Advance(TimeSpan.FromDays(3));
        @case.Appointment!.Advance(AppointmentStatus.Missed, _clock.UtcNow, "Appointment/SYN-APPT-0001");
        @case.ClearDomainEvents();
        var rebookedFor = _clock.UtcNow.AddDays(2);

        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", rebookedFor, _clock.UtcNow, Clinic);

        Assert.Equal(AppointmentStatus.Scheduled, @case.Appointment.Status);
        Assert.Equal(rebookedFor, @case.Appointment.ScheduledFor);
        Assert.Equal(rebookedFor, Assert.Single(@case.DomainEvents.OfType<AppointmentScheduled>()).ScheduledFor);
    }

    [Fact]
    public void Moving_a_booked_appointment_to_another_time_raises_again()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", _clock.UtcNow.AddDays(3), _clock.UtcNow, Clinic);
        @case.ClearDomainEvents();
        var movedTo = _clock.UtcNow.AddDays(4);

        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", movedTo, _clock.UtcNow, Clinic);

        Assert.Equal(movedTo, Assert.Single(@case.DomainEvents.OfType<AppointmentScheduled>()).ScheduledFor);
    }

    [Fact]
    public void The_same_booking_again_raises_nothing_so_nobody_is_told_twice()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        var scheduledFor = _clock.UtcNow.AddDays(3);
        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", scheduledFor, _clock.UtcNow, Clinic);
        @case.ClearDomainEvents();

        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", scheduledFor, _clock.UtcNow.AddMinutes(5), Clinic);

        Assert.Empty(@case.DomainEvents.OfType<AppointmentScheduled>());
    }

    [Fact]
    public void An_appointment_first_reported_as_attended_or_missed_raises_nothing()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ClearDomainEvents();

        @case.BeginAppointment("Appointment/SYN-APPT-0001", _clock.UtcNow.AddDays(-1), _clock.UtcNow);

        Assert.Empty(@case.DomainEvents.OfType<AppointmentScheduled>());
    }

    [Fact]
    public void An_attended_appointment_cannot_be_scheduled_again()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ScheduleAppointment("Appointment/SYN-APPT-0001", _clock.UtcNow.AddDays(1), _clock.UtcNow);
        @case.Appointment!.Advance(AppointmentStatus.Attended, _clock.UtcNow.AddDays(1), "Appointment/SYN-APPT-0001");
        @case.ClearDomainEvents();

        Assert.Throws<InvalidOperationException>(() =>
            @case.ScheduleAppointment("Appointment/SYN-APPT-0001", _clock.UtcNow.AddDays(5), _clock.UtcNow));
        Assert.Empty(@case.DomainEvents);
    }

    [Fact]
    public void A_closed_case_refuses_a_booking()
    {
        var @case = DomainTestData.Closed(_clock);

        Assert.Throws<InvalidOperationException>(() =>
            @case.ScheduleAppointment("Appointment/SYN-APPT-0002", _clock.UtcNow.AddDays(3), _clock.UtcNow, Clinic));
    }
}
