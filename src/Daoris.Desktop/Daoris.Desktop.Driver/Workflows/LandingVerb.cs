using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver landing</c>, retired (WORKFLOWCLEAN1 1a; the cleanup design §1.4): a landing rule is no longer written. How work lands
/// is the workflow a place follows, set up with <c>place</c> or chosen with <c>workflow use</c>, and how the line integrates is its policy,
/// <c>driver line</c>. The verb still lists the rules a home was written with, which the conversion reads, and refuses to write one.
/// </summary>
public sealed class LandingVerb : ChoiceVerb
{
    public override string Word => "landing";

    /// <summary>What the retired verb tells whoever presses it.</summary>
    public const string Retired =
        "retired: `driver landing` no longer writes a landing rule. How work lands is the workflow a place follows — `daoris driver place` sets one up, "
        + "`daoris driver workflow use <id>` chooses one — and how the line integrates is its policy, set on the place's page (Line). Nothing was written.";

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world) => throw new ChoiceWords(Retired);

    /// <summary>The lines this verb's keys add to <c>list</c>: the rules a home was written with, until the conversion has read them.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        static string Spelled(LandingRule rule) =>
            (rule.Form == LandingForm.Branch ? $"branch {rule.Pattern}" : rule.Form)
            + (rule.Key is { Length: > 0 } key ? $", key {key}" : "")
            + (rule.Plugin is { Length: > 0 } plugin ? $", plugin {plugin}" : "") + (rule.Tidy ? ", tidy" : "")
            + (rule.AutoAccept ? ", accept automatically" : "") + (rule.Squash ? ", squash" : "");
        foreach (var (repository, rule) in choices.Landings) Say($"  landing    {repository}  {Spelled(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceLandings) Say($"  landing    workspace {workspace}  {Spelled(rule)}  (for each repository there that sets none)");

        // FABRIC-GRAPH1d: what the file keeps that no unit reads is said, never skipped without a word.
        foreach (var stray in NodeGraphHomes.Strays(choices)) Say($"  unread     {stray.Words}");
    }
}

/// <summary>
/// A cycle as a door says it and reads it (FABRIC-GRAPH1c): what the work cycles' verb uses.
/// </summary>
internal static class ChoiceCycles
{
    /// <summary>
    /// A cycle's size and end, as a door says it: `1 node and 1 wire, ending at `qa``; a fragment's end is the line where it names none,
    /// and a work cycle's (<paramref name="end"/> empty) none.
    /// </summary>
    public static string Said(string cycle, string end = NodeKinds.Line)
    {
        var parts = NodeGraphs.Parts(cycle, out _);
        var nodes = parts?.Nodes.Count ?? 0;
        var wires = parts?.Wires.Count ?? 0;
        return $"{nodes} node{(nodes == 1 ? "" : "s")} and {wires} wire{(wires == 1 ? "" : "s")}, ending at `{parts?.End ?? end}`";
    }

    /// <summary>
    /// A cycle's file, read for a door that sets it (FABRIC-GRAPH1c): its JSON as <c>driver.json</c> keeps it, compact, every value as
    /// written, or the refusal naming the file. A path is the terminal's, from the folder it was run in.
    /// </summary>
    public static string File(DriverChoicesWorld world, string file)
    {
        var path = Path.IsPathFullyQualified(file) ? file : Path.GetFullPath(Path.Combine(world.Folder, file));
        if (!System.IO.File.Exists(path)) throw new ChoiceWords($"there is no file `{file}`. Nothing was written.");
        try
        {
            return NodeGraphHomes.Compact(System.IO.File.ReadAllText(path));
        }
        catch (JsonException)
        {
            throw new ChoiceWords($"`{file}` is not readable JSON. Nothing was written.");
        }
    }
}
