using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>
/// A ref transaction on a memory repository's refs, as <c>git update-ref --stdin</c> makes it: every expectation is read first and, where
/// one fails, nothing moves; a create also refuses a name whose case-alias stands where the disk reads names without case.
/// </summary>
internal static class MemoryRefTransaction
{
    internal static GitRefRun Run(MemoryHistory history, GitRefTransaction transaction, bool caseIgnored)
    {
        foreach (var check in transaction.Checks)
        {
            if (history.Refs.GetValueOrDefault(check.Ref) != check.Value) return Refused($"'{check.Ref}' is not at the commit expected");
        }

        foreach (var act in transaction.Acts)
        {
            var standing = history.Refs.GetValueOrDefault(act.Ref);
            if (act.Kind == GitRefActKind.Create)
            {
                if (standing is not null) return Refused($"cannot lock ref '{act.Ref}': reference already exists");
                if (caseIgnored && history.Refs.Keys.FirstOrDefault(each => string.Equals(each, act.Ref, StringComparison.OrdinalIgnoreCase)) is { } alias)
                {
                    return Refused($"'{act.Ref}' would be one ref with '{alias}'");
                }
            }
            else if (standing is null || standing != act.Old) return Refused($"cannot lock ref '{act.Ref}': is at a different commit than expected");

            if (act.New is not null && history.Commit(act.New) is null) return Refused($"{act.New} is not a valid object");
        }

        foreach (var act in transaction.Acts)
        {
            if (act.New is null) history.Refs.Remove(act.Ref);
            else history.Refs[act.Ref] = act.New;
        }

        return new GitRefRun(true, "");
    }

    private static GitRefRun Refused(string said) => new(false, said);
}
