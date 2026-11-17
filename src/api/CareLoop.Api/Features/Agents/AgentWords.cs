using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Domain.Cases;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Puts coordination state into the plain words the agent prompts use.
/// </summary>
/// <remarks>
/// The words match the dashboard's, so what an agent writes reads like the screen around it. They describe roles,
/// steps, next actions, deadlines and exceptions only; nothing here can name or identify a patient. English only, like
/// the message templates, and versioned with the prompts.
/// </remarks>
internal static class AgentWords
{
    /// <summary>How many display steps a case goes through.</summary>
    public const int StepCount = 10;

    private static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [DemoRoles.Clinician] = "clinician",
        [DemoRoles.ReceivingClinician] = "receiving clinician",
        [DemoRoles.NurseNavigator] = "nurse navigator",
        [DemoRoles.Coordinator] = "programme coordinator",
        [DemoRoles.FacilityLead] = "facility lead",
        [DemoRoles.Lab] = "lab team",
    };

    private static readonly IReadOnlyDictionary<string, string> NextActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["assign-owner"] = "Choose an owner",
        ["review-result"] = "Review the result",
        ["acknowledge-notification"] = "Make sure the patient has the message",
        ["answer-consultation"] = "Answer the consultation",
        ["respond-to-referral"] = "Accept or decline the referral",
        ["arrange-follow-up"] = "Arrange the follow-up",
        ["call-patient"] = "Call the patient back",
        ["contact-patient-another-way"] = "Contact the patient another way",
        ["reassign-consultation"] = "Find another clinician for the consultation",
        ["arrange-alternative-referral"] = "Send a new referral",
        ["rebook-appointment"] = "Rebook the appointment",
        ["start-treatment"] = "Start the treatment",
        ["decide-closure"] = "Decide whether to close the case",
    };

    private static readonly IReadOnlyDictionary<CaseDisplayLabel, string> Steps = new Dictionary<CaseDisplayLabel, string>
    {
        [CaseDisplayLabel.ResultReceived] = "Result received",
        [CaseDisplayLabel.CaseOpened] = "Case opened",
        [CaseDisplayLabel.OwnerAssigned] = "Owner assigned",
        [CaseDisplayLabel.PatientNotified] = "Patient notified",
        [CaseDisplayLabel.PatientAcknowledged] = "Patient acknowledged",
        [CaseDisplayLabel.Consultation] = "Consultation",
        [CaseDisplayLabel.FollowUp] = "Follow-up",
        [CaseDisplayLabel.OutcomeRecorded] = "Outcome recorded",
        [CaseDisplayLabel.ClosureReview] = "Closure review",
        [CaseDisplayLabel.Closed] = "Closed",
    };

    private static readonly IReadOnlyDictionary<CaseExceptions, string> ExceptionWords = new Dictionary<CaseExceptions, string>
    {
        [CaseExceptions.Overdue] = "overdue",
        [CaseExceptions.Rejected] = "referral declined",
        [CaseExceptions.Unreachable] = "not reached",
        [CaseExceptions.Unacknowledged] = "not acknowledged",
        [CaseExceptions.Unassigned] = "needs an owner",
    };

    /// <summary>Names a role.</summary>
    /// <param name="role">The role key.</param>
    /// <returns>For example <c>nurse navigator</c>; an unknown key in words.</returns>
    public static string Role(string role) => Roles.TryGetValue(role, out var words) ? words : MessageFormat.Role(role);

    /// <summary>Names a programme from its pathway identifier.</summary>
    /// <param name="pathwayId">The pathway, for example <c>breast-screening</c>.</param>
    /// <returns>For example <c>breast screening</c>.</returns>
    public static string Programme(string pathwayId) => MessageFormat.Role(pathwayId);

    /// <summary>Names a display step with its place in the journey.</summary>
    /// <param name="label">The display label the Domain computed.</param>
    /// <returns>For example <c>Follow-up (step 7 of 10)</c>.</returns>
    public static string Step(CaseDisplayLabel label) =>
        $"{(Steps.TryGetValue(label, out var words) ? words : label.ToString())} (step {(int)label} of {StepCount})";

    /// <summary>Names a display step on its own.</summary>
    /// <param name="label">The display label.</param>
    /// <returns>For example <c>Patient notified</c>.</returns>
    public static string StepName(CaseDisplayLabel label) => Steps.TryGetValue(label, out var words) ? words : label.ToString();

    /// <summary>Describes a next action.</summary>
    /// <param name="key">The next-action key, or <see langword="null"/>.</param>
    /// <returns>For example <c>Send a new referral</c>; an unknown key in words.</returns>
    public static string NextAction(string? key) =>
        key is null ? "No next action" : NextActions.TryGetValue(key, out var words) ? words : MessageFormat.Role(key);

    /// <summary>Lists a case's unresolved exceptions, and its escalation, in words.</summary>
    /// <param name="case">The case.</param>
    /// <returns>For example <c>overdue, referral declined, escalated</c>; empty when there are none.</returns>
    public static IReadOnlyList<string> Flags(Case @case)
    {
        var flags = ExceptionWords.Where(e => @case.Exceptions.HasFlag(e.Key)).Select(e => e.Value).ToList();
        if (@case.IsEscalated)
        {
            flags.Add("escalated");
        }

        return flags;
    }

    /// <summary>Names an exception flag.</summary>
    /// <param name="exception">One exception flag.</param>
    /// <returns>For example <c>not reached</c>.</returns>
    public static string Exception(CaseExceptions exception) =>
        ExceptionWords.TryGetValue(exception, out var words) ? words : exception.ToString();

    /// <summary>Says when a next action is due, relative to now, in local time.</summary>
    /// <param name="due">The due time, or <see langword="null"/>.</param>
    /// <param name="now">The current time.</param>
    /// <param name="format">Gives local dates and times.</param>
    /// <returns>For example <c>late by 18 hours</c>, <c>due today at 12:00</c>, <c>due tomorrow at 09:00</c> or <c>no deadline</c>.</returns>
    public static string Due(DateTimeOffset? due, DateTimeOffset now, MessageFormat format)
    {
        if (due is not { } at)
        {
            return "no deadline";
        }

        if (at < now)
        {
            return $"late by {Span(now - at)}";
        }

        if (IsSameLocalDay(at, now, format))
        {
            return $"due today at {format.Time(at)}";
        }

        return IsSameLocalDay(at, now.AddDays(1), format)
            ? $"due tomorrow at {format.Time(at)}"
            : $"due {format.Day(at)} at {format.Time(at)}";
    }

    /// <summary>Tells whether two instants fall on the same local day.</summary>
    /// <param name="a">One instant.</param>
    /// <param name="b">The other.</param>
    /// <param name="format">Supplies the local time zone.</param>
    /// <returns><see langword="true"/> when both have the same local date.</returns>
    public static bool IsSameLocalDay(DateTimeOffset a, DateTimeOffset b, MessageFormat format) => format.Date(a) == format.Date(b);

    /// <summary>Gives a moment as the local date and time.</summary>
    /// <param name="at">The instant.</param>
    /// <param name="format">Gives local dates and times.</param>
    /// <returns>For example <c>Tuesday 17 November 2026, 12:47</c>.</returns>
    public static string Moment(DateTimeOffset at, MessageFormat format) => $"{format.Date(at)}, {format.Time(at)}";

    /// <summary>Gives a length of time in the largest whole unit that reads naturally.</summary>
    /// <param name="span">The length of time.</param>
    /// <returns>For example <c>35 minutes</c>, <c>18 hours</c> or <c>3 days</c>.</returns>
    public static string Span(TimeSpan span)
    {
        if (span.TotalMinutes < 60)
        {
            return Count(Math.Max(1, (int)span.TotalMinutes), "minute");
        }

        return span.TotalHours < 48 ? Count((int)span.TotalHours, "hour") : Count((int)span.TotalDays, "day");
    }

    /// <summary>Gives a count with its noun.</summary>
    /// <param name="count">How many.</param>
    /// <param name="noun">The singular noun.</param>
    /// <returns>For example <c>1 hour</c> or <c>3 hours</c>.</returns>
    public static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
