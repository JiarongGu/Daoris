using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.DriverWorkflowsCommand;

namespace Daoris.Driver;

/// <summary>A workflow's file as found: the file as parsed (null where it is not JSON) and what it reads as.</summary>
internal sealed record WorkflowFound(string Id, string Path, JsonNode? File, NamedWorkflowRead Read)
{
    public WorkflowFileFound Of => new(Id, File, Read);
}
