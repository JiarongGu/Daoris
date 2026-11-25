using System.Reflection;

namespace Daoris.Driver;

/// <summary>
/// Why a staged build is not installed now: its check, then a going back to an older build while a delivery is unresolved
/// (<see cref="GoingBackRefusals"/>), then its start on a copy of the home (<see cref="HomeProbe"/>). The application asks it before it closes for a build, and <c>daoris update --now</c> before it says
/// the word is taken, so both doors answer one refusal in one sentence.
/// </summary>
/// <remarks>Not compiled into the launcher, which checks the build again by <see cref="StagedBuild"/> alone and meets no delivery.</remarks>
public static class UpdateRefusal
{
    /// <summary>The version this build carries, as stamped, over which a staged build is refused; null where none is.</summary>
    public static string? Running => typeof(UpdateRefusal).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>The refusal's code and sentence for the build staged beside <paramref name="install"/>, at <paramref name="staged"/> over <paramref name="running"/>; null where it may be installed. <paramref name="proof"/>, the slowest, is asked last.</summary>
    public static (string Code, string Sentence)? Of(string install, string home, string? staged, string? running, StagedProof? proof = null) =>
        StagedBuild.Check(install) is { } problem ? (problem.Code, problem.Sentence)
        : GoingBackRefusals.Current.Refused(home, staged, running) is { } back ? back
        : proof?.Invoke(install, home, staged);
}
