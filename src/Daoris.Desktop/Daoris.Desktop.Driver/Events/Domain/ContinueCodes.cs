namespace Daoris.Driver;

/// <summary>
/// The resume reason codes the notes word (FOUNDATION1 mod-ix). This is their one literal owner; Agents' ContinueWhy forwards
/// these codes and owns the rules and sentences that select them, while Events owns their note keys and value declarations.
/// </summary>
public static class ContinueCodes
{
    public const string Account = "account";
    public const string Adapter = "adapter";
    public const string Unkept = "unkept";
    public const string Tree = "tree";
    public const string Unable = "unable";
    public const string Offered = "offered";
    public const string Gone = "gone";
    public const string Exited = "exited";
    public const string Refused = "refused";
    public const string Ended = "ended";
    public const string Elsewhere = "elsewhere";
    public const string Teammate = "teammate";
    public const string Intake = "intake";
    public const string StoodDown = "stood-down";
    public const string Started = "started";
    public const string Resumed = "resumed";
}
