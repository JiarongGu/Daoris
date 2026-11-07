using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What one status answer says of a home: signed in or not, and on a yes who, with the organisation
/// and the plan the agent names beside them, each null where it says none. Read fresh and written nowhere.
/// </summary>
public readonly record struct LoginRead(LoginState Login, string? Account, string? Org = null, string? Plan = null);

/// <summary>
/// How a harness answers "is this configuration home logged in?" — the arguments to ask with, and the
/// two shapes its answer takes.
/// </summary>
/// <remarks>
/// <para><b>Patterns, not exit codes.</b> Both supported harnesses exit 0 whether or not they are
/// logged in, so the exit code says nothing and the OUTPUT is the answer. Verified against the real
/// binaries, because every clause of it is a claim about somebody else's
/// tool.</para>
///
/// <para><b>Neither pattern matching means <see cref="LoginState.Unknown"/></b>, never logged-out: an
/// answer this build cannot read is not evidence.</para>
/// </remarks>
/// <param name="Arguments">What to run, under the profile's configuration home.</param>
/// <param name="LoggedIn">A pattern whose presence means yes.</param>
/// <param name="LoggedOut">A pattern whose presence means no.</param>
/// <param name="Account">
/// A pattern whose first group is <b>who</b> is signed in there, read from the same answer:
/// an account is named by who signed in rather than by a name typed before anyone knew. Taken only on
/// a yes. Null asks nobody's name.
/// </param>
/// <param name="Org">
/// A pattern whose first group is the organisation the sign-in is in, read from the same answer and only on a yes:
/// one email may sign in to two organisations, and this is what tells the two accounts apart. Read fresh,
/// held in memory, written nowhere. Null asks none.
/// </param>
/// <param name="Plan">The plan the sign-in is on, read as <paramref name="Org"/> is. Null asks none.</param>
public sealed record LoginQuestion(
    IReadOnlyList<string> Arguments, string LoggedIn, string LoggedOut, string? Account = null,
    string? Org = null, string? Plan = null)
{
    /// <summary>
    /// What one answer says (read in one place so a table can hold it): yes where <see cref="LoggedIn"/> matches,
    /// with who where <see cref="Account"/> reads one; else no where <see cref="LoggedOut"/> matches; else unknown. Matched
    /// without case, the yes first, since it is the specific one.
    /// </summary>
    public LoginRead Read(string output) =>
        Matches(output, LoggedIn) ? new LoginRead(LoginState.In, Who(output, Account), Who(output, Org), Who(output, Plan))
        : new LoginRead(Matches(output, LoggedOut) ? LoginState.Out : LoginState.Unknown, null);

    /// <summary>The first group of one of the answer's patterns, trimmed — or null when there is none to read.</summary>
    private static string? Who(string output, string? pattern)
    {
        if (pattern is null) return null;
        try
        {
            var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            return match.Success && match.Groups.Count > 1 && match.Groups[1].Value.Trim() is { Length: > 0 } who
                ? who
                : null;
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static bool Matches(string output, string pattern)
    {
        try
        {
            return Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            // A pattern this build cannot run is no evidence, exactly like an answer it cannot read.
            return false;
        }
    }
}

/// <summary>
/// What one of a harness's own commands runs as (<see cref="HarnessProbe.CommandOf"/>): the command and its
/// arguments' head, the pin's executable where it came from a pin, and the version pinned.
/// </summary>
/// <param name="Harness">Whose command: the name its pin and its <c>driver.json</c> command are kept under.</param>
/// <param name="Run">The binary, then any arguments it is declared with; empty where <see cref="Missing"/>.</param>
/// <param name="Managed">The pinned executable, or null for a named command or <c>PATH</c>'s.</param>
/// <param name="Pinned">The version this machine pins, or null.</param>
public sealed record HarnessCommand(string Harness, IReadOnlyList<string> Run, string? Managed, string? Pinned)
{
    /// <summary>Pinned with nothing installed at the pin: nobody to run, and never a fall back to <c>PATH</c>.</summary>
    public bool Missing => Pinned is not null && Managed is null;
}


