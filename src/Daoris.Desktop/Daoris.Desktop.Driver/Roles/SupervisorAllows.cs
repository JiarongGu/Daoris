using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The three subjects an allow decision answers.</summary>
public static class SupervisorAllowShapes
{
    /// <summary>A permission request held in the asking mode.</summary>
    public const string Request = "request";

    /// <summary>A widening of the rules a session proposed.</summary>
    public const string Widening = "widening";

    /// <summary>A steward's act outside its grants.</summary>
    public const string Steward = "steward";

    public const string Press = "press";

    public const string Files = "files";
}

public static class SupervisorAllowActs
{
    public const string Leave = "leave";
}

/// <summary>An allow decision to keep: what it answered, its act, and who took it.</summary>
/// <param name="Session">The session the decision is read back by, where its subject has one; none keeps it with the machine's.</param>
/// <param name="Subject">The subject's own id: the request's, the proposal's, the steward's ask or a rule file's held act.</param>
/// <param name="Do">The act: <c>once</c>, <c>reject</c>, <c>standing</c>, <c>steward</c> or <c>leave</c>.</param>
/// <param name="Act">The steward's act a supervisor allowed, or the kind of act a files ask held.</param>
/// <param name="Bound">Set for a leave, which is <c>left</c> at its keeping.</param>
public sealed record SupervisorAllowed(
    string Id, string Shape, string Subject, string? Session, string Repository, string Do, string? Role, string? Act, SupervisorHolder Holder,
    IReadOnlyList<SupervisorReason> Reasons, string? Bound = null);

/// <summary>
/// The allow supervisor's decision record: each allow taken, whether of a held request, a widening or a steward's act, written once as a
/// file beside the session's other decisions and read back by the line and the page. An allow takes effect at its keeping, so it is
/// <c>went-on</c> at once. A record with no state line reads <c>held</c>, so the start's sweep closes it <c>stale</c>.
/// </summary>
internal sealed class SupervisorAllows(SupervisorDecisionFiles files, MachineLog? log)
{
    /// <summary>The folder a decision with no session is kept in.</summary>
    public const string Machine = "machine";

    /// <summary>Keep <paramref name="allow"/> as decided at <paramref name="now"/>.</summary>
    public void Keep(SupervisorAllowed allow, DateTimeOffset now)
    {
        var folder = allow.Session ?? Machine;
        Directory.CreateDirectory(files.FolderOf(folder));
        AtomicFile.WriteText(files.FileOf(folder, allow.Id), Json(allow, now));
        Append(folder, allow.Id, allow.Bound is not null ? SupervisorState.Left : SupervisorState.WentOn, now, null);
        log?.Info(allow.Bound is null ? "supervisor.allowed" : "supervisor.left", ("shape", allow.Shape), ("do", allow.Do));
    }

    /// <summary>A session's allow decisions, oldest first.</summary>
    public IReadOnlyList<SupervisorAllowView> AllowsOf(string session)
    {
        if (!SessionEventText.IsLocalId(session) || !Directory.Exists(files.FolderOf(session))) return [];
        return
        [
            .. Directory.EnumerateFiles(files.FolderOf(session), "*.json")
                .Select(file => SupervisorAllowView.Read(files, session, Path.GetFileNameWithoutExtension(file)))
                .OfType<SupervisorAllowView>()
                .OrderBy(view => view.KeptAt).ThenBy(view => view.Id, StringComparer.Ordinal),
        ];
    }

    /// <summary>The start's sweep: a decision still <c>held</c> belongs to a run that is gone, so it is closed <c>stale</c>.</summary>
    public void SweepAtStart(DateTimeOffset now)
    {
        foreach (var folder in files.Folders())
        {
            var name = Path.GetFileName(folder)[..^QuestionSupervisorDecisions.DecisionsSuffix.Length];
            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (SupervisorAllowView.Read(files, name, id) is { State: SupervisorState.Held }) Append(name, id, SupervisorState.Stale, now, SupervisorWhy.Restarted);
            }
        }
    }

    private void Append(string folder, string id, SupervisorState to, DateTimeOffset at, string? why) =>
        SupervisorDecisionFiles.TryAppend(files.StatesOf(folder, id), SupervisorDecisionFiles.StateLine(to, at, why, null));

    private static string Json(SupervisorAllowed allow, DateTimeOffset now) => new JsonObject
    {
        ["id"] = allow.Id,
        ["kind"] = allow.Shape == SupervisorAllowShapes.Files ? SupervisorKinds.Files : SupervisorKinds.Allow,
        ["session"] = allow.Session,
        ["holder"] = SupervisorHolders.Node(allow.Holder),
        ["allow"] = new JsonObject
        {
            ["shape"] = allow.Shape, ["subject"] = allow.Subject, ["repository"] = allow.Repository, ["do"] = allow.Do, ["role"] = allow.Role, ["act"] = allow.Act,
            ["bound"] = allow.Bound,
        },
        ["reasons"] = new JsonArray([.. allow.Reasons.Select(reason => (JsonNode)new JsonObject { ["says"] = reason.Says, ["source"] = reason.Source })]),
        ["keptAt"] = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["goesOnAt"] = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
