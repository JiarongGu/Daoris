using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The choices' own shape, written by hand for the same AOT reason <see cref="DriverConfigReader"/> reads it by hand.</summary>
internal static class DriverConfigWriter
{
    public static string Written(DriverConfig config)
    {
        var asFile = config.AsFile;
        return JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("drivable");
            foreach (var name in config.Drivable) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteStartArray("holds");
            foreach (var name in config.Holds) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteNumber("cap", config.Cap);
            ActorKey.Write(writer, asFile?.Actors ?? config.Actors);
            // Written only while full: absent is restricted, and the CLI twin writes it the same way.
            if (config.HelpMode == HelpModeNames.Full) writer.WriteString("helpMode", config.HelpMode);
            if (config.Terminal == TerminalTiers.Act) writer.WriteString(TerminalTiers.File, TerminalTiers.Act);
            writer.WriteBoolean("notify", config.Notify);
            writer.WriteNumber("pollSeconds", config.PollSeconds);
            // Written only when set: absent is the default, and an edit elsewhere must not pin today's default.
            if (config.CoolOffMinutes is { } coolOff) writer.WriteNumber("cooloff", coolOff);
            if (config.QuietMinutes is { } quiet) writer.WriteNumber(QuietSetting.File, quiet);
            config.Manifest.Write(writer);
            writer.WriteStartObject("forgiven");
            foreach (var (quest, mark) in config.Forgiven.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                writer.WriteNumber(quest, mark);
            }

            writer.WriteEndObject();
            // Written only when set, as the CLI writes it: absent is no release.
            WriteMap(writer, "released", config.Released);
            // Written only when set: absent is no transient release.
            WriteTransientReleases(writer, config.TransientReleased);
            WriteRoleReleases(writer, config.RoleReleases);
            // Written only when set: absent is no set-up admitted alone.
            WriteMap(writer, "admitted", config.Admitted);
            // Written only when set, as the CLI keeps them: absent is no pause.
            WritePauses(writer, "pausedAsks", config.PausedAsks);
            WritePauses(writer, "pausedQuests", config.PausedQuests);
            // Written only when set, as the CLI writes it: absent is no standing answer.
            WriteStanding(writer, config.Standing);
            // Written only when set: absent is the checkout's guess, and a file that never chose
            // a line should not start carrying an empty one.
            WriteMap(writer, "lines", config.Lines);
            WriteMap(writer, "workspaceLines", config.WorkspaceLines);
            // Written only when set, as the CLI writes them: absent is no language, and no line handed.
            WriteMap(writer, "languages", config.Languages);
            WriteMap(writer, "workspaceLanguages", config.WorkspaceLanguages);
            // Written only when set, as the CLI writes them: absent is no review environment, today's behaviour.
            ReviewKey.WriteMap(writer, "reviews", config.Reviews);
            ReviewKey.WriteMap(writer, "workspaceReviews", config.WorkspaceReviews);
            // Written only when set, as the CLI writes them: absent is no second opinion, today's behaviour.
            OpinionKey.WriteMap(writer, "opinions", config.Opinions);
            OpinionKey.WriteMap(writer, "workspaceOpinions", config.WorkspaceOpinions);
            // Written only when set, as the CLI writes them: absent is the default, `ask`.
            HelperKey.WriteMap(writer, "helpers", config.Helpers);
            HelperKey.WriteMap(writer, "workspaceHelpers", config.WorkspaceHelpers);
            // Written only when set, as the CLI writes them: absent is the default, `ask`.
            HandOverKey.WriteMap(writer, "handOver", config.HandOver);
            HandOverKey.WriteMap(writer, "workspaceHandOver", config.WorkspaceHandOver);
            GrowthKey.WriteMap(writer, "growth", config.Growth);
            GrowthKey.WriteMap(writer, "workspaceGrowth", config.WorkspaceGrowth);
            // Written only when set, as the CLI writes them: absent is the proposed defaults.
            BrainstormKey.WriteMachine(writer, config.Brainstorms);
            BrainstormKey.WriteMap(writer, "workspaceBrainstorms", config.WorkspaceBrainstorms);
            CommitKey.WriteMap(writer, "commits", config.Commits);
            CommitKey.WriteMap(writer, "workspaceCommits", config.WorkspaceCommits);
            // Written only when set, as the CLI writes them: absent is Current, today's behaviour.
            WorkflowKey.WriteMap(writer, "workflows", config.Workflows, workspace: false);
            WorkflowKey.WriteMap(writer, "workspaceWorkflows", config.WorkspaceWorkflows, workspace: true);
            WorkflowValuesKey.WriteMap(writer, config.WorkflowValues);
            // Written only when set: absent is no home default (a place follows Current) and no line policy.
            if (config.DefaultWorkflow is { } homeDefault) writer.WriteString("defaultWorkflow", homeDefault);
            LinePolicyKey.WritePolicies(writer, "linePolicies", config.LinePolicies);
            LinePolicyKey.WritePolicies(writer, "workspaceLinePolicies", config.WorkspaceLinePolicies);
            // Written only when set, beside the landed route's acceptance: absent is nothing accepted for integration.
            IntegrationGatesKey.WriteMap(writer, config.IntegrationGates);
            // Written only when set, as the CLI writes them: absent is today's behaviour, and nothing loosens by itself.
            AccessKey.WriteMap(writer, "access", config.Access);
            AccessKey.WriteMap(writer, "workspaceAccess", config.WorkspaceAccess);
            AccessKey.WriteMachine(writer, config.MachineAccess);
            // Written only when set, as the CLI writes it: absent is the repository's own heavy gates alone.
            HeavyKey.WriteMap(writer, config.HeavyGates);
            // Written only when set, as the CLI writes it: absent is today's, a session running unchanged.
            SessionChecksKey.WriteMap(writer, config.SessionChecks);
            // Written only where set: absent is each kind's default, and an edit elsewhere must not pin today's defaults.
            config.Keep.Write(writer);
            // Written only when set, for the same reason: absent is reading on and no relationship.
            WriteFlags(writer, "readAcross", config.ReadAcross);
            WriteFlags(writer, "workspaceReadAcross", config.WorkspaceReadAcross);
            // Written only when on: absent is the switch off.
            WriteFlags(writer, "checkoutWrites", config.CheckoutWrites);
            if (config.WriteAcross.Count > 0)
            {
                writer.WriteStartObject("writeAcross");
                foreach (var (repository, targets) in config.WriteAcross.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartArray(repository);
                    foreach (var target in targets) writer.WriteStringValue(target);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteStartObject("commands");
            foreach (var (name, command) in config.Commands.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                writer.WriteStartArray(name);
                foreach (var part in command) writer.WriteStringValue(part);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    /// <summary>The transient releases: each quest an object, its <c>session</c> and the <c>ended</c> it releases in UTC.</summary>
    private static void WriteTransientReleases(Utf8JsonWriter writer, IReadOnlyDictionary<string, TransientRelease> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject("transientReleased");
        foreach (var (quest, release) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(quest);
            writer.WriteString("session", release.Session);
            writer.WriteString("ended", release.Ended.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>A role's releases: each quest an object, its <c>kind</c>, <c>session</c>, <c>by</c> and <c>act</c>.</summary>
    private static void WriteRoleReleases(Utf8JsonWriter writer, IReadOnlyDictionary<string, RoleRelease> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject("roleReleases");
        foreach (var (quest, release) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(quest);
            writer.WriteString("kind", release.Kind);
            writer.WriteString("session", release.Session);
            writer.WriteString("by", release.By);
            writer.WriteString("act", release.Act);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(key, value);
        writer.WriteEndObject();
    }

    /// <summary>The pauses' shape: each id an object, its time in UTC to the second where known, and its stops always, none as <c>{}</c>.</summary>
    private static void WritePauses(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, WorkPause> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (id, pause) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(id);
            if (pause.At is { } at) writer.WriteString("at", at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            writer.WriteStartObject("stopped");
            foreach (var (quest, session) in pause.Stopped.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(quest, session);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>The standing answers: each repository an object, its words, and its time in UTC to the second where known.</summary>
    private static void WriteStanding(Utf8JsonWriter writer, IReadOnlyDictionary<string, StandingAnswer> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject("standing");
        foreach (var (repository, answer) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(repository);
            writer.WriteString("says", answer.Says);
            if (answer.At is { } at) writer.WriteString("at", at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteFlags(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, bool> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteBoolean(key, value);
        writer.WriteEndObject();
    }
}
