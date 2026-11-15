using HisSimulator.State;
using HisSimulator.Time;
using HisSimulator.Outbound;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Inbound;

/// <summary>
/// Parses FHIR resources sent by CareLoop and files them into the message log and the staff inbox.
/// </summary>
/// <remarks>
/// <para>Expected resources and what they become:</para>
/// <list type="bullet">
/// <item><description><c>Task</c>: escalation notice, proposed handoff, handoff or referral rejection, or call request
/// (<see cref="StaffInboxItemKind"/> is chosen from the task code); addressed to <c>Task.owner</c>.</description></item>
/// <item><description><c>DocumentReference</c>: a handoff summary accompanying a proposed handoff.</description></item>
/// <item><description><c>CommunicationRequest</c>: a staff notification, if CareLoop sends those as FHIR.</description></item>
/// </list>
/// <para>
/// Receiving a message never accepts a handoff or acknowledges anything: a notification is not an
/// acknowledgement. Acceptance or rejection only happens when a person acts and the simulator sends
/// a new outbound event.
/// </para>
/// </remarks>
public sealed class InboundFhirReceiver
{
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;
    private readonly CareLoopFhirClient _client;

    /// <summary>
    /// Creates the receiver.
    /// </summary>
    /// <param name="store">Receives the log entry and any staff inbox item.</param>
    /// <param name="clock">Timestamps what is received.</param>
    /// <param name="client">Resolves a notice's case Task through FHIR only.</param>
    public InboundFhirReceiver(SimulatorStore store, ISimulatorClock clock, CareLoopFhirClient client)
    {
        _store = store;
        _clock = clock;
        _client = client;
    }

