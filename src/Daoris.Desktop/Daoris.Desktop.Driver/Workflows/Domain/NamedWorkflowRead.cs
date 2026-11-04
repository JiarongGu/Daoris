using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A workflow file as read: its id and name and every version, each readable or not; or the file's own first problem.</summary>
public sealed record NamedWorkflowRead(string? Id, string? Name, IReadOnlyList<NamedVersion> Versions, string? Problem)
{
    /// <summary>
    /// The file's mark (FABRIC-PIPELINE1a, <see cref="WorkflowPipelines"/>): <c>development</c> where it says none, null where the file
    /// does not read.
    /// </summary>
    public string? Pipeline { get; init; }

    /// <summary>Whether it is marked a verification pipeline, which only the <c>landed</c> route names.</summary>
    public bool Verifies => Pipeline == WorkflowPipelines.Verification;
}
