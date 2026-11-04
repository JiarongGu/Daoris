using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One field a kind takes, in the order a version reads it: its shape, whether it must be named, and what absence reads as.</summary>
/// <param name="Default">Text, a switch, or null: what an absent field reads as.</param>
public sealed record WorkflowKindField(string Name, string Type, IReadOnlyList<string>? Choices, bool Required, object? Default)
{
    /// <summary>
    /// Whether a version's canonical text writes it only where it is set (FABRIC-PIPELINE1b2): a field a kind gains after versions were
    /// saved, absent read as none, so every version saved before it keeps its digest and every run bound to one reads as it was.
    /// </summary>
    public bool WhereSet { get; init; }
}
