namespace Daoris.Driver;

/// <summary>
/// Why a staged build is not installed now (D139 §4): its check, then a going back to an older build while a delivery is unresolved
/// (<see cref="InstallRollback"/>). The application asks it before it closes for a build, and <c>daoris update --now</c> before it says
/// the word is taken, so both doors answer one refusal in one sentence.
/// </summary>
/// <remarks>Not compiled into the launcher, which checks the build again by <see cref="StagedBuild"/> alone and meets no delivery.</remarks>
public static class UpdateRefusal
{
    /// <summary>The refusal's code and sentence for the build staged beside <paramref name="install"/>, at <paramref name="staged"/> over <paramref name="running"/>; null where it may be installed.</summary>
    public static (string Code, string Sentence)? Of(string install, string home, string? staged, string? running) =>
        StagedBuild.Check(install) is { } problem ? (problem.Code, problem.Sentence) : InstallRollback.Refused(home, staged, running);
}
