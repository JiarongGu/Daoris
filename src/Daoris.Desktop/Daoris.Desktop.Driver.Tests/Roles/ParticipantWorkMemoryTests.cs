using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The works that keep their repository's memory are the ones whose rules do not deny <c>memory_write</c>.</summary>
public sealed class ParticipantWorkMemoryTests
{
    [Fact]
    public void Only_a_build_a_design_an_exact_build_and_a_chat_keep_memory()
    {
        Assert.Equal(
            [ParticipantWork.Build, ParticipantWork.Design, ParticipantWork.Exact, ParticipantWork.Chat],
            ParticipantWork.Codes.Where(ParticipantWork.KeepsMemory));
    }

    [Fact]
    public void A_work_that_keeps_none_is_denied_the_write_by_its_own_rules()
    {
        var composed = PermissionRules.Compose(PermissionFile.Empty, null, "reports");

        foreach (var helper in new[] { false, true })
        {
            Assert.Contains(OpinionPermissions.Shape(composed, helper).Deny, rule => rule.EndsWith("memory_write", StringComparison.Ordinal));
        }

        Assert.False(ParticipantWork.KeepsMemory(ParticipantWork.Review));
        Assert.False(ParticipantWork.KeepsMemory(ParticipantWork.Help));
        Assert.False(ParticipantWork.KeepsMemory(ParticipantWork.Brainstorm));
        Assert.Contains("memory_write", BrainstormPermissions.ConnectorTools);
    }
}
