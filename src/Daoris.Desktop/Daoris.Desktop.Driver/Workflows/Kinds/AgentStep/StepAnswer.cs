using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An agent-performed step's files under the home's workflows, by the attempt's invocation: its ask (<see cref="StepAgentAsk"/>) and its
/// answer, kept in the wire's own reconcile shape (<see cref="WorkWire.ReadReconcileAnswer"/>) so the performer reads the agent's answer as a
/// plugin's. The answer is written once: by <c>step_answer</c>, or as <see cref="NoAnswer"/> where the session ended without one.
/// </summary>
public static class StepAnswer
{
    public const string Tool = "step_answer";

    /// <summary>The code a step fails with whose session ended unanswered.</summary>
    public const string NoAnswer = "no-answer";

    public static string Folder(string home) => DaorisHome.Entry(home, HomeEntry.Workflows, "agent-steps");

    public static string AskPath(string home, string invocation) => Path.Combine(Folder(home), $"{invocation}.json");

    public static string AnswerPath(string home, string invocation) => Path.Combine(Folder(home), $"{invocation}.answer.json");

    /// <summary>The ask kept for <paramref name="invocation"/>, or null.</summary>
    public static StepAgentAsk? AskOf(string home, string invocation) =>
        File.Exists(AskPath(home, invocation)) ? StepAgentAsk.Read(AtomicFile.ReadText(AskPath(home, invocation))) : null;

    /// <summary>The answer kept, as the performer reads it; null where none is, or it does not read.</summary>
    public static WorkReconcileAnswer? Read(string home, string invocation)
    {
        if (!File.Exists(AnswerPath(home, invocation))) return null;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(AnswerPath(home, invocation)));
            return WorkWire.ReadReconcileAnswer(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The agent's answer composed in the wire's shape and kept, or why not: an answer kept already, a state that is neither reached nor
    /// failed, a decision or output the step does not declare, or a shape the wire refuses. Its evidence is named under the step's schema by
    /// the invocation, so it points at the session's own record.
    /// </summary>
    public static string? Answer(string home, StepAgentAsk ask, JsonElement? arguments)
    {
        if (File.Exists(AnswerPath(home, ask.Invocation))) return "the step is answered already: it takes one answer.";
        string? Text(string name) => arguments is { ValueKind: JsonValueKind.Object } given && given.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String && value.GetString()!.Trim() is { Length: > 0 } text ? text : null;
        var state = Text("state");
        if (state is not (WorkStates.Reached or WorkStates.Failed)) return "`state` is `reached` or `failed`.";
        var outcome = new JsonObject { ["state"] = state };
        if (state == WorkStates.Failed) outcome["code"] = Text("code") ?? WorkPerformer.NoCode;
        if (Text("decision") is { } decision)
        {
            if (!ask.Outcomes.Contains(decision, StringComparer.Ordinal)) return $"`decision` is one of the step's routes: {string.Join(", ", ask.Outcomes)}.";
            outcome["outcome"] = decision;
        }

        var evidence = new JsonObject { ["message"] = Text("message") ?? state };
        if (ask.Evidence is { } schema) evidence["ref"] = $"{schema}:{ask.Invocation}";
        if (Text("url") is { } url) evidence["url"] = url;
        outcome["evidence"] = evidence;
        if (state == WorkStates.Reached && arguments is { ValueKind: JsonValueKind.Object } named && named.TryGetProperty("outputs", out var outputs)
            && outputs.ValueKind == JsonValueKind.Object)
        {
            var refs = new JsonObject();
            foreach (var output in outputs.EnumerateObject())
            {
                if (!ask.Outputs.TryGetValue(output.Name, out var of) || output.Value.ValueKind != JsonValueKind.String)
                    return $"`outputs` names the step's own, each with its id: {string.Join(", ", ask.Outputs.Keys)}.";
                refs[output.Name] = new JsonObject { ["ref"] = $"{of}:{output.Value.GetString()}" };
            }

            outcome["outputs"] = refs;
        }

        var answer = new JsonObject { ["state"] = ReconcileStates.Done, ["outcome"] = outcome };
        using (var document = JsonDocument.Parse(answer.ToJsonString()))
        {
            if (WorkWire.ReadReconcileAnswer(document.RootElement) is null) return "the answer is not one the step's wire reads: keep the message short and the address a web page.";
        }

        Directory.CreateDirectory(Folder(home));
        AtomicFile.WriteText(AnswerPath(home, ask.Invocation), answer.ToJsonString());
        return null;
    }

    /// <summary>The failure kept for a session that ended without answering; nothing where an answer is kept already.</summary>
    public static void Unanswered(string home, string invocation)
    {
        if (File.Exists(AnswerPath(home, invocation))) return;
        var answer = new JsonObject
        {
            ["state"] = ReconcileStates.Done,
            ["outcome"] = new JsonObject
            {
                ["state"] = WorkStates.Failed, ["code"] = NoAnswer,
                ["evidence"] = new JsonObject { ["message"] = "the step's session ended without calling step_answer." },
            },
        };
        Directory.CreateDirectory(Folder(home));
        AtomicFile.WriteText(AnswerPath(home, invocation), answer.ToJsonString());
    }
}
