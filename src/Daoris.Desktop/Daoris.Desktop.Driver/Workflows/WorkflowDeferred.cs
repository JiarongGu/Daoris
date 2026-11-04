using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.DriverWorkflowsCommand;

namespace Daoris.Driver;

/// <summary>
/// The write, the person's: sent to the running Daoris's route, confirmed in its window, whatever the terminal's environment says;
/// done here only where none runs, refused from a session's terminal (D194 point 5), and on a named file, which no instance reads.
/// </summary>
internal sealed record WorkflowDeferred(InstanceRoute Route, IReadOnlyList<JsonObject> Payloads, Action Write, IReadOnlyList<string> Lines)
{
    /// <summary>Why the route cannot carry the write whole, or null; said and nothing done where an instance answers.</summary>
    public string? Cannot { get; init; }
}
