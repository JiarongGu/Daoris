using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.NodeGraphs;

namespace Daoris.Driver;

/// <summary>A graph's digest and each node's place in it: what an acceptance, a scope and a firing key bind.</summary>
internal static class NodeGraphPlaces
{
    /// <summary>
    /// The graph's digest (§E2's <c>graphDigest</c>, which an acceptance binds): <c>gph-</c> and 32 hex of a SHA-256 over every declaration,
    /// nodes and wires in id order, entries in order of their names and each node's ports and options by name, so the order they were
    /// written in never moves it and any declaration changed does.
    /// </summary>
    internal static string Digest(NodeGraph graph)
    {
        var text = new DigestText();
        text.Line("graph");
        text.Line(graph.SchemaVersion);
        text.Line(graph.Nodes.Count);
        foreach (var node in graph.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal).ThenBy(node => node.Kind, StringComparer.Ordinal)) text.Node(node);
        text.Line(graph.Wires.Count);
        foreach (var wire in graph.Wires.OrderBy(wire => wire.Id, StringComparer.Ordinal).ThenBy(wire => wire.Kind, StringComparer.Ordinal)) text.Wire(wire);
        text.Line(graph.Entries.Count);
        foreach (var entry in graph.Entries.Order(StringComparer.Ordinal)) text.Line(entry);
        text.Line(graph.End);
        text.Line(graph.Cleanup.Until);
        text.Line(graph.Cleanup.Days);
        return text.Hash("gph-");
    }

    /// <summary>
    /// Each node's place in a live graph, by node id (FABRIC-RUN1f; the cutover design §9, D190 point 9): <c>plc-</c> and 32 hex of a SHA-256 over
    /// the graph's schema version, its declaration, the wires into it in id order and whether it is an entry, each as <see cref="Digest"/> writes
    /// it. What a skip's scope, a receipt, a firing key and a binding name on a live graph where they named the whole graph's digest, so an edit
    /// elsewhere leaves them current; a node declared twice has none, and is judged on the whole graph's digest.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Places(NodeGraph graph)
    {
        var places = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var same in graph.Nodes.GroupBy(node => node.Id, StringComparer.Ordinal).Where(same => same.Count() == 1))
        {
            var node = same.Single();
            var into = graph.Wires.Where(wire => wire.To is { Unit: null } to && to.Node == node.Id)
                .OrderBy(wire => wire.Id, StringComparer.Ordinal).ThenBy(wire => wire.Kind, StringComparer.Ordinal).ToList();
            var text = new DigestText();
            text.Line("place");
            text.Line(graph.SchemaVersion);
            text.Node(node);
            text.Line(into.Count);
            foreach (var wire in into) text.Wire(wire);
            text.Line(graph.Entries.Contains(node.Id, StringComparer.Ordinal));
            places[node.Id] = text.Hash(PlacePrefix);
        }

        return places;
    }

    /// <summary>A place's prefix (<see cref="Places"/>), which tells a record written since FABRIC-RUN1f from one naming the whole graph's <c>gph-</c>.</summary>
    internal const string PlacePrefix = "plc-";

    /// <summary>The text a graph's digest and a node's place are taken over: one line per value, escaped, so no two declarations write the same.</summary>
    private sealed class DigestText
    {
        private readonly StringBuilder text = new();

        public void Line(object? value) => text.Append(value switch
        {
            null => "\\0",
            bool flag => flag ? "1" : "0",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()!.Replace("\\", "\\\\").Replace("\n", "\\n"),
        }).Append('\n');

        public void Node(NodeDeclaration node)
        {
            Line("node");
            Line(node.Id);
            Line(node.Kind);
            Line(node.Version);
            Line(node.Reach);
            Line(node.Synthetic);
            Line(node.Evidence);
            Line(node.Retry?.MaxAttempts);
            Line(node.Retry is { } retry ? string.Join(',', retry.DelaysSeconds) : null);
            Line(node.TimeoutSeconds);
            Line(node.Rejectable);
            Line(node.Skippable);
            Map(node.Inputs);
            Map(node.Outputs);
            Map(node.Options);
            // DECIDE1b: written only where declared, so a graph that names none digests, and places its nodes, as it did.
            if (node.Outcomes.Count > 0)
            {
                Line("outcomes");
                Line(node.Outcomes.Count);
                foreach (var outcome in node.Outcomes) Line(outcome);
            }

            // DECIDE1d: the same, for the outcomes that carry an output.
            if (node.OutputsOn.Count == 0) return;
            Line("outputsOn");
            Line(node.OutputsOn.Count);
            foreach (var (output, on) in node.OutputsOn)
            {
                Line(output);
                Line(on.Count);
                foreach (var outcome in on) Line(outcome);
            }
        }

        public void Wire(Wire wire)
        {
            Line("wire");
            Line(wire.Id);
            Line(wire.Kind);
            End(wire.From);
            End(wire.To);
            Line(wire.Waits?.Unit);
            Line(wire.Waits?.Revision);
            Line(wire.Waits?.Node);
            Line(wire.Waits?.End);
            Line(wire.Waits?.Commitment);
            Line(wire.Group?.Id);
            Line(wire.Group?.Mode);
            if (wire.Outcome is not null)
            {
                Line("outcome");
                Line(wire.Outcome);
            }

            if (wire.On is not null)
            {
                Line("on");
                Line(wire.On);
            }
        }

        public string Hash(string prefix) => prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];

        private void Map(IReadOnlyDictionary<string, string> map)
        {
            Line(map.Count);
            foreach (var (name, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Line(name);
                Line(value);
            }
        }

        private void End(WireEnd? end)
        {
            Line(end?.Node);
            Line(end?.Port);
            Line(end?.Unit);
        }
    }
}
