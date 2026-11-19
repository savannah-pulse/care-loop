using System.Text.Json.Nodes;
using CareLoop.Api.Features.Ai;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Translates between CareLoop's simple request and what each Microsoft Foundry agent expects and returns.
/// </summary>
/// <remarks>
/// <para>
/// The endpoints and prompt files know nothing about Foundry. An agent, though, has its own instructions in the
/// Foundry project, and they fix the input it accepts and the shape of its answer. This file is the only place that
/// knows those shapes, so when an agent's owner changes its instructions, this is the only file to change.
/// </para>
/// <para>
/// As read from the project on 19 November 2026 (role-briefing version 8, impact-summary 7, patient-notification 7):
/// <c>role-briefing</c> and <c>impact-summary</c> follow the task they are given over data they treat as facts, so they
/// are sent the prompt file's instructions and the data; <c>patient-notification</c> fills the approved template it is
/// given with the values it is given. All three answer plain text, or <c>CANNOT_COMPLY</c>.
/// </para>
/// <para>Nothing is added here that the endpoint did not already allow: the agent input is built only from the request.</para>
/// </remarks>
public static class FoundryAgentFormat
{
    /// <summary>Builds the message an agent is sent.</summary>
    /// <param name="request">The use, the prompt file's instructions and the data.</param>
    /// <param name="synthetic">Whether the figures are synthetic; told to the impact agent with its data.</param>
    /// <returns>The message for that use's agent.</returns>
    /// <exception cref="AiUnavailableException">No translation exists for the use or the situation.</exception>
    public static string ToAgentInput(AiRequest request, bool synthetic) => request.Use switch
    {
        // The agent follows a task over data it treats as facts, so the prompt file's instructions apply as written.
        AiPrompts.RoleBriefing => new JsonObject { ["task"] = request.Task, ["data"] = JsonNode.Parse(request.Data) }.ToJsonString(),
        AiPrompts.ImpactSummary => new JsonObject { ["task"] = request.Task, ["data"] = WithSynthetic(JsonNode.Parse(request.Data)!.AsObject(), synthetic) }.ToJsonString(),
        AiPrompts.PatientMessage => PatientInput(JsonNode.Parse(request.Data)!.AsObject()),
        _ => throw new AiUnavailableException($"The Foundry agent for '{request.Use}' has no translation yet."),
    };

    /// <summary>Reads an agent's answer as the text staff see.</summary>
    /// <param name="use">The use the agent answered for.</param>
    /// <param name="answer">The agent's answer.</param>
    /// <returns>Plain text.</returns>
    /// <exception cref="AiUnavailableException">The agent refused, or its answer is empty.</exception>
    public static string ToText(string use, string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new AiUnavailableException("The agent wrote no text.");
        }

        if (answer.Contains("CANNOT_COMPLY", StringComparison.OrdinalIgnoreCase) && answer.Length < 200)
        {
            throw new AiUnavailableException($"The agent declined: {answer.Trim()}.");
        }

        return answer.Trim();
    }

    private static JsonObject WithSynthetic(JsonObject data, bool synthetic)
    {
        data["synthetic_data"] = synthetic;
        return data;
    }

    /// <summary>The placeholder a draft carries where the patient's first name goes; the name itself is never sent.</summary>
    public const string FirstNamePlaceholder = "{first name}";

    private const string PatientTask = """
        TASK: Draft a patient notification using the supplied approved template.
        Replace each placeholder with its corresponding verified value.
        Do not change any other wording.
        """;

    private const string ReplyCodes = "Reply 1 to confirm you got this message. Reply 2 if you would like your care team to call you.";

    // patient-notification: CareLoop chooses the template from the situation and supplies the values; the agent adds no
    // wording of its own. The patient's name is never sent: its value is the reviewer's placeholder. There is no slot for
    // a contact number: CareLoop holds none, and the agent refuses a placeholder where a number should be.
    // Placeholder wording, not approved (WS4), like the templates under Infrastructure/Templates.
    private static string PatientInput(JsonObject data)
    {
        var situation = data["situation"]!.GetValue<string>();
        var middle = situation switch
        {
            "appointment reminder" => "This is a reminder of your appointment on {appointment_date} at {appointment_time}.",
            "could not reach the patient" => "We tried to contact you but could not reach you.",
            "please contact the care team" => "Your care team would like to speak with you.",
            var other => throw new AiUnavailableException($"No patient-notification template is mapped for the situation '{other}'."),
        };
        var email = data["channel"]!.GetValue<string>() == "email";
        var template = $"{(email ? "Subject: A message from your care team\n\n" : string.Empty)}Hello {{patient_name}}, this is the care team at {{facility_name}}. {middle} {ReplyCodes}";
        var values = new JsonObject { ["patient_name"] = FirstNamePlaceholder, ["facility_name"] = data["clinic"]!.GetValue<string>() };
        if (situation == "appointment reminder")
        {
            values["appointment_date"] = data["appointment_date"]!.GetValue<string>();
            values["appointment_time"] = data["appointment_time"]!.GetValue<string>();
        }

        return $"{PatientTask}\nTEMPLATE:\n{template}\nVALUES:\n{values.ToJsonString()}";
    }
}
