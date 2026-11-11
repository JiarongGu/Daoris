using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>The standing choices' <c>list</c> verb: what this machine drives, as <c>daoris place list</c> always said it.</summary>
internal static class DriverChoicesList
{
    public static int List(DriverConfig choices, DriverChoicesWorld world)
    {
        var write = world.Instance.Out;
        void Say(string line) => write.WriteLine(line);
        void Section(string word) => world.Verbs.FirstOrDefault(each => each.Word == word)?.Listed(choices, world);
        Say($"daoris: {world.ConfigPath}");
        Say("  roles      `daoris role` says each role's agents, and `daoris role set` changes them");
        Say($"  cap        {choices.Cap} concurrent session(s)");
        Say("  rules      `daoris role supervisor` says the supervisor's rules, the failed sessions that park a quest among them");
        foreach (var (quest, mark) in choices.Forgiven) Say($"  retried    #{quest}  (counting from {mark} failure(s))");
        foreach (var (quest, session) in choices.Released) Say($"  released   #{quest}  (your stop of session {session})");

        foreach (var (scope, pauses) in new[] { ("ask", choices.PausedAsks), ("quest", choices.PausedQuests) })
        {
            foreach (var (id, pause) in pauses)
            {
                var when = pause.At is { } at ? $"since {Moment(at)}" : "when is not recorded";
                var stops = pause.Stopped.Keys.OrderBy(quest => quest, StringComparer.Ordinal).Select(quest => $"#{quest}'s session {pause.Stopped[quest]}").ToList();
                var resume = scope == "ask" ? $"daoris ask resume {id}" : $"daoris quest resume {id}";
                Say($"  paused     {scope} #{id}  ({when}{(stops.Count > 0 ? $"; its pause stopped {string.Join(", ", stops)}" : "")}) — resume: {resume}");
            }
        }

        Say(choices.QuietMinutes is { } quiet
            ? $"  quiet      {quiet} minutes before Daoris looks in on a session"
            : $"  quiet      {QuietSetting.Default} minutes before Daoris looks in on a session  (the default — "
              + "`daoris settings quiet <minutes>` changes it)");
        var manifest = choices.Manifest.Bounds;
        Say($"  manifest   {manifest.Files} files and {(int)manifest.Time.TotalMinutes} minutes before a folder is too large to follow"
            + (choices.Manifest == ManifestSetting.Unset ? "  (the default — `daoris settings manifest files|minutes <n>` changes either)" : ""));
        Say(choices.CoolOffMinutes is { } cooloff
            ? $"  cooloff    {cooloff} minutes an account cools when its agent names no time for its limit"
            : $"  cooloff    {(int)CoolOffDefault.Value.TotalMinutes} minutes an account cools when its agent names no time for its limit  (the default — "
              + "`daoris settings cooloff <minutes>` changes it)");
        Say($"  notify     {(choices.Notify ? "on" : "off")}  (a session parking, or ending without you asking{(choices.Notify ? "" : " — not said")})");
        Section(TerminalTiers.File);

        if (choices.Drivable.Count == 0)
        {
            Say("  drivable   nothing — this machine drives no repository, which is the default (D46 §2).");
            Say("             `daoris place drive <repository>` opts one in.");
        }

        foreach (var repository in choices.Drivable)
        {
            var held = FindName(choices.Holds, repository) is not null;
            Say($"  drivable   {repository}{(held ? "  (held by you — `daoris place resume` releases it)" : "")}");
        }

        foreach (var (repository, branch) in choices.Lines) Say($"  line       {repository}  {branch}");
        foreach (var (workspace, branch) in choices.WorkspaceLines) Say($"  line       workspace {workspace}  {branch}  (for each repository there that sets none)");

        Section("landing");

        foreach (var (repository, read) in choices.ReadAcross) Say($"  across     {repository}  read {(read ? "on" : "off")}");
        foreach (var (workspace, read) in choices.WorkspaceReadAcross) Say($"  across     workspace {workspace}  read {(read ? "on" : "off")}  (for each repository there that sets none)");
        foreach (var (repository, targets) in choices.WriteAcross) Say($"  across     {repository}  writes into {string.Join(", ", targets)}  (declared by you)");

        foreach (var (repository, answer) in choices.Standing)
        {
            Say($"  standing   {repository}  \"{answer.Says}\"{(answer.At is { } at ? $"  (set {Moment(at)})" : "")}");
        }

        foreach (var (repository, code) in choices.Languages) Say($"  language   {repository}  {code} ({SessionLanguages.NameOf(code)})  (set for it)");
        foreach (var (workspace, code) in choices.WorkspaceLanguages) Say($"  language   workspace {workspace}  {code} ({SessionLanguages.NameOf(code)})  (for each repository there that sets none)");
        if (choices.Languages.Count == 0 && choices.WorkspaceLanguages.Count == 0)
        {
            Say("  language   none set — sessions are asked for no language; `daoris place language <repository> en|zh` sets one");
        }

        Section("review");

        Section("opinion");

        Section("helpers");

        Section("handover");

        Section("brainstorms");

        Section("commits");

        // A hold on something not opted in is inert, and saying so is the point: it reads as protection and is not.
        foreach (var held in choices.Holds.Where(each => FindName(choices.Drivable, each) is null))
        {
            Say($"  held       {held}  (not drivable anyway — the hold changes nothing)");
        }

        return 0;
    }
}
