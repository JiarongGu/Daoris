using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The shapes a step's field takes (the workflow design §3.2), as the shared table spells them.</summary>
public static class WorkflowFieldTypes
{
    public const string Names = "names";
    public const string Flag = "flag";
    public const string Choice = "choice";
    public const string Environment = "environment";
    public const string Pattern = "pattern";
    public const string PluginOrNone = "plugin-or-none";
    public const string Text = "text";
    public const string Plugin = "plugin";
    public const string Checks = "checks";
    public const string Id = "id";
    public const string Members = "members";
}
