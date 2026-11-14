using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

public static class AttentionCheckRows
{
    public static IEnumerable<JsonObject> Rows(
        AttentionFacts facts, IReadOnlyDictionary<string, JsonObject> sessions, IReadOnlyDictionary<string, JsonObject> quests)
    {
        foreach (var verdict in facts.Checks)
        {
            var (ask, state) = verdict;
            if (state.Handed is not null || state.State is not (CheckStates.Failed or CheckStates.Stopped)) continue;
            if (sessions.GetValueOrDefault(ask.Session) is not { } session) continue;
            var quest = Text(session, "quest") ?? Text(session, "tookQuest");
            if (quest is null || quests.GetValueOrDefault(quest) is not { } record || Text(record, "status") != "Done") continue;

            var row = Row("check-failed", ask.Id, Moments.Write(state.At), Scope(session));
            row["session"] = ask.Session;
            row["where"] = Text(session, "repository");
            row["check"] = new JsonObject
            {
                ["name"] = verdict.Name,
                ["tip"] = state.Tip,
                ["tail"] = state.Tail,
                ["state"] = state.State,
            };
            yield return row;
        }
    }
}
