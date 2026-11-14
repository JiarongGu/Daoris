using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>The input <c>git update-ref --stdin</c> reads, as the transaction it says; the inverse of <see cref="GitRefTransaction.Input"/>.</summary>
internal static class MemoryUpdateRefs
{
    /// <summary>The transaction <paramref name="input"/> names, or null where a line is no command this reads.</summary>
    internal static GitRefTransaction? Read(string input)
    {
        var acts = new List<GitRefAct>();
        var checks = new List<GitRefCheck>();
        foreach (var line in input.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.TrimEnd('\r').Split(' ');
            switch (parts)
            {
                case ["verify", var name]: checks.Add(new GitRefCheck(name, null)); break;
                case ["verify", var name, var value]: checks.Add(new GitRefCheck(name, value)); break;
                case ["create", var name, var made]: acts.Add(new GitRefAct(GitRefActKind.Create, name, null, made)); break;
                case ["update", var name, var made, var old]: acts.Add(new GitRefAct(GitRefActKind.Update, name, old, made)); break;
                case ["delete", var name, var old]: acts.Add(new GitRefAct(GitRefActKind.Delete, name, old, null)); break;
                default: return null;
            }
        }

        return new GitRefTransaction(acts, checks);
    }
}
