namespace Daoris.Service.Tests;

public sealed partial class QuestClosureTests
{
    /// <summary>🔴 A role's yes accepts a departure alone: a submission held for its unread evidence refuses it, and the person's yes still accepts it.</summary>
    [Fact]
    public async Task A_roles_yes_never_accepts_a_done_held_for_its_evidence()
    {
        var quest = await Bound();
        Assert.Equal(QuestHold.EvidenceUnread, (await Ready(quest)).Quest!.Hold);

        var role = await _exchange.AcceptAsync(quest.Id, Now, by: "role:daoris-landing · codex-acp");

        Assert.Equal(QuestRespondRefusal.NotHeld, role.Refusal);
        Assert.Contains("a role's yes does not accept", role.Message, StringComparison.Ordinal);
        Assert.Equal(QuestHold.EvidenceUnread, (await Find(quest.Id)).Hold);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(quest.Id, Now)).Refusal);
    }
}
