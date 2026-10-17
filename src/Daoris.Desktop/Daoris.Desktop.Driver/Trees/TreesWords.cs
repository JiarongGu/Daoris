using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a <c>trees</c> press says in a terminal (RPC1b3, the terminal RPC design §7.3): the lines for the output, the lines for the
/// error, and the exit code. The alone path prints it from the records its core returns; the route that is the press's window twin
/// answers it beside the page's fields (<c>said</c>), so the verb that becomes a client prints the same words from the answer.
/// </summary>
/// <param name="Out">The lines the verb writes to its output, in order.</param>
/// <param name="Error">The lines the verb writes to its error.</param>
/// <param name="Code">The verb's exit code: 0 clean, 1 refused.</param>
public sealed record TreesSaid(IReadOnlyList<string> Out, IReadOnlyList<string> Error, int Code)
{
    /// <summary>Writes the words, the output first, as the alone path does.</summary>
    public void Print(TextWriter output, TextWriter error)
    {
        foreach (var line in Out) output.WriteLine(line);
        foreach (var line in Error) error.WriteLine(line);
    }

    /// <summary>The words as an answer carries them (<c>said</c>): added beside the page's fields, never in their place.</summary>
    public object ToWire() => new { Out, Error, Code };

    /// <summary>The words an answer carries, or null where it carries none (an instance from before this field).</summary>
    public static TreesSaid? Read(JsonElement answer)
    {
        if (Property(answer, "said") is not { ValueKind: JsonValueKind.Object } said) return null;
        if (Property(said, "code") is not { ValueKind: JsonValueKind.Number } code || !code.TryGetInt32(out var exit)) return null;
        return new(Lines(Property(said, "out")), Lines(Property(said, "error")), exit);
    }

    /// <summary>
    /// A press's answer in the verb's own words, as <see cref="InstanceDoor.PressAsync"/> takes it: the words printed, the exit code
    /// returned. An answer with none is said so, exit 2, and nothing is done again.
    /// </summary>
    public static Func<JsonElement, int> Words(TextWriter output, TextWriter error, string verb = "trees") => answer =>
    {
        if (Read(answer) is not { } said)
        {
            error.WriteLine($"{verb}: Daoris answered in a form this terminal does not read: it carries no words for this press.");
            return 2;
        }

        said.Print(output, error);
        return said.Code;
    };

    private static JsonElement? Property(JsonElement from, string name)
    {
        if (from.ValueKind != JsonValueKind.Object) return null;
        foreach (var each in from.EnumerateObject())
        {
            if (string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase)) return each.Value;
        }

        return null;
    }

    private static IReadOnlyList<string> Lines(JsonElement? array) => array is { ValueKind: JsonValueKind.Array } lines
        ? [.. lines.EnumerateArray().Where(line => line.ValueKind == JsonValueKind.String).Select(line => line.GetString()!)]
        : [];
}

/// <summary>
/// The words of the <c>trees</c> presses, one function per press, shared by the alone path and the routes (RPC1b3). Each takes the
/// record the press's core returns and says what <c>daoris-driver trees</c> prints; nothing here runs git or asks the service.
/// </summary>
public static class TreesWords
{
    /// <summary>A plugin's own line, said under its name as the landing and the hand-off say it (D64 §4).</summary>
    public static string PluginLine(string plugin, string line) => $"  plugin:{plugin}  {line}";

    /// <summary>One branch of the clean-up's list, in words: whose, which, and what it holds.</summary>
    public static string Describe(SweepItem item) => $"{item.Repository}  {item.Branch}  " + item.Kind switch
    {
        SweepKind.Empty => $"nothing beyond `{item.Where}`",
        SweepKind.Landed => item.HeldBy is { } held ? held.Said : $"landed{(item.Where is { } where ? $" on `{where}`" : "")}",
        SweepKind.Unlanded => item.Commits > 0
            ? $"{item.Commits} commit(s) no branch of yours holds"
            : $"unlanded — {item.Detail}",
        SweepKind.Carried => $"{item.Commits} commit(s) `{item.Where}`'s completed pull request carried",
        SweepKind.Dirty => $"its tree has uncommitted work ({item.Detail})",
        SweepKind.InUse => "a session still running or waiting holds its tree",
        _ => item.Kind,
    };

    /// <summary><c>trees remove</c>: a tree discarded, or a refusal that preserves work, which is policy and exit 1.</summary>
    public static TreesSaid Remove(TreeRemoval removal) => new([$"trees: {removal.Message}"], [], removal.Removed ? 0 : 1);

    /// <summary><c>trees land &lt;session&gt;</c>: the plugin's lines as they came, then the landing's sentence; 1 where the step the rule asked for did not happen.</summary>
    public static TreesSaid Land(TreeLanding landed, IReadOnlyList<string> pluginLines) =>
        new([.. pluginLines, $"trees: {landed.Message}"], [],
            landed.Landed && landed.Plugin is not { Failed: true } and not { Pushed: false } ? 0 : 1);

    /// <summary><c>trees hand</c>: the plugin's lines, then the hand-off's sentence; 1 where the branch was not pushed.</summary>
    public static TreesSaid Hand(TreeHand handed, IReadOnlyList<string> pluginLines) =>
        new([.. pluginLines, $"trees: {handed.Message}"], [], handed.Handed ? 0 : 1);

    /// <summary><c>trees hand</c> refused before it speaks to any plugin: a sentence on the error, exit 1.</summary>
    public static TreesSaid HandRefused(string sentence) => new([], [$"trees: {sentence}"], 1);

