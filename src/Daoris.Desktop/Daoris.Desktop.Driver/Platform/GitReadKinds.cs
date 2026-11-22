using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The read kind of a git ask as the tally counts it: the git verb past git's own options, a walk that marks equivalent patches told
/// apart as <c>cherry</c>, a start with no command (a version) as <c>meta</c>. Finer than <see cref="GitKind"/>, which classes a start for its launch count.
/// </summary>
internal static class GitReadKinds
{
    internal const string Meta = "meta";

    internal static string Of(IReadOnlyList<string> arguments)
    {
        var command = GitLaunch.Command(arguments);
        if (command.Count == 0) return Meta;
        var verb = command[0];
        return verb is "log" or "rev-list" && command.Any(word => word.StartsWith("--cherry", StringComparison.Ordinal)) ? "cherry" : verb;
    }
}

/// <summary>Where a git is asked from, for an ask no caller named: the nearest frames of Daoris's own code, state machines and lambdas read as their methods.</summary>
internal static partial class GitSites
{
    // The git door's own plumbing: frames of these are the way to an ask, never the asker.
    private static readonly HashSet<string> Plumbing = new(StringComparer.Ordinal)
    {
        "GitService", "GitScheduler", "GitRunPolicy", "GitProcessRuns", "GitRuns", "WorkingTree", "GitAskers", "GitSites", "GitStartAsks",
        "GitFacts", "GitReadOnce", "RefSnapshot", "AddressedReads", "GitReads", "GitLaunch", "GitObjectReads",
    };

    internal static string Here(int skip)
    {
        var names = new List<string>(3);
        foreach (var frame in new StackTrace(skip, false).GetFrames())
        {
            if (Named(frame.GetMethod()) is { } name && !Plumbing.Contains(name.Type) && names.Count < 3) names.Add($"{name.Type}.{name.Method}");
            if (names.Count == 3) break;
        }

        return names.Count == 0 ? "unknown" : string.Join(" < ", names);
    }

    private static (string Type, string Method)? Named(MethodBase? method)
    {
        var type = method?.DeclaringType;
        if (method is null || type is null || type.Namespace?.StartsWith("Daoris", StringComparison.Ordinal) != true) return null;
        var label = method.Name;
        if (Machine().Match(type.Name) is { Success: true } machine)
        {
            label = machine.Groups["name"].Value;
            type = type.DeclaringType ?? type;
        }
        else if (Lambda().Match(method.Name) is { Success: true } lambda)
        {
            label = lambda.Groups["name"].Value;
            while (type.DeclaringType is { } outer && (type.Name.StartsWith('<') || type.Name.Contains("DisplayClass", StringComparison.Ordinal))) type = outer;
        }

        return (type.Name, label);
    }

    [GeneratedRegex(@"^<(?<name>\w+)>d__\d+$")]
    private static partial Regex Machine();

    [GeneratedRegex(@"^<(?<name>\w+)>b__")]
    private static partial Regex Lambda();
}
