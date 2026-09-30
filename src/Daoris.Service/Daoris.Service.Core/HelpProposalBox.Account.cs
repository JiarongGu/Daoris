namespace Daoris.Knowledge;

/// <summary>The <c>account</c> kind's writer (HELP6): an account's own model and effort, with its own <c>account</c>, <c>model</c> and <c>effort</c>.</summary>
public sealed partial class HelpProposalBox
{
    /// <summary>
    /// Write one proposal to change an account's own model and effort (HELP6): the Agents screen's
    /// <i>Model &amp; effort</i>, which the driver judges with that route's own rules.
    /// </summary>
    /// <param name="model">A model the tool reads, or <c>unset</c> for the tool's own default; null leaves it.</param>
    /// <param name="effort">An effort the tool's settings keep, or <c>unset</c>; null leaves it.</param>
    public (string? Id, string Message) ProposeAgentSettings(
        string agent, string account, string? model, string? effort, string why, string? session, DateTimeOffset at)
    {
        var named = Blank(agent);
        var whose = Blank(account);
        var chosenModel = Blank(model);
        var chosenEffort = Blank(effort);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var refused = named is null ? "the change names the agent whose account it is, as `daoris agent` spells it."
            : whose is null ? "the change names the account — one of Daoris's accounts for that agent; the tool's own configuration home is never touched."
            : chosenModel is null && chosenEffort is null ? "the change sets a model, an effort, or both — `unset` returns either to the tool's own default."
            : Word(named, "an agent") ?? Word(whose, "an account")
              ?? (chosenModel is null ? null : Word(chosenModel, "a model"))
              ?? (chosenEffort is null ? null : Word(chosenEffort, "an effort"));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "account");
            writer.WriteString("door", "settings");
            writer.WriteString("target", named);
            writer.WriteNull("workspace");
            writer.WriteNull("value");
            writer.WriteNull("sentence");
            writer.WriteString("account", whose);
            Nullable(writer, "model", chosenModel);
            Nullable(writer, "effort", chosenEffort);
        }, why, session, at);
    }
}