    /// <summary>
    /// Handles one resource posted or put by CareLoop.
    /// </summary>
    /// <param name="method">HTTP method of the request (<c>POST</c> or <c>PUT</c>), recorded in the log.</param>
    /// <param name="resourceType">Resource type from the route.</param>
    /// <param name="resourceId">Logical id from the route for a <c>PUT</c>; <see langword="null"/> for a <c>POST</c>.</param>
    /// <param name="json">The request body: one FHIR R4 resource as JSON.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The log entry id, the inbox item created (if any) and the body to echo back.</returns>
    /// <exception cref="FormatException">The body is not valid FHIR R4 JSON.</exception>
    /// <exception cref="ArgumentException">
    /// The resource's type does not match <paramref name="resourceType"/>, or its id does not match <paramref name="resourceId"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">The resource type is not one the simulated HIS accepts.</exception>
    public async Task<InboundReceiveResult> ReceiveAsync(
        string method,
        string resourceType,
        string? resourceId,
        string json,
        CancellationToken cancellationToken)
    {
        var logId = Guid.NewGuid();
        int? status = null;
        string? localCaseRef = null;
        Resource? resource = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (method is not ("POST" or "PUT")) throw new ArgumentException("The FHIR intake method must be POST or PUT.");
            resource = FhirSupport.Parse(json);
            if (resource.TypeName != resourceType || resourceId is not null && resource.Id != resourceId)
                throw new ArgumentException("The FHIR resource type and id must match the URL.");
            if (resource is not (FhirTask or DocumentReference or CommunicationRequest))
                throw new NotSupportedException("The simulated HIS accepts Task, DocumentReference and CommunicationRequest.");
            if (resource is FhirTask incomingCase && string.IsNullOrWhiteSpace(incomingCase.Id)
                && incomingCase.Code?.Coding.Any(c => c.System == FhirSupport.Base + "/CodeSystem/task-code" && c.Code == "case") == true)
                throw new ArgumentException("A CareLoop case Task must carry its actual logical id.");
            resource.Id ??= FhirSupport.Id(logId.ToString("N"));
            string? taskId = null;
            string? recipient = null;
            string? role = null;
            string? subject = null;
            string? link = null;
            var kind = StaffInboxItemKind.StaffNotification;
            if (resource is FhirTask task)
            {
                var code = task.Code?.Coding.FirstOrDefault(c => c.System == FhirSupport.Base + "/CodeSystem/task-code")?.Code;
                kind = code switch
                {
                    "escalation" => StaffInboxItemKind.Escalation,
                    "call-request" => StaffInboxItemKind.CallRequested,
                    "case" when task.Status == FhirTask.TaskStatus.Rejected
                        || task.StatusReason?.Coding.Any(c => c.Code == "rejected") == true => StaffInboxItemKind.HandoffRejected,
                    "case" when task.Status == FhirTask.TaskStatus.Requested => StaffInboxItemKind.HandoffProposed,
                    _ => StaffInboxItemKind.StaffNotification,
                };
                recipient = task.Owner?.Identifier?.Value ?? ReferenceId(task.Owner);
                if (code == "staff-notice" && (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrWhiteSpace(task.Description)))
                    throw new ArgumentException("A staff notice needs an addressed recipient and message.");
                role = (task.Owner?.Extension.FirstOrDefault(e => e.Url == FhirSupport.Base + "/StructureDefinition/actor-role")?.Value as Code)?.Value;
                subject = task.Description ?? task.Code?.Text ?? code ?? task.TypeName;
                taskId = code == "case" ? task.Id : ReferenceId(task.Focus, "Task");
                FhirTask? caseTask = code == "case" ? task : null;
                if (caseTask is null && taskId is not null)
                    caseTask = await _client.GetTaskAsync(taskId, cancellationToken)
                        ?? throw new InvalidOperationException("The notice refers to a case Task that CareLoop cannot supply.");
                var reportId = caseTask?.BasedOn.Prepend(caseTask.Focus)
                    .Select(r => ReferenceId(r, "DiagnosticReport")).FirstOrDefault(id => id is not null);
                if (reportId is not null && taskId is not null)
                {
                    _store.RecordCaseTask(reportId, taskId);
                    localCaseRef = _store.GetCases().FirstOrDefault(c => c.DiagnosticReportId == reportId)?.CaseRef;
                }
            }
            else if (resource is DocumentReference document)
            {
                kind = StaffInboxItemKind.HandoffSummary;
                subject = document.Description ?? document.TypeName;
                taskId = document.Context?.Related.Select(r => ReferenceId(r, "Task")).FirstOrDefault(id => id is not null);
            }
            else if (resource is CommunicationRequest communication)
            {
                recipient = communication.Recipient.Select(r => r.Identifier?.Value ?? ReferenceId(r)).FirstOrDefault(id => id is not null);
                subject = communication.Payload.Select(p => (p.Content as FhirString)?.Value).FirstOrDefault(text => text is not null)
                    ?? communication.TypeName;
                taskId = communication.About.Select(r => ReferenceId(r, "Task")).FirstOrDefault(id => id is not null);
            }
            var suppliedLink = System.Text.RegularExpressions.Regex.Match(subject ?? "", @"https?://[^\s]+");
            if (suppliedLink.Success) link = suppliedLink.Value;
            localCaseRef ??= _store.GetCases().FirstOrDefault(c => c.CareLoopTaskId == taskId)?.CaseRef;
            var item = new StaffInboxItem(FhirSupport.InboxId(resource.TypeName + "/" + resource.Id), kind, _clock.UtcNow,
                recipient, role, subject ?? resource.TypeName, link, taskId, resource.TypeName, logId);
            _store.UpsertInboxItem(item);
            status = method == "POST" ? 201 : 200;
            return new InboundReceiveResult(resource.TypeName, resource.Id, logId, item, FhirSupport.Serialize(resource));
        }
        catch (FormatException) { status = 400; throw; }
        catch (ArgumentException) { status = 400; throw; }
        catch (NotSupportedException) { status = 422; throw; }
        catch (HttpRequestException) { status = 502; throw; }
        catch (InvalidOperationException) { status = 409; throw; }
        catch (OperationCanceledException) { status = cancellationToken.IsCancellationRequested ? null : 504; throw; }
        finally
        {
            _store.AppendLogEntry(new FhirMessageLogEntry(logId, FhirMessageDirection.Received, _clock.UtcNow, method,
                resourceType, resource?.Id ?? resourceId, status, localCaseRef, json));
        }
    }

    private static string? ReferenceId(ResourceReference? reference, string? type = null)
    {
        if (reference?.Reference is not { } value) return null;
        var parts = value.TrimEnd('/').Split('/');
        for (var i = 0; i < parts.Length - 1; i++)
            if ((type is null && parts[i] is "Task" or "Patient" or "Practitioner" or "Organization") || parts[i] == type)
                return parts[i + 1];
        return null;
    }
}
