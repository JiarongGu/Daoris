using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The sources of the driver, its host and the modules, as the say-door censuses read them: each file's text from the run's shared
/// reading (<see cref="SourceTexts"/>, never again by each census), and each file's calls to the say door found once. A file
/// with no <c>.SayAsync(</c> in it is not searched for a receiver.
/// </summary>
internal static partial class SayDoorSources
{
    /// <summary>A source file: its name, its text, and every <c>receiver.SayAsync(</c> in it.</summary>
    internal sealed record Source(string File, string Text, IReadOnlyList<Say> Says);

    /// <summary>A call to a method named <c>SayAsync</c>: the receiver's name, where the receiver starts and where the argument text starts.</summary>
    internal readonly record struct Say(string Receiver, int Index, int Start);

    [GeneratedRegex(@"(?<receiver>[\w.]+)\s*\.SayAsync\(")]
    private static partial Regex Call();

    private static readonly string[] Projects = [SourceFiles.DriverProject, "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules"];

    private static readonly Lazy<IReadOnlyList<Source>> Read = new(() =>
        [.. Projects.SelectMany(project => SourceFiles.All(project, "*.cs"))
            .AsParallel().AsOrdered().Select(path =>
            {
                var text = SourceTexts.Of(path);
                var says = text.Contains(".SayAsync(", StringComparison.Ordinal)
                    ? Call().Matches(text).Select(call => new Say(call.Groups["receiver"].Value, call.Index, call.Index + call.Length)).ToList()
                    : [];
                return new Source(Path.GetFileName(path), text, says);
            })]);

    /// <summary>Every source, project by project, each project's files in path order.</summary>
    internal static IReadOnlyList<Source> All => Read.Value;
}
