using System.Reflection;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Census of the native door's work frames: a child's or a background task's start and end are read off Claude Code's
/// stream by its two readers alone (<see cref="NativeChildren"/>, <see cref="NativeTasks"/>), so no other driver source names a task frame's
/// subtype or a subagent's task type in code, and the run's own ending takes no frame: it reads those readings, as the streams beside do.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class NativeWorkFramesCensusTests
{
    /// <summary>The wire's words for a task's start, update and end and for a subagent's task, as a string in code: a comment says them bare.</summary>
    private static readonly Regex Words = new(@"""(?:task_started|task_updated|task_notification|local_agent)""", RegexOptions.CultureInvariant);

    private static readonly string[] Readers = ["Agents/NativeChildren.cs", "Agents/NativeTasks.cs"];

    [Fact]
    public void A_child_s_and_a_task_s_start_and_end_are_read_off_the_frame_by_their_two_readers_alone()
    {
        var root = SourceFiles.Project(SourceFiles.DriverProject);
        var reading = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Where(path => Words.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToList();

        Assert.Equal(Readers, reading);
        Assert.DoesNotContain(
            typeof(HarnessWords).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(NativeFrame)));
    }
}
