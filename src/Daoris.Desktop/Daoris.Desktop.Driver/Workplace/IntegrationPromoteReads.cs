using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a promotion reads before it writes: the line's commit, the committed declaration's fence, the verdict's inputs. Nothing here writes.</summary>
internal static class IntegrationPromoteReads
{
    /// <summary>
    /// The fence: who integrates the line, read from the declaration committed at the line's commit being moved, never the working tree or the
    /// accepted blob. Null where Daoris does (<c>integration.by</c> is <c>daoris</c> or absent, or no declaration is committed); else the words.
    /// </summary>
    internal static async Task<string?> FenceAsync(string root, string commit, CancellationToken ct)
    {
        var at = TextBounds.ShortCommit(commit, 8);
        if (await IntegrationAcceptance.BlobAtAsync(root, commit, ct).ConfigureAwait(false) is not { } blob)
        {
            return $"git could not say which declaration `{at}` holds, so who integrates the line is not known";
        }

        if (blob == IntegrationAcceptance.None) return null;
        if (await IntegrationAcceptance.TextAsync(root, blob, ct).ConfigureAwait(false) is not { } text)
        {
            return $"the declaration `{at}` holds could not be read, so who integrates the line is not known";
        }

        var read = IntegrationDeclaration.Read(text);
        if (read.Problem is { } problem) return $"the declaration `{at}` holds does not read, so who integrates the line is not known: {problem}";
        return read.Facts?.By == "tool" ? $"the declaration `{at}` holds says `integration.by: tool`: another integrator owns the line" : null;
    }

    /// <summary>The line's commit as git names it, or the words of git's refusal.</summary>
    internal static async Task<(string? Commit, string Why)> LineAsync(string root, string line, CancellationToken ct)
    {
        return await MachinePorts.Repository.CommitOfAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false) is { } commit ? (commit, "") : (null, ContentHolds.Said(""));
    }

    /// <summary>
    /// The one word the port is handed for an unconfirmed failure: the owner's rule for a unit no member reaches and for one a member
    /// reaches, which the port cannot tell apart before it runs. Where the two differ, the less permissive of them (<c>split</c>, <c>hold</c>,
    /// <c>land</c>, in that order) stands, so <c>land</c> is only ever the rule saying it for both.
    /// </summary>
    internal static string Unconfirmed(LandingIntegrate policy)
    {
        var words = new[] { policy.Unconfirmed(reached: false, wholeSet: true), policy.Unconfirmed(reached: true, wholeSet: true) };
        return words.MinBy(word => Math.Max(0, LandingIntegrate.Unconfirmeds.ToList().IndexOf(word)))!;
    }

    /// <summary>The declaration's <c>verification</c> runner (its command and its folder); none where it declares no command.</summary>
    internal static VerdictRunner? RunnerOf(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("verification", out var verification) || verification.ValueKind != JsonValueKind.Object
                || !verification.TryGetProperty("run", out var run) || run.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(run.GetString()))
            {
                return null;
            }

            return new VerdictRunner(
                run.GetString()!.Trim(),
                verification.TryGetProperty("cwd", out var cwd) && cwd.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(cwd.GetString()) ? cwd.GetString()!.Trim() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
