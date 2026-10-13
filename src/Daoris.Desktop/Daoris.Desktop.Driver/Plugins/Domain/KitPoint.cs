using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// One point as the plugin kit tells an author about it (PLUG8): what kind of question it is, when it
/// is asked, the frame it is sent, what it must answer, how long the driver waits, and the handler a new
/// plugin starts from.
/// </summary>
/// <param name="Kind">`decision`, `observation` or `act` — what the answer does (D64 §4, D100).</param>
/// <param name="OnFailure">What the driver does with a late, wrong or missing answer, finishing "so …".</param>
/// <param name="Frame">
/// The frame the driver sends for the kit's sample input, built by <see cref="HookFrames"/> — the
/// function the driver itself sends it with. A shared instance: clone it before changing it.
/// </param>
/// <param name="Handler">The wire script's entry for this point, as the scaffold writes it.</param>
public sealed record KitPoint(
    string Name, string Kind, string When, string Answer, string OnFailure, TimeSpan Patience, JsonObject Frame, string Handler);

/// <summary>One file a new plugin's folder gets.</summary>
public sealed record ScaffoldFile(string Name, string Content);

/// <summary>What `plugins new` would write, and where — printable and assertable before anything is on disk.</summary>
public sealed record ScaffoldPlan(string Id, string Folder, IReadOnlyList<string> Points, IReadOnlyList<ScaffoldFile> Files);
