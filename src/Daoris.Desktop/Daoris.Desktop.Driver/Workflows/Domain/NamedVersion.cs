using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One version as read: its number, when and through which door it is saved, and its steps and digest, or its first problem. A graph
/// version (schema 2) holds <see cref="Graph"/> and no steps, and its digest is the graph's own
/// (<see cref="NodeGraphs.Digest"/>), which a run bound to it binds.
/// </summary>
public sealed record NamedVersion(
    int Version, string? At, string? Door, IReadOnlyList<NamedStep>? Steps, string? Problem, string? Digest, NodeGraph? Graph = null)
{
    /// <summary>
    /// Why it cannot be run here, or null where it can: its problem. A graph version runs as a version of steps does, the node runtime governing its run and its gates read from its nodes (<see cref="WorkflowProcesses"/>).
    /// </summary>
    public string? Unrun => Steps is not null || Graph is not null ? null : Problem;

    /// <summary>The default it is a copy of, or null: one its file does not name, or names in a shape this build does not read.</summary>
    public WorkflowFrom? From { get; init; }
}
