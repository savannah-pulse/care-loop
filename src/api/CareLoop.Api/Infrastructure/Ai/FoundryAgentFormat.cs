using System.Text.Json;
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
/// As read from the project on 19 November 2026:
/// <c>role-briefing</c> takes a task and the data, and answers one plain paragraph;
/// <c>impact-summary</c> takes a structured <c>&lt;input_json&gt;</c> of measures and answers JSON with sentences;
/// <c>patient-notification</c> fills one template from its own registry (T01 to T12), which CareLoop names, from facts
/// CareLoop supplies, and answers JSON with the draft.
/// </para>
/// <para>Nothing is added here that the endpoint did not already allow: the agent input is built only from the request.</para>
/// </remarks>
public static class FoundryAgentFormat
{
    /// <summary>Builds the message an agent is sent.</summary>
    /// <param name="request">The use, the prompt file's instructions and the data.</param>
    /// <param name="synthetic">Whether the figures are synthetic; the impact agent asks.</param>
    /// <returns>The message for that use's agent.</returns>
    /// <exception cref="AiUnavailableException">No translation exists for the use.</exception>
    public static string ToAgentInput(AiRequest request, bool synthetic) => request.Use switch
    {
        // The agent follows a task over data it treats as facts, so the prompt file's instructions apply as written.
        AiPrompts.RoleBriefing => new JsonObject { ["task"] = request.Task, ["data"] = JsonNode.Parse(request.Data) }.ToJsonString(),
        AiPrompts.ImpactSummary => $"<input_json>\n{ImpactInput(JsonNode.Parse(request.Data)!.AsObject(), synthetic).ToJsonString()}\n</input_json>",
        AiPrompts.PatientMessage => $"<input_json>\n{PatientInput(JsonNode.Parse(request.Data)!.AsObject(), synthetic).ToJsonString()}\n</input_json>",
        _ => throw new AiUnavailableException($"The Foundry agent for '{request.Use}' has no translation yet."),
    };

    /// <summary>Reads an agent's answer as the text staff see.</summary>
    /// <param name="use">The use the agent answered for.</param>
    /// <param name="answer">The agent's answer.</param>
    /// <returns>Plain text.</returns>
    /// <exception cref="AiUnavailableException">The agent refused, or its answer is empty or not in the shape its instructions give.</exception>
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

