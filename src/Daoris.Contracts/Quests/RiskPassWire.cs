namespace Daoris.Contracts.Quests;

/// <summary>
/// The risk policy's yes at a person's door, where the driver's judge passed the act: the act it judged (<c>work.done</c>,
/// <c>direction.set</c>), the rule that decided and the policy's revision. Posted by the driver alone, with the key; the service keeps it as
/// the yes's author and judges nothing of it. A pass naming no act, rule or revision is refused.
/// </summary>
public sealed record RiskPassWire(string Act, string Rule, string Revision)
{
    /// <summary>Why the form is refused as it stands: a part left blank, which would name no rule's pass; null where it reads.</summary>
    public string? Malformed =>
        string.IsNullOrWhiteSpace(Act) || string.IsNullOrWhiteSpace(Rule) || string.IsNullOrWhiteSpace(Revision)
            ? "policy names the act, the rule that passed it and the policy's revision, none blank: a pass names what decided it"
        : Act is not ("work.done" or "direction.set") ? "policy's act is `work.done` or `direction.set`, the two acts a done's departure is judged as"
        : Rule.Contains('\n') || Rule.Contains('\r') || Revision.Contains('\n') || Revision.Contains('\r') ? "policy's rule and revision are one line each"
        : null;

    /// <summary>The yes's author as the quest's log keeps it: <c>policy:&lt;act&gt; · rule &lt;rule&gt; at &lt;revision&gt;</c>.</summary>
    public string By => $"{Prefix}{Act} · rule {Rule} at {Revision}";

    /// <summary>What every author the policy's yes is kept as begins with.</summary>
    public const string Prefix = "policy:";
}
