namespace Daoris.Driver;

/// <summary>
/// Why an order was refused (TOOL4e, D125 §3.1): the account it names that is not on this machine, or the one it names
/// twice. Both doors refuse it — the terminal's <c>daoris agent account order</c> and the screen's — as
/// <c>profile default</c> refuses a name that does not exist.
/// </summary>
public sealed record RotationProblem(string Account, bool Twice)
{
    /// <summary>The refusal a person reads: which account, and what is there to name instead.</summary>
    public string Sentence(string agent, IReadOnlyCollection<string> accounts) => Twice
        ? $"`{Account}` is named twice — an order names each account once, in the order rotation tries them."
        : $"`{agent}` has no account `{Account}` on this machine, so an order cannot name it — accounts there: "
          + $"{(accounts.Count > 0 ? string.Join(", ", accounts) : "(none)")}.";
}