    /// <summary>The sentence for a branch name that stands in more than one repository, so the person says which.</summary>
    public static string NamesSeveral(string named, IEnumerable<string> repositories) =>
        $"`{named}` names a landed branch in {string.Join(", ", repositories.Select(r => $"`{r}`"))} — say which with `--repository <name>`.";

    /// <summary>The sentence for a landed branch whose repository has no checkout here to hand it from.</summary>
    public static string HandNoCheckout(string repository, string branch) =>
        $"`{repository}` has no checkout on this machine, so there is no `{branch}` here to hand on.";

    /// <summary><c>trees clean --yes</c>: each branch removed or kept with why, the folders left, and the count.</summary>
    public static TreesSaid Clean(SweepDone done)
    {
        var said = new List<string>();
        foreach (var result in done.Sessions)
        {
            // A removal on content says where its commits stay (SQUASHTIDY1c); every other removal says nothing more.
            said.Add($"  {(result.Removed ? "removed" : "kept   ")} {Describe(result.Item)}"
                + ((result.Removed && result.Item.HeldBy is null) || result.Message == "kept" ? "" : $" — {result.Message}"));
        }

        foreach (var result in done.Landed)
        {
            said.Add($"  {(result.Removed ? "removed" : "kept   ")} {LandedWords.Describe(result.Item)}"
                + (result.Removed || result.Message == "kept" ? "" : $" — {result.Message}"));
        }

        // Folders trees left behind where something held them open, tried again.
        foreach (var folder in done.Folders ?? []) said.Add($"  {folder}");
        var total = done.Sessions.Count + done.Landed.Count;
        said.Add($"trees: removed {done.Sessions.Count(result => result.Removed) + done.Landed.Count(result => result.Removed)} of {total}.");
        return new(said, [], 0);
    }

    /// <summary>
    /// <c>trees sync --yes</c>: what was not fetched, each row moved or stayed, what was left apart, the lines the registry followed,
    /// and the count; 1 where something the proofs cleared did not happen.
    /// </summary>
    /// <param name="followed">The lines the registry's following said, already worded (<see cref="Followed"/>), or none.</param>
    public static TreesSaid Sync(SyncDone done, DateTimeOffset now, FollowedSaid? followed = null)
    {
        var said = new List<string>(SyncWords.NotFetched([.. done.Lines.Select(result => result.Pull)], now));
        foreach (var result in done.Lines) said.Add($"  {(result.Moved ? "moved " : "stayed")}  {result.Pull.Repository}  {result.Message}");
        foreach (var result in done.Rebases) said.Add($"  {(result.Replayed ? "moved " : "stayed")}  {result.Item.Repository}  {result.Message}");
        foreach (var result in done.Deletes)
        {
            said.Add($"  {(result.Removed ? "removed" : "kept   ")} {LandedWords.Describe(result.Item)}"
                + (result.Removed || result.Message == "kept" ? "" : $" — {result.Message}"));
        }

        if (SyncWords.Apart(done.Apart) is { } untouched) said.Add(untouched);
        said.AddRange(followed?.Out ?? []);

        // 1 where something the proofs cleared did not happen: a conflict, or a branch or line that moved since.
        var missed = done.Lines.Count(result => result.Pull.Moves && !result.Moved)
                     + done.Rebases.Count(result => result.Item.Replays && !result.Replayed)
                     + done.Deletes.Count(result => result.Item.Removable && !result.Removed);
        said.Add($"trees: {done.Lines.Count(r => r.Moved)} line(s) moved, {done.Rebases.Count(r => r.Replayed)} branch(es) replayed, "
            + $"{done.Deletes.Count(r => r.Removed)} removed." + (missed > 0 ? $" {missed} did not happen, each said above." : ""));
        return new(said, followed?.Error ?? [], missed > 0 ? 1 : 0);
    }

    /// <summary>
    /// The lines a press moved, registered from the line at once (WSSETUP5, D124 §3.1) rather than at a loop's next look, so a landing
    /// reads as registered when its press returns; each outcome to <paramref name="log"/>. What changed or must be fixed is said. A
    /// service that did not answer leaves them due, for the loop to follow. Shared by the terminal's presses and the routes that answer them.
    /// </summary>
    public static async Task<FollowedSaid> FollowMovedAsync(
        ServiceClient service, string home, Func<DriverConfig> config, MachineLog? log, CancellationToken ct = default)
    {
        var due = RegistryFollowing.Due(home);
        if (due.Count == 0) return new([], []);
        Action<RegistrationFollowed>? write = null;
        if (log is not null)
        {
            write = followed => SessionLog.WriteFollowed(log, followed);
            service.RegistryFollowed += write;
        }

        try
        {
            var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, home, config()), due, ct).ConfigureAwait(false);
            var said = report.Followed.Select(RegistrationFollow.EventLine).OfType<string>().Select(line => $"  {line}").ToList();
            if (report.Refresh is { } refresh) said.Add($"  registry  the index was not read again after registering: {refresh}");
            return new(said, []);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException)
        {
            return new([], [$"trees: the registry did not follow the lines this moved ({error.Message}); the driver follows them at its next look."]);
        }
        finally
        {
            if (write is not null) service.RegistryFollowed -= write;
        }
    }

    /// <summary>A look or a press that took no checkout, or a workspace the registry does not name: a sentence on the error, exit 1.</summary>
    public static TreesSaid Refused(string sentence) => new([], [sentence], 1);
}

/// <summary>What the registry's following said of the lines a press moved (WSSETUP5, D124 §3.1): the lines, and the failure if it had one.</summary>
/// <param name="Out">The lines said on the output, each already indented as the terminal says them.</param>
/// <param name="Error">The sentence said on the error where the registry did not follow, or none.</param>
public sealed record FollowedSaid(IReadOnlyList<string> Out, IReadOnlyList<string> Error);
