using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using static Daoris.Driver.WorkflowNamed;

namespace Daoris.Driver;

/// <summary>A version's steps in their canonical text and the digest taken of it.</summary>
internal static class NamedStepCanon
{
    /// <summary>
    /// The text a version's digest is of, built by hand so both runtimes write the same bytes — the CLI's
    /// <c>versionCanonical</c>, byte for byte: <c>workflow</c>, then each step's line, its quoted id and its kind, each followed
    /// by every field its kind takes, a line each in order, absence written as its default; a group's members follow it, a step deeper.
    /// A field the table writes only where set (<see cref="WorkflowKindField.WhereSet"/>) has no line where it is none.
    /// </summary>
    internal static string Canonical(IReadOnlyList<NamedStep> steps)
    {
        var lines = new List<string> { "workflow" };
        void Write(NamedStep step, string indent)
        {
            lines.Add($"{indent}step {WorkflowSteps.Quoted(step.Id)} {step.Kind}");
            var row = WorkflowKindTable.Of(step.Kind);
            foreach (var each in step.Fields)
            {
                if (each.Value is null && row?.Fields.FirstOrDefault(field => field.Name == each.Name) is { WhereSet: true }) continue;
                if (each.Value is IReadOnlyList<NamedStep> members)
                {
                    lines.Add($"{indent}  {each.Name}:");
                    foreach (var member in members) Write(member, indent + "    ");
                }
                else
                {
                    lines.Add($"{indent}  {each.Name}={CanonicalValue(each.Value)}");
                }
            }
        }

        foreach (var step in steps) Write(step, "");
        return string.Join('\n', lines);
    }

    /// <summary>A version's digest: the first 12 hex characters of the SHA-256 of <see cref="Canonical"/>'s text.</summary>
    internal static string Digest(IReadOnlyList<NamedStep> steps) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(steps))))[..12];

    private static string CanonicalValue(object? value) => value switch
    {
        null => "-",
        bool flag => flag ? "true" : "false",
        string text => WorkflowSteps.Quoted(text),
        IEnumerable<string> list => $"[{string.Join(',', list.Select(WorkflowSteps.Quoted))}]",
        _ => throw new ArgumentException($"a field is text, a switch or a list of names, not {value.GetType().Name}."),
    };
}
