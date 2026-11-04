using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One commit a landing leaves out, and why (<see cref="LeftOutWhy"/>).</summary>
/// <param name="On">The line's commit with the same patch, for <see cref="LeftOutWhy.Patch"/> where git paired one; else null.</param>
public sealed record LeftOutCommit(string Sha, string Subject, string Why, string? On = null);