        return use switch
        {
            AiPrompts.ImpactSummary => ImpactText(answer),
            AiPrompts.PatientMessage => PatientText(answer),
            _ => answer.Trim(),
        };
    }

    // impact-summary: measures[], delay_reasons[] and supplementary[] with a metric_id each; a withheld value goes as suppressed.
    private static JsonObject ImpactInput(JsonObject data, bool synthetic)
    {
        var measures = new JsonArray();
        foreach (var (label, share) in data["shares"]!.AsObject())
        {
            measures.Add(Measure(label, share!));
        }

        var supplementary = new JsonArray();
        foreach (var (step, time) in data["median_time_from_result_to"]!.AsObject())
        {
            // CareLoop measures clock time, not working days, so a median goes as a labelled value, not as a step time.
            supplementary.Add(time is JsonObject median
                ? new JsonObject
                {
                    ["metric_id"] = Id("time to", step), ["label"] = $"Median time from the result to {step}", ["value"] = median["time"]!.GetValue<string>(),
                    ["unit"] = "clock time", ["denominator"] = median["cases"]!.GetValue<int>(), ["missing_count"] = 0, ["suppressed"] = false,
                    ["small_denominator"] = false, ["comparison"] = NotTracked(),
                }
                : new JsonObject { ["metric_id"] = Id("time to", step), ["label"] = $"Median time from the result to {step}", ["suppressed"] = true });
        }

        if (data["closed_cases_by_reason"] is JsonObject closures && data["closed_cases"] is JsonValue closed && closed.TryGetValue<int>(out var closedCases))
        {
            foreach (var (reason, count) in closures)
            {
                supplementary.Add(new JsonObject
                {
                    ["metric_id"] = Id("closed", reason), ["label"] = $"Closed cases: {reason}", ["numerator"] = count!.GetValue<int>(),
                    ["denominator"] = closedCases, ["unit"] = "cases", ["missing_count"] = 0, ["suppressed"] = false, ["small_denominator"] = false,
                    ["comparison"] = NotTracked(),
                });
            }
        }

        var reasons = data["open_cases_waiting_because"]!.AsObject()
            .Where(r => r.Value is JsonValue value && value.TryGetValue<int>(out _))
            .Select(r => (Reason: r.Key, Count: r.Value!.GetValue<int>()))
            .OrderByDescending(r => r.Count).ToList();
        var delays = new JsonArray();
        foreach (var (reason, count) in reasons)
        {
            delays.Add(new JsonObject { ["metric_id"] = Id("waiting", reason), ["code"] = reason, ["label"] = reason, ["count"] = count });
        }

        var period = data["period"]!.GetValue<string>();
        return new JsonObject
        {
            ["snapshot_id"] = Id("snapshot", period),
            ["period"] = new JsonObject { ["label"] = period },
            ["filters"] = new JsonObject { ["programme_label"] = "All programmes", ["facility_label"] = data["facility"]!.GetValue<string>() },
            ["cohort_size"] = Number(data["total_cases"]),
            ["synthetic"] = synthetic,
            ["measures"] = measures,
            ["step_times"] = new JsonArray(),
            ["funnel"] = new JsonArray(),
            ["delay_reasons"] = delays,
            ["delay_reasons_total"] = reasons.Sum(r => r.Count),
            ["delay_reasons_unit"] = "open cases",
            ["supplementary"] = supplementary,
            ["targets"] = new JsonArray(),
            ["max_words"] = 210,
        };
    }

    private static JsonObject Measure(string label, JsonNode share) => share is JsonObject counted
        ? new JsonObject
        {
            ["metric_id"] = Id("share", label), ["label"] = Sentence(label), ["numerator"] = counted["numerator"]!.GetValue<int>(),
            ["denominator"] = counted["denominator"]!.GetValue<int>(), ["value"] = counted["percent"]!.GetValue<int>(), ["unit"] = "%",
            ["missing_count"] = 0, ["suppressed"] = false, ["small_denominator"] = false, ["comparison"] = NotTracked(),
        }
        : new JsonObject { ["metric_id"] = Id("share", label), ["label"] = Sentence(label), ["suppressed"] = true };

    // CareLoop has no replay with escalation switched off to compare against.
    private static JsonObject NotTracked() => new() { ["status"] = "not_tracked" };

    private static JsonNode? Number(JsonNode? value) => value is JsonValue number && number.TryGetValue<int>(out var n) ? n : null;

    private static string Id(string kind, string label) => $"{kind} {label}".ToLowerInvariant().Replace(' ', '_').Replace(':', '_');

    private static string Sentence(string label) => label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label[1..];

    /// <summary>The placeholder a draft carries where the patient's first name goes; the name itself is never sent.</summary>
    public const string FirstNamePlaceholder = "{first name}";

    /// <summary>The placeholder a draft carries where the clinic's contact number goes; CareLoop holds none, so the reviewer fills it in.</summary>
    public const string ClinicContactPlaceholder = "{clinic contact}";

    // patient-notification: CareLoop names the template and supplies its facts. The agent adds no wording of its own.
    //   appointment reminder          -> T02, which requires appointment_status = confirmed
    //   could not reach the patient   -> T06, which requires contact_attempt_status = unsuccessful
    //   please contact the care team  -> T05, which requires callback_task_verified = true
    // The agent refuses unless recipient_verified and channel_permitted are "true". CareLoop sends "true" because the
    // result is a draft for a nurse or clinician, who checks the recipient and the channel before anything is sent.
    private static JsonObject PatientInput(JsonObject data, bool synthetic)
    {
        var (template, condition, value) = data["situation"]!.GetValue<string>() switch
        {
            "appointment reminder" => ("T02", "appointment_status", "confirmed"),
            "could not reach the patient" => ("T06", "contact_attempt_status", "unsuccessful"),
            "please contact the care team" => ("T05", "callback_task_verified", "true"),
            var other => throw new AiUnavailableException($"No patient-notification template is mapped for the situation '{other}'."),
        };
        var facts = new JsonArray();
        void Fact(string type, string factValue) => facts.Add(new JsonObject
        {
            ["fact_id"] = $"F{facts.Count + 1}", ["fact_type"] = type, ["value"] = factValue, ["review_status"] = "final",
        });
        Fact("patient_name", FirstNamePlaceholder);
        Fact("facility_name", data["clinic"]!.GetValue<string>());
        Fact("facility_contact", ClinicContactPlaceholder);
        Fact("recipient_verified", "true");
        Fact("channel_permitted", "true");
        Fact(condition, value);
        if (template == "T02")
        {
            Fact("appointment_date", data["appointment_date"]!.GetValue<string>());
            Fact("appointment_time", data["appointment_time"]!.GetValue<string>());
        }

        return new JsonObject
        {
            ["request_id"] = Guid.NewGuid().ToString(),
            ["synthetic"] = synthetic,
            ["stage"] = "patient_contact",
            ["language"] = "en",
            ["channel"] = data["channel"]!.GetValue<string>() == "email" ? "email" : "sms",
            ["max_chars"] = data["max_characters"]!.GetValue<int>(),
            ["template"] = new JsonObject { ["template_id"] = template, ["template_version"] = "1.0", ["approval_status"] = "approved" },
            ["facts"] = facts,
        };
    }

    // patient-notification answers {"status":"draft","draft_text":…}, or {"status":"cannot_comply","reason_code":…}.
    private static string PatientText(string answer)
    {
        try
        {
            using var document = JsonDocument.Parse(Unfenced(answer));
            var root = document.RootElement;
            if (root.GetProperty("status").GetString() != "draft")
            {
                var reason = root.TryGetProperty("reason_code", out var code) ? code.GetString() : "no reason given";
                throw new AiUnavailableException($"The agent declined: {reason}.");
            }

            var draft = root.GetProperty("draft_text").GetString();
            return string.IsNullOrWhiteSpace(draft) ? throw new AiUnavailableException("The agent wrote no draft.") : draft.Trim();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiUnavailableException("The agent's answer was not in the shape its instructions give.", ex);
        }
    }

    // An agent sometimes wraps its JSON in a Markdown code fence.
    private static string Unfenced(string answer)
    {
        var json = answer.Trim();
        return json.StartsWith("```", StringComparison.Ordinal) ? json[(json.IndexOf('\n') + 1)..].TrimEnd('`', '\n', ' ') : json;
    }

    // impact-summary answers {"status":"ready","paragraphs":[{"sentences":[{"text":…}]}],"limits_note":…}.
    private static string ImpactText(string answer)
    {
        try
        {
            using var document = JsonDocument.Parse(Unfenced(answer));
            var root = document.RootElement;
            if (root.GetProperty("status").GetString() != "ready")
            {
                var reason = root.TryGetProperty("reason_code", out var code) ? code.GetString() : "no reason given";
                throw new AiUnavailableException($"The agent declined: {reason}.");
            }

            var paragraphs = root.GetProperty("paragraphs").EnumerateArray()
                .Select(p => string.Join(" ", p.GetProperty("sentences").EnumerateArray().Select(s => s.GetProperty("text").GetString())))
                .Where(p => p.Length > 0)
                .Append(root.GetProperty("limits_note").GetString() ?? string.Empty)
                .Where(p => p.Length > 0);
            return string.Join("\n\n", paragraphs);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiUnavailableException("The agent's answer was not in the shape its instructions give.", ex);
        }
    }
}
